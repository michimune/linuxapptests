# ConnectionStringTest

An automated C# console application that tests Azure App Service deployment and database connectivity.

## Prerequisites

1. **Azure CLI**: Install and login to Azure CLI
   ```powershell
   az login
   ```

2. **Azure Subscription**: Ensure you have an active Azure subscription with appropriate permissions to:
   - Create resource groups
   - Create App Service plans and web apps
   - Create PostgreSQL servers
   - Deploy applications

3. **Resource Provider Registration**: Register the PostgreSQL resource provider (one-time setup)
   ```powershell
   az provider register --namespace Microsoft.DBforPostgreSQL --wait
   ```
   
   Verify registration:
   ```powershell
   az provider show --namespace Microsoft.DBforPostgreSQL --query registrationState
   ```

4. **Sample Application**: Ensure you have the sample marketing app zip file available

## What the Application Does

This console application automatically runs through the following test sequence:

### Step A: Create Resources
- Creates a new resource group
- Creates a PostgreSQL Flexible Server instance with:
  - Standard_B1ms SKU (Burstable tier)
  - 32GB storage
  - PostgreSQL version 14
  - Firewall rule to allow Azure services
- Creates a database within the PostgreSQL server
- Creates an App Service Plan (Free tier, Linux)
- Creates a Web App with Python 3.11 runtime
- Deploys the sample marketing application from the zip file

### Step B: Remove Environment Variables
- Removes `DATABASE_URL` and `SECRET_KEY` environment variables if they exist

### Step C: Test Error State
- Makes an HTTP request to the web app
- Confirms it returns a 500+ status code (expected due to missing database connection)

### Step D: Add Environment Variables
- Adds `DATABASE_URL` with actual PostgreSQL connection string from the created server
- Adds `SECRET_KEY` for the application
- Displays the connection details for verification

### Step E: Validate Working State
- Makes HTTP requests to the web app
- Confirms it returns a 200 status code
- Retries up to 6 times with 10-second delays if it still returns 500+

### Step F: Cleanup
- Deletes all created Azure resources by removing the resource group

## Running the Application

1. Open PowerShell in the project directory
2. Restore NuGet packages:
   ```powershell
   dotnet restore
   ```
3. (Optional) Customize settings by editing `appsettings.json` if needed
4. Run the application with required command line arguments:
   ```powershell
   dotnet run <subscription-id> <zip-file-path>
   ```   
   Example:
   ```powershell
   dotnet run 12345678-1234-1234-1234-123456789012 "C:\MyProject\SampleMarketingApp_Complete.zip"
   ```

### Command Line Arguments

- **First argument**: Azure subscription ID (required)
- **Second argument**: Full path to the deployment zip file (required)

## Configuration

The application includes an optional `appsettings.json` file that allows you to customize:

- **Azure Location** - Default: "Brazil South"
- **Database Settings** - Admin username, password, and database name
- **Application Settings** - Secret key for the deployed app
- **Test Settings** - Retry attempts and delay intervals

**Note**: The deployment zip path and subscription ID are now provided via command line arguments and are no longer configurable in appsettings.json.

If no configuration file is found, the application uses sensible defaults.

## Important Notes

- The application runs non-interactively - no user input is required
- All Azure resources are created with unique names based on timestamp
- Resources are automatically cleaned up at the end
- The application uses `DefaultAzureCredential` for authentication (requires Azure CLI login)
- Deployment may take several minutes to complete
- Free tier App Service Plan is used to minimize costs

## Troubleshooting

- Ensure you're logged into Azure CLI: `az login`
- Verify you have the required permissions in your Azure subscription
- Check that the sample application zip file exists at the specified path
- Monitor Azure portal for resource creation progress if needed

## Dependencies

- Azure.ResourceManager
- Azure.ResourceManager.AppService
- Azure.ResourceManager.PostgreSql
- Azure.Identity
- Newtonsoft.Json

## Recent Updates

### Database Initialization Fixes (June 2025)
- **Fixed**: ModuleNotFoundError for psycopg2 by adding automatic Python package installation
- **Fixed**: Program continuation after Step A failure - now exits early if Azure resources cannot be created
- **Added**: `InstallPythonPackages()` method for automatic dependency management
- **Enhanced**: Error handling and early termination logic for critical failures
