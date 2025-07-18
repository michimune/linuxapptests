# BadScenarioLinux

A C# console application that implements various failure scenarios for Azure App Service Linux environments. This tool helps test and validate application resilience by simulating common production issues.

## Overview

This application implements several kinds of failure scenarios for App Service Linux, using a Linux web app that is specified as the command line parameter. It provides an interactive menu system to select and execute different failure scenarios.

## Architecture

The application uses an extensible architecture where each scenario is a derived class of the base class `ScenarioBase`. When the application starts, it creates a list of scenarios by scanning all classes that have the `[Scenario]` attribute.

### ScenarioBase Class

Each scenario implements the following lifecycle methods:

- **Description**: A string property that describes what scenario it is going to test
- **TargetAddress**: A URL property of the test app (automatically generated)
- **Prevalidate()**: Make HTTP request to the target URL and confirm 200 is returned
- **Setup()**: Configure the failure condition
- **Validate()**: Make HTTP request and confirm error (500+ status code) is returned
- **Recover()**: Restore the system to working state
- **Finalize()**: Make HTTP request to the target URL and confirm 200 is returned

## Command Line Interface

### Syntax
```bash
dotnet run <resource-name>
```

### Parameters
1. **resource-name**: Target resource name which should match the name used for SampleDeployApps

### Example
```bash
dotnet run mymarketingapp
```

## Resource Naming Convention

The application uses the following resource naming pattern based on the provided resource name:

```csharp
string ResourceGroupName => $"rg-{ResourceName}";
string VNetName => $"vnet-{ResourceName}";
string SubnetName => "subnet-appservice";
string PostgreSqlSubnetName => "subnet-postgresql";
string AppServicePlanName => $"asp-{ResourceName}";
string WebAppName => $"webapp-{ResourceName}";
string PostgreSqlServerName => $"psql-{ResourceName}";
string PrivateEndpointName => $"pe-postgresql-{ResourceName}";
```

## Main Loop

1. Display all available scenarios
2. User selects a scenario by number (0 to exit) or enters `all` to execute all scenarios sequentially
3. Execute the selected scenario (or all scenarios) following the lifecycle steps
4. Return to step 1 after completion

## Implemented Scenarios

### 1. Missing Environment Variable
- **Setup**: Retrieve app setting named `SECRET_KEY` and save the value. Remove the app setting from the web app, and wait for 30 seconds
- **Recover**: Add the removed app setting with the saved value back. Wait for 30 seconds

### 2. Missing Connection String
- **Setup**: Retrieve app setting named `DATABASE_URL` and save the value. Remove the app setting from the web app, and wait for 30 seconds
- **Recover**: Add the removed app setting with the saved value back. Wait for 30 seconds

### 3. Missing Dependency
- **Setup**: Swap to staging slot (which contains bad requirements)
- **Recover**: Swap back to production slot

### 4. High Memory
- **Setup**: Make 10 HTTP requests to route `/api/faults/highmemory`
- **Recover**: Wait for 60 seconds

### 5. SNAT Port Exhaustion
- **Setup**: Make 10 HTTP requests to route `/api/faults/snat`
- **Recover**: Wait for 60 seconds

### 6. Incorrect Startup Command
- **Setup**: Add a new app setting: name `AZURE_WEBAPP_STARTUP_COMMAND`, value `bogus_command_line`
- **Recover**: Delete the app setting. Wait for 30 seconds

### 7. High CPU
- **Setup**: Make 10 HTTP requests to route `/api/faults/highcpu`
- **Recover**: Wait for 60 seconds

### 8. SQL Connection Rejected
- **Setup**: Retrieve the networking configuration of the PostgreSQL flexible server and save the setting. Change the connection state of the private endpoint to Reject. Wait for 60 seconds
- **Recover**: Restore the connection state back to Approved. Wait for 60 seconds

### 9. SQL Server Not Responding
- **Setup**: Stop the PostgreSQL flexible server. Wait for 60 seconds
- **Recover**: Start the PostgreSQL flexible server. Wait for 60 seconds

### 10. Misconfigured Connection String (Wrong Host Name)
- **Setup**: Retrieve app setting named `DATABASE_URL` and save the value. Update the app setting by trimming the host name part from the URL. Wait for 30 seconds
- **Recover**: Restore the saved app setting back. Wait for 30 seconds

### 11. Firewall Blocks Connection
- **Setup**: Add Deny rule for outbound TCP/5432 for VirtualNetwork to the VNet NSG. Wait for 60 seconds
- **Recover**: Delete the Deny rule. Wait for 60 seconds

### 12. Specific API Paths Failing
- **Prevalidate**: Make HTTP request to `/products` and confirm HTTP 200 success
- **Setup**: Remove `PRODUCTS_ENABLED` app setting and restart the app. Wait for 30 seconds
- **Validate**: Request `/products` and expect HTTP 404 Not Found
- **Recover**: Add `PRODUCTS_ENABLED=1`, restart the app, wait for 30 seconds
- **Finalize**: Request `/products` and confirm HTTP 200 success

### 13. Missing Entry Point
- **Setup**: Add incorrect startup command `python aaa.py` and restart the app. Wait for 30 seconds
- **Recover**: Remove startup command setting and restart the app. Wait for 30 seconds

