@echo off
REM Smoke/regression tests for the currently deployed Task4.2 stack.
setlocal EnableExtensions EnableDelayedExpansion
chcp 65001 >nul
cd /d "%~dp0"
set "BASE=http://localhost:3000"
set "FRBASE=http://localhost:3001"
set "TMP=%TEMP%\task42_tests"
if not exist "%TMP%" mkdir "%TMP%"
set PASS=0
set FAIL=0

echo.
echo 🏁 Регрессионный тест Task4.2 - Camunda 8 + Payment + FraudCheck Services
echo.

echo 🧪 Проверка окружения...

docker version >nul 2>&1
if errorlevel 1 (call :fail "Docker не запущен") else call :pass "Docker запущен"

docker compose ps -a --format "{{.Service}} {{.Status}}" > "%TMP%\svc.txt" 2>nul
for %%s in (zeebe elasticsearch operate tasklist postgres payment-service fraud-check-service) do (
    findstr /b /c:"%%s Up" "%TMP%\svc.txt" >nul
    if errorlevel 1 (call :fail "Сервис %%s не запущен") else call :pass "Сервис %%s запущен"
)
findstr /c:"deploy-process Exited (0)" "%TMP%\svc.txt" >nul
if errorlevel 1 (call :fail "deploy-process не отработал с кодом 0") else call :pass "deploy-process отработал успешно"

echo 🧪 Ожидание Payment Service...
set /a wh=0
:wait_health
curl.exe -s -f "%BASE%/health" >nul 2>&1
if not errorlevel 1 goto health_ok
set /a wh+=1
if !wh! geq 10 goto health_timeout
timeout /t 3 /nobreak >nul
goto wait_health
:health_timeout
call :fail "Payment Service /health не отвечает"
goto after_health
:health_ok
call :pass "Payment Service /health отвечает"
:after_health

echo 🧪 Ожидание FraudCheckService...
set /a fw=0
:wait_fhealth
curl.exe -s -f "%FRBASE%/health" >nul 2>&1
if not errorlevel 1 goto fhealth_ok
set /a fw+=1
if !fw! geq 10 goto fhealth_timeout
timeout /t 3 /nobreak >nul
goto wait_fhealth
:fhealth_timeout
call :fail "FraudCheckService /health не отвечает"
goto after_fhealth
:fhealth_ok
call :pass "FraudCheckService /health отвечает"
:after_fhealth

REM ============ CRUD и валидация ============
echo.
echo 🧪 HTTP-тесты Payment Service - CRUD и валидация...

call :newid P1
curl.exe -s -X POST "%BASE%/api/payments" -H "Content-Type: application/json" -d "{\"paymentId\":\"%P1%\",\"payerId\":\"cust-test\",\"counterpartyId\":\"acct-test\",\"amount\":1250.50,\"currency\":\"RUB\"}" -o "%TMP%\create.json" >nul 2>&1
findstr /C:"PAYMENT_STARTED" "%TMP%\create.json" >nul
if errorlevel 1 (call :fail "Создание платежа: PAYMENT_STARTED не вернулся") else call :pass "Создание платежа: PAYMENT_STARTED"

curl.exe -s -X POST "%BASE%/api/payments" -H "Content-Type: application/json" -d "{\"paymentId\":\"%P1%\",\"payerId\":\"cust-test\",\"counterpartyId\":\"acct-test\",\"amount\":1250.50,\"currency\":\"RUB\"}" -o "%TMP%\create2.json" >nul 2>&1
findstr /C:"PAYMENT_STARTED" "%TMP%\create2.json" >nul
if errorlevel 1 (call :fail "Повторное создание не идемпотентно") else call :pass "Повторное создание идемпотентно"

curl.exe -s "%BASE%/api/payments/%P1%" -o "%TMP%\get.json" >nul 2>&1
findstr /C:"cust-test" "%TMP%\get.json" >nul
if errorlevel 1 (call :fail "GET платежа: payerId не найден") else call :pass "GET платежа возвращает данные"

