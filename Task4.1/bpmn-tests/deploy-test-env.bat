@echo off
setlocal
cd /d "%~dp0"

echo ============================================
echo  Deploy throwaway Zeebe test stack
echo  camunda/zeebe:8.5.0  (gateway :26500)
echo ============================================

docker compose -f docker-compose.zeebe-test.yml up -d
if errorlevel 1 (
  echo [ERROR] docker compose up failed.
  exit /b 1
)

echo.
echo Stack started. Containers:
docker compose -f docker-compose.zeebe-test.yml ps

echo.
echo Zeebe gateway is exposed on localhost:26500 (network zeebe-test-net).
echo Next step: run run_test.bat to execute the BPMN checks.
echo Teardown : docker compose -f docker-compose.zeebe-test.yml down -v
endlocal