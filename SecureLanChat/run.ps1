# Secure LAN Chat System - PowerShell Script
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "    Secure LAN Chat System" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if .NET SDK is installed
Write-Host "Checking .NET SDK..." -ForegroundColor Yellow
try {
    $dotnetVersion = dotnet --version
    Write-Host "✅ .NET SDK found: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host "❌ .NET SDK not found!" -ForegroundColor Red
    Write-Host "Please install .NET 6.0 SDK from: https://dotnet.microsoft.com/download/dotnet/6.0" -ForegroundColor Yellow
    Read-Host "Press Enter to exit"
    exit 1
}

# Navigate to Server directory
Set-Location "src\Server"

# Restore packages
Write-Host ""
Write-Host "Restoring packages..." -ForegroundColor Yellow
dotnet restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Failed to restore packages" -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit 1
}
Write-Host "✅ Packages restored successfully" -ForegroundColor Green

# Build project
Write-Host ""
Write-Host "Building project..." -ForegroundColor Yellow
dotnet build
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Failed to build project" -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit 1
}
Write-Host "✅ Project built successfully" -ForegroundColor Green

# Create database
Write-Host ""
Write-Host "Creating database..." -ForegroundColor Yellow
try {
    dotnet ef database update
    Write-Host "✅ Database created/updated successfully" -ForegroundColor Green
} catch {
    Write-Host "⚠️ Database migration failed, but continuing..." -ForegroundColor Yellow
}

# Start the application
Write-Host ""
Write-Host "Starting Secure LAN Chat Server..." -ForegroundColor Cyan
Write-Host "Server will be available at:" -ForegroundColor White
Write-Host "  • HTTPS: https://localhost:3000" -ForegroundColor Green
Write-Host "  • HTTP:  http://localhost:3001" -ForegroundColor Green
Write-Host "  • Swagger: https://localhost:3000/swagger" -ForegroundColor Green
Write-Host ""
Write-Host "Press Ctrl+C to stop the server" -ForegroundColor Yellow
Write-Host ""

# Run the application with HTTPS profile
dotnet run --launch-profile https