curl.exe -s -o NUL -w "%%{http_code}" -X POST "%BASE%/api/payments" -H "Content-Type: application/json" -d "{\"paymentId\":\"\",\"payerId\":\"p\",\"counterpartyId\":\"c\",\"amount\":0,\"currency\":\"RUB\"}" > "%TMP%\code.txt"
set /p CC=<"%TMP%\code.txt"
if "!CC!"=="400" (call :pass "Валидация amount=0 вернула 400") else call :fail "Валидация amount=0: ожидали 400, получили !CC!"

curl.exe -s -o NUL -w "%%{http_code}" "%BASE%/api/payments/00000000-0000-4000-8000-0000000000ff" > "%TMP%\code.txt"
set /p CC=<"%TMP%\code.txt"
if "!CC!"=="404" (call :pass "Неизвестный платеж вернул 404") else call :fail "Неизвестный платеж: ожидали 404, получили !CC!"

REM ============ Циклы жизненного цикла платажа ============
echo.
echo 🧪 HTTP-тесты Payment Service - жизненный цикл платежа...

REM Детерминированная цепочка: при случайном отказе hold/transfer (p=0.05)
REM берём новый платёж, до 3 попыток.
set /a att=0
:chain_retry
set /a att+=1
call :newid CH
curl.exe -s -X POST "%BASE%/api/payments" -H "Content-Type: application/json" -d "{\"paymentId\":\"%CH%\",\"payerId\":\"cust-chain\",\"counterpartyId\":\"acct-chain\",\"amount\":99.99,\"currency\":\"RUB\"}" -o NUL >nul 2>&1

curl.exe -s -X POST "%BASE%/api/payments/%CH%/hold" -H "Idempotency-Key: %CH%:HOLD" -o "%TMP%\hold.json" >nul 2>&1
findstr /C:"FUNDS_HELD" "%TMP%\hold.json" >nul
if errorlevel 1 goto chain_fail

REM Идемпотентность: повтор того же запроса обязан дать байт-в-байт тот же ответ.
copy /y "%TMP%\hold.json" "%TMP%\hold1.json" >nul
curl.exe -s -X POST "%BASE%/api/payments/%CH%/hold" -H "Idempotency-Key: %CH%:HOLD" -o "%TMP%\hold2.json" >nul 2>&1
fc /b "%TMP%\hold1.json" "%TMP%\hold2.json" >nul
if errorlevel 1 (call :fail "Идемпотентность hold: повтор дал другой ответ") else call :pass "Идемпотентность hold: повтор идентичен"

curl.exe -s -X POST "%BASE%/api/payments/%CH%/transfer" -H "Idempotency-Key: %CH%:TRANSFER" -o "%TMP%\tr.json" >nul 2>&1
findstr /C:"FUNDS_TRANSFERRED" "%TMP%\tr.json" >nul
if errorlevel 1 goto chain_fail

curl.exe -s -X POST "%BASE%/api/payments/%CH%/complete" -H "Idempotency-Key: %CH%:COMPLETE" -o "%TMP%\cp.json" >nul 2>&1
findstr /C:"PAYMENT_PROCESS_COMPLETED" "%TMP%\cp.json" >nul
if errorlevel 1 goto chain_fail

curl.exe -s "%BASE%/api/payments/%CH%" -o "%TMP%\final.json" >nul 2>&1
findstr /C:"PAYMENT_PROCESS_COMPLETED" "%TMP%\final.json" >nul
if errorlevel 1 (call :fail "Финальный статус не PAYMENT_PROCESS_COMPLETED") else call :pass "Полная цепочка hold → transfer → complete прошла"
goto chain_done

:chain_fail
if !att! lss 3 goto chain_retry
call :fail "Цепочка hold→transfer→complete не прошла за 3 попытки"
:chain_done

REM ============ Компенсации ============
echo.
echo 🧪 HTTP-тесты Payment Service - компенсации...

