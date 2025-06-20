# Test script for ConnectionStringTest application
Write-Host "Testing ConnectionStringTest application..." -ForegroundColor Green
Write-Host ""

Write-Host "Building the application..." -ForegroundColor Yellow
try {
    $buildResult = dotnet build 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed!" -ForegroundColor Red
        Write-Host $buildResult -ForegroundColor Red
        Read-Host "Press Enter to exit"
        exit 1
    }
    
    Write-Host ""
    Write-Host "Build successful!" -ForegroundColor Green
    Write-Host ""
    Write-Host "To run the full test (which creates actual Azure resources):" -ForegroundColor Cyan
    Write-Host "  dotnet run <subscription-id> <zip-file-path>" -ForegroundColor White
    Write-Host ""    Write-Host "Example:" -ForegroundColor Cyan
    Write-Host "  dotnet run 12345678-1234-1234-1234-123456789012 `"C:\MyProject\SampleMarketingApp_Complete.zip`"" -ForegroundColor White
    Write-Host ""
    Write-Host "Command line arguments:" -ForegroundColor Cyan
    Write-Host "  1. Azure subscription ID (required)" -ForegroundColor White
    Write-Host "  2. Full path to deployment zip file (required)" -ForegroundColor White
    Write-Host ""
    Write-Host "To run with custom configuration:" -ForegroundColor Cyan
    Write-Host "  1. Edit appsettings.json for database and other settings" -ForegroundColor White
    Write-Host "  2. dotnet run <subscription-id> <zip-file-path>" -ForegroundColor White
    Write-Host ""
    Write-Host "Note: Running the application will create Azure resources that may incur costs." -ForegroundColor Yellow
    Write-Host "Make sure you have Azure CLI logged in and appropriate permissions." -ForegroundColor Yellow
    Write-Host ""
} catch {
    Write-Host "Error during build: $($_.Exception.Message)" -ForegroundColor Red
}

Read-Host "Press Enter to exit"
