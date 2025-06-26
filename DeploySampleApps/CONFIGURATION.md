# Sample Configuration for DeploySampleApps

## Environment Variables (Optional)
You can set these environment variables instead of using DefaultAzureCredential:

```bash
# Service Principal Authentication
AZURE_CLIENT_ID=your-service-principal-client-id
AZURE_CLIENT_SECRET=your-service-principal-secret
AZURE_TENANT_ID=your-tenant-id

# Or use Azure CLI
az login
```

## Sample Commands

### Development Environment
```bash
# Build the project
dotnet build

# Run with sample parameters (replace with your values)
dotnet run 12345678-1234-1234-1234-123456789012 eastus C:\MyProjects
```

### Production Environment
```bash
# Publish the application
dotnet publish -c Release -o ./publish

# Run the published version
./publish/DeploySampleApps.exe subscription-id region base-directory
```

## Resource Naming Convention

The application uses these resource names:
- Resource Group: `rg-samplemarketingapps-test`
- VNet: `vnet-samplemarketingapps`
- App Service Plan: `asp-samplemarketingapps`
- Web App: `webapp-samplemarketingapps`
- PostgreSQL Server: `psql-samplemarketingapps`

Names are hardcoded for consistency but can be modified in the Program.cs file.

## Cleanup

To clean up all resources:
```bash
az group delete --name rg-samplemarketingapps-test --yes --no-wait
```
