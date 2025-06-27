# LinuxAppTests

This repository has projects that allow you to automatically create a test environment of Azure App Service for Linux and perform some failure and recovery scenarios.

## Projects

- DeploySampleApps: C# console program that automates creating a test environment
- SampleMarketingApp: a sample web app code with a few routes that can cause failures written in Python
- BadScenarioLinux: C# console program that implements failures and recovery scenarios that can happen to Web App for Linux

## Prerequisites

- Valid Azure subscription

## Instructions

```bash
.\setup.bat <subscription-id> <region> <resource-name>
.\badapps.bat
```

### Example
```bash
.\setup.bat 12345678-1234-1234-1234-123456789012 eastus myapp
```

## Parameters

- `subscription-id`: Your Azure subscription ID
- `region`: Azure region for deployment (e.g., eastus, westus2, etc.)
- `resource-name`: Custom name used for all Azure resources (e.g., mymarketingapp, testapp123)

## Notes

- `badapps.bat` will be created after setup.bat is successfully finished.
- If you encounter a quota error, try a different region (e.g. brazilsouth).
