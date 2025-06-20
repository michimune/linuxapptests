# SNAT Exhaustion Test

This C# console application automates testing of SNAT (Source Network Address Translation) exhaustion scenarios in Azure App Service with advanced networking features.

## Features

The application automatically performs the following test sequence:

1. **Step A**: Creates a comprehensive Azure infrastructure with:
   - Virtual Network with dual subnets (app and PostgreSQL)
   - PostgreSQL Flexible Server with private endpoint
   - Private DNS zone for secure connectivity
   - S1 Linux App Service Plan with VNet integration
   - Web app deployment with environment variables

2. **Step B**: Tests app health with retry logic (up to 6 attempts with 10-second delays)

3. **Step C**: Triggers SNAT exhaustion by calling `/api/faults/snat` endpoint

4. **Step D**: Waits 10 seconds for recovery

5. **Step E**: Validates app recovery with retry logic

6. **Step F**: Cleans up all Azure resources

## Architecture

### Network Infrastructure
- **VNet**: 10.0.0.0/16 address space
- **App Subnet**: 10.0.1.0/24 (delegated to Microsoft.Web/serverFarms)
- **PostgreSQL Subnet**: 10.0.2.0/24 (for private endpoints)
- **Private DNS Zone**: `privatelink.postgres.database.azure.com`

### Security Features
- PostgreSQL with public access disabled
- Private endpoint connectivity
- VNet integration for App Service
- Generated secure passwords

## Prerequisites

- .NET 8.0 SDK
- Azure CLI or Azure PowerShell (for authentication)
- Valid Azure subscription with sufficient permissions
- The deployment package zip file (e.g., `SampleMarketingApp_Complete.zip`)

## Configuration

### Command Line Arguments
The application requires two command line arguments:
```bash
dotnet run <subscription-id> <zip-file-path>
```

**Example:**
```bash
dotnet run 12345678-abcd-1234-efgh-123456789012 "C:\TempProjects\MyApp\SampleMarketingApp_Complete.zip"
```

### Other Settings
- **Location**: Brazil South
- **Authentication**: DefaultAzureCredential (Bearer token)
- **HTTP Timeout**: 300 seconds
- **Resource naming**: Timestamped to avoid conflicts

## Running the Application

1. Ensure you're authenticated with Azure:
   ```bash
   az login
   az account set --subscription "your-subscription-id"
   ```

2. Build and run the application:
   ```bash
   dotnet build
   dotnet run <subscription-id> <zip-file-path>
   ```

**Example:**
```bash
dotnet run 12345678-abcd-1234-efgh-123456789012 "C:\MyProject\SampleMarketingApp_Complete.zip"
```

The application runs automatically without user interaction and provides detailed console output for each step.

## Expected Output

### Successful SNAT Test
```
✓ App is healthy! Status code: OK
⚠ Expected error status (500+), but got: OK
Response content: {"failed_calls":0,"message":"Completed 500 requests to www.bing.com","successful_calls":500,"total_calls":500}
```

### SNAT Exhaustion Detected
```
✓ SNAT exhaustion triggered successfully! Status code: 500
✓ App recovered after 10 seconds
```

## Troubleshooting

### Common Issues
1. **Missing Arguments**: Ensure both subscription ID and zip file path are provided as command line arguments
2. **File Not Found**: Verify the zip file path exists and is accessible
3. **Authentication Failure**: Run `az login` and verify subscription access
4. **Resource Creation Timeout**: PostgreSQL Flexible Server creation takes 5-10 minutes
5. **Private DNS Zone Error**: Ensure using "global" location for DNS zones

### Manual Cleanup
If automatic cleanup fails:
```bash
az group delete --name rg-snat-test-[timestamp] --yes --no-wait
```

## Important Notes

- **Private Networking**: PostgreSQL uses private endpoints instead of subnet delegation
- **VNet Integration**: App Service configured for VNet connectivity
- **Security**: All databases use private networking with no public access
- **Scalability**: Infrastructure supports production-level networking patterns
- **Cleanup**: Resources are automatically cleaned up after the test
- **Passwords**: Generated passwords for enhanced security
- **Error Handling**: Comprehensive retry logic and error reporting

## Dependencies

- Azure.Identity 1.12.0
- Azure.ResourceManager 1.13.0
- Azure.ResourceManager.AppService 1.2.0
- Azure.ResourceManager.Network 1.6.0
- Azure.ResourceManager.PrivateDns 1.2.0
- Azure.ResourceManager.PostgreSql 1.2.0
- Newtonsoft.Json 13.0.3
