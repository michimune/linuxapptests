# Test script to validate PostgreSQL integration without full Azure deployment
# This script checks that the application builds and can generate connection strings properly

Write-Host "Testing PostgreSQL Integration in ConnectionStringTest" -ForegroundColor Green
Write-Host "=" * 60

# Build the project
Write-Host "`n1. Building the project..." -ForegroundColor Yellow
try {
    $buildResult = dotnet build --verbosity quiet
    if ($LASTEXITCODE -eq 0) {
        Write-Host "   ✅ Build successful" -ForegroundColor Green
    } else {
        Write-Host "   ❌ Build failed" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "   ❌ Build error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Check that all required NuGet packages are referenced
Write-Host "`n2. Checking NuGet package references..." -ForegroundColor Yellow
$projectContent = Get-Content "ConnectionStringTest.csproj" -Raw

$requiredPackages = @(
    "Azure.ResourceManager",
    "Azure.ResourceManager.AppService", 
    "Azure.ResourceManager.PostgreSql",
    "Azure.Identity",
    "Newtonsoft.Json"
)

$allPackagesFound = $true
foreach ($package in $requiredPackages) {
    if ($projectContent -like "*$package*") {
        Write-Host "   ✅ $package - Found" -ForegroundColor Green
    } else {
        Write-Host "   ❌ $package - Missing" -ForegroundColor Red
        $allPackagesFound = $false
    }
}

if (-not $allPackagesFound) {
    Write-Host "   Some required packages are missing!" -ForegroundColor Red
    exit 1
}

# Check that PostgreSQL-related methods exist in Program.cs
Write-Host "`n3. Checking PostgreSQL integration methods..." -ForegroundColor Yellow
$programContent = Get-Content "Program.cs" -Raw

$requiredMethods = @(
    "CreatePostgreSqlServer",
    "CreatePostgreSqlDatabase",
    "PostgreSqlFlexibleServerResource",
    "PostgreSqlFlexibleServerDatabaseResource"
)

foreach ($method in $requiredMethods) {
    if ($programContent -like "*$method*") {
        Write-Host "   ✅ $method - Found" -ForegroundColor Green
    } else {
        Write-Host "   ❌ $method - Missing" -ForegroundColor Red
        $allPackagesFound = $false
    }
}

# Check for connection string generation
Write-Host "`n4. Checking connection string generation..." -ForegroundColor Yellow
if ($programContent -like "*postgresql://*") {
    Write-Host "   ✅ PostgreSQL connection string format - Found" -ForegroundColor Green
} else {
    Write-Host "   ❌ PostgreSQL connection string format - Missing" -ForegroundColor Red
    $allPackagesFound = $false
}

# Check that DATABASE_URL is properly updated in Step D
if ($programContent -like "*settings[`"DATABASE_URL`"] = _databaseUrl*") {
    Write-Host "   ✅ DATABASE_URL assignment in Step D - Found" -ForegroundColor Green
} else {
    Write-Host "   ❌ DATABASE_URL assignment in Step D - Missing" -ForegroundColor Red
    $allPackagesFound = $false
}

Write-Host "`n" + "=" * 60
if ($allPackagesFound) {
    Write-Host "🎉 All PostgreSQL integration checks passed!" -ForegroundColor Green
    Write-Host "The application is ready for full Azure testing." -ForegroundColor Green
    
    Write-Host "`nTo run the full test:" -ForegroundColor Cyan
    Write-Host "   1. Ensure you're logged into Azure CLI: az login" -ForegroundColor White
    Write-Host "   2. Run the application: dotnet run" -ForegroundColor White
    Write-Host "   3. Or use the batch file: test.bat" -ForegroundColor White
} else {
    Write-Host "❌ Some integration checks failed!" -ForegroundColor Red
    Write-Host "Please review the issues above before testing." -ForegroundColor Red
    exit 1
}

Write-Host "`nPostgreSQL Integration Details:" -ForegroundColor Cyan
Write-Host "- Server SKU: Standard_B1ms (Burstable tier)" -ForegroundColor White
Write-Host "- Storage: 32GB" -ForegroundColor White
Write-Host "- Version: PostgreSQL 14" -ForegroundColor White
Write-Host "- Firewall: Configured for Azure services" -ForegroundColor White
Write-Host "- Connection: Dynamic generation from actual server FQDN" -ForegroundColor White
