# SNAT Exhaustion Test Application - Instructions

## Overview

This C# console application automates Azure App Service SNAT (Source Network Address Translation) exhaustion testing using Azure Resource Manager SDK. It creates a complete Azure infrastructure, deploys a sample marketing application, triggers SNAT exhaustion, validates recovery, and cleans up resources.

## Architecture

The application creates the following Azure resources:
- **Virtual Network (VNet)** with two subnets
- **PostgreSQL Flexible Server** with private endpoint
- **App Service Plan** (S1 Standard Linux)
- **Web App** with VNet integration
- **Private DNS Zone** for PostgreSQL connectivity
- **Private Endpoint** for secure database access

## Key Requirements

### Hardcoded Configuration
- **Subscription ID**: Provided as first command line argument
- **Package Path**: Provided as second command line argument
- **Location**: Brazil South (`brazilsouth`)

### Infrastructure Specifications
- **App Service Plan**: S1 Linux with Python 3.11 runtime
- **PostgreSQL**: Flexible Server v14, Standard_B1ms, 32GB storage
- **VNet**: 10.0.0.0/16 address space
  - App subnet: 10.0.1.0/24 (delegated to Microsoft.Web/serverFarms)
  - PostgreSQL subnet: 10.0.2.0/24 (for private endpoint)
- **HTTP Timeout**: 300 seconds with retry logic

## Key Learnings and Best Practices

### 1. Azure SDK Package Management

#### PostgreSQL Flexible Servers
- **Package**: `Azure.ResourceManager.PostgreSql` v1.2.0
- **Namespace**: `Azure.ResourceManager.PostgreSql.FlexibleServers`
- **Critical**: Use the main PostgreSQL package, not a separate FlexibleServers package

#### Private Networking
- **Private DNS Package**: `Azure.ResourceManager.PrivateDns` v1.2.0
- **Location Requirement**: Private DNS zones must use `"global"` location, not regional locations

### 2. PostgreSQL Flexible Server Configuration

#### Public vs Private Networking
```csharp
// For private endpoint configuration
Network = new PostgreSqlFlexibleServerNetwork
{
    PublicNetworkAccess = PostgreSqlFlexibleServerPublicNetworkAccessState.Disabled
}

// NO delegated subnet when using private endpoints
// DelegatedSubnetResourceId is only for subnet delegation approach
```

#### Private Endpoint Pattern
1. Create PostgreSQL server with public access disabled
2. Create Private DNS zone (`privatelink.postgres.database.azure.com`) with `"global"` location
3. Create private endpoint in dedicated subnet
4. Link private endpoint to PostgreSQL server

#### Subnet Delegation Rules
- **App Service**: Requires subnet delegation to `Microsoft.Web/serverFarms`
- **PostgreSQL Private Endpoint**: Does NOT require subnet delegation
- **PostgreSQL Subnet Delegation**: Only needed for direct subnet integration (not private endpoints)

### 3. VNet Integration Patterns

#### Subnet Configuration
```csharp
// App Service subnet - requires delegation
var appSubnetData = new SubnetData()
{
    Name = SubnetName,
    AddressPrefix = "10.0.1.0/24"
};
appSubnetData.Delegations.Add(new ServiceDelegation()
{
    Name = "Microsoft.Web/serverFarms",
    ServiceName = "Microsoft.Web/serverFarms"
});

// PostgreSQL subnet - no delegation for private endpoints
var postgresSubnetData = new SubnetData()
{
    Name = PostgreSqlSubnetName,
    AddressPrefix = "10.0.2.0/24"
};
// No delegation needed for private endpoint approach
```

#### VNet Integration for App Service
- **Challenge**: Direct VNet integration via Azure SDK is complex
- **Solution**: Configure via HTTP client using SCM API after web app creation
- **Alternative**: Use Azure CLI or ARM templates for production scenarios

### 4. Resource Targeting and Authentication

#### Subscription Targeting
```csharp
// WRONG - uses default subscription
var subscription = await _armClient.GetDefaultSubscriptionAsync();

// CORRECT - targets specific subscription
var subscription = await _armClient.GetSubscriptionResource(
    new Azure.Core.ResourceIdentifier($"/subscriptions/{SubscriptionId}")).GetAsync();
// Access via subscription.Value for operations
```

#### Authentication Pattern
```csharp
var credential = new DefaultAzureCredential();
var token = await credential.GetTokenAsync(
    new Azure.Core.TokenRequestContext(new[] { "https://management.azure.com/.default" }));
```

### 5. Error Handling and Common Issues

