@echo off
setlocal EnableExtensions EnableDelayedExpansion
cd /d "%~dp0"

set "ZBCTL=%~dp0node_modules\.bin\zbctl.cmd"
set "ZB_URL=--address localhost:26500 --insecure"
set "BPMN_DIR=%~dp0..\bpmn"

echo.
echo #####################################################
echo #   Zeebe BPMN smoke test  -  OrchestrPay saga      #
echo #####################################################
echo.

echo [0/9] Check local zbctl and install if none.
if exist "%ZBCTL%" goto zbctl_ready
echo Installing zbctl via npm ...
npm install --prefix "%~dp0." --no-fund --no-audit zbctl@8.6.0
if errorlevel 1 ( echo   [FAIL] npm install zbctl & exit /b 1 )
:zbctl_ready

echo [1/9] Waiting for Zeebe gateway (localhost:26500) ...
set /a tries=0
:waitzeebe
  call "%ZBCTL%" status %ZB_URL% >nul 2>&1
  if not errorlevel 1 goto ready
  set /a tries+=1
  if !tries! geq 30 ( echo   [FAIL] Zeebe gateway not ready. Run deploy-test-env.bat first. & exit /b 1 )
  echo Waiting ...
  timeout /t 5 /nobreak >nul
  goto waitzeebe
:ready
echo Zeebe is up.
echo.

echo [2/9] Deploy payment-saga.bpmn ...
call "%ZBCTL%" deploy %ZB_URL% "%BPMN_DIR%\payment-saga.bpmn"
if errorlevel 1 ( echo   [FAIL] deploy payment-saga.bpmn & exit /b 1 )

echo [3/9] Deploy cancel-payment.bpmn ...
call "%ZBCTL%" deploy %ZB_URL% "%BPMN_DIR%\cancel-payment.bpmn"
if errorlevel 1 ( echo   [FAIL] deploy cancel-payment.bpmn & exit /b 1 )

echo Waiting ...
timeout /t 3 /nobreak >nul

echo [4/9] Publish START_PAYMENT message (correlationKey=pmt-5) ...
call "%ZBCTL%" publish message %ZB_URL% START_PAYMENT --correlationKey pmt-5
if errorlevel 1 ( echo   [FAIL] publish START_PAYMENT & exit /b 1 )

echo Waiting ...
timeout /t 5 /nobreak >nul

echo [5/9] Verify PaymentSaga reached CREATE_PAYMENT job ...
call "%ZBCTL%" activate jobs %ZB_URL% CREATE_PAYMENT --maxJobsToActivate 1 --timeout 300ms > "%~dp0r_create.json" 2>&1
findstr /C:"CREATE_PAYMENT" "%~dp0r_create.json" >nul
if errorlevel 1 ( echo   [FAIL] no CREATE_PAYMENT job found - instance may not have started. & type "%~dp0r_create.json" & exit /b 1 )
echo   [OK] CREATE_PAYMENT job activated.
type "%~dp0r_create.json"

echo.
echo [6/9] Publish CANCEL_PAYMENT (correlationKey=pmt-5) ...
call "%ZBCTL%" publish message %ZB_URL% CANCEL_PAYMENT --correlationKey pmt-5
if errorlevel 1 ( echo   [FAIL] publish CANCEL_PAYMENT & exit /b 1 )

echo Waiting ...
timeout /t 5 /nobreak >nul

echo [7/9] Verify CancelPayment reached RETURN_FUNDS job ...
call "%ZBCTL%" activate jobs %ZB_URL% RETURN_FUNDS --maxJobsToActivate 1 --timeout 300ms > "%~dp0r_return.json" 2>&1
findstr /C:"RETURN_FUNDS" "%~dp0r_return.json" >nul
if errorlevel 1 ( echo   [FAIL] no RETURN_FUNDS job found - instance may not have started. & type "%~dp0r_return.json" & exit /b 1 )
echo   [OK] RETURN_FUNDS job activated.
type "%~dp0r_return.json"

echo.
echo [8/9] Cluster status:
call "%ZBCTL%" status %ZB_URL%

del /q "%~dp0r_create.json" "%~dp0r_return.json" 2>nul

echo.
echo =============================================
echo ALL CHECKS PASSED - both BPMN models deploy
echo and their instances start correctly.
echo =============================================
exit /b 0