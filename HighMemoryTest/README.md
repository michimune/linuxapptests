# HighMemoryTest

A C# console application that automates testing of Azure App Service deployment and high memory fault simulation.

## Description

This application automatically performs the following test sequence:

1. **Step A**: Creates a webapp in Azure App Service and deploys code from the provided zip file
   - Creates a resource group in Brazil South region
   - Creates a PostgreSQL database instance
   - Creates an App Service plan (Linux)
   - Creates and configures the web app
   - Deploys the marketing app code

2. **Step B**: Validates the app returns a 200 status code (with retry logic)

3. **Step C**: Tests the high memory endpoint (`/api/faults/highmemory`) and confirms it returns an error (500+)

4. **Step D**: Waits for 10 seconds

5. **Step E**: Validates the app is healthy again (returns 200 status code)

6. **Step F**: Deletes all Azure resources

## Configuration

The application uses the following configuration:
- **Subscription ID**: Provided as first command line argument
- **Package File**: Provided as second command line argument
- **Location**: Brazil South
- **Authentication**: DefaultAzureCredential (Bearer token)
- **HTTP Timeout**: 300 seconds
- **SECRET_KEY**: Set to the same value as admin password

## Prerequisites

1. .NET 8.0 SDK
2. Azure CLI installed and authenticated, or appropriate Azure credentials configured
3. A deployment package zip file (e.g., `SampleMarketingApp_Complete.zip`) with the marketing app code

## Usage

### Running the Application

The application requires both a subscription ID and the path to the deployment package as command line arguments.

1. Build the application:
   ```
   dotnet build
   ```

2. Run the application with your subscription ID and package path:
   ```
   dotnet run <subscription-id> <path-to-zip-file>
   ```

   **Example:**
   ```
   dotnet run 12345678-abcd-1234-efgh-123456789012 "C:\MyProject\SampleMarketingApp_Complete.zip"
   ```

The application will run automatically without user interaction and complete all test steps.

## Features

- **Non-interactive**: Runs automatically without user input
- **Retry logic**: Attempts health checks up to 6 times with 10-second delays
- **Bearer token authentication**: Uses DefaultAzureCredential for secure API access
- **Extended timeout**: 300-second HTTP client timeout for reliable operations
- **Automatic cleanup**: Deletes all created Azure resources after testing

## Dependencies

- Azure.Identity
- Azure.ResourceManager
- Azure.ResourceManager.AppService
- Azure.ResourceManager.PostgreSql
- Newtonsoft.Json
- System.IO.Compression.ZipFile