#### PostgreSQL Creation Errors
- **Empty Private DNS Zone Error**: Occurs when using subnet delegation without Private DNS zone
- **Solution**: Either use private endpoints OR configure Private DNS zone for subnet delegation

#### API Response Patterns
```csharp
// Many operations return Response<T> - access via .Value
var response = await resource.GetAsync(resourceName);
var actualResource = response.Value;
```

#### Build Warnings
- `CS1998`: Async method without await - expected for initialization methods
- Can be safely ignored if method doesn't require async operations

### 6. Application Deployment

#### ZIP Deployment via SCM API
```csharp
var deployUrl = $"https://{WebAppName}.scm.azurewebsites.net/api/zipdeploy";
// Use Bearer token authentication
// Set Content-Type to application/zip
// 300-second timeout for large deployments
```

#### Environment Variables
```csharp
webAppData.SiteConfig.AppSettings.Add(new AppServiceNameValuePair() 
{ 
    Name = "DATABASE_URL", 
    Value = GetDatabaseConnectionString() 
});
```

### 7. SNAT Testing

#### Test Sequence
1. **Health Check**: Verify application responds (200 OK)
2. **SNAT Trigger**: Call `/api/faults/snat` endpoint
3. **Wait Period**: 10 seconds for recovery
4. **Recovery Validation**: Verify application health restored
5. **Cleanup**: Delete all resources

#### Expected Behavior
- Healthy app: SNAT endpoint returns 200 with successful request stats
- SNAT exhaustion: Endpoint returns 500+ with failed connection information
- Recovery: App returns to healthy state after brief period

## Prerequisites

### Azure Setup
1. Azure subscription access with appropriate permissions
2. Azure CLI authentication: `az login`
3. Set target subscription: `az account set --subscription "your-subscription-id"`

### Local Development
1. .NET 8.0 SDK
2. Sample application ZIP file at specified path
3. Visual Studio Code or Visual Studio

### Required Permissions
- Create/delete resource groups
- Create/manage VNets and subnets
- Create/manage App Service plans and web apps
- Create/manage PostgreSQL Flexible Servers
- Create/manage Private DNS zones and private endpoints
- Deploy applications via SCM API

## Usage

### Build and Run
```bash
cd "C:\Projects\SnatExhaustionTest"
dotnet build

# Usage: dotnet run <subscription-id> <zip-file-path>
dotnet run your-subscription-id "C:\path\to\SampleMarketingApp_Complete.zip"

# Example:
dotnet run 12345678-abcd-1234-efgh-123456789012 "C:\MyProjects\AppPackages\SampleMarketingApp_Complete.zip"
```

### Manual Cleanup (if needed)
```bash
# If automatic cleanup fails
az group delete --name rg-snat-test-[timestamp] --yes --no-wait
```

## Troubleshooting

### Common Build Errors
1. **Package not found**: Verify exact package names and versions
2. **Missing using statements**: Add required Azure SDK namespaces
3. **API not available**: Check if using correct resource manager patterns

### Runtime Errors
1. **Authentication failures**: Verify Azure CLI login and subscription access
2. **Private DNS zone errors**: Ensure using "global" location
3. **VNet integration issues**: Complex - consider alternative configuration methods

### Performance Considerations
- Resource creation takes 5-10 minutes total
- PostgreSQL Flexible Server creation is the slowest step
- Private endpoint configuration adds 2-3 minutes
- ZIP deployment varies by application size

## Future Enhancements

### Potential Improvements
1. **Full VNet Integration**: Implement complete App Service VNet integration via ARM templates
2. **Private DNS Automation**: Automatic DNS record creation for private endpoints
3. **Network Security Groups**: Add NSG rules for enhanced security
4. **Monitoring**: Integrate Application Insights for detailed SNAT metrics
5. **Load Testing**: Scale testing to trigger actual SNAT exhaustion

### Alternative Approaches
1. **Bicep Templates**: Use infrastructure as code for complex networking
2. **Azure Functions**: Alternative to App Service for testing
3. **Container Apps**: Modern serverless alternative with better VNet integration

## References

- [Azure Resource Manager SDK Documentation](https://docs.microsoft.com/en-us/dotnet/api/azure.resourcemanager)
- [PostgreSQL Flexible Server Networking](https://docs.microsoft.com/en-us/azure/postgresql/flexible-server/concepts-networking)
- [App Service VNet Integration](https://docs.microsoft.com/en-us/azure/app-service/web-sites-integrate-with-vnet)
- [Private Endpoints Overview](https://docs.microsoft.com/en-us/azure/private-link/private-endpoint-overview)
