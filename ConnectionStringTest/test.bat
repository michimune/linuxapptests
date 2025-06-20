@echo off
echo Testing ConnectionStringTest application...
echo.

echo Building the application...
dotnet build
if errorlevel 1 (
    echo Build failed!
    pause
    exit /b 1
)

echo.
echo Build successful!
echo.
echo To run the full test (which creates actual Azure resources):
echo   dotnet run ^<subscription-id^> ^<zip-file-path^>
echo.
echo Example:
echo   dotnet run 12345678-1234-1234-1234-123456789012 "C:\MyProject\SampleMarketingApp_Complete.zip"
echo.
echo Command line arguments:
echo   1. Azure subscription ID (required)
echo   2. Full path to deployment zip file (required)
echo.
echo To run with custom configuration:
echo   1. Edit appsettings.json for database and other settings
echo   2. dotnet run ^<subscription-id^> ^<zip-file-path^>
echo.
echo Note: Running the application will create Azure resources that may incur costs.
echo Make sure you have Azure CLI logged in and appropriate permissions.
echo.
pause
