@echo off
if "%1"=="" (
    echo Usage: run-test.bat ^<subscription-id^>
    echo Example: run-test.bat f07f3711-b45e-40fe-a941-4e6d93f851e6
    echo.
    echo Alternatively, set the AZURE_SUBSCRIPTION_ID environment variable and run without arguments.
    pause >nul
    exit /b 1
)

echo Running PackageDependencyTest with subscription ID: %1
echo.
cd /d "d:\repos\VibeCoding\PackageDependencyTest"
dotnet run %1
echo.
echo Test completed. Press any key to exit...
pause >nul
