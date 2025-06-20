# WebApiApp

A simple C# Web API application that reads an environment variable and exposes it through a REST endpoint.

## Features

- Reads `APP_VALUE` environment variable during startup
- Validates that the environment variable exists (throws `InvalidOperationException` if not found)
- Exposes a GET endpoint at `/` that returns the environment variable value as JSON

## Usage

1. Set the `APP_VALUE` environment variable:
   ```powershell
   $env:APP_VALUE = "MyTestValue"
   ```

2. Run the application:
   ```powershell
   dotnet run
   ```

3. Test the endpoint:
   ```powershell
   curl http://localhost:5000/
   ```

## Expected Response

```json
{
  "APP_VALUE": "MyTestValue"
}
```

## Error Handling

If the `APP_VALUE` environment variable is not set, the application will:
1. Log an error message
2. Exit with code 1

## Project Structure

- `Program.cs` - Main entry point and application configuration
- `AppConfiguration.cs` - Static class to hold the environment variable
- `Controllers/HomeController.cs` - API controller with the GET endpoint
- `WebApiApp.csproj` - Project file