set /a att=0
:rel_retry
set /a att+=1
call :newid R1
curl.exe -s -X POST "%BASE%/api/payments" -H "Content-Type: application/json" -d "{\"paymentId\":\"%R1%\",\"payerId\":\"cust-rel\",\"counterpartyId\":\"acct-rel\",\"amount\":50,\"currency\":\"RUB\"}" -o NUL >nul 2>&1
curl.exe -s -X POST "%BASE%/api/payments/%R1%/hold" -H "Idempotency-Key: %R1%:HOLD" -o "%TMP%\h.json" >nul 2>&1
findstr /C:"FUNDS_HELD" "%TMP%\h.json" >nul
if not errorlevel 1 goto hold_ok1
if !att! lss 3 goto rel_retry
call :fail "hold для release-теста не прошёл"
goto release_done
:hold_ok1
curl.exe -s -X POST "%BASE%/api/payments/%R1%/release" -H "Idempotency-Key: %R1%:RELEASE" -o "%TMP%\rl.json" >nul 2>&1
findstr /C:"FUNDS_RELEASED" "%TMP%\rl.json" >nul
if errorlevel 1 (call :fail "release: FUNDS_RELEASED не вернулся") else call :pass "release: FUNDS_RELEASED"
curl.exe -s -X POST "%BASE%/api/payments/%R1%/finish" -H "Idempotency-Key: %R1%:FINISH" -o "%TMP%\fn.json" >nul 2>&1
findstr /C:"PAYMENT_PROCESS_CANCELED" "%TMP%\fn.json" >nul
if errorlevel 1 (call :fail "finish: PAYMENT_PROCESS_CANCELED не вернулся") else call :pass "finish: PAYMENT_PROCESS_CANCELED"
:release_done

set /a att=0
:ret_retry
set /a att+=1
call :newid R2
curl.exe -s -X POST "%BASE%/api/payments" -H "Content-Type: application/json" -d "{\"paymentId\":\"%R2%\",\"payerId\":\"cust-ret\",\"counterpartyId\":\"acct-ret\",\"amount\":70,\"currency\":\"RUB\"}" -o NUL >nul 2>&1
curl.exe -s -X POST "%BASE%/api/payments/%R2%/hold" -H "Idempotency-Key: %R2%:HOLD" -o "%TMP%\h.json" >nul 2>&1
findstr /C:"FUNDS_HELD" "%TMP%\h.json" >nul
if not errorlevel 1 goto hold_ok2
if !att! lss 3 goto ret_retry
call :fail "hold для return-теста не прошёл"
goto return_done
:hold_ok2
curl.exe -s -X POST "%BASE%/api/payments/%R2%/transfer" -H "Idempotency-Key: %R2%:TRANSFER" -o "%TMP%\t2.json" >nul 2>&1
findstr /C:"FUNDS_TRANSFERRED" "%TMP%\t2.json" >nul
if errorlevel 1 (
    call :warn "transfer отказался - случайность, берём новый платёж"
    if !att! lss 3 goto ret_retry
    call :fail "transfer для return-теста не прошёл"
    goto return_done
)
curl.exe -s -X POST "%BASE%/api/payments/%R2%/return" -H "Idempotency-Key: %R2%:RETURN" -o "%TMP%\rt.json" >nul 2>&1
findstr /C:"FUNDS_RETURNED" "%TMP%\rt.json" >nul
if errorlevel 1 (call :fail "return: FUNDS_RETURNED не вернулся") else call :pass "return: FUNDS_RETURNED"
:return_done

REM ============ FraudCheckService ============
echo.
echo 🧪 HTTP-тесты FraudCheckService - антифрод-проверки...

REM Авто-проверка (ANTIFRAUD_AUTOCHECK): детерминированный mock-ответ ALLOW.
call :newid F1
curl.exe -s -X POST "%FRBASE%/api/fraud/checks/" -H "Content-Type: application/json" -d "{\"paymentId\":\"%F1%\",\"payerId\":\"cust-fraud\",\"counterpartyId\":\"acct-fraud\",\"amount\":1500.75,\"currency\":\"RUB\",\"checkType\":\"AUTO\"}" -o "%TMP%\fcreate.json" >nul 2>&1
findstr /C:"ANTIFRAUD_CHECKED" "%TMP%\fcreate.json" >nul
if errorlevel 1 (call :fail "Авто-проверка: ANTIFRAUD_CHECKED не вернулся") else call :pass "Авто-проверка: ANTIFRAUD_CHECKED"
findstr /C:"ALLOW" "%TMP%\fcreate.json" >nul
if errorlevel 1 (call :fail "Авто-проверка: решение ALLOW не вернулось") else call :pass "Авто-проверка: решение ALLOW"

