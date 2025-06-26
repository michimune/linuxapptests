# DeploySampleApps

A C# console application that creates a complete Azure infrastructure and deploys sample marketing applications using the Azure Resource Manager SDK.

## Overview

This application automates the deployment of:
- Azure Resource Group
- Virtual Network with subnets
- PostgreSQL Flexible Server with private endpoint
- App Service Plan (S1 Linux with Python 3.11)
- Web App with VNet integration
- Application deployments to production and staging slots

## Prerequisites

- .NET 8.0 SDK
- Azure CLI installed and authenticated
- Valid Azure subscription
- SampleMarketingApp and SampleMarketingAppBad directories in the current working directory

## Usage

```bash
dotnet run <subscription-id> <region> <sample-app-base-dir> <resource-name>
```

### Example
```bash
dotnet run 12345678-1234-1234-1234-123456789012 eastus C:\MyProjects mymarketingapp
```

## Parameters

- `subscription-id`: Your Azure subscription ID
- `region`: Azure region for deployment (e.g., eastus, westus2, etc.)
- `sample-app-base-dir`: Base directory containing SampleMarketingApp and SampleMarketingAppBad folders
- `resource-name`: Custom name used for all Azure resources (e.g., mymarketingapp, testapp123)

## Directory Structure

The application expects the following directory structure under the specified base directory:
```
<sample-app-base-dir>/
├── SampleMarketingApp/     # Main application files
├── SampleMarketingAppBad/  # Bad scenario application files
└── zip/                    # Created automatically for zip files
    ├── SampleMarketingApp.zip
    └── SampleMarketingAppBad.zip
```

## Infrastructure Created

### Networking
- **VNet**: 10.0.0.0/16 address space
- **App Service Subnet**: 10.0.1.0/24 (delegated to Microsoft.Web/serverFarms)
- **PostgreSQL Subnet**: 10.0.2.0/24 (for private endpoint)
- **Private DNS Zone**: privatelink.postgres.database.azure.com

### Compute & Storage
- **App Service Plan**: S1 Linux tier
- **PostgreSQL Flexible Server**: Standard_B1ms, 32GB storage, version 14
- **Web App**: Python 3.11 runtime with VNet integration

### Security
- PostgreSQL server with public access disabled
- Private endpoint for database connectivity
- HTTPS-only web application
- Randomly generated secure database passwords (16 characters with mixed case, digits, and special characters)

## Application Deployment

1. **Production Slot**: Deploys SampleMarketingApp.zip and restarts the web app
2. **Staging Slot**: Deploys SampleMarketingAppBad.zip and restarts the staging slot
3. **Configuration**: Sets DATABASE_URL and SECRET_KEY environment variables
4. **Connectivity Testing**: Performs HTTP requests to verify production web app is responsive

### Production Web App Testing
After deployment completes, the application automatically tests the production web app:
- Makes HTTP GET requests to the web app's default hostname
- Attempts up to 5 times with 10-second intervals between attempts
- Reports success on first successful response (HTTP 200-299)
- Provides detailed status information for each attempt
- Continues deployment even if web app doesn't respond (may be normal during startup)

## Output

The application creates:
- Complete Azure infrastructure
- Two zip files: SampleMarketingApp.zip and SampleMarketingAppBad.zip
- badapps.bat script for running bad scenario tests

## Key Features

- **Customizable Resource Naming**: Users can specify custom resource names for their deployments
- **Secure Password Generation**: Automatically generates secure random database passwords
- **Production Web App Testing**: Automatically tests web app connectivity after deployment (up to 5 attempts)
- **Robust Error Handling**: Validates inputs, authentication, and directory existence
- **Azure SDK Best Practices**: Implements proper authentication patterns and API response handling
- **Private Networking**: Uses private endpoints for secure database access
- **Automated VNet Linking**: Automatically links VNet to private DNS zone with auto-registration enabled
- **Proper SDK Usage**: Follows Azure SDK best practices and patterns
- **VNet Integration**: Configures App Service VNet integration via SCM API
- **Deployment Automation**: Automated zip deployment to production and staging
- **Enhanced Authentication**: Pre-deployment validation with clear error messages
- **Production Ready**: Comprehensive error handling and timeout management

## Technical Notes

### Azure SDK Packages Used
- `Azure.ResourceManager` - Core ARM functionality
- `Azure.ResourceManager.PostgreSql` v1.2.0 - PostgreSQL Flexible Servers
- `Azure.ResourceManager.Network` - VNet and private endpoints
- `Azure.ResourceManager.PrivateDns` v1.2.0 - Private DNS zones
- `Azure.ResourceManager.AppService` - App Service resources

### Important Configurations
- Private DNS zones must use "global" location
- PostgreSQL subnet does NOT require delegation when using private endpoints
- App Service subnet REQUIRES delegation to Microsoft.Web/serverFarms
- VNet integration configured via SCM API due to SDK limitations
- VNet to private DNS zone linking configured via Azure REST API with auto-registration enabled
- Authentication validated before deployment begins
- 300-second timeout for large application deployments
- Database passwords are randomly generated (16 characters, mixed case, digits, special characters)
- Production web app connectivity tested automatically after deployment (up to 5 attempts with 10-second intervals)

### Resource Naming Convention
All Azure resources are named using a user-defined resource name:
- Format: `{resource-type}-{resource-name}`
- User provides the resource name as a command line parameter
- Example with resource name "mymarketingapp":
  - Resource Group: `rg-mymarketingapp`
  - VNet: `vnet-mymarketingapp`
  - App Service Plan: `asp-mymarketingapp`
  - Web App: `webapp-mymarketingapp`
  - PostgreSQL Server: `psql-mymarketingapp`
  - Private Endpoint: `pe-postgresql-mymarketingapp`

This naming convention allows for:
- **Customizable naming**: Users can choose meaningful names for their deployments
- **Consistent prefixes**: All resources follow Azure naming best practices
- **Easy identification**: Resources are grouped logically by the custom name

## Error Handling

The application will exit with error code 1 if:
- Incorrect number of command line arguments
- SampleMarketingApp directory not found
- SampleMarketingAppBad directory not found
- Azure authentication fails
- Critical infrastructure creation fails

## Authentication

Uses `DefaultAzureCredential` which supports:
- Azure CLI authentication
- Managed Identity
- Environment variables
- Visual Studio authentication
- Interactive browser authentication
