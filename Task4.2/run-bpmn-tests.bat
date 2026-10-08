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
for %%s in (zeebe elasticsearch operate tasklist postgres) do (
    findstr /b /c:"%%s Up" "%TMP%\svc.txt" >nul
    if errorlevel 1 (call :fail "Сервис %%s не запущен") else call :pass "Сервис %%s запущен"
)
findstr /c:"deploy-process Exited (0)" "%TMP%\svc.txt" >nul
if errorlevel 1 (call :fail "deploy-process не отработал с кодом 0") else call :pass "deploy-process отработал успешно"

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



REM ============ Orchestrator (.NET) E2E tests ============
echo.
echo Test Orchestrator (E2E: Zeebe - Orchestrator - domain services)...
echo.

set "OBASE=http://localhost:3003"

curl.exe -s -f "%OBASE%/health" >nul 2>&1
if not errorlevel 1 (call :pass "Orchestrator /health отвечает (3003)") else (call :fail "Orchestrator /health не отвечает (3003)")

curl.exe -s -f "%BASE%/health" >nul 2>&1
if not errorlevel 1 (call :pass "PaymentService /health отвечает") else (call :fail "PaymentService /health не отвечает")
curl.exe -s -f "%FRBASE%/health" >nul 2>&1
if not errorlevel 1 (call :pass "FraudCheckService /health отвечает") else (call :fail "FraudCheckService /health не отвечает")
curl.exe -s -f "http://localhost:3002/health" >nul 2>&1
if not errorlevel 1 (call :pass "NotificationService /health отвечает") else (call :fail "NotificationService /health не отвечает")
echo.

call :newid E1
set "PAY=%E1%"

echo {"paymentId":"%PAY%","payerId":"e2e-user","counterpartyId":"e2e-acct","amount":99.5,"currency":"USD"} > "%TMP%\vars.json"
call "%ZBCTL%" publish message --address localhost:26500 --insecure START_PAYMENT --correlationKey %PAY% --variables "%TMP%\vars.json" >nul 2>&1
if not errorlevel 1 (call :pass "START_PAYMENT опубликован") else (call :fail "START_PAYMENT publish failed")

set /a att=0
:wait_saga
timeout /t 3 /nobreak >nul

curl.exe -s -f "%OBASE%/sagas/%PAY%/status" > "%TMP%\saga.txt" 2>nul
if not errorlevel 1 (
    findstr /C:"COMPLETED" "%TMP%\saga.txt" >nul
    if not errorlevel 1 (
        call :pass "Сага COMPLETED (E2E через оркестратор)"
        goto saga_done
    )
    findstr /C:"CANCELED" "%TMP%\saga.txt" >nul
    if not errorlevel 1 (
        call :pass "Сага CANCELED (E2E через оркестратор)"
        goto saga_done
    )
    set /a att+=1
    if !att! lss 20 goto wait_saga
    call :fail "Сага не завершилась за ~60с:"
    type "%TMP%\saga.txt"
) else (
    set /a att+=1
    if !att! lss 20 goto wait_saga
    call :fail "Orchestrator не вернул статус саги за ~60с"
)
:saga_done
del /q "%TMP%\saga.txt" "%TMP%\vars.json" 2>nul

curl.exe -s -f "%BASE%/api/payments/%PAY%" > "%TMP%\pay.txt" 2>nul
if not errorlevel 1 (
    findstr /C:"%PAY%" "%TMP%\pay.txt" >nul
    if not errorlevel 1 (call :pass "Платеж виден в PaymentService (создан оркестратором)") else (call :fail "PaymentService вернул чужой платеж")
) else (call :fail "Платеж не найден в PaymentService")
del /q "%TMP%\pay.txt" 2>nul

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