REM Идемпотентность: повтор того же запроса обязан дать байт-в-байт тот же ответ.
copy /y "%TMP%\fcreate.json" "%TMP%\fcreate1.json" >nul
curl.exe -s -X POST "%FRBASE%/api/fraud/checks/" -H "Content-Type: application/json" -d "{\"paymentId\":\"%F1%\",\"payerId\":\"cust-fraud\",\"counterpartyId\":\"acct-fraud\",\"amount\":1500.75,\"currency\":\"RUB\",\"checkType\":\"AUTO\"}" -o "%TMP%\fcreate2.json" >nul 2>&1
fc /b "%TMP%\fcreate1.json" "%TMP%\fcreate2.json" >nul
if errorlevel 1 (call :fail "Идемпотентность автопроверки: повтор дал другой ответ") else call :pass "Идемпотентность автопроверки: повтор идентичен"

REM Состояние проверки и решение читаются из БД.
curl.exe -s "%FRBASE%/api/fraud/checks/%F1%" -o "%TMP%\fget.json" >nul 2>&1
findstr /C:"cust-fraud" "%TMP%\fget.json" >nul
if errorlevel 1 (call :fail "GET проверки: payerId не найден") else call :pass "GET проверки возвращает данные"

curl.exe -s "%FRBASE%/api/fraud/checks/%F1%/decision" -o "%TMP%\fdec.json" >nul 2>&1
findstr /C:"ALLOW" "%TMP%\fdec.json" >nul
if errorlevel 1 (call :fail "GET decision: ALLOW не вернулся") else call :pass "GET decision возвращает ALLOW"

REM Ручная проверка: checkType=MANUAL -> AWAITING_MANUAL_CHECK и очередь pending.
call :newid F2
curl.exe -s -X POST "%FRBASE%/api/fraud/checks/" -H "Content-Type: application/json" -d "{\"paymentId\":\"%F2%\",\"payerId\":\"cust-big\",\"counterpartyId\":\"acct-big\",\"amount\":900000,\"currency\":\"RUB\",\"checkType\":\"MANUAL\"}" -o "%TMP%\fmanual.json" >nul 2>&1
findstr /C:"AWAITING_MANUAL_CHECK" "%TMP%\fmanual.json" >nul
if errorlevel 1 (call :fail "Ручная проверка: AWAITING_MANUAL_CHECK не вернулся") else call :pass "Ручная проверка: AWAITING_MANUAL_CHECK"

curl.exe -s "%FRBASE%/api/fraud/checks/pending" -o "%TMP%\fpending.json" >nul 2>&1
findstr /C:"%F2%" "%TMP%\fpending.json" >nul
if errorlevel 1 (call :fail "Очередь pending не содержит ручную проверку") else call :pass "Очередь pending содержит ручную проверку"

REM Решение оператора: BLOCK -> FRAUD_OPERATION_DETECTED.
curl.exe -s -X POST "%FRBASE%/api/fraud/checks/%F2%/manual-decision" -H "Content-Type: application/json" -d "{\"paymentId\":\"%F2%\",\"decision\":\"BLOCK\",\"operatorId\":\"op-test\",\"comment\":\"blocked by test\"}" -o "%TMP%\fblock.json" >nul 2>&1
findstr /C:"FRAUD_OPERATION_DETECTED" "%TMP%\fblock.json" >nul
if errorlevel 1 (call :fail "Решение оператора: FRAUD_OPERATION_DETECTED не вернулся") else call :pass "Решение оператора: FRAUD_OPERATION_DETECTED"
findstr /C:"BLOCK" "%TMP%\fblock.json" >nul
if errorlevel 1 (call :fail "Решение оператора: BLOCK не вернулось") else call :pass "Решение оператора: BLOCK"

REM Идемпотентность ручного решения: повтор идентичен.
copy /y "%TMP%\fblock.json" "%TMP%\fblock1.json" >nul
curl.exe -s -X POST "%FRBASE%/api/fraud/checks/%F2%/manual-decision" -H "Content-Type: application/json" -d "{\"paymentId\":\"%F2%\",\"decision\":\"BLOCK\",\"operatorId\":\"op-test\",\"comment\":\"blocked by test\"}" -o "%TMP%\fblock2.json" >nul 2>&1
fc /b "%TMP%\fblock1.json" "%TMP%\fblock2.json" >nul
if errorlevel 1 (call :fail "Идемпотентность manual-decision: повтор дал другой ответ") else call :pass "Идемпотентность manual-decision: повтор идентичен"

