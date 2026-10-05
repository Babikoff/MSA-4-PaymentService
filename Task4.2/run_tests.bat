@echo off
REM Smoke/regression tests for the currently deployed Task4.2 stack.
setlocal EnableExtensions EnableDelayedExpansion
chcp 65001 >nul
cd /d "%~dp0"
set "BASE=http://localhost:3000"
set "TMP=%TEMP%\task42_tests"
if not exist "%TMP%" mkdir "%TMP%"
set PASS=0
set FAIL=0

echo.
echo 🏁 Регрессионный тест Task4.2 - Camunda 8 + Payment Service
echo.

echo 🧪 Проверка окружения...

docker version >nul 2>&1
if errorlevel 1 (call :fail "Docker не запущен") else call :pass "Docker запущен"

docker compose ps -a --format "{{.Service}} {{.Status}}" > "%TMP%\svc.txt" 2>nul
for %%s in (zeebe elasticsearch operate tasklist postgres payment-service) do (
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

REM ============ Платформа Camunda 8 ============
echo.
echo 🧪 Проверка платформы Camunda 8...

set "ZBCTL=%~dp0..\Task4.1\bpmn-tests\node_modules\.bin\zbctl.cmd"
if not exist "%ZBCTL%" (
    call :warn "zbctl не найден - проверка Zeebe пропущена"
    goto zeebe_done
)
call "%ZBCTL%" status --address localhost:26500 --insecure >nul 2>&1
if errorlevel 1 (call :fail "Zeebe gateway недоступен") else call :pass "Zeebe gateway доступен"
:zeebe_done

docker compose logs --no-color deploy-process > "%TMP%\deploy.log" 2>&1
findstr /C:"PaymentSaga" "%TMP%\deploy.log" >nul
if errorlevel 1 (call :fail "Процесс PaymentSaga не задеплоен") else call :pass "Процесс PaymentSaga задеплоен"
findstr /C:"CancelPayment" "%TMP%\deploy.log" >nul
if errorlevel 1 (call :fail "Процесс CancelPayment не задеплоен") else call :pass "Процесс CancelPayment задеплоен"
findstr /C:"Processes deployed" "%TMP%\deploy.log" >nul
if errorlevel 1 (call :fail "deploy-process не сообщил об успехе") else call :pass "deploy-process сообщил об успехе"

for /f %%c in ('curl.exe -s -o NUL -w "%%{http_code}" http://localhost:8081/') do set "OC=%%c"
if "!OC!"=="200" (call :pass "Operate отвечает (8081)") else call :fail "Operate не отвечает, код !OC!"
for /f %%c in ('curl.exe -s -o NUL -w "%%{http_code}" http://localhost:8082/') do set "TC=%%c"
if "!TC!"=="200" (call :pass "Tasklist отвечает (8082)") else call :fail "Tasklist не отвечает, код !TC!"

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

REM ============ Жизненный цикл денег ============
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
