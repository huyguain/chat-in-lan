@echo off
echo ========================================
echo    Secure LAN Chat System
echo ========================================
echo.

echo Checking .NET SDK...
dotnet --version
if %errorlevel% neq 0 (
    echo ERROR: .NET SDK not found!
    echo Please install .NET 6.0 SDK from: https://dotnet.microsoft.com/download/dotnet/6.0
    pause
    exit /b 1
)

echo.
echo Building project...
cd src\Server
dotnet restore
if %errorlevel% neq 0 (
    echo ERROR: Failed to restore packages
    pause
    exit /b 1
)

dotnet build
if %errorlevel% neq 0 (
    echo ERROR: Failed to build project
    pause
    exit /b 1
)

echo.
echo Creating database...
dotnet ef database update
if %errorlevel% neq 0 (
    echo WARNING: Database migration failed, but continuing...
)

echo.
echo Starting Secure LAN Chat Server...
echo Server will be available at: https://localhost:5001
echo HTTP version: http://localhost:5000
echo.
echo Press Ctrl+C to stop the server
echo.

dotnet run

pause