REM Повтор с другим решением -> 409.
curl.exe -s -o NUL -w "%%{http_code}" -X POST "%FRBASE%/api/fraud/checks/%F2%/manual-decision" -H "Content-Type: application/json" -d "{\"paymentId\":\"%F2%\",\"decision\":\"ALLOW\",\"operatorId\":\"op-test\",\"comment\":\"try allow\"}" > "%TMP%\code.txt"
set /p FSC=<"%TMP%\code.txt"
if "!FSC!"=="409" (call :pass "Конфликт решений вернул 409") else call :fail "Конфликт решений: ожидали 409, получили !FSC!"

REM Валидация входных данных.
curl.exe -s -o NUL -w "%%{http_code}" -X POST "%FRBASE%/api/fraud/checks/%F1%/manual-decision" -H "Content-Type: application/json" -d "{\"paymentId\":\"%F1%\",\"decision\":\"MANUAL\",\"operatorId\":\"op-test\",\"comment\":\"x\"}" > "%TMP%\code.txt"
set /p FSC=<"%TMP%\code.txt"
if "!FSC!"=="400" (call :pass "Валидация decision=MANUAL вернула 400") else call :fail "Валидация decision=MANUAL: ожидали 400, получили !FSC!"

curl.exe -s -o NUL -w "%%{http_code}" -X POST "%FRBASE%/api/fraud/checks/" -H "Content-Type: application/json" -d "{\"payerId\":\"p\",\"counterpartyId\":\"c\",\"amount\":1,\"currency\":\"RUB\",\"checkType\":\"AUTO\"}" > "%TMP%\code.txt"
set /p FSC=<"%TMP%\code.txt"
if "!FSC!"=="400" (call :pass "Валидация без paymentId вернула 400") else call :fail "Валидация без paymentId: ожидали 400, получили !FSC!"

REM Неизвестная проверка -> 404.
curl.exe -s -o NUL -w "%%{http_code}" "%FRBASE%/api/fraud/checks/00000000-0000-4000-8000-0000000000fe" > "%TMP%\code.txt"
set /p FSC=<"%TMP%\code.txt"
if "!FSC!"=="404" (call :pass "Неизвестная проверка вернула 404") else call :fail "Неизвестная проверка: ожидали 404, получили !FSC!"

curl.exe -s -o NUL -w "%%{http_code}" -X POST "%FRBASE%/api/fraud/checks/00000000-0000-4000-8000-0000000000fe/manual-decision" -H "Content-Type: application/json" -d "{\"paymentId\":\"00000000-0000-4000-8000-0000000000fe\",\"decision\":\"BLOCK\",\"operatorId\":\"op-test\",\"comment\":\"x\"}" > "%TMP%\code.txt"
set /p FSC=<"%TMP%\code.txt"
if "!FSC!"=="404" (call :pass "manual-decision для неизвестного вернул 404") else call :fail "manual-decision для неизвестного: ожидали 404, получили !FSC!"

REM ============ PostgreSQL ============
echo.
echo 🧪 Проверка данных в PostgreSQL...

