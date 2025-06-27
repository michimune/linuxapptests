@echo off
setlocal enabledelayedexpansion

:: Check command line parameters
if "%~3"=="" (
    echo Error: Missing required parameters
    echo Usage: setup.bat ^<subscription-id^> ^<region^> ^<prefix^>
    echo Example: setup.bat "12345678-1234-1234-1234-123456789012" "eastus" "myapp"
    exit /b 1
)

set SUBSCRIPTION_ID=%~1
set REGION=%~2
set PREFIX=%~3
set BASE_DIR=%~dp0

echo ========================================
echo Azure Sample Apps Setup Script
echo ========================================
echo Subscription ID: %SUBSCRIPTION_ID%
echo Region: %REGION%
echo Prefix: %PREFIX%
echo Base Directory: %BASE_DIR%
echo ========================================

:: Check if .NET 8.0 SDK is installed
echo Checking for .NET 8.0 SDK...
dotnet --list-sdks | findstr "8.0" >nul
if %errorlevel% neq 0 (
    echo .NET 8.0 SDK not found. Installing...
    
    :: Download and install .NET 8.0 SDK
    echo Downloading .NET 8.0 SDK installer...
    set DOTNET_INSTALLER=dotnet-sdk-8.0-win-x64.exe
      :: Use PowerShell to download the installer
    powershell -Command "& {[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; Invoke-WebRequest -Uri 'https://builds.dotnet.microsoft.com/dotnet/Sdk/8.0.411/dotnet-sdk-8.0.411-win-x64.exe' -OutFile '%DOTNET_INSTALLER%'}"
    
    if not exist "%DOTNET_INSTALLER%" (
        echo Error: Failed to download .NET 8.0 SDK installer
        exit /b 1
    )
    
    echo Installing .NET 8.0 SDK...
    "%DOTNET_INSTALLER%" /quiet /norestart
    
    if %errorlevel% neq 0 (
        echo Error: Failed to install .NET 8.0 SDK
        exit /b 1
    )
    
    :: Clean up installer
    del "%DOTNET_INSTALLER%"
    
    echo .NET 8.0 SDK installed successfully
) else (
    echo .NET 8.0 SDK is already installed
)

:: Verify .NET installation
echo Verifying .NET installation...
dotnet --version
if %errorlevel% neq 0 (
    echo Error: .NET SDK verification failed
    exit /b 1
)

:: Check if Azure CLI is installed
echo ========================================
echo Checking Azure CLI installation...
echo ========================================
az --version >nul 2>&1
if %errorlevel% neq 0 (
    echo Azure CLI not found. Installing...
    
    :: Download and install Azure CLI
    echo Downloading Azure CLI installer...
    set AZ_INSTALLER=AzureCLI.msi
    
    :: Use PowerShell to download the installer
    powershell -Command "& {[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; Invoke-WebRequest -Uri 'https://aka.ms/installazurecliwindows' -OutFile '%AZ_INSTALLER%'}"
    
    if not exist "%AZ_INSTALLER%" (
        echo Error: Failed to download Azure CLI installer
        exit /b 1
    )
    
    echo Installing Azure CLI...
    msiexec /i "%AZ_INSTALLER%" /quiet /norestart
    
    if %errorlevel% neq 0 (
        echo Error: Failed to install Azure CLI
        exit /b 1
    )
    
    :: Clean up installer
    del "%AZ_INSTALLER%"
    
    echo Azure CLI installed successfully
    echo Note: You may need to restart your command prompt for az command to work
) else (
    echo Azure CLI is already installed
)

:: Verify Azure CLI installation
echo Verifying Azure CLI installation...
az --version >nul 2>&1
if %errorlevel% neq 0 (
    echo Warning: Azure CLI verification failed. You may need to restart your command prompt.
)

:: Restore and build DeploySampleApps
echo ========================================
echo Restoring and building DeploySampleApps...
echo ========================================
cd /d "%BASE_DIR%DeploySampleApps"
if not exist "DeploySampleApps.csproj" (
    echo Error: DeploySampleApps.csproj not found in %BASE_DIR%DeploySampleApps
    exit /b 1
)

echo Restoring DeploySampleApps...
dotnet restore
if %errorlevel% neq 0 (
    echo Error: Failed to restore DeploySampleApps
    exit /b 1
)

echo Building DeploySampleApps...
dotnet build --configuration Release
if %errorlevel% neq 0 (
    echo Error: Failed to build DeploySampleApps
    exit /b 1
)

:: Check if BadScenarioLinux directory exists
echo ========================================
echo Restoring and building BadScenarioLinux...
echo ========================================
cd /d "%BASE_DIR%BadScenarioLinux"
if not exist "BadScenarioLinux.csproj" (
    echo Warning: BadScenarioLinux.csproj not found in %BASE_DIR%BadScenarioLinux
    echo Skipping BadScenarioLinux build...
) else (
    echo Restoring BadScenarioLinux...
    dotnet restore
    if %errorlevel% neq 0 (
        echo Error: Failed to restore BadScenarioLinux
        exit /b 1
    )

    echo Building BadScenarioLinux...
    dotnet build --configuration Release
    if %errorlevel% neq 0 (
        echo Error: Failed to build BadScenarioLinux
        exit /b 1
    )
)

:: Run DeploySampleApps
echo ========================================
echo Running DeploySampleApps...
echo ========================================
cd /d "%BASE_DIR%DeploySampleApps"

echo Executing: dotnet run --configuration Release -- "%SUBSCRIPTION_ID%" "%REGION%" "%BASE_DIR%" "%PREFIX%"
dotnet run --configuration Release -- "%SUBSCRIPTION_ID%" "%REGION%" "%BASE_DIR%\" "%PREFIX%"

set DEPLOY_EXIT_CODE=%errorlevel%
if %DEPLOY_EXIT_CODE% neq 0 (
    echo ========================================
    echo DeploySampleApps completed with exit code: %DEPLOY_EXIT_CODE%
    echo ========================================
) else (
    echo ========================================
    echo DeploySampleApps completed successfully!
    echo ========================================
)

:: Return to original directory
cd /d "%BASE_DIR%"

echo Setup script completed.
exit /b %DEPLOY_EXIT_CODE%
