# SampleMarketingApp Copier

This C# console application copies the SampleMarketingApp directory to a new directory named SampleMarketingAppBad and removes the first line from the requirements.txt file.

## Features

- Takes a command line parameter specifying the SampleMarketingApp directory path
- Exits with error code 1 if no directory is specified
- Copies the entire directory structure recursively
- Removes the first line from requirements.txt in the copied directory
- Provides detailed console output about the operations being performed

## Usage

```bash
dotnet run -- <SampleMarketingApp_directory_path>
```

### Example

```bash
dotnet run -- "d:\repos\VibeCoding\SampleMarketingApp"
```

## Build and Run

1. Build the project:
   ```bash
   dotnet build
   ```

2. Run the application:
   ```bash
   dotnet run -- <path_to_SampleMarketingApp>
   ```

## Error Handling

- Returns exit code 1 if no command line parameter is provided
- Returns exit code 1 if the source directory doesn't exist
- Returns exit code 1 if any error occurs during copying or file modification
- Returns exit code 0 on successful completion

## Output

The program will create a new directory named `SampleMarketingAppBad` in the same parent directory as the source SampleMarketingApp directory, and modify the requirements.txt file by removing its first line.