docker exec postgres psql -U orchestrpay -d paymentdb -t -A -c "SELECT count(*) FROM \"Payments\"" > "%TMP%\cnt.txt" 2>&1
set /p PCNT=<"%TMP%\cnt.txt"
echo(!PCNT!| findstr /r /x "[0-9][0-9]*" >nul
if errorlevel 1 (call :fail "Запрос Payments в БД не вернул число: !PCNT!") else call :pass "Таблица Payments доступна, строк: !PCNT!"

docker exec postgres psql -U orchestrpay -d paymentdb -t -A -c "SELECT count(*) FROM \"OperationRecords\"" > "%TMP%\cnt.txt" 2>&1
set /p OCNT=<"%TMP%\cnt.txt"
echo(!OCNT!| findstr /r /x "[0-9][0-9]*" >nul
if errorlevel 1 (call :fail "Запрос OperationRecords в БД не вернул число: !OCNT!") else call :pass "Таблица OperationRecords доступна, строк: !OCNT!"

REM Ровно одна запись на ключ: повтор hold не должен был добавить вторую.
docker exec postgres psql -U orchestrpay -d paymentdb -t -A -c "SELECT count(*) FROM \"OperationRecords\" WHERE \"Key\"='%CH%:HOLD'" > "%TMP%\cnt.txt" 2>&1
set /p KCNT=<"%TMP%\cnt.txt"
if "!KCNT!"=="1" (call :pass "Дубликатов ключа идемпотентности нет (ровно 1)") else call :fail "Ключ %CH%:HOLD встречается !KCNT! раз(а), ожидалось 1"

REM Финальный статус цепочки зафиксирован в БД.
docker exec postgres psql -U orchestrpay -d paymentdb -t -A -c "SELECT \"Status\" FROM \"Payments\" WHERE \"Id\"='%CH%'" > "%TMP%\st.txt" 2>&1
set /p PST=<"%TMP%\st.txt"
if "!PST!"=="PAYMENT_PROCESS_COMPLETED" (call :pass "Статус в БД: PAYMENT_PROCESS_COMPLETED") else call :fail "Статус в БД: !PST!, ожидался PAYMENT_PROCESS_COMPLETED"

REM ============ PostgreSQL fraudcheckdb ============
echo.
echo 🧪 Проверка данных FraudCheckService в PostgreSQL...

docker exec postgres psql -U orchestrpay -d fraudcheckdb -t -A -c "SELECT count(*) FROM \"Payments\"" > "%TMP%\fcnt.txt" 2>&1
set /p FCNT=<"%TMP%\fcnt.txt"
echo(!FCNT!| findstr /r /x "[0-9][0-9]*" >nul
if errorlevel 1 (call :fail "Запрос Payments в fraudcheckdb не вернул число: !FCNT!") else call :pass "Таблица Payments (fraudcheckdb) доступна, строк: !FCNT!"

docker exec postgres psql -U orchestrpay -d fraudcheckdb -t -A -c "SELECT count(*) FROM \"OperationRecords\"" > "%TMP%\focnt.txt" 2>&1
set /p FOCNT=<"%TMP%\focnt.txt"
echo(!FOCNT!| findstr /r /x "[0-9][0-9]*" >nul
if errorlevel 1 (call :fail "Запрос OperationRecords в fraudcheckdb не вернул число: !FOCNT!") else call :pass "Таблица OperationRecords (fraudcheckdb) доступна, строк: !FOCNT!"

REM Ровно одна запись на ключ идемпотентности автопроверки.
docker exec postgres psql -U orchestrpay -d fraudcheckdb -t -A -c "SELECT count(*) FROM \"OperationRecords\" WHERE \"Key\"='%F1%:AUTOCHECK'" > "%TMP%\fkc.txt" 2>&1
set /p FKC=<"%TMP%\fkc.txt"
if "!FKC!"=="1" (call :pass "Ключ %F1%:AUTOCHECK встречается ровно 1 раз") else call :fail "Ключ %F1%:AUTOCHECK встречается !FKC! раз(а), ожидалось 1"

REM Финальный статус ручной проверки зафиксирован в БД.
docker exec postgres psql -U orchestrpay -d fraudcheckdb -t -A -c "SELECT \"Status\" FROM \"Payments\" WHERE \"PaymentId\"='%F2%'" > "%TMP%\fst.txt" 2>&1
set /p FST=<"%TMP%\fst.txt"
if "!FST!"=="FRAUD_OPERATION_DETECTED" (call :pass "Статус FraudCheck в БД: FRAUD_OPERATION_DETECTED") else call :fail "Статус FraudCheck в БД: !FST!, ожидался FRAUD_OPERATION_DETECTED"

REM ============ Итог ============
echo.
echo =============================================
echo 🏁 Итого: !PASS! ✅ пройдено, !FAIL! ❌ упало
echo =============================================
if not "!FAIL!"=="0" exit /b 1
echo ✅ Все тесты пройдены!
exit /b 0

REM ============ Вспомогательные подпрограммы ============
:pass
set /a PASS+=1
echo ✅ %~1
goto :eof

:fail
set /a FAIL+=1
echo ❌ %~1
goto :eof

:warn
echo ⚠️  %~1
goto :eof

:newid
for /f %%g in ('powershell -NoProfile -Command "[guid]::NewGuid()"') do set "%~1=%%g"
goto :eof
