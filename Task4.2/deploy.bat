@echo off
REM Deployment of all containers from docker-compose.yml (Camunda 8 platform + Postgres + services).
setlocal

cd /d "%~dp0"
echo Deploying Task4.2 stack from %CD% ...

REM Ensure Docker is running.
docker version >nul 2>&1
if errorlevel 1 (
    echo [ERROR] Docker is not running. Start Docker Desktop and retry.
    exit /b 1
)

REM Build images and start all services (.env is picked up automatically).
docker compose up -d --build
if errorlevel 1 (
    echo [ERROR] docker compose up failed.
    exit /b 1
)

echo.
echo --- Container status ---
docker compose ps
echo.
echo Done. Key endpoints:
echo   Zeebe gateway : localhost:26500
echo   Operate       : http://localhost:8081
echo   Tasklist      : http://localhost:8082
echo   Payment       : http://localhost:3000/api/payments
exit /b 0
