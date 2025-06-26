# Test script to validate prerequisites
param(
    [Parameter(Mandatory=$true)]
    [string]$SubscriptionId,
    
    [Parameter(Mandatory=$true)]
    [string]$Region,
    
    [Parameter(Mandatory=$true)]
    [string]$SampleAppBaseDir
)

Write-Host "Validating prerequisites for DeploySampleApps..." -ForegroundColor Yellow

# Check if .NET is installed
try {
    $dotnetVersion = dotnet --version
    Write-Host "✓ .NET SDK version: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host "✗ .NET SDK not found. Please install .NET 8.0 SDK" -ForegroundColor Red
    exit 1
}

# Check if Azure CLI is installed and authenticated
try {
    az version --output table 2>$null | Out-Null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Azure CLI is installed" -ForegroundColor Green
        
        # Check if logged in
        az account show 2>$null | Out-Null
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✓ Azure CLI is authenticated" -ForegroundColor Green
        } else {
            Write-Host "✗ Azure CLI not authenticated. Run 'az login'" -ForegroundColor Red
            exit 1
        }
    } else {
        Write-Host "✗ Azure CLI not found. Please install Azure CLI" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "✗ Azure CLI not found or not working properly" -ForegroundColor Red
    exit 1
}

# Validate subscription access
try {
    az account show --subscription $SubscriptionId 2>$null | Out-Null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Subscription $SubscriptionId is accessible" -ForegroundColor Green
    } else {
        Write-Host "✗ Cannot access subscription $SubscriptionId" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "✗ Cannot validate subscription access" -ForegroundColor Red
    exit 1
}

# Check required directories
if (Test-Path (Join-Path $SampleAppBaseDir "SampleMarketingApp")) {
    Write-Host "✓ SampleMarketingApp directory found" -ForegroundColor Green
} else {
    Write-Host "✗ SampleMarketingApp directory not found in $SampleAppBaseDir" -ForegroundColor Red
    Write-Host "  Please ensure you're using the correct base directory" -ForegroundColor Yellow
}

if (Test-Path (Join-Path $SampleAppBaseDir "SampleMarketingAppBad")) {
    Write-Host "✓ SampleMarketingAppBad directory found" -ForegroundColor Green
} else {
    Write-Host "✗ SampleMarketingAppBad directory not found in $SampleAppBaseDir" -ForegroundColor Red
    Write-Host "  Please ensure you're using the correct base directory" -ForegroundColor Yellow
}

# Validate region
$validRegions = @('eastus', 'eastus2', 'westus', 'westus2', 'westus3', 'centralus', 'northcentralus', 'southcentralus', 'westcentralus', 'canadacentral', 'canadaeast', 'brazilsouth', 'northeurope', 'westeurope', 'uksouth', 'ukwest', 'francecentral', 'germanywestcentral', 'norwayeast', 'switzerlandnorth', 'swedencentral', 'eastasia', 'southeastasia', 'japaneast', 'japanwest', 'koreacentral', 'koreasouth', 'southindia', 'centralindia', 'westindia', 'australiaeast', 'australiasoutheast', 'southafricanorth')

if ($validRegions -contains $Region.ToLower()) {
    Write-Host "✓ Region '$Region' is valid" -ForegroundColor Green
} else {
    Write-Host "⚠ Region '$Region' may not be valid. Common regions: eastus, westus2, eastus2" -ForegroundColor Yellow
}

Write-Host "`nPrerequisites validation completed!" -ForegroundColor Yellow
Write-Host "To run the deployment:" -ForegroundColor Cyan
Write-Host "  dotnet run $SubscriptionId $Region `"$SampleAppBaseDir`"" -ForegroundColor White
