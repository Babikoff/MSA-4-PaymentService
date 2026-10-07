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
