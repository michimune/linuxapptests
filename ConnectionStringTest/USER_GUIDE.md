# ConnectionStringTest - User Guide

## Overview

The ConnectionStringTest application is an automated C# console application that performs end-to-end testing of Azure App Service deployment and PostgreSQL database connectivity. It simulates a real-world scenario where an application initially fails due to missing database configuration, then succeeds after proper configuration is applied.

## Prerequisites

### 1. Azure Setup
- **Azure CLI**: Must be installed and authenticated
  ```powershell
  # Install Azure CLI (if not already installed)
  winget install Microsoft.AzureCLI
  
  # Login to Azure
  az login
  
  # Verify your subscription
  az account show
  ```

- **Azure Permissions**: Your account must have permissions to:
  - Create and delete resource groups
  - Create App Service plans and web apps
  - Create PostgreSQL servers
  - Deploy applications to App Service

### 2. Sample Application
Ensure you have the deployment zip file ready. You'll specify its path when running the application.

### 3. Development Environment
- .NET 8.0 SDK
- Visual Studio Code (optional, but recommended)

## Configuration

### Default Configuration
The application works out-of-the-box with sensible defaults:
- **Location**: Brazil South
- **Deployment Path**: `C:\MyProject\SampleMarketingApp_Complete.zip`
- **Database**: PostgreSQL with basic tier
- **App Service**: Free tier (F1)

### Custom Configuration
You can customize settings by editing `appsettings.json`:

```json
{
  "AzureSettings": {
    "Location": "Brazil South"
  },
  "DatabaseSettings": {
    "AdminUsername": "adminuser",
    "AdminPassword": "P@ssw0rd123!",
    "DatabaseName": "marketingappdb"
  },
  "AppSettings": {
    "SecretKey": "your-secret-key-here-12345"
  },
  "Test": {
    "MaxRetryAttempts": 6,
    "RetryDelaySeconds": 10
  }
}
```

**Note**: The deployment zip path and subscription ID are now provided via command line arguments.

## Running the Application

### Method 1: Command Line
```powershell
# Navigate to the project directory
cd "C:\MyProject\ConnectionStringTest"

# Build the project
dotnet build

# Run the application with required arguments
dotnet run <subscription-id> <zip-file-path>
```

**Example**:
```powershell
dotnet run 12345678-1234-1234-1234-123456789012 "C:\MyProject\SampleMarketingApp_Complete.zip"
```

### Command Line Arguments
- **First argument**: Your Azure subscription ID (required)
- **Second argument**: Full path to the deployment zip file (required)

### Method 2: Using Test Scripts
**Note**: Test scripts need to be updated to pass the required command line arguments.

```powershell
# Windows Batch (update test.bat to include arguments)
test.bat

# PowerShell (update test.ps1 to include arguments)
.\test.ps1
```

### Method 3: VS Code
1. Open the project in VS Code
2. Press `F5` to run with debugging
3. Or use `Ctrl+F5` to run without debugging

## Test Sequence

The application performs these steps automatically:

### Step A: Create Azure Resources
- Creates a unique resource group
- Provisions a PostgreSQL server
- Creates an App Service plan (Free tier)
- Creates a web app
- Deploys the sample application

**Expected Time**: 5-10 minutes

### Step B: Remove Environment Variables
- Removes `DATABASE_URL` and `SECRET_KEY` from app settings
- This simulates an incomplete deployment

**Expected Time**: 10-20 seconds

### Step C: Test Error State
- Makes HTTP request to the deployed app
- Verifies it returns HTTP 500+ (due to missing database connection)

**Expected Time**: 5-10 seconds

### Step D: Add Environment Variables
- Adds proper `DATABASE_URL` with PostgreSQL connection string
- Adds `SECRET_KEY` for application security

**Expected Time**: 10-20 seconds

### Step E: Validate Success
- Makes HTTP requests to verify app returns HTTP 200
- Retries up to 6 times with 10-second delays if needed
- This accounts for Azure app restart time

**Expected Time**: 10-60 seconds

### Step F: Cleanup
- Deletes the entire resource group
- Removes all created Azure resources

**Expected Time**: 2-5 minutes

## Cost Considerations

- **App Service**: Free tier (F1) - No cost
- **PostgreSQL**: Basic tier - Minimal cost (~$0.02/hour)
- **Storage**: Minimal usage - Negligible cost

**Total estimated cost**: Less than $0.10 for a complete test run.

## Troubleshooting

### Common Issues

#### 1. Authentication Errors
```
Error: No Azure subscription found
```
**Solution**: Run `az login` and ensure you're authenticated.

#### 2. Permission Errors
```
Error: Insufficient privileges to complete the operation
```
**Solution**: Ensure your Azure account has Contributor access to the subscription.

#### 3. Missing Deployment File
```
Error: Deployment zip file not found
```
**Solution**: 
- Verify the file exists at the specified path
- Update `appsettings.json` with the correct path
- Ensure the file is a valid zip archive

#### 4. Resource Creation Timeouts
```
Error: Operation timed out
```
**Solution**: 
- Check Azure service health
- Try a different Azure region
- Retry the operation

#### 5. App Deployment Issues
```
Error: Deployment failed
```
**Solution**:
- Verify the zip file contains a valid Python web application
- Check that `requirements.txt` is properly formatted
- Ensure the application is compatible with Azure App Service

### Debugging Tips

1. **Enable Verbose Logging**: The application already provides detailed console output
2. **Check Azure Portal**: Monitor resource creation in real-time
3. **Use Breakpoints**: Run in debug mode to step through the process
4. **Test Individual Steps**: Modify the code to skip certain steps for isolated testing

## Security Notes

- **Passwords**: Default passwords are used for testing. In production, use strong, unique passwords
- **Cleanup**: The application automatically cleans up resources to prevent security exposure
- **Credentials**: Azure credentials are handled via Azure CLI authentication

## Extending the Application

### Adding New Test Steps
1. Create a new method following the pattern: `StepX_Description()`
2. Add error handling with try-catch blocks
3. Add the step to the `RunTestMethod()` sequence
4. Update console output with clear progress indicators

### Supporting Other Database Types
1. Add new NuGet packages for the desired database provider
2. Create database-specific creation methods
3. Update connection string format in `StepD_AddEnvironmentVariables()`

### Testing Different Application Types
1. Modify the `CreateWebApp()` method to support different runtimes
2. Update deployment logic in `DeployCode()` if needed
3. Adjust health check logic in steps C and E

## Support

For issues or questions:
1. Check the troubleshooting section above
2. Review Azure service documentation
3. Verify your Azure subscription status and permissions

## Version History

- **v1.0**: Initial release with basic PostgreSQL testing
- **v1.1**: Added configuration file support
- **v1.2**: Added VS Code integration and improved error handling