### 14. VNET Integration Breaks Connectivity
- **Setup**: Remove PostgreSql subnet from the VNet and restart the app. Wait for 30 seconds
- **Recover**: Restore PostgreSql subnet to the VNet and restart the app. Wait for 30 seconds

### 15. Misconfigured DNS
- **Setup**: Simulate DNS misconfiguration and restart the app. Wait for 30 seconds
- **Recover**: Simulate DNS restoration and restart the app. Wait for 30 seconds

### 16. Incorrect Docker Image
- **Setup**: Simulate Docker image misconfiguration via marker app setting. Restart the app. Wait for 30 seconds
- **Recover**: Remove marker and simulate Docker image restoration. Restart the app. Wait for 30 seconds

### 17. Incorrect Write Access
- **Validate**: Make HTTP request to `/api/faults/badwrite` and confirm write failure or exception
- **Recover**: Wait for write access issue to resolve. Wait for 30 seconds

### 18. Outdated TLS Version
- **Validate**: Make HTTP request to `/api/faults/badtls` and confirm TLS failure or 5xx status

### 19. External API Latency
- **Validate**: Measure latency of HTTP request to `/api/faults/slowcall`, categorize severity based on thresholds

### 20. App Restarts Triggered by Auto-Heal Rules
- **Setup**: Enable auto-heal rule for high memory thresholds, restart the app, trigger high memory request, and wait for rule activation
- **Validate**: Query Azure Monitor Activity Logs for restart events in the last 10 minutes
- **Recover**: Disable the auto-heal rule and restart the app

### 21. Poorly Tuned Auto-Heal Rules Cause Instability
- **Setup**: Enable poorly tuned auto-heal rules and restart the app
- **Validate**: Query Azure Monitor Activity Logs for restart events

### 22. Cold Starts After Scale-Out
- **Description**: Scenario implementation in progress, tests cold starts after scaling out

## Prerequisites

- .NET 8.0 SDK
- Azure CLI installed and authenticated (`az login`)
- Valid Azure subscription with deployed infrastructure from DeploySampleApps
- `AZURE_SUBSCRIPTION_ID` environment variable set

## Infrastructure Specifications

- **HTTP Timeout**: 300 seconds with retry logic
- **Azure SDK**: Uses Azure Resource Manager SDK with proper authentication patterns
- **Authentication**: DefaultAzureCredential (supports Azure CLI, Managed Identity, etc.)

## Usage

1. Ensure you have a deployed infrastructure from the DeploySampleApps project
2. Set the `AZURE_SUBSCRIPTION_ID` environment variable:
   ```bash
   set AZURE_SUBSCRIPTION_ID=your-subscription-id-here
   ```
3. Build and run the application:
   ```bash
   dotnet build
   dotnet run mymarketingapp
   ```
4. Select scenarios from the interactive menu
5. Monitor the console output for detailed execution information

## Key Features

- **Interactive Menu**: Easy-to-use console interface for scenario selection
- **Extensible Architecture**: Easy to add new scenarios by creating classes with `[Scenario]` attribute
- **Robust Error Handling**: Automatic recovery attempts even if scenarios fail
- **Detailed Logging**: Comprehensive console output for debugging and monitoring
- **Azure SDK Integration**: Uses modern Azure Resource Manager SDK patterns

## Error Handling

The application includes comprehensive error handling:

- Automatic recovery attempts if scenarios fail during execution
- Detailed error messages and stack traces
- Graceful degradation when Azure resources are not found
- Warning messages for non-critical failures

## Authentication

The application uses `DefaultAzureCredential` which supports multiple authentication methods:
- Azure CLI authentication (`az login`)
- Managed Identity
- Environment variables
- Visual Studio authentication
- Interactive browser authentication

## Dependencies

```xml
<PackageReference Include="Azure.Identity" Version="1.12.1" />
<PackageReference Include="Azure.ResourceManager" Version="1.13.0" />
<PackageReference Include="Azure.ResourceManager.AppService" Version="1.2.0" />
<PackageReference Include="Azure.ResourceManager.PostgreSql" Version="1.2.0" />
<PackageReference Include="Azure.ResourceManager.Network" Version="1.9.0" />
<PackageReference Include="Azure.ResourceManager.PrivateDns" Version="1.2.1" />
<PackageReference Include="Azure.ResourceManager.Resources" Version="1.9.0" />
```

## Contributing

To add a new scenario:

1. Create a new class that inherits from `ScenarioBase`
2. Add the `[Scenario]` attribute to the class
3. Implement the required methods: `Setup()`, `Recover()`, and set the `Description` property
4. Optionally override `Prevalidate()`, `Validate()`, and `Finalize()` if custom behavior is needed

Example:
```csharp
[Scenario]
public class MyCustomScenario : ScenarioBase
{
    public override string Description => "My custom failure scenario";
    
    public MyCustomScenario(string resourceName) : base(resourceName) { }
    
    public override async Task Setup()
    {
        // Implement failure setup logic
    }
    
    public override async Task Recover()
    {
        // Implement recovery logic
    }
}
```
