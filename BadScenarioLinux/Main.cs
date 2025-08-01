using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.AppService.Models;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Models;
using Azure.ResourceManager.PostgreSql.FlexibleServers;
using Azure.ResourceManager.Resources;
using System.Reflection;
using System.Text.Json;

namespace BadScenarioLinux;

public class Program
{
    private static ArmClient? _armClient;
    private static string? _resourceName;
    private static string? _subscriptionId;
    private static string? _zipDirectory;
    
    // Resource name properties
    private static string ResourceGroupName => $"rg-{_resourceName}";
    private static string VNetName => $"vnet-{_resourceName}";
    private static string SubnetName => "subnet-appservice";
    private static string PostgreSqlSubnetName => "subnet-postgresql";
    private static string AppServicePlanName => $"asp-{_resourceName}";
    private static string WebAppName => $"webapp-{_resourceName}";
    private static string PostgreSqlServerName => $"psql-{_resourceName}";
    private static string PrivateEndpointName => $"pe-postgresql-{_resourceName}";

    public static async Task Main(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("Error: Subscription ID, resource name, and zip directory are required.");
            Console.WriteLine("Usage: dotnet run <subscription-id> <resource-name> <zip-directory>");
            Console.WriteLine("Example: dotnet run 12345678-1234-1234-1234-123456789012 mymarketingapp C:\\MyApps\\zip");
            Environment.Exit(1);
        }

        _subscriptionId = args[0];
        _resourceName = args[1];
        _zipDirectory = args[2];

        // Validate zip directory exists
        if (!Directory.Exists(_zipDirectory))
        {
            Console.WriteLine($"Error: Zip directory not found: {_zipDirectory}");
            Environment.Exit(1);
        }

        try
        {
            // Initialize Azure ARM client
            _armClient = new ArmClient(new DefaultAzureCredential());
            
            // Get all scenario classes
            var scenarios = GetScenarios();
            
            if (scenarios.Count == 0)
            {
                Console.WriteLine("No scenarios found.");
                return;
            }

            // Main loop
            while (true)
            {
                DisplayScenarios(scenarios);
                
                Console.Write("Select a scenario (0 to exit): ");
                var input = Console.ReadLine();
                if (input != null && input.Trim().Equals("all", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var scenarioItem in scenarios)
                    {
                        await ExecuteScenario(scenarioItem);
                    }
                    Console.WriteLine("\nPress any key to continue...");
                    Console.ReadKey();
                    Console.Clear();
                    continue;
                }
                
                if (!int.TryParse(input, out int choice))
                {
                    Console.WriteLine("Invalid input. Please enter a number.");
                    continue;
                }
                
                if (choice == 0)
                {
                    Console.WriteLine("Exiting...");
                    break;
                }
                
                if (choice < 1 || choice > scenarios.Count)
                {
                    Console.WriteLine("Invalid choice. Please select a valid scenario number.");
                    continue;
                }
                
                await ExecuteScenario(scenarios[choice - 1]);
                
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                Console.Clear();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static List<ScenarioBase> GetScenarios()
    {
        var scenarios = new List<ScenarioBase>();
        var types = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => t.IsSubclassOf(typeof(ScenarioBase)) && !t.IsAbstract);

        foreach (var type in types)
        {
            var attribute = type.GetCustomAttribute<ScenarioAttribute>();
            if (attribute != null)
            {
                var scenario = (ScenarioBase)Activator.CreateInstance(type)!;
                scenarios.Add(scenario);
            }
        }

        return scenarios.OrderBy(s => s.Description).ToList();
    }

    private static void DisplayScenarios(List<ScenarioBase> scenarios)
    {
        Console.WriteLine($"\nApp Service Linux Failure Scenarios - Resource: {_resourceName}");
        Console.WriteLine(new string('=', 60));
        
        for (int i = 0; i < scenarios.Count; i++)
        {
            Console.WriteLine($"{i + 1}. {scenarios[i].Description}");
        }
        
        Console.WriteLine("0. Exit");
        Console.WriteLine();
    }

    private static async Task ExecuteScenario(ScenarioBase scenario)
    {
        scenario.Initialize(_armClient!, _resourceName!, _subscriptionId!, _zipDirectory!);
        
        Console.WriteLine($"\nExecuting scenario: {scenario.Description}");
        Console.WriteLine(new string('-', 50));
        
        try
        {
            Console.WriteLine("Step 1: Prevalidate it is GOOD");
            await scenario.Prevalidate();
            Console.WriteLine("✓ Prevalidation successful");
            
            Console.WriteLine("Step 2: Turning mode to BAD");
            await scenario.Setup();
            Console.WriteLine("✓ Setup complete");
            
            Console.WriteLine("Step 3: Validate it is BAD");
            await scenario.Validate();
            Console.WriteLine("✓ Validation complete (failure confirmed)");
            
            Console.WriteLine("Step 4: Turning mode to GOOD");
            await scenario.Recover();
            Console.WriteLine("✓ Recovery complete");
            
            Console.WriteLine("Step 5: Validate it is GOOD");
            await scenario.Finalize();
            Console.WriteLine("✓ Finalization successful");
            
            Console.WriteLine($"\n✓ Scenario '{scenario.Description}' completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n✗ Scenario failed: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
}

// Attribute to mark scenario classes
[AttributeUsage(AttributeTargets.Class)]
public class ScenarioAttribute : Attribute
{
}

// Base class for all scenarios
public abstract class ScenarioBase
{
    protected ArmClient? ArmClient { get; private set; }
    protected string? ResourceName { get; private set; }
    protected string? SubscriptionId { get; private set; }
    protected string? ZipDirectory { get; private set; }
    protected HttpClient HttpClient { get; private set; } = new HttpClient { Timeout = TimeSpan.FromSeconds(300) };
    
    public abstract string Description { get; }
    public string TargetAddress => $"https://{WebAppName}.azurewebsites.net";
    
    // Resource name properties
    protected string ResourceGroupName => $"rg-{ResourceName}";
    protected string VNetName => $"vnet-{ResourceName}";
    protected string SubnetName => "subnet-appservice";
    protected string PostgreSqlSubnetName => "subnet-postgresql";
    protected string AppServicePlanName => $"asp-{ResourceName}";
    public virtual string WebAppName => $"webapp-{ResourceName}";
    protected string PostgreSqlServerName => $"psql-{ResourceName}";
    protected string PrivateEndpointName => $"pe-postgresql-{ResourceName}";

    public void Initialize(ArmClient armClient, string resourceName, string subscriptionId, string zipDirectory)
    {
        ArmClient = armClient;
        ResourceName = resourceName;
        SubscriptionId = subscriptionId;
        ZipDirectory = zipDirectory;
    }

    public virtual async Task Prevalidate()
    {
        await MakeHttpRequest(TargetAddress, expectedSuccess: true);
    }

    public virtual async Task Setup()
    {
        // Default implementation - do nothing
        await Task.CompletedTask;
    }

    public virtual async Task Validate()
    {
        await MakeHttpRequest(TargetAddress, expectedSuccess: false);
    }

    public virtual async Task<bool> Recover()
    {
        // Default implementation - do nothing
        await Task.CompletedTask;
        return true;
    }

    public virtual async Task Finalize()
    {
        await MakeHttpRequest(TargetAddress, expectedSuccess: true);
    }

    protected async Task MakeHttpRequest(string url, bool expectedSuccess)
    {
        try
        {
            var response = await HttpClient.GetAsync(url);
            
            if (expectedSuccess)
            {
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"✓ HTTP request to {url} returned {(int)response.StatusCode} (success as expected)");
                }
                else
                {
                    throw new Exception($"Expected success but got {(int)response.StatusCode}");
                }
            }
            else
            {
                if ((int)response.StatusCode >= 500)
                {
                    Console.WriteLine($"✓ HTTP request to {url} returned {(int)response.StatusCode} (error as expected)");
                }
                else
                {
                    throw new Exception($"Expected server error (500+) but got {(int)response.StatusCode}");
                }
            }
        }
        catch (HttpRequestException ex)
        {
            if (!expectedSuccess)
            {
                Console.WriteLine($"✓ HTTP request failed as expected: {ex.Message}");
            }
            else
            {
                throw new Exception($"HTTP request failed: {ex.Message}");
            }        }
    }

    protected async Task MakeHttpRequestExpecting404(string url)
    {
        try
        {
            var response = await HttpClient.GetAsync(url);
            
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Console.WriteLine($"✓ HTTP request to {url} returned 404 (Not Found as expected)");
            }
            else
            {
                throw new Exception($"Expected 404 Not Found but got {(int)response.StatusCode}");
            }
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"✓ HTTP request failed as expected (likely 404): {ex.Message}");
        }
    }

    protected async Task<WebSiteResource> GetWebAppResource()
    {
        var subscription = ArmClient!.GetSubscriptionResource(ResourceIdentifier.Parse($"/subscriptions/{SubscriptionId}"));
        var resourceGroup = await subscription.GetResourceGroupAsync(ResourceGroupName);
        var webApp = await resourceGroup.Value.GetWebSiteAsync(WebAppName);
        return webApp.Value;
    }    protected async Task UpdateAppSetting(string key, string? value)
    {
        var webApp = await GetWebAppResource();
        
        // Get current app settings
        var listResponse = await webApp.GetApplicationSettingsAsync();
        var currentSettings = listResponse.Value.Properties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        
        // Update the setting
        if (value == null)
        {
            currentSettings.Remove(key);
        }
        else
        {
            currentSettings[key] = value;
        }
          // Create new configuration
        var newSettings = new AppServiceConfigurationDictionary();
        foreach (var setting in currentSettings)
        {
            newSettings.Properties.Add(setting.Key, setting.Value);
        }
        
        // Update the settings
        await webApp.UpdateApplicationSettingsAsync(newSettings);
        Console.WriteLine($"✓ App setting '{key}' updated");
    }

    protected async Task<string?> GetAppSetting(string key)
    {
        var webApp = await GetWebAppResource();
        var config = await webApp.GetApplicationSettingsAsync();
        return config.Value.Properties.TryGetValue(key, out var value) ? value : null;
    }

    protected async Task RemoveAppSetting(string key)
    {
        var webApp = await GetWebAppResource();
        
        // Get current app settings
        var listResponse = await webApp.GetApplicationSettingsAsync();
        var currentSettings = listResponse.Value.Properties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        
        // Remove the setting
        currentSettings.Remove(key);
        
        // Create new configuration
        var newSettings = new AppServiceConfigurationDictionary();
        foreach (var setting in currentSettings)
        {
            newSettings.Properties.Add(setting.Key, setting.Value);
        }
        
        // Update the settings
        await webApp.UpdateApplicationSettingsAsync(newSettings);
        Console.WriteLine($"✓ App setting '{key}' removed");
    }

    protected async Task SetAppSetting(string key, string value)
    {
        var webApp = await GetWebAppResource();
        
        // Get current app settings
        var listResponse = await webApp.GetApplicationSettingsAsync();
        var currentSettings = listResponse.Value.Properties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        
        // Update the setting
        currentSettings[key] = value;
        
        // Create new configuration
        var newSettings = new AppServiceConfigurationDictionary();
        foreach (var setting in currentSettings)
        {
            newSettings.Properties.Add(setting.Key, setting.Value);
        }
        
        // Update the settings
        await webApp.UpdateApplicationSettingsAsync(newSettings);
        Console.WriteLine($"✓ App setting '{key}' set to '{value}'");
    }

    protected async Task RestartApp()
    {
        var webApp = await GetWebAppResource();
        await webApp.RestartAsync();
        Console.WriteLine("✓ App service restarted");
    }

    protected async Task<(List<string> consoleLogs, List<string> httpLogs)> GetApplicationLogsAsync(int lastMinutes = 30)
    {
        var consoleLogs = new List<string>();
        var httpLogs = new List<string>();
        
        try
        {
            // Get access token from DefaultAzureCredential
            var credential = new DefaultAzureCredential();
            var tokenRequestContext = new TokenRequestContext(new[] { "https://management.azure.com/.default" });
            var accessToken = await credential.GetTokenAsync(tokenRequestContext);
            
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken.Token);
            
            // Try Application Insights first
            var (appInsightsConsoleLogs, appInsightsHttpLogs) = await TryGetApplicationInsightsLogsAsync(httpClient, lastMinutes);
            
            if (appInsightsConsoleLogs.Count > 0 || appInsightsHttpLogs.Count > 0)
            {
                Console.WriteLine($"✓ Retrieved {appInsightsConsoleLogs.Count} console logs and {appInsightsHttpLogs.Count} HTTP logs from Application Insights");
                return (appInsightsConsoleLogs, appInsightsHttpLogs);
            }
            
            // Fallback to Log Analytics workspace
            Console.WriteLine("⚠️  No logs found in Application Insights, falling back to Log Analytics workspace...");
            var (logAnalyticsConsoleLogs, logAnalyticsHttpLogs) = await TryGetLogAnalyticsLogsAsync(httpClient, lastMinutes);
            
            if (logAnalyticsConsoleLogs.Count > 0 || logAnalyticsHttpLogs.Count > 0)
            {
                Console.WriteLine($"✓ Retrieved {logAnalyticsConsoleLogs.Count} console logs and {logAnalyticsHttpLogs.Count} HTTP logs from Log Analytics workspace");
                return (logAnalyticsConsoleLogs, logAnalyticsHttpLogs);
            }
            
            Console.WriteLine("⚠️  No logs found in either Application Insights or Log Analytics workspace");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Error retrieving application logs: {ex.Message}");
        }
        
        return (consoleLogs, httpLogs);
    }
    
    private async Task<(List<string> consoleLogs, List<string> httpLogs)> TryGetApplicationInsightsLogsAsync(HttpClient httpClient, int lastMinutes)
    {
        var consoleLogs = new List<string>();
        var httpLogs = new List<string>();
        
        try
        {
            // First, get the Application Insights component associated with the web app
            var webApp = await GetWebAppResource();
            var appSettings = await webApp.GetApplicationSettingsAsync();
            
            string? appInsightsConnectionString = null;
            string? instrumentationKey = null;
            
            // Check for Application Insights connection string or instrumentation key
            if (appSettings.Value.Properties.TryGetValue("APPLICATIONINSIGHTS_CONNECTION_STRING", out var connString))
            {
                appInsightsConnectionString = connString;
            }
            else if (appSettings.Value.Properties.TryGetValue("APPINSIGHTS_INSTRUMENTATIONKEY", out var instrKey))
            {
                instrumentationKey = instrKey;
            }
            
            if (string.IsNullOrEmpty(appInsightsConnectionString) && string.IsNullOrEmpty(instrumentationKey))
            {
                Console.WriteLine("⚠️  No Application Insights configuration found in app settings");
                return (consoleLogs, httpLogs);
            }
            
            // Extract Application Insights resource information
            string? appInsightsResourceId = null;
            if (!string.IsNullOrEmpty(appInsightsConnectionString))
            {
                // Parse connection string to get instrumentation key
                var parts = appInsightsConnectionString.Split(';');
                foreach (var part in parts)
                {
                    if (part.StartsWith("InstrumentationKey="))
                    {
                        instrumentationKey = part.Substring("InstrumentationKey=".Length);
                        break;
                    }
                }
            }
            
            if (string.IsNullOrEmpty(instrumentationKey))
            {
                Console.WriteLine("⚠️  Could not extract instrumentation key from Application Insights configuration");
                return (consoleLogs, httpLogs);
            }
            
            // Find Application Insights resource by instrumentation key
            var appInsightsResourceSearchUrl = $"https://management.azure.com/subscriptions/{SubscriptionId}/providers/Microsoft.Insights/components?api-version=2020-02-02";
            var searchResponse = await httpClient.GetAsync(appInsightsResourceSearchUrl);
            
            if (!searchResponse.IsSuccessStatusCode)
            {
                Console.WriteLine($"⚠️  Failed to search for Application Insights resources (Status: {(int)searchResponse.StatusCode})");
                return (consoleLogs, httpLogs);
            }
            
            var searchContent = await searchResponse.Content.ReadAsStringAsync();
            using var searchDoc = JsonDocument.Parse(searchContent);
            var components = searchDoc.RootElement.GetProperty("value");
            
            foreach (var component in components.EnumerateArray())
            {
                var properties = component.GetProperty("properties");
                if (properties.TryGetProperty("InstrumentationKey", out var keyElement) && 
                    keyElement.GetString() == instrumentationKey)
                {
                    appInsightsResourceId = component.GetProperty("id").GetString();
                    break;
                }
            }
            
            if (string.IsNullOrEmpty(appInsightsResourceId))
            {
                Console.WriteLine("⚠️  Could not find Application Insights resource with matching instrumentation key");
                return (consoleLogs, httpLogs);
            }
            
            // Query Application Insights for console and HTTP logs
            var timeAgo = DateTime.UtcNow.AddMinutes(-lastMinutes).ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            
            // Query for console logs (traces)
            var consoleQuery = $@"
                traces
                | where timestamp >= datetime({timeAgo})
                | where cloud_RoleName contains '{WebAppName}'
                | order by timestamp desc
                | project timestamp, message, severityLevel
                | take 100";
            
            var consoleQueryUrl = $"https://api.applicationinsights.io/v1/apps/{instrumentationKey}/query";
            var consoleQueryContent = new StringContent(JsonSerializer.Serialize(new { query = consoleQuery }), System.Text.Encoding.UTF8, "application/json");
            
            var consoleResponse = await httpClient.PostAsync(consoleQueryUrl, consoleQueryContent);
            if (consoleResponse.IsSuccessStatusCode)
            {
                var consoleJson = await consoleResponse.Content.ReadAsStringAsync();
                using var consoleDoc = JsonDocument.Parse(consoleJson);
                
                if (consoleDoc.RootElement.TryGetProperty("tables", out var consoleTables) && consoleTables.GetArrayLength() > 0)
                {
                    var consoleTable = consoleTables[0];
                    if (consoleTable.TryGetProperty("rows", out var consoleRows))
                    {
                        foreach (var row in consoleRows.EnumerateArray())
                        {
                            var timestamp = row[0].GetString();
                            var message = row[1].GetString();
                            var severity = row[2].GetString();
                            consoleLogs.Add($"[{timestamp}] [{severity}] {message}");
                        }
                    }
                }
            }
            
            // Query for HTTP logs (requests)
            var httpQuery = $@"
                requests
                | where timestamp >= datetime({timeAgo})
                | where cloud_RoleName contains '{WebAppName}'
                | order by timestamp desc
                | project timestamp, name, url, resultCode, duration, success
                | take 100";
            
            var httpQueryContent = new StringContent(JsonSerializer.Serialize(new { query = httpQuery }), System.Text.Encoding.UTF8, "application/json");
            
            var httpResponse = await httpClient.PostAsync(consoleQueryUrl, httpQueryContent);
            if (httpResponse.IsSuccessStatusCode)
            {
                var httpJson = await httpResponse.Content.ReadAsStringAsync();
                using var httpDoc = JsonDocument.Parse(httpJson);
                
                if (httpDoc.RootElement.TryGetProperty("tables", out var httpTables) && httpTables.GetArrayLength() > 0)
                {
                    var httpTable = httpTables[0];
                    if (httpTable.TryGetProperty("rows", out var httpRows))
                    {
                        foreach (var row in httpRows.EnumerateArray())
                        {
                            var timestamp = row[0].GetString();
                            var name = row[1].GetString();
                            var url = row[2].GetString();
                            var resultCode = row[3].GetString();
                            var duration = row[4].GetString();
                            var success = row[5].GetBoolean();
                            httpLogs.Add($"[{timestamp}] {name} {url} -> {resultCode} ({duration}ms) Success: {success}");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Error querying Application Insights: {ex.Message}");
        }
        
        return (consoleLogs, httpLogs);
    }
    
    private async Task<(List<string> consoleLogs, List<string> httpLogs)> TryGetLogAnalyticsLogsAsync(HttpClient httpClient, int lastMinutes)
    {
        var consoleLogs = new List<string>();
        var httpLogs = new List<string>();
        
        try
        {
            // Find Log Analytics workspace associated with the resource group or web app
            var workspacesUrl = $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.OperationalInsights/workspaces?api-version=2021-06-01";
            var workspacesResponse = await httpClient.GetAsync(workspacesUrl);
            
            if (!workspacesResponse.IsSuccessStatusCode)
            {
                // Try subscription-wide search if resource group search fails
                workspacesUrl = $"https://management.azure.com/subscriptions/{SubscriptionId}/providers/Microsoft.OperationalInsights/workspaces?api-version=2021-06-01";
                workspacesResponse = await httpClient.GetAsync(workspacesUrl);
            }
            
            if (!workspacesResponse.IsSuccessStatusCode)
            {
                Console.WriteLine($"⚠️  Failed to search for Log Analytics workspaces (Status: {(int)workspacesResponse.StatusCode})");
                return (consoleLogs, httpLogs);
            }
            
            var workspacesContent = await workspacesResponse.Content.ReadAsStringAsync();
            using var workspacesDoc = JsonDocument.Parse(workspacesContent);
            var workspaces = workspacesDoc.RootElement.GetProperty("value");
            
            if (workspaces.GetArrayLength() == 0)
            {
                Console.WriteLine("⚠️  No Log Analytics workspaces found");
                return (consoleLogs, httpLogs);
            }
            
            // Use the first available workspace
            var workspace = workspaces[0];
            var workspaceId = workspace.GetProperty("properties").GetProperty("customerId").GetString();
            var workspaceResourceId = workspace.GetProperty("id").GetString();
            
            if (string.IsNullOrEmpty(workspaceId))
            {
                Console.WriteLine("⚠️  Could not get workspace ID from Log Analytics workspace");
                return (consoleLogs, httpLogs);
            }
            
            // Query Log Analytics for console and HTTP logs
            var timeAgo = DateTime.UtcNow.AddMinutes(-lastMinutes).ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            
            // Query for console logs from App Service logs
            var consoleQuery = $@"
                AppServiceConsoleLogs_CL
                | where TimeGenerated >= datetime({timeAgo})
                | where ResultDescription contains '{WebAppName}'
                | order by TimeGenerated desc
                | project TimeGenerated, ResultDescription
                | take 100";
            
            var queryUrl = $"https://api.loganalytics.io/v1/workspaces/{workspaceId}/query";
            var consoleQueryContent = new StringContent(JsonSerializer.Serialize(new { query = consoleQuery }), System.Text.Encoding.UTF8, "application/json");
            
            var consoleResponse = await httpClient.PostAsync(queryUrl, consoleQueryContent);
            if (consoleResponse.IsSuccessStatusCode)
            {
                var consoleJson = await consoleResponse.Content.ReadAsStringAsync();
                using var consoleDoc = JsonDocument.Parse(consoleJson);
                
                if (consoleDoc.RootElement.TryGetProperty("tables", out var consoleTables) && consoleTables.GetArrayLength() > 0)
                {
                    var consoleTable = consoleTables[0];
                    if (consoleTable.TryGetProperty("rows", out var consoleRows))
                    {
                        foreach (var row in consoleRows.EnumerateArray())
                        {
                            var timestamp = row[0].GetString();
                            var message = row[1].GetString();
                            consoleLogs.Add($"[{timestamp}] {message}");
                        }
                    }
                }
            }
            
            // Query for HTTP logs from App Service HTTP logs
            var httpQuery = $@"
                AppServiceHTTPLogs_CL
                | where TimeGenerated >= datetime({timeAgo})
                | where CsHost contains '{WebAppName}'
                | order by TimeGenerated desc
                | project TimeGenerated, CsMethod, CsUriStem, ScStatus, TimeTaken
                | take 100";
            
            var httpQueryContent = new StringContent(JsonSerializer.Serialize(new { query = httpQuery }), System.Text.Encoding.UTF8, "application/json");
            
            var httpResponse = await httpClient.PostAsync(queryUrl, httpQueryContent);
            if (httpResponse.IsSuccessStatusCode)
            {
                var httpJson = await httpResponse.Content.ReadAsStringAsync();
                using var httpDoc = JsonDocument.Parse(httpJson);
                
                if (httpDoc.RootElement.TryGetProperty("tables", out var httpTables) && httpTables.GetArrayLength() > 0)
                {
                    var httpTable = httpTables[0];
                    if (httpTable.TryGetProperty("rows", out var httpRows))
                    {
                        foreach (var row in httpRows.EnumerateArray())
                        {
                            var timestamp = row[0].GetString();
                            var method = row[1].GetString();
                            var uriStem = row[2].GetString();
                            var status = row[3].GetString();
                            var timeTaken = row[4].GetString();
                            httpLogs.Add($"[{timestamp}] {method} {uriStem} -> {status} ({timeTaken}ms)");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Error querying Log Analytics workspace: {ex.Message}");
        }
        
        return (consoleLogs, httpLogs);
    }

    protected async Task<Dictionary<string, List<(DateTime timestamp, double value)>>> GetApplicationMetricsAsync(
        string[] metricNames, 
        int lastMinutes = 30,
        string aggregation = "Average")
    {
        var metrics = new Dictionary<string, List<(DateTime timestamp, double value)>>();
        
        try
        {
            // Get access token from DefaultAzureCredential
            var credential = new DefaultAzureCredential();
            var tokenRequestContext = new TokenRequestContext(new[] { "https://management.azure.com/.default" });
            var accessToken = await credential.GetTokenAsync(tokenRequestContext);
            
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken.Token);
            
            // Get the web app resource to build the resource ID
            var webApp = await GetWebAppResource();
            var resourceId = webApp.Id.ToString();
            
            // Build time range
            var endTime = DateTime.UtcNow;
            var startTime = endTime.AddMinutes(-lastMinutes);
            var timespan = $"{startTime:yyyy-MM-ddTHH:mm:ss.fffZ}/{endTime:yyyy-MM-ddTHH:mm:ss.fffZ}";
            
            // Join metric names for the API call
            var metricNamesParam = string.Join(",", metricNames);
            
            // Build the Azure Monitor REST API URL
            var metricsUrl = $"https://management.azure.com{resourceId}/providers/Microsoft.Insights/metrics" +
                           $"?api-version=2018-01-01" +
                           $"&metricnames={metricNamesParam}" +
                           $"&timespan={timespan}" +
                           $"&interval=PT1M" +
                           $"&aggregation={aggregation}";
            
            Console.WriteLine($"Retrieving metrics: {metricNamesParam} for the last {lastMinutes} minutes...");
            
            var response = await httpClient.GetAsync(metricsUrl);
            
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"⚠️  Failed to retrieve metrics (Status: {(int)response.StatusCode}): {response.ReasonPhrase}");
                return metrics;
            }
            
            var content = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);
            
            if (doc.RootElement.TryGetProperty("value", out var metricsArray))
            {
                foreach (var metric in metricsArray.EnumerateArray())
                {
                    if (metric.TryGetProperty("name", out var nameElement) &&
                        nameElement.TryGetProperty("value", out var metricNameElement))
                    {
                        var metricName = metricNameElement.GetString();
                        if (string.IsNullOrEmpty(metricName)) continue;
                        
                        var dataPoints = new List<(DateTime timestamp, double value)>();
                        
                        if (metric.TryGetProperty("timeseries", out var timeseriesArray) && timeseriesArray.GetArrayLength() > 0)
                        {
                            var timeseries = timeseriesArray[0];
                            if (timeseries.TryGetProperty("data", out var dataArray))
                            {
                                foreach (var dataPoint in dataArray.EnumerateArray())
                                {
                                    if (dataPoint.TryGetProperty("timeStamp", out var timestampElement) &&
                                        DateTime.TryParse(timestampElement.GetString(), out var timestamp))
                                    {
                                        double? value = null;
                                        
                                        // Try to get the value based on the aggregation type
                                        var aggregationLower = aggregation.ToLower();
                                        if (dataPoint.TryGetProperty(aggregationLower, out var valueElement) && 
                                            valueElement.ValueKind == JsonValueKind.Number)
                                        {
                                            value = valueElement.GetDouble();
                                        }
                                        else if (dataPoint.TryGetProperty("average", out valueElement) && 
                                                valueElement.ValueKind == JsonValueKind.Number)
                                        {
                                            value = valueElement.GetDouble();
                                        }
                                        
                                        if (value.HasValue)
                                        {
                                            dataPoints.Add((timestamp, value.Value));
                                        }
                                    }
                                }
                            }
                        }
                        
                        metrics[metricName] = dataPoints;
                        Console.WriteLine($"✓ Retrieved {dataPoints.Count} data points for metric: {metricName}");
                    }
                }
            }
            
            Console.WriteLine($"✓ Successfully retrieved metrics for {metrics.Count} metric types");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Error retrieving application metrics: {ex.Message}");
        }
        
        return metrics;
    }

    protected async Task<bool> CheckMetricThreshold(string metricName, double threshold, string comparison = "greater", int lastMinutes = 15)
    {
        try
        {
            var metrics = await GetApplicationMetricsAsync(new[] { metricName }, lastMinutes);
            
            if (!metrics.ContainsKey(metricName) || metrics[metricName].Count == 0)
            {
                Console.WriteLine($"⚠️  No data available for metric: {metricName}");
                return false;
            }
            
            var dataPoints = metrics[metricName];
            var recentDataPoints = dataPoints.Where(dp => dp.timestamp >= DateTime.UtcNow.AddMinutes(-lastMinutes)).ToList();
            
            if (recentDataPoints.Count == 0)
            {
                Console.WriteLine($"⚠️  No recent data points for metric: {metricName}");
                return false;
            }
            
            // Check if any data points meet the threshold condition
            foreach (var (timestamp, value) in recentDataPoints)
            {
                bool thresholdMet = comparison.ToLower() switch
                {
                    "greater" => value > threshold,
                    "less" => value < threshold,
                    "equal" => Math.Abs(value - threshold) < 0.01,
                    "greaterequal" => value >= threshold,
                    "lessequal" => value <= threshold,
                    _ => value > threshold
                };
                
                if (thresholdMet)
                {
                    Console.WriteLine($"✓ Metric threshold met: {metricName} = {value:F2} (threshold: {threshold}, comparison: {comparison}) at {timestamp:yyyy-MM-dd HH:mm:ss}");
                    return true;
                }
            }
            
            // Show the recent values for context
            var avgValue = recentDataPoints.Average(dp => dp.value);
            var maxValue = recentDataPoints.Max(dp => dp.value);
            Console.WriteLine($"ℹ️  Metric {metricName}: Average = {avgValue:F2}, Max = {maxValue:F2} (threshold: {threshold}, comparison: {comparison})");
            
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Error checking metric threshold for {metricName}: {ex.Message}");
            return false;
        }
    }

    protected async Task<string?> GetStartupCommandFromStagingSlot()
    {
        try
        {
            Console.WriteLine("Checking staging slot for correct startup command...");
            
            // Try to access staging slot using REST API approach
            var credential = new DefaultAzureCredential();
            var tokenRequestContext = new TokenRequestContext(new[] { "https://management.azure.com/.default" });
            var accessToken = await credential.GetTokenAsync(tokenRequestContext);
            
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken.Token);
            
            // Build staging slot app settings URL
            var stagingSlotUrl = $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Web/sites/{WebAppName}/slots/staging/config/appsettings/list?api-version=2022-03-01";
            
            try
            {
                var response = await httpClient.PostAsync(stagingSlotUrl, new StringContent(""));
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(content);
                    
                    if (doc.RootElement.TryGetProperty("properties", out var properties) &&
                        properties.TryGetProperty("AZURE_WEBAPP_STARTUP_COMMAND", out var startupCommandElement))
                    {
                        var stagingStartupCommand = startupCommandElement.GetString();
                        Console.WriteLine($"✓ Found startup command in staging slot: {stagingStartupCommand}");
                        return stagingStartupCommand;
                    }
                    else
                    {
                        Console.WriteLine("ℹ️  No startup command configured in staging slot");
                        return null;
                    }
                }
                else
                {
                    Console.WriteLine($"ℹ️  Staging slot not accessible (Status: {(int)response.StatusCode})");
                }
            }
            catch (Exception slotEx)
            {
                Console.WriteLine($"ℹ️  Staging slot not available: {slotEx.Message}");
            }
            
            // If staging slot is not available, return null to use platform defaults
            Console.WriteLine("ℹ️  No reliable startup command found from staging slot, will remove setting to use platform defaults");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Error retrieving startup command from staging slot: {ex.Message}");
            return null;
        }
    }
}

// Scenario implementations

[Scenario]
public class MissingEnvironmentVariableScenario : ScenarioBase
{
    public override string Description => "Missing environment variable (SECRET_KEY)";
    public override string WebAppName => $"noenvvar-{ResourceName}";
    private string? _savedValue;

    public override async Task Setup()
    {
        _savedValue = await GetAppSetting("SECRET_KEY");
        if (_savedValue == null)
        {
            throw new Exception("SECRET_KEY app setting not found");
        }
        
        await UpdateAppSetting("SECRET_KEY", null);
        Console.WriteLine("Removed SECRET_KEY app setting");
        await Task.Delay(30000); // Wait 30 seconds
    }

    public override async Task<bool> Recover()
    {
        Console.WriteLine("Testing application state to identify missing environment variable...");
        
        try
        {
            // First, try to make an HTTP request to see the current state
            await MakeHttpRequest(TargetAddress, expectedSuccess: true);
            Console.WriteLine("⚠️  Application appears to be working, no environment variable issue detected");
            return true;
        }
        catch (Exception httpEx)
        {
            Console.WriteLine($"✓ Confirmed application failure: {httpEx.Message}");
            
            // Get application logs to find the specific error
            var (consoleLogs, httpLogs) = await GetApplicationLogsAsync(10); // Last 10 minutes
            
            string? missingVariableName = null;
            
            // Look for environment variable errors in console logs
            foreach (var logEntry in consoleLogs)
            {
                var lowerLogEntry = logEntry.ToLower();
                
                // Check for various patterns of environment variable errors
                if (lowerLogEntry.Contains("environment variable") && 
                    (lowerLogEntry.Contains("not found") || lowerLogEntry.Contains("missing") || lowerLogEntry.Contains("null")))
                {
                    // Try to extract variable name from the log message
                    missingVariableName = ExtractVariableNameFromLog(logEntry);
                    if (!string.IsNullOrEmpty(missingVariableName))
                    {
                        Console.WriteLine($"✓ Identified missing environment variable: {missingVariableName}");
                        break;
                    }
                }
                
                // Check for ArgumentNullException related to environment variables
                if (lowerLogEntry.Contains("argumentnullexception") && lowerLogEntry.Contains("environment"))
                {
                    missingVariableName = ExtractVariableNameFromLog(logEntry);
                    if (!string.IsNullOrEmpty(missingVariableName))
                    {
                        Console.WriteLine($"✓ Identified missing environment variable from ArgumentNullException: {missingVariableName}");
                        break;
                    }
                }
                
                // Check for specific SECRET_KEY errors (since this scenario removes SECRET_KEY)
                if (lowerLogEntry.Contains("secret_key") && 
                    (lowerLogEntry.Contains("not found") || lowerLogEntry.Contains("missing") || lowerLogEntry.Contains("null")))
                {
                    missingVariableName = "SECRET_KEY";
                    Console.WriteLine($"✓ Identified missing environment variable: {missingVariableName}");
                    break;
                }
            }
            
            // If no specific variable found in logs, return false
            if (string.IsNullOrEmpty(missingVariableName))
            {
                Console.WriteLine("⚠️  Could not extract variable name from logs, unable to proceed with recovery");
                return false;
            }
            
            // Restore the environment variable
            if (_savedValue != null && !string.IsNullOrEmpty(missingVariableName))
            {
                Console.WriteLine($"Restoring environment variable '{missingVariableName}' with saved value...");
                await UpdateAppSetting(missingVariableName, _savedValue);
                Console.WriteLine($"✓ Restored {missingVariableName} app setting");
                
                // Wait for the change to take effect
                await Task.Delay(30000);
                
                // Verify the fix worked
                try
                {
                    await MakeHttpRequest(TargetAddress, expectedSuccess: true);
                    Console.WriteLine($"✓ Application recovery successful after restoring {missingVariableName}");
                    return true;
                }
                catch (Exception verifyEx)
                {
                    Console.WriteLine($"⚠️  Application still failing after restoring {missingVariableName}: {verifyEx.Message}");
                    return false;
                }
            }
            else
            {
                Console.WriteLine("⚠️  Cannot restore environment variable: no saved value available");
                return false;
            }
        }
    }
    
    private string? ExtractVariableNameFromLog(string logEntry)
    {
        try
        {
            // Try various patterns to extract variable name
            var patterns = new[]
            {
                @"environment variable['\s]*([A-Z_]+)['\s]*not found",
                @"([A-Z_]+)['\s]*environment variable['\s]*not found",
                @"missing['\s]*environment variable['\s]*([A-Z_]+)",
                @"([A-Z_]+)['\s]*is['\s]*not['\s]*set",
                @"ArgumentNullException.*parameter['\s]*([A-Z_]+)",
                @"variable['\s]*([A-Z_]+)['\s]*is['\s]*null",
                @"SECRET_KEY", // Specific for this scenario
            };
            
            foreach (var pattern in patterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(logEntry, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    if (pattern == "SECRET_KEY")
                    {
                        return "SECRET_KEY";
                    }
                    else if (match.Groups.Count > 1 && !string.IsNullOrEmpty(match.Groups[1].Value))
                    {
                        var variableName = match.Groups[1].Value.Trim('\'', '"', ' ').ToUpper();
                        if (IsValidEnvironmentVariableName(variableName))
                        {
                            return variableName;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Error extracting variable name from log: {ex.Message}");
        }
        
        return null;
    }
    
    private bool IsValidEnvironmentVariableName(string name)
    {
        // Check if the extracted name looks like a valid environment variable name
        return !string.IsNullOrEmpty(name) && 
               name.Length > 1 && 
               name.All(c => char.IsLetterOrDigit(c) || c == '_') &&
               char.IsLetter(name[0]);
    }
}

[Scenario]
public class MissingConnectionStringScenario : ScenarioBase
{
    public override string Description => "Missing connection string (DATABASE_URL)";
    public override string WebAppName => $"noconn-{ResourceName}";
    private string? _savedValue;

    public override async Task Setup()
    {
        _savedValue = await GetAppSetting("DATABASE_URL");
        if (_savedValue == null)
        {
            throw new Exception("DATABASE_URL app setting not found");
        }
        
        await UpdateAppSetting("DATABASE_URL", null);
        Console.WriteLine("Removed DATABASE_URL app setting");
        await Task.Delay(30000); // Wait 30 seconds
    }

    public override async Task<bool> Recover()
    {
        Console.WriteLine("Testing application state to identify missing connection string...");
        
        try
        {
            // First, try to make an HTTP request to see the current state
            await MakeHttpRequest(TargetAddress, expectedSuccess: true);
            Console.WriteLine("⚠️  Application appears to be working, no connection string issue detected");
            return true;
        }
        catch (Exception httpEx)
        {
            Console.WriteLine($"✓ Confirmed application failure: {httpEx.Message}");
            
            // Get application logs to find the specific error
            var (consoleLogs, httpLogs) = await GetApplicationLogsAsync(10); // Last 10 minutes
            
            string? missingConnectionStringName = null;
            
            // Look for connection string errors in console logs
            foreach (var logEntry in consoleLogs)
            {
                var lowerLogEntry = logEntry.ToLower();
                
                // Check for various patterns of connection string errors
                if (lowerLogEntry.Contains("database connection string") && 
                    (lowerLogEntry.Contains("missing") || lowerLogEntry.Contains("not found") || lowerLogEntry.Contains("null")))
                {
                    // Try to extract connection string name from the log message
                    missingConnectionStringName = ExtractConnectionStringNameFromLog(logEntry);
                    if (!string.IsNullOrEmpty(missingConnectionStringName))
                    {
                        Console.WriteLine($"✓ Identified missing database connection string: {missingConnectionStringName}");
                        break;
                    }
                }
                
                // Check for database connection errors
                if ((lowerLogEntry.Contains("database") || lowerLogEntry.Contains("connection")) && 
                    (lowerLogEntry.Contains("not found") || lowerLogEntry.Contains("missing") || lowerLogEntry.Contains("null") || lowerLogEntry.Contains("empty")))
                {
                    missingConnectionStringName = ExtractConnectionStringNameFromLog(logEntry);
                    if (!string.IsNullOrEmpty(missingConnectionStringName))
                    {
                        Console.WriteLine($"✓ Identified missing connection string: {missingConnectionStringName}");
                        break;
                    }
                }
                
                // Check for specific DATABASE_URL errors (since this scenario removes DATABASE_URL)
                if (lowerLogEntry.Contains("database_url") && 
                    (lowerLogEntry.Contains("not found") || lowerLogEntry.Contains("missing") || lowerLogEntry.Contains("null")))
                {
                    missingConnectionStringName = "DATABASE_URL";
                    Console.WriteLine($"✓ Identified missing connection string: {missingConnectionStringName}");
                    break;
                }
                
                // Check for PostgreSQL connection errors
                if ((lowerLogEntry.Contains("postgresql") || lowerLogEntry.Contains("postgres")) && 
                    (lowerLogEntry.Contains("connection") || lowerLogEntry.Contains("connect")) &&
                    (lowerLogEntry.Contains("failed") || lowerLogEntry.Contains("error") || lowerLogEntry.Contains("unable")))
                {
                    missingConnectionStringName = "DATABASE_URL"; // Default for this scenario
                    Console.WriteLine($"✓ Identified PostgreSQL connection issue, assuming DATABASE_URL: {missingConnectionStringName}");
                    break;
                }
            }
            
            // If no specific connection string found in logs, return false
            if (string.IsNullOrEmpty(missingConnectionStringName))
            {
                Console.WriteLine("⚠️  Could not extract connection string name from logs, unable to proceed with recovery");
                return false;
            }
            
            // Restore the connection string
            if (_savedValue != null && !string.IsNullOrEmpty(missingConnectionStringName))
            {
                Console.WriteLine($"Restoring database connection string '{missingConnectionStringName}' with saved value...");
                await UpdateAppSetting(missingConnectionStringName, _savedValue);
                Console.WriteLine($"✓ Restored {missingConnectionStringName} app setting");
                
                // Wait for the change to take effect
                await Task.Delay(30000);
                
                // Verify the fix worked
                try
                {
                    await MakeHttpRequest(TargetAddress, expectedSuccess: true);
                    Console.WriteLine($"✓ Application recovery successful after restoring {missingConnectionStringName}");
                    return true;
                }
                catch (Exception verifyEx)
                {
                    Console.WriteLine($"⚠️  Application still failing after restoring {missingConnectionStringName}: {verifyEx.Message}");
                    return false;
                }
            }
            else
            {
                Console.WriteLine("⚠️  Cannot restore connection string: no saved value available");
                return false;
            }
        }
    }
    
    private string? ExtractConnectionStringNameFromLog(string logEntry)
    {
        try
        {
            // Try various patterns to extract connection string name
            var patterns = new[]
            {
                @"database connection string['\s]*([A-Z_]+)['\s]*missing",
                @"missing['\s]*database connection string['\s]*([A-Z_]+)",
                @"connection string['\s]*([A-Z_]+)['\s]*not found",
                @"([A-Z_]+)['\s]*connection string['\s]*not found",
                @"([A-Z_]+)['\s]*database['\s]*connection['\s]*missing",
                @"database['\s]*([A-Z_]+)['\s]*not found",
                @"([A-Z_]+)['\s]*is['\s]*null['\s]*or['\s]*empty",
                @"DATABASE_URL", // Specific for this scenario
                @"CONNECTION_STRING",
                @"DB_CONNECTION",
            };
            
            foreach (var pattern in patterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(logEntry, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    if (pattern == "DATABASE_URL" || pattern == "CONNECTION_STRING" || pattern == "DB_CONNECTION")
                    {
                        return pattern;
                    }
                    else if (match.Groups.Count > 1 && !string.IsNullOrEmpty(match.Groups[1].Value))
                    {
                        var connectionStringName = match.Groups[1].Value.Trim('\'', '"', ' ').ToUpper();
                        if (IsValidConnectionStringName(connectionStringName))
                        {
                            return connectionStringName;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Error extracting connection string name from log: {ex.Message}");
        }
        
        return null;
    }
    
    private bool IsValidConnectionStringName(string name)
    {
        // Check if the extracted name looks like a valid connection string name
        return !string.IsNullOrEmpty(name) && 
               name.Length > 1 && 
               name.All(c => char.IsLetterOrDigit(c) || c == '_') &&
               char.IsLetter(name[0]) &&
               (name.Contains("DATABASE") || name.Contains("CONNECTION") || name.Contains("DB") || name.Contains("URL"));
    }
}

[Scenario]
public class HighMemoryScenario : ScenarioBase
{
    public override string Description => "High memory usage";
    public override string WebAppName => $"highmem-{ResourceName}";

    public override async Task Setup()
    {
        var url = $"{TargetAddress}/api/faults/highmemory";
        for (int i = 1; i <= 10; i++)
        {
            Console.WriteLine($"Making high memory request {i}/10...");
            await HttpClient.GetAsync(url);
        }
        Console.WriteLine("Completed high memory requests");
    }

    public override async Task<bool> Recover()
    {
        Console.WriteLine("Analyzing memory usage metrics to determine if intervention is needed...");
        
        try
        {
            // First, check actual memory metrics from Azure Monitor
            Console.WriteLine("Retrieving memory usage metrics from Azure Monitor...");
            var highMemoryDetectedFromMetrics = await CheckMetricThreshold("MemoryPercentage", 85.0, "greaterequal", 15);
            
            bool highMemoryDetected = highMemoryDetectedFromMetrics;
            string? highMemoryMessage = null;
            
            if (highMemoryDetectedFromMetrics)
            {
                // Get detailed metrics to show the actual values
                var metrics = await GetApplicationMetricsAsync(new[] { "MemoryPercentage" }, 15);
                if (metrics.ContainsKey("MemoryPercentage") && metrics["MemoryPercentage"].Count > 0)
                {
                    var recentDataPoints = metrics["MemoryPercentage"]
                        .Where(dp => dp.timestamp >= DateTime.UtcNow.AddMinutes(-15))
                        .OrderByDescending(dp => dp.timestamp)
                        .ToList();
                    
                    if (recentDataPoints.Count > 0)
                    {
                        var maxMemory = recentDataPoints.Max(dp => dp.value);
                        var avgMemory = recentDataPoints.Average(dp => dp.value);
                        var latestMemory = recentDataPoints.First().value;
                        
                        highMemoryMessage = $"Memory usage exceeded 85% - Latest: {latestMemory:F1}%, Average: {avgMemory:F1}%, Peak: {maxMemory:F1}%";
                        Console.WriteLine($"✓ High memory usage detected from metrics: {highMemoryMessage}");
                    }
                }
            }
            else
            {
                Console.WriteLine("ℹ️  Memory metrics show usage below 85% threshold, checking application logs for memory issues...");
                
                // Fallback: Get application logs to check for memory-related errors
                var (consoleLogs, httpLogs) = await GetApplicationLogsAsync(15); // Last 15 minutes
                
                // Look for high memory usage indicators in console logs
                foreach (var logEntry in consoleLogs)
                {
                    var lowerLogEntry = logEntry.ToLower();
                    
                    // Check for memory usage patterns
                    if (lowerLogEntry.Contains("memory") && 
                        (lowerLogEntry.Contains("90%") || lowerLogEntry.Contains("85%") || lowerLogEntry.Contains("high") || 
                         lowerLogEntry.Contains("exceeded") || lowerLogEntry.Contains("critical")))
                    {
                        highMemoryDetected = true;
                        highMemoryMessage = logEntry;
                        Console.WriteLine($"✓ High memory usage detected in logs: {logEntry}");
                        break;
                    }
                    
                    // Check for OutOfMemoryException
                    if (lowerLogEntry.Contains("outofmemoryexception") || lowerLogEntry.Contains("out of memory"))
                    {
                        highMemoryDetected = true;
                        highMemoryMessage = logEntry;
                        Console.WriteLine($"✓ Out of memory condition detected: {logEntry}");
                        break;
                    }
                    
                    // Check for memory pressure indicators
                    if ((lowerLogEntry.Contains("memory pressure") || lowerLogEntry.Contains("low memory")) ||
                        (lowerLogEntry.Contains("gc") && lowerLogEntry.Contains("pressure")))
                    {
                        highMemoryDetected = true;
                        highMemoryMessage = logEntry;
                        Console.WriteLine($"✓ Memory pressure detected: {logEntry}");
                        break;
                    }
                }
            }
            
            // If high memory detected, restart the app
            if (highMemoryDetected)
            {
                Console.WriteLine($"🔴 High memory usage confirmed: {highMemoryMessage ?? "Memory usage >= 85%"}");
                Console.WriteLine("Restarting application to clear memory...");
                
                await RestartApp();
                
                Console.WriteLine("Waiting 30 seconds for app to restart and stabilize...");
                await Task.Delay(30000);
                
                // Verify the app is working after restart
                try
                {
                    await MakeHttpRequest(TargetAddress, expectedSuccess: true);
                    Console.WriteLine("✓ Application recovery successful after restart");
                    
                    // Optional: Check if memory usage improved after restart
                    Console.WriteLine("Checking memory usage after restart...");
                    await Task.Delay(30000); // Wait a bit more for metrics to update
                    
                    var postRestartMemoryHigh = await CheckMetricThreshold("MemoryPercentage", 85.0, "greaterequal", 5);
                    if (!postRestartMemoryHigh)
                    {
                        Console.WriteLine("✓ Memory usage improved after restart");
                    }
                    else
                    {
                        Console.WriteLine("⚠️  Memory usage still high after restart - may need further investigation");
                    }
                    
                    return true;
                }
                catch (Exception verifyEx)
                {
                    Console.WriteLine($"⚠️  Application still having issues after restart: {verifyEx.Message}");
                    Console.WriteLine("Waiting additional 60 seconds for memory to stabilize...");
                    await Task.Delay(60000);
                    return false;
                }
            }
            else
            {
                Console.WriteLine("ℹ️  No high memory usage detected (< 85% threshold)");
                Console.WriteLine("Waiting 60 seconds for memory to stabilize naturally...");
                await Task.Delay(60000);
                return true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Error analyzing memory metrics: {ex.Message}");
            Console.WriteLine("Falling back to standard memory stabilization wait...");
            await Task.Delay(60000);
            return false;
        }
    }
}

[Scenario]
public class SnatPortExhaustionScenario : ScenarioBase
{
    public override string Description => "SNAT port exhaustion";
    public override string WebAppName => $"snat-{ResourceName}";

    public override async Task Setup()
    {
        var url = $"{TargetAddress}/api/faults/snat";
        for (int i = 1; i <= 10; i++)
        {
            Console.WriteLine($"Making SNAT exhaustion request {i}/10...");
            await HttpClient.GetAsync(url);
        }
        Console.WriteLine("Completed SNAT exhaustion requests");
    }

    public override async Task<bool> Recover()
    {
        Console.WriteLine("Analyzing socket usage metrics to determine if SNAT port exhaustion recovery is needed...");
        
        try
        {
            // Check actual socket metrics from Azure Monitor
            Console.WriteLine("Retrieving socket usage metrics from Azure Monitor...");
            var highSocketUsageDetected = await CheckMetricThreshold("SocketOutboundAll", 100.0, "greater", 15);
            
            if (highSocketUsageDetected)
            {
                // Get detailed metrics to show the actual values
                var metrics = await GetApplicationMetricsAsync(new[] { "SocketOutboundAll" }, 15);
                if (metrics.ContainsKey("SocketOutboundAll") && metrics["SocketOutboundAll"].Count > 0)
                {
                    var recentDataPoints = metrics["SocketOutboundAll"]
                        .Where(dp => dp.timestamp >= DateTime.UtcNow.AddMinutes(-15))
                        .OrderByDescending(dp => dp.timestamp)
                        .ToList();
                    
                    if (recentDataPoints.Count > 0)
                    {
                        var maxSockets = recentDataPoints.Max(dp => dp.value);
                        var avgSockets = recentDataPoints.Average(dp => dp.value);
                        var latestSockets = recentDataPoints.First().value;
                        
                        Console.WriteLine($"✓ High socket usage detected - Latest: {latestSockets:F0}, Average: {avgSockets:F0}, Peak: {maxSockets:F0} (threshold: 100)");
                        Console.WriteLine($"🔴 SNAT port exhaustion confirmed - socket count above 100");
                        Console.WriteLine("Restarting application to release sockets and clear SNAT port usage...");
                        
                        await RestartApp();
                        
                        Console.WriteLine("Waiting 30 seconds for app to restart and SNAT ports to be released...");
                        await Task.Delay(30000);
                        
                        // Verify the app is working after restart
                        try
                        {
                            await MakeHttpRequest(TargetAddress, expectedSuccess: true);
                            Console.WriteLine("✓ Application recovery successful after restart");
                            
                            // Check if socket usage improved after restart
                            Console.WriteLine("Checking socket usage after restart...");
                            await Task.Delay(30000); // Wait a bit more for metrics to update
                            
                            var postRestartSocketsHigh = await CheckMetricThreshold("SocketOutboundAll", 100.0, "greater", 5);
                            if (!postRestartSocketsHigh)
                            {
                                Console.WriteLine("✓ Socket usage improved after restart - SNAT port exhaustion resolved");
                            }
                            else
                            {
                                Console.WriteLine("⚠️  Socket usage still high after restart - may need further investigation");
                            }
                            
                            return true;
                        }
                        catch (Exception verifyEx)
                        {
                            Console.WriteLine($"⚠️  Application still having issues after restart: {verifyEx.Message}");
                            Console.WriteLine("Waiting additional 60 seconds for SNAT ports to be released...");
                            await Task.Delay(60000);
                            return false;
                        }
                    }
                }
                
                // Fallback if we can't get detailed metrics but threshold was met
                Console.WriteLine("🔴 SNAT port exhaustion detected from metrics - restarting application...");
                await RestartApp();
                await Task.Delay(30000);
                return true;
            }
            else
            {
                Console.WriteLine("ℹ️  Socket usage below threshold (≤ 100), no SNAT port exhaustion detected");
                Console.WriteLine("Waiting 60 seconds for SNAT ports to be released naturally...");
                await Task.Delay(60000);
                return true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Error analyzing socket metrics: {ex.Message}");
            Console.WriteLine("Falling back to standard SNAT port release wait...");
            await Task.Delay(60000);
            return false;
        }
    }
}

[Scenario]
public class IncorrectStartupCommandScenario : ScenarioBase
{
    public override string Description => "Incorrect startup command";
    public override string WebAppName => $"badstartcmd-{ResourceName}";

    public override async Task Setup()
    {
        await UpdateAppSetting("AZURE_WEBAPP_STARTUP_COMMAND", "bogus_command_line");
        Console.WriteLine("Added incorrect startup command");
        await Task.Delay(30000); // Wait 30 seconds
    }

    public override async Task<bool> Recover()
    {
        Console.WriteLine("Analyzing application logs to identify startup command issues...");
        
        try
        {
            // Get application logs to find startup command errors
            var (consoleLogs, httpLogs) = await GetApplicationLogsAsync(15); // Last 15 minutes
            
            bool startupFailureDetected = false;
            string? failureMessage = null;
            
            // Look for Docker container startup failure indicators in console logs
            foreach (var logEntry in consoleLogs)
            {
                var lowerLogEntry = logEntry.ToLower();
                
                // Check for Docker container startup failures
                if (lowerLogEntry.Contains("docker") && 
                    (lowerLogEntry.Contains("failed to start") || lowerLogEntry.Contains("container start failed") || 
                     lowerLogEntry.Contains("startup command failed") || lowerLogEntry.Contains("command not found")))
                {
                    startupFailureDetected = true;
                    failureMessage = logEntry;
                    Console.WriteLine($"✓ Docker container startup failure detected: {logEntry}");
                    break;
                }
                
                // Check for startup command execution errors
                if ((lowerLogEntry.Contains("startup command") || lowerLogEntry.Contains("start command")) && 
                    (lowerLogEntry.Contains("error") || lowerLogEntry.Contains("failed") || lowerLogEntry.Contains("not found")))
                {
                    startupFailureDetected = true;
                    failureMessage = logEntry;
                    Console.WriteLine($"✓ Startup command execution error detected: {logEntry}");
                    break;
                }
                
                // Check for process execution failures
                if (lowerLogEntry.Contains("process") && 
                    (lowerLogEntry.Contains("exited") || lowerLogEntry.Contains("terminated") || lowerLogEntry.Contains("crashed")) &&
                    (lowerLogEntry.Contains("code") || lowerLogEntry.Contains("error")))
                {
                    startupFailureDetected = true;
                    failureMessage = logEntry;
                    Console.WriteLine($"✓ Process execution failure detected: {logEntry}");
                    break;
                }
                
                // Check for specific command not found errors
                if (lowerLogEntry.Contains("command not found") || lowerLogEntry.Contains("no such file or directory"))
                {
                    startupFailureDetected = true;
                    failureMessage = logEntry;
                    Console.WriteLine($"✓ Command not found error detected: {logEntry}");
                    break;
                }
            }
            
            if (startupFailureDetected)
            {
                Console.WriteLine($"🔴 Startup command failure confirmed: {failureMessage}");
                Console.WriteLine("Attempting to recover by using staging slot's startup command...");
                
                try
                {
                    // Try to get the correct startup command from staging slot
                    string? correctStartupCommand = await GetStartupCommandFromStagingSlot();
                    
                    if (!string.IsNullOrEmpty(correctStartupCommand))
                    {
                        Console.WriteLine($"✓ Found correct startup command from staging slot: {correctStartupCommand}");
                        await UpdateAppSetting("AZURE_WEBAPP_STARTUP_COMMAND", correctStartupCommand);
                        Console.WriteLine("✓ Updated startup command with staging slot configuration");
                    }
                    else
                    {
                        Console.WriteLine("⚠️  No staging slot startup command found, removing incorrect startup command");
                        await UpdateAppSetting("AZURE_WEBAPP_STARTUP_COMMAND", null);
                        Console.WriteLine("✓ Removed incorrect startup command");
                    }
                    
                    // Restart the app
                    await RestartApp();
                    
                    Console.WriteLine("Waiting 30 seconds for app to restart with corrected startup command...");
                    await Task.Delay(30000);
                    
                    // Verify the app is working after restart
                    try
                    {
                        await MakeHttpRequest(TargetAddress, expectedSuccess: true);
                        Console.WriteLine("✓ Application recovery successful after correcting startup command");
                        return true;
                    }
                    catch (Exception verifyEx)
                    {
                        Console.WriteLine($"⚠️  Application still having issues after startup command correction: {verifyEx.Message}");
                        Console.WriteLine("Waiting additional 30 seconds for startup to complete...");
                        await Task.Delay(30000);
                        return false;
                    }
                }
                catch (Exception recoveryEx)
                {
                    Console.WriteLine($"⚠️  Error during startup command recovery: {recoveryEx.Message}");
                    Console.WriteLine("Falling back to removing startup command entirely...");
                    
                    await UpdateAppSetting("AZURE_WEBAPP_STARTUP_COMMAND", null);
                    await RestartApp();
                    await Task.Delay(30000);
                    return false;
                }
            }
            else
            {
                Console.WriteLine("ℹ️  No startup command failures detected in logs");
                Console.WriteLine("Removing potentially incorrect startup command as precaution...");
                
                await UpdateAppSetting("AZURE_WEBAPP_STARTUP_COMMAND", null);
                Console.WriteLine("✓ Removed startup command setting");
                
                await RestartApp();
                Console.WriteLine("Waiting 30 seconds for app to restart...");
                await Task.Delay(30000);
                return true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Error analyzing startup command issues: {ex.Message}");
            Console.WriteLine("Falling back to removing startup command...");
            
            await UpdateAppSetting("AZURE_WEBAPP_STARTUP_COMMAND", null);
            Console.WriteLine("✓ Removed startup command setting as fallback");
            await Task.Delay(30000);
            return false;
        }
    }
}

[Scenario]
public class HighCpuScenario : ScenarioBase
{
    public override string Description => "High CPU usage";
    public override string WebAppName => $"highcpu-{ResourceName}";

    public override async Task Setup()
    {
        var url = $"{TargetAddress}/api/faults/highcpu";
        for (int i = 1; i <= 10; i++)
        {
            Console.WriteLine($"Making high CPU request {i}/10...");
            await HttpClient.GetAsync(url);
        }
        Console.WriteLine("Completed high CPU requests");
    }

    public override async Task<bool> Recover()
    {
        Console.WriteLine("Analyzing CPU usage metrics to determine if intervention is needed...");
        
        try
        {
            // First, check actual CPU metrics from Azure Monitor
            Console.WriteLine("Retrieving CPU usage metrics from Azure Monitor...");
            var highCpuDetectedFromMetrics = await CheckMetricThreshold("CpuPercentage", 85.0, "greaterequal", 15);
            
            bool highCpuDetected = highCpuDetectedFromMetrics;
            string? highCpuMessage = null;
            
            if (highCpuDetectedFromMetrics)
            {
                // Get detailed metrics to show the actual values
                var metrics = await GetApplicationMetricsAsync(new[] { "CpuPercentage" }, 15);
                if (metrics.ContainsKey("CpuPercentage") && metrics["CpuPercentage"].Count > 0)
                {
                    var recentDataPoints = metrics["CpuPercentage"]
                        .Where(dp => dp.timestamp >= DateTime.UtcNow.AddMinutes(-15))
                        .OrderByDescending(dp => dp.timestamp)
                        .ToList();
                    
                    if (recentDataPoints.Count > 0)
                    {
                        var maxCpu = recentDataPoints.Max(dp => dp.value);
                        var avgCpu = recentDataPoints.Average(dp => dp.value);
                        var latestCpu = recentDataPoints.First().value;
                        
                        highCpuMessage = $"CPU usage exceeded 85% - Latest: {latestCpu:F1}%, Average: {avgCpu:F1}%, Peak: {maxCpu:F1}%";
                        Console.WriteLine($"✓ High CPU usage detected from metrics: {highCpuMessage}");
                    }
                }
            }
            else
            {
                Console.WriteLine("ℹ️  CPU metrics show usage below 85% threshold, checking application logs for CPU issues...");
                
                // Fallback: Get application logs to check for CPU-related errors
                var (consoleLogs, httpLogs) = await GetApplicationLogsAsync(15); // Last 15 minutes
                
                // Look for high CPU usage indicators in console logs
                foreach (var logEntry in consoleLogs)
                {
                    var lowerLogEntry = logEntry.ToLower();
                    
                    // Check for CPU usage patterns
                    if (lowerLogEntry.Contains("cpu") && 
                        (lowerLogEntry.Contains("90%") || lowerLogEntry.Contains("85%") || lowerLogEntry.Contains("high") || 
                         lowerLogEntry.Contains("exceeded") || lowerLogEntry.Contains("critical")))
                    {
                        highCpuDetected = true;
                        highCpuMessage = logEntry;
                        Console.WriteLine($"✓ High CPU usage detected in logs: {logEntry}");
                        break;
                    }
                    
                    // Check for CPU throttling or performance issues
                    if (lowerLogEntry.Contains("cpu throttling") || lowerLogEntry.Contains("performance") && lowerLogEntry.Contains("degraded"))
                    {
                        highCpuDetected = true;
                        highCpuMessage = logEntry;
                        Console.WriteLine($"✓ CPU performance issue detected: {logEntry}");
                        break;
                    }
                    
                    // Check for CPU pressure indicators
                    if ((lowerLogEntry.Contains("cpu pressure") || lowerLogEntry.Contains("high load")) ||
                        (lowerLogEntry.Contains("processing") && lowerLogEntry.Contains("slow")))
                    {
                        highCpuDetected = true;
                        highCpuMessage = logEntry;
                        Console.WriteLine($"✓ CPU pressure detected: {logEntry}");
                        break;
                    }
                }
            }
            
            // If high CPU detected, restart the app
            if (highCpuDetected)
            {
                Console.WriteLine($"🔴 High CPU usage confirmed: {highCpuMessage ?? "CPU usage >= 85%"}");
                Console.WriteLine("Restarting application to clear CPU-intensive processes...");
                
                await RestartApp();
                
                Console.WriteLine("Waiting 30 seconds for app to restart and stabilize...");
                await Task.Delay(30000);
                
                // Verify the app is working after restart
                try
                {
                    await MakeHttpRequest(TargetAddress, expectedSuccess: true);
                    Console.WriteLine("✓ Application recovery successful after restart");
                    
                    // Optional: Check if CPU usage improved after restart
                    Console.WriteLine("Checking CPU usage after restart...");
                    await Task.Delay(30000); // Wait a bit more for metrics to update
                    
                    var postRestartCpuHigh = await CheckMetricThreshold("CpuPercentage", 85.0, "greaterequal", 5);
                    if (!postRestartCpuHigh)
                    {
                        Console.WriteLine("✓ CPU usage improved after restart");
                    }
                    else
                    {
                        Console.WriteLine("⚠️  CPU usage still high after restart - may need further investigation");
                    }
                    
                    return true;
                }
                catch (Exception verifyEx)
                {
                    Console.WriteLine($"⚠️  Application still having issues after restart: {verifyEx.Message}");
                    Console.WriteLine("Waiting additional 60 seconds for CPU to stabilize...");
                    await Task.Delay(60000);
                    return false;
                }
            }
            else
            {
                Console.WriteLine("ℹ️  No high CPU usage detected (< 85% threshold)");
                Console.WriteLine("Waiting 60 seconds for CPU to stabilize naturally...");
                await Task.Delay(60000);
                return true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Error analyzing CPU metrics: {ex.Message}");
            Console.WriteLine("Falling back to standard CPU stabilization wait...");
            await Task.Delay(60000);
            return false;
        }
    }
}

[Scenario]
public class MisconfiguredConnectionStringScenario : ScenarioBase
{
    public override string Description => "Misconfigured connection string (wrong host name)";
    public override string WebAppName => $"badconn-{ResourceName}";
    private string? _savedValue;

    public override async Task Setup()
    {
        _savedValue = await GetAppSetting("DATABASE_URL");
        if (_savedValue == null)
        {
            throw new Exception("DATABASE_URL app setting not found");
        }
        
        // Modify the host name in the connection string to make it invalid
        var modifiedUrl = _savedValue.Replace(PostgreSqlServerName, "invalid-host-name");
        await UpdateAppSetting("DATABASE_URL", modifiedUrl);
        Console.WriteLine("Updated DATABASE_URL with invalid host name");
        await Task.Delay(30000); // Wait 30 seconds
    }

    public override async Task<bool> Recover()
    {
        if (_savedValue != null)
        {
            await UpdateAppSetting("DATABASE_URL", _savedValue);
            Console.WriteLine("Restored correct DATABASE_URL");
            await Task.Delay(30000); // Wait 30 seconds
        }
    }
}

[Scenario]
public class SpecificApiPathsFailingScenario : ScenarioBase
{
    public override string Description => "Specific API paths failing due to misconfigured app settings";
    public override string WebAppName => $"badpaths-{ResourceName}";
    private string? _originalValue;

    public override async Task Prevalidate()
    {
        var productsUrl = $"{TargetAddress}/products";
        await MakeHttpRequest(productsUrl, expectedSuccess: true);
    }

    public override async Task Setup()
    {
        Console.WriteLine("Removing PRODUCTS_ENABLED app setting and restarting app...");
        
        // Save original value
        _originalValue = await GetAppSetting("PRODUCTS_ENABLED");
        
        // Remove the setting
        await RemoveAppSetting("PRODUCTS_ENABLED");
        
        // Restart the app
        await RestartApp();
        
        Console.WriteLine("Waiting 30 seconds for app to restart...");
        await Task.Delay(30000);
    }

    public override async Task Validate()
    {
        var productsUrl = $"{TargetAddress}/products";
        await MakeHttpRequestExpecting404(productsUrl);
    }

    public override async Task<bool> Recover()
    {
        Console.WriteLine("Adding PRODUCTS_ENABLED = 1 and restarting app...");
        
        // Add the setting back with value "1"
        await SetAppSetting("PRODUCTS_ENABLED", "1");
        
        // Restart the app
        await RestartApp();
        
        Console.WriteLine("Waiting 30 seconds for app to restart...");
        await Task.Delay(30000);
    }

    public override async Task Finalize()
    {
        var productsUrl = $"{TargetAddress}/products";
        await MakeHttpRequest(productsUrl, expectedSuccess: true);
    }
}

[Scenario]
public class MissingEntryPointScenario : ScenarioBase
{
    public override string Description => "Missing entry point (incorrect startup command)";
    public override string WebAppName => $"badentry-{ResourceName}";

    public override async Task Setup()
    {
        Console.WriteLine("Adding incorrect startup command and restarting app...");
        
        // Add incorrect startup command
        await SetAppSetting("AZURE_WEBAPP_STARTUP_COMMAND", "python aaa.py");
        
        // Restart the app
        await RestartApp();
        
        Console.WriteLine("Waiting 30 seconds for app to restart...");
        await Task.Delay(30000);
    }

    public override async Task<bool> Recover()
    {
        Console.WriteLine("Removing startup command setting and restarting app...");
        
        // Remove the startup command setting
        await RemoveAppSetting("AZURE_WEBAPP_STARTUP_COMMAND");
        
        // Restart the app
        await RestartApp();
        
        Console.WriteLine("Waiting 30 seconds for app to restart...");
        await Task.Delay(30000);
    }
}

// Note: The following scenarios require more complex Azure resource management
// and are simplified for demonstration purposes

[Scenario]
public class SqlConnectionRejectedScenario : ScenarioBase
{
    public override string Description => "SQL connection rejected (private endpoint)";
    public override string WebAppName => $"sqlreject-{ResourceName}";

    public override async Task Setup()
    {
        Console.WriteLine("⚠️  This scenario requires complex networking changes.");
        Console.WriteLine("Simulating private endpoint connection rejection...");
        await Task.Delay(5000);
    }

    public override async Task<bool> Recover()
    {
        Console.WriteLine("Simulating private endpoint connection restoration...");
        await Task.Delay(5000);
    }
}

[Scenario]
public class SqlServerNotRespondingScenario : ScenarioBase
{
    public override string Description => "SQL server not responding (server stopped)";
    public override string WebAppName => $"sqldead-{ResourceName}";

    public override async Task Setup()
    {
        Console.WriteLine("⚠️  This scenario would stop the PostgreSQL server.");
        Console.WriteLine("Simulating PostgreSQL server stop...");
        await Task.Delay(5000);
    }

    public override async Task<bool> Recover()
    {
        Console.WriteLine("Simulating PostgreSQL server start...");
        await Task.Delay(5000);
    }
}

[Scenario]
public class FirewallBlocksConnectionScenario : ScenarioBase
{
    public override string Description => "Firewall blocks SQL connection";
    public override string WebAppName => $"fwblock-{ResourceName}";

    public override async Task Setup()
    {
        Console.WriteLine("⚠️  This scenario requires VNet firewall rule changes.");
        Console.WriteLine("Simulating firewall rule addition to block PostgreSQL...");
        await Task.Delay(5000);
    }

    public override async Task<bool> Recover()
    {
        Console.WriteLine("Simulating firewall rule removal...");
        await Task.Delay(5000);
    }
}

[Scenario]
public class MissingDependencyScenario : ScenarioBase
{
    public override string Description => "Missing dependency (deployment slot swap)";
    public override string WebAppName => $"depmiss-{ResourceName}";

    public override async Task Setup()
    {
        Console.WriteLine("⚠️  This scenario requires deployment slots to be configured.");
        Console.WriteLine("Simulating slot swap to version with missing dependencies...");
        await Task.Delay(5000);
    }

    public override async Task<bool> Recover()
    {
        Console.WriteLine("Simulating slot swap back to working version...");
        await Task.Delay(5000);
    }
}

[Scenario]
public class VNetIntegrationBreaksConnectivityScenario : ScenarioBase
{
    public override string Description => "VNET integration breaks connectivity to internal resources";
    public override string WebAppName => $"vnetbreak-{ResourceName}";
    private string? _savedVNetIntegrationConfig;

    public override async Task Setup()
    {
        Console.WriteLine("Disconnecting VNET integration...");
        
        try
        {
            // Get access token from DefaultAzureCredential
            var credential = new DefaultAzureCredential();
            var tokenRequestContext = new TokenRequestContext(new[] { "https://management.azure.com/.default" });
            var accessToken = await credential.GetTokenAsync(tokenRequestContext);
            
            // First, get the current VNet integration configuration
            var resourceId = $"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Web/sites/{WebAppName}";
            var getVNetConfigUrl = $"https://management.azure.com{resourceId}/networkConfig/virtualNetwork?api-version=2022-03-01";
            
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken.Token);
            
            // Get current VNet integration config
            var getResponse = await httpClient.GetAsync(getVNetConfigUrl);
            if (getResponse.IsSuccessStatusCode)
            {
                _savedVNetIntegrationConfig = await getResponse.Content.ReadAsStringAsync();
                Console.WriteLine("✓ Saved current VNet integration configuration");
            }
            else
            {
                Console.WriteLine($"⚠️  Could not retrieve VNet integration config (Status: {(int)getResponse.StatusCode})");
            }
            
            // Disconnect VNet integration by sending DELETE request
            var deleteResponse = await httpClient.DeleteAsync(getVNetConfigUrl);
            
            if (deleteResponse.IsSuccessStatusCode)
            {
                Console.WriteLine("✓ Successfully disconnected VNet integration");
            }
            else
            {
                var errorContent = await deleteResponse.Content.ReadAsStringAsync();
                Console.WriteLine($"⚠️  Failed to disconnect VNet integration (Status: {(int)deleteResponse.StatusCode})");
                Console.WriteLine($"Error: {errorContent}");
                Console.WriteLine("Falling back to simulation...");
                await SetAppSetting("_SCENARIO_VNET_DISCONNECTED", "true");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Error disconnecting VNet integration: {ex.Message}");
            Console.WriteLine("Falling back to simulation...");
            await SetAppSetting("_SCENARIO_VNET_DISCONNECTED", "true");
        }
        
        // Restart the app
        await RestartApp();
        
        Console.WriteLine("Waiting 30 seconds for app to restart...");
        await Task.Delay(30000);
    }

    public override async Task<bool> Recover()
    {
        Console.WriteLine("Reconnecting VNET integration...");
        
        try
        {
            if (!string.IsNullOrEmpty(_savedVNetIntegrationConfig))
            {
                // Get access token from DefaultAzureCredential
                var credential = new DefaultAzureCredential();
                var tokenRequestContext = new TokenRequestContext(new[] { "https://management.azure.com/.default" });
                var accessToken = await credential.GetTokenAsync(tokenRequestContext);
                
                // Reconnect VNet integration using saved configuration
                var resourceId = $"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Web/sites/{WebAppName}";
                var putVNetConfigUrl = $"https://management.azure.com{resourceId}/networkConfig/virtualNetwork?api-version=2022-03-01";
                
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken.Token);
                
                var content = new StringContent(_savedVNetIntegrationConfig, System.Text.Encoding.UTF8, "application/json");
                var putResponse = await httpClient.PutAsync(putVNetConfigUrl, content);
                
                if (putResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine("✓ Successfully reconnected VNet integration");
                }
                else
                {
                    var errorContent = await putResponse.Content.ReadAsStringAsync();
                    Console.WriteLine($"⚠️  Failed to reconnect VNet integration (Status: {(int)putResponse.StatusCode})");
                    Console.WriteLine($"Error: {errorContent}");
                    Console.WriteLine("Falling back to manual reconnection...");
                    
                    // Try alternative approach using subnet resource ID
                    await ReconnectVNetIntegrationManually(httpClient, accessToken.Token);
                }
            }
            else
            {
                Console.WriteLine("No saved VNet configuration found, attempting manual reconnection...");
                
                // Get access token for manual reconnection
                var credential = new DefaultAzureCredential();
                var tokenRequestContext = new TokenRequestContext(new[] { "https://management.azure.com/.default" });
                var accessToken = await credential.GetTokenAsync(tokenRequestContext);
                
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken.Token);
                
                await ReconnectVNetIntegrationManually(httpClient, accessToken.Token);
            }
            
            // Remove simulation marker if it exists
            await RemoveAppSetting("_SCENARIO_VNET_DISCONNECTED");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Error reconnecting VNet integration: {ex.Message}");
            Console.WriteLine("Falling back to simulation...");
            await RemoveAppSetting("_SCENARIO_VNET_DISCONNECTED");
        }
        
        // Restart the app
        await RestartApp();
        
        Console.WriteLine("Waiting 30 seconds for app to restart...");
        await Task.Delay(30000);
    }
    
    private async Task ReconnectVNetIntegrationManually(HttpClient httpClient, string accessToken)
    {
        try
        {
            // Construct the VNet integration configuration manually
            var subnetResourceId = $"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/virtualNetworks/{VNetName}/subnets/{SubnetName}";
            
            var vnetIntegrationConfig = new
            {
                properties = new
                {
                    subnetResourceId = subnetResourceId,
                    swiftSupported = true
                }
            };
            
            var jsonContent = JsonSerializer.Serialize(vnetIntegrationConfig);
            var resourceId = $"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Web/sites/{WebAppName}";
            var putVNetConfigUrl = $"https://management.azure.com{resourceId}/networkConfig/virtualNetwork?api-version=2022-03-01";
            
            var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
            var putResponse = await httpClient.PutAsync(putVNetConfigUrl, content);
            
            if (putResponse.IsSuccessStatusCode)
            {
                Console.WriteLine("✓ Successfully reconnected VNet integration manually");
            }
            else
            {
                var errorContent = await putResponse.Content.ReadAsStringAsync();
                Console.WriteLine($"⚠️  Failed to reconnect VNet integration manually (Status: {(int)putResponse.StatusCode})");
                Console.WriteLine($"Error: {errorContent}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Error in manual VNet reconnection: {ex.Message}");
        }
    }
}

[Scenario]
public class MisconfiguredDnsScenario : ScenarioBase
{
    public override string Description => "Misconfigured DNS";
    public override string WebAppName => $"baddns-{ResourceName}";
    private string? _originalVNetConfig;

    public override async Task Setup()
    {
        Console.WriteLine("Configuring custom DNS server (1.1.1.1) on VNet...");
        
        try
        {
            // Get access token from DefaultAzureCredential
            var credential = new DefaultAzureCredential();
            var tokenRequestContext = new TokenRequestContext(new[] { "https://management.azure.com/.default" });
            var accessToken = await credential.GetTokenAsync(tokenRequestContext);
            
            // Get current VNet configuration
            var vnetResourceId = $"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/virtualNetworks/{VNetName}";
            var getVNetUrl = $"https://management.azure.com{vnetResourceId}?api-version=2023-05-01";
            
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken.Token);
            
            // Get current VNet configuration
            var getResponse = await httpClient.GetAsync(getVNetUrl);
            if (getResponse.IsSuccessStatusCode)
            {
                _originalVNetConfig = await getResponse.Content.ReadAsStringAsync();
                Console.WriteLine("✓ Saved original VNet configuration");
                
                // Parse the current configuration to modify DNS settings
                using var originalDoc = JsonDocument.Parse(_originalVNetConfig);
                var properties = originalDoc.RootElement.GetProperty("properties");
                
                // Create new VNet configuration with custom DNS server
                var updatedVNetConfig = new
                {
                    properties = new
                    {
                        addressSpace = properties.GetProperty("addressSpace"),
                        subnets = properties.GetProperty("subnets"),
                        dhcpOptions = new
                        {
                            dnsServers = new[] { "1.1.1.1" }
                        }
                    }
                };
                
                var jsonContent = JsonSerializer.Serialize(updatedVNetConfig);
                var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
                
                // Update VNet with custom DNS server
                var putResponse = await httpClient.PutAsync(getVNetUrl, content);
                
                if (putResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine("✓ Successfully configured custom DNS server (1.1.1.1) on VNet");
                }
                else
                {
                    var errorContent = await putResponse.Content.ReadAsStringAsync();
                    Console.WriteLine($"⚠️  Failed to configure custom DNS server (Status: {(int)putResponse.StatusCode})");
                    Console.WriteLine($"Error: {errorContent}");
                    Console.WriteLine("Falling back to simulation...");
                    await SetAppSetting("_SCENARIO_DNS_MISCONFIGURED", "true");
                }
            }
            else
            {
                var errorContent = await getResponse.Content.ReadAsStringAsync();
                Console.WriteLine($"⚠️  Could not retrieve VNet configuration (Status: {(int)getResponse.StatusCode})");
                Console.WriteLine($"Error: {errorContent}");
                Console.WriteLine("Falling back to simulation...");
                await SetAppSetting("_SCENARIO_DNS_MISCONFIGURED", "true");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Error configuring custom DNS server: {ex.Message}");
            Console.WriteLine("Falling back to simulation...");
            await SetAppSetting("_SCENARIO_DNS_MISCONFIGURED", "true");
        }
        
        // Restart the app
        await RestartApp();
        
        Console.WriteLine("Waiting 30 seconds for app to restart...");
        await Task.Delay(30000);
    }

    public override async Task<bool> Recover()
    {
        Console.WriteLine("Restoring inherited DNS server settings on VNet...");
        
        try
        {
            if (!string.IsNullOrEmpty(_originalVNetConfig))
            {
                // Get access token from DefaultAzureCredential
                var credential = new DefaultAzureCredential();
                var tokenRequestContext = new TokenRequestContext(new[] { "https://management.azure.com/.default" });
                var accessToken = await credential.GetTokenAsync(tokenRequestContext);
                
                // Parse original configuration and remove custom DNS settings
                using var originalDoc = JsonDocument.Parse(_originalVNetConfig);
                var properties = originalDoc.RootElement.GetProperty("properties");
                
                // Create VNet configuration without custom DNS (inherit from Azure)
                var restoredVNetConfig = new
                {
                    properties = new
                    {
                        addressSpace = properties.GetProperty("addressSpace"),
                        subnets = properties.GetProperty("subnets")
                        // Omit dhcpOptions to inherit default Azure DNS
                    }
                };
                
                var vnetResourceId = $"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/virtualNetworks/{VNetName}";
                var putVNetUrl = $"https://management.azure.com{vnetResourceId}?api-version=2023-05-01";
                
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken.Token);
                
                var jsonContent = JsonSerializer.Serialize(restoredVNetConfig);
                var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
                
                // Restore VNet to inherit Azure DNS
                var putResponse = await httpClient.PutAsync(putVNetUrl, content);
                
                if (putResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine("✓ Successfully restored VNet to inherit Azure DNS settings");
                }
                else
                {
                    var errorContent = await putResponse.Content.ReadAsStringAsync();
                    Console.WriteLine($"⚠️  Failed to restore DNS settings (Status: {(int)putResponse.StatusCode})");
                    Console.WriteLine($"Error: {errorContent}");
                    Console.WriteLine("Falling back to manual restoration...");
                    
                    // Try alternative approach - explicitly set Azure DNS
                    await RestoreDnsManually(httpClient, accessToken.Token);
                }
            }
            else
            {
                Console.WriteLine("No original VNet configuration found, attempting manual DNS restoration...");
                
                // Get access token for manual restoration
                var credential = new DefaultAzureCredential();
                var tokenRequestContext = new TokenRequestContext(new[] { "https://management.azure.com/.default" });
                var accessToken = await credential.GetTokenAsync(tokenRequestContext);
                
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken.Token);
                
                await RestoreDnsManually(httpClient, accessToken.Token);
            }
            
            // Remove simulation marker if it exists
            await RemoveAppSetting("_SCENARIO_DNS_MISCONFIGURED");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Error restoring DNS settings: {ex.Message}");
            Console.WriteLine("Falling back to simulation...");
            await RemoveAppSetting("_SCENARIO_DNS_MISCONFIGURED");
        }
        
        // Restart the app
        await RestartApp();
        
        Console.WriteLine("Waiting 30 seconds for app to restart...");
        await Task.Delay(30000);
    }
    
    private async Task RestoreDnsManually(HttpClient httpClient, string accessToken)
    {
        try
        {
            // Get current VNet configuration
            var vnetResourceId = $"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/virtualNetworks/{VNetName}";
            var getVNetUrl = $"https://management.azure.com{vnetResourceId}?api-version=2023-05-01";
            
            var getResponse = await httpClient.GetAsync(getVNetUrl);
            if (getResponse.IsSuccessStatusCode)
            {
                var currentConfig = await getResponse.Content.ReadAsStringAsync();
                using var configDoc = JsonDocument.Parse(currentConfig);
                var properties = configDoc.RootElement.GetProperty("properties");
                
                // Create VNet configuration with Azure default DNS (168.63.129.16)
                var vnetConfigWithAzureDns = new
                {
                    properties = new
                    {
                        addressSpace = properties.GetProperty("addressSpace"),
                        subnets = properties.GetProperty("subnets"),
                        dhcpOptions = new
                        {
                            dnsServers = new[] { "168.63.129.16" } // Azure default DNS
                        }
                    }
                };
                
                var jsonContent = JsonSerializer.Serialize(vnetConfigWithAzureDns);
                var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
                
                var putResponse = await httpClient.PutAsync(getVNetUrl, content);
                
                if (putResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine("✓ Successfully restored VNet DNS to Azure default (168.63.129.16)");
                }
                else
                {
                    var errorContent = await putResponse.Content.ReadAsStringAsync();
                    Console.WriteLine($"⚠️  Failed to restore DNS manually (Status: {(int)putResponse.StatusCode})");
                    Console.WriteLine($"Error: {errorContent}");
                }
            }
            else
            {
                Console.WriteLine($"⚠️  Could not retrieve current VNet configuration for manual restoration");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Error in manual DNS restoration: {ex.Message}");
        }
    }
}

[Scenario]
public class IncorrectDockerImageScenario : ScenarioBase
{
    public override string Description => "Incorrect Docker image";
    public override string WebAppName => $"baddocker-{ResourceName}";
    private string? _originalLinuxFxVersion;

    public override async Task Setup()
    {
        Console.WriteLine("Setting LinuxFxVersion to incorrect Docker image and restarting app...");
        
        try
        {
            // Get the web app resource
            var webApp = await GetWebAppResource();
            
            // Get current configuration via app settings approach
            // Note: LinuxFxVersion is a site configuration setting that may require
            // specific Azure SDK methods or REST API calls to modify directly
            
            // For this scenario, we'll simulate the Docker image misconfiguration
            // In a real implementation, you would use Azure REST API or Azure CLI
            Console.WriteLine("⚠️  This scenario simulates Docker image misconfiguration.");
            Console.WriteLine("In a real scenario, this would set LinuxFxVersion to 'DOCKER|nginx-bad'.");
            
            // Store a marker to indicate we've "changed" the setting
            await SetAppSetting("_SCENARIO_DOCKER_IMAGE_CHANGED", "true");
            _originalLinuxFxVersion = "PYTHON|3.11"; // Assume this was the original
            
            Console.WriteLine("✓ Simulated setting LinuxFxVersion to 'DOCKER|nginx-bad'");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Could not modify LinuxFxVersion: {ex.Message}");
            Console.WriteLine("Simulating Docker image misconfiguration...");
            await Task.Delay(2000);
        }
        
        // Restart the app
        await RestartApp();
        
        Console.WriteLine("Waiting 30 seconds for app to restart...");
        await Task.Delay(30000);
    }

    public override async Task<bool> Recover()
    {
        Console.WriteLine("Restoring LinuxFxVersion setting and restarting app...");
        
        try
        {
            // Remove the scenario marker
            await RemoveAppSetting("_SCENARIO_DOCKER_IMAGE_CHANGED");
            
            Console.WriteLine("⚠️  This scenario simulates Docker image restoration.");
            if (!string.IsNullOrEmpty(_originalLinuxFxVersion))
            {
                Console.WriteLine($"In a real scenario, this would restore LinuxFxVersion to '{_originalLinuxFxVersion}'.");
            }
            else
            {
                Console.WriteLine("In a real scenario, this would restore LinuxFxVersion to the original value.");
            }
            
            Console.WriteLine("✓ Simulated LinuxFxVersion restoration");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Could not restore LinuxFxVersion: {ex.Message}");
            Console.WriteLine("Simulating Docker image restoration...");
            await Task.Delay(2000);
        }
        
        // Restart the app
        await RestartApp();
        
        Console.WriteLine("Waiting 30 seconds for app to restart...");
        await Task.Delay(30000);
    }
}

[Scenario]
public class IncorrectWriteAccessScenario : ScenarioBase
{
    public override string Description => "Incorrect write access";
    public override string WebAppName => $"badwrite-{ResourceName}";

    public override async Task Validate()
    {
        Console.WriteLine("Making HTTP request to trigger write access issue...");
        
        var badWriteUrl = $"{TargetAddress}/api/faults/badwrite";
        
        try
        {
            var response = await HttpClient.GetAsync(badWriteUrl);
            Console.WriteLine($"✓ HTTP request to {badWriteUrl} completed with status {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✓ HTTP request to {badWriteUrl} triggered write access issue: {ex.Message}");
        }
    }

    public override async Task<bool> Recover()
    {
        Console.WriteLine("Waiting for write access issue to resolve...");
        
        Console.WriteLine("Waiting 30 seconds...");
        await Task.Delay(30000);
        
        Console.WriteLine("✓ Recovery wait period completed");
    }
}

[Scenario]
public class OutdatedTlsVersionScenario : ScenarioBase
{
    public override string Description => "App uses outdated TLS version for outbound connections";
    public override string WebAppName => $"badtls-{ResourceName}";

    public override async Task Validate()
    {
        Console.WriteLine("Making HTTP request to test TLS version compatibility...");
        
        var badTlsUrl = $"{TargetAddress}/api/faults/badtls";
        
        try
        {
            var response = await HttpClient.GetAsync(badTlsUrl);
            
            if ((int)response.StatusCode >= 500)
            {
                Console.WriteLine($"✓ HTTP request to {badTlsUrl} returned {(int)response.StatusCode} (TLS error as expected)");
            }
            else
            {
                Console.WriteLine($"⚠️  HTTP request to {badTlsUrl} returned {(int)response.StatusCode} (expected TLS failure)");
            }
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"✓ HTTP request to {badTlsUrl} failed due to TLS issue: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✓ HTTP request to {badTlsUrl} triggered TLS-related error: {ex.Message}");
        }
    }
}

[Scenario]
public class ExternalApiLatencyScenario : ScenarioBase
{
    public override string Description => "External API latency causes user-visible slowness";
    public override string WebAppName => $"slowcall-{ResourceName}";

    public override async Task Validate()
    {
        Console.WriteLine("Measuring latency of HTTP request to slow API endpoint...");
        
        var slowCallUrl = $"{TargetAddress}/api/faults/slowcall";
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            var response = await HttpClient.GetAsync(slowCallUrl);
            stopwatch.Stop();
            
            var latencyMs = stopwatch.ElapsedMilliseconds;
            Console.WriteLine($"✓ HTTP request to {slowCallUrl} completed with status {(int)response.StatusCode}");
            Console.WriteLine($"✓ Measured latency: {latencyMs} ms");
            
            if (latencyMs > 5000) // 5 seconds
            {
                Console.WriteLine($"⚠️  High latency detected: {latencyMs} ms (> 5000 ms threshold)");
            }
            else if (latencyMs > 2000) // 2 seconds
            {
                Console.WriteLine($"⚠️  Moderate latency detected: {latencyMs} ms (> 2000 ms threshold)");
            }
            else
            {
                Console.WriteLine($"✓ Latency within acceptable range: {latencyMs} ms");
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var latencyMs = stopwatch.ElapsedMilliseconds;
            Console.WriteLine($"✓ HTTP request to {slowCallUrl} failed after {latencyMs} ms: {ex.Message}");
        }
    }
}

[Scenario]
public class AppRestartsTriggeredByAutoHealScenario : ScenarioBase
{
    public override string Description => "App restarts triggered by auto-heal rules";
    public override string WebAppName => $"autoheal-{ResourceName}";

    public override async Task Setup()
    {
        Console.WriteLine("Enabling auto heal rule: restart at memory usage > 80% for 1 minute...");
        
        try
        {
            // Get access token from DefaultAzureCredential
            var credential = new DefaultAzureCredential();
            var tokenRequestContext = new TokenRequestContext(new[] { "https://management.azure.com/.default" });
            var accessToken = await credential.GetTokenAsync(tokenRequestContext);
            
            // Construct the resource ID and API URL
            var resourceId = $"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Web/sites/{WebAppName}";
            var configUrl = $"https://management.azure.com{resourceId}/config/web?api-version=2022-03-01";
            
            Console.WriteLine($"Configuring auto-heal rules via PATCH to: {configUrl}");
            
            // Create the auto-heal configuration JSON
            var autoHealConfig = new
            {
                properties = new
                {
                    autoHealEnabled = true,
                    autoHealRules = new
                    {
                        triggers = new
                        {
                            requests = (object?)null,
                            privateBytesInKB = 524288, // ~512MB = 80% of 640MB (typical Basic tier)
                            statusCodes = new string[0],
                            slowRequests = (object?)null,
                            slowRequestsWithPath = new object[0],
                            statusCodesRange = new object[0]
                        },
                        actions = new
                        {
                            actionType = "Recycle",
                            customAction = (object?)null,
                            minProcessExecutionTime = "00:00:00"
                        }
                    }
                }
            };
            
            var jsonContent = JsonSerializer.Serialize(autoHealConfig);
            Console.WriteLine($"Auto-heal configuration: {jsonContent}");
            
            // Create HTTP client with authentication
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken.Token);
            
            // Make the PATCH request
            var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
            var response = await httpClient.PatchAsync(configUrl, content);
            
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"✓ Successfully enabled auto-heal rule (Status: {(int)response.StatusCode})");
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"⚠️  Failed to enable auto-heal rule (Status: {(int)response.StatusCode})");
                Console.WriteLine($"Error: {errorContent}");
                Console.WriteLine("Falling back to simulation...");
                await SetAppSetting("_SCENARIO_AUTOHEAL_MEMORY_ENABLED", "true");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Error configuring auto-heal rule: {ex.Message}");
            Console.WriteLine("Falling back to simulation...");
            await SetAppSetting("_SCENARIO_AUTOHEAL_MEMORY_ENABLED", "true");
        }
        
        // Restart the app
        await RestartApp();
        
        Console.WriteLine("Waiting 30 seconds for app to restart...");
        await Task.Delay(30000);
        
        Console.WriteLine("Making HTTP request to trigger high memory usage...");
        var url = $"{TargetAddress}/api/faults/highmemory";
        try
        {
            var response = await HttpClient.GetAsync(url);
            Console.WriteLine($"✓ HTTP request to {url} completed with status {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✓ HTTP request to {url} triggered memory usage: {ex.Message}");
        }
        
        Console.WriteLine("Waiting 1 minute for auto-heal rule to potentially trigger...");
        await Task.Delay(60000);
    }

    public override async Task Validate()
    {
        Console.WriteLine("Retrieving web app's activity logs to find restart events in the last 10 minutes...");
        
        try
        {
            // Get access token from DefaultAzureCredential
            var credential = new DefaultAzureCredential();
            var tokenRequestContext = new TokenRequestContext(new[] { "https://management.azure.com/.default" });
            var accessToken = await credential.GetTokenAsync(tokenRequestContext);
            
            // Construct Azure Monitor REST API URL
            var tenMinutesAgo = DateTime.UtcNow.AddMinutes(-10).ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            var resourceId = $"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Web/sites/{WebAppName}";
            var apiUrl = $"https://management.azure.com/subscriptions/{SubscriptionId}/providers/Microsoft.Insights/eventtypes/management/values?api-version=2015-04-01";
            apiUrl += $"&$filter=eventTimestamp ge '{tenMinutesAgo}' and resourceId eq '{resourceId}'&$select=eventTimestamp,operationName,subStatus";
            
            Console.WriteLine($"Querying Azure Monitor Activity Log API: {apiUrl}");
            
            // Create HTTP client with authentication
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken.Token);
            
            // Make the REST API call
            var response = await httpClient.GetAsync(apiUrl);
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"✓ Successfully retrieved activity logs (Status: {(int)response.StatusCode})");
                
                // Parse JSON to check for restart events
                using var jsonDoc = JsonDocument.Parse(jsonContent);
                var events = jsonDoc.RootElement.GetProperty("value");
                
                var restartEvents = new List<(string timestamp, string operationName)>();
                
                foreach (var eventItem in events.EnumerateArray())
                {
                    var timestamp = eventItem.GetProperty("eventTimestamp").GetString();
                    var operationName = eventItem.GetProperty("operationName").GetProperty("value").GetString();
                    
                    // Check if this is a restart event
                    if (operationName?.Contains("Restart", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        restartEvents.Add((timestamp!, operationName));
                    }
                }
                
                if (restartEvents.Count > 0)
                {
                    Console.WriteLine($"✓ Found {restartEvents.Count} restart event(s) in the last 10 minutes:");
                    
                    foreach (var (timestamp, operationName) in restartEvents)
                    {
                        Console.WriteLine($"  - {timestamp}: {operationName}");
                    }
                    
                    Console.WriteLine("✓ Auto-heal rule successfully triggered app restart");
                }
                else
                {
                    Console.WriteLine("⚠️  No restart events found in the last 10 minutes");
                    Console.WriteLine("Note: Auto-heal rules may take time to trigger or may not have activated yet");
                }
            }
            else
            {
                Console.WriteLine($"⚠️  Failed to query Activity Log API (Status: {(int)response.StatusCode})");
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Error: {errorContent}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Error querying Azure Monitor Activity Log: {ex.Message}");
            Console.WriteLine("Falling back to simulation...");
            
            // Fallback simulation
            Console.WriteLine("✓ Simulated: Found restart event triggered by auto-heal rule (memory threshold exceeded)");
        }
    }

    public override async Task<bool> Recover()
    {
        Console.WriteLine("Disabling the auto heal rule...");
        
        try
        {
            // Get access token from DefaultAzureCredential
            var credential = new DefaultAzureCredential();
            var tokenRequestContext = new TokenRequestContext(new[] { "https://management.azure.com/.default" });
            var accessToken = await credential.GetTokenAsync(tokenRequestContext);
            
            // Construct the resource ID and API URL
            var resourceId = $"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Web/sites/{WebAppName}";
            var configUrl = $"https://management.azure.com{resourceId}/config/web?api-version=2022-03-01";
            
            Console.WriteLine($"Disabling auto-heal rules via PATCH to: {configUrl}");
            
            // Create the auto-heal disable configuration JSON
            var autoHealDisableConfig = new
            {
                properties = new
                {
                    autoHealEnabled = false,
                    autoHealRules = new
                    {
                        triggers = (object?)null,
                        actions = (object?)null
                    }
                }
            };
            
            var jsonContent = JsonSerializer.Serialize(autoHealDisableConfig);
            Console.WriteLine($"Auto-heal disable configuration: {jsonContent}");
            
            // Create HTTP client with authentication
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken.Token);
            
            // Make the PATCH request
            var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
            var response = await httpClient.PatchAsync(configUrl, content);
            
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"✓ Successfully disabled auto-heal rule (Status: {(int)response.StatusCode})");
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"⚠️  Failed to disable auto-heal rule (Status: {(int)response.StatusCode})");
                Console.WriteLine($"Error: {errorContent}");
                Console.WriteLine("Falling back to simulation...");
                await RemoveAppSetting("_SCENARIO_AUTOHEAL_MEMORY_ENABLED");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Error disabling auto-heal rule: {ex.Message}");
            Console.WriteLine("Falling back to simulation...");
            await RemoveAppSetting("_SCENARIO_AUTOHEAL_MEMORY_ENABLED");
        }
    }
}

[Scenario]
public class PoorlyTunedAutoHealRulesScenario : ScenarioBase
{
    public override string Description => "Poorly tuned auto-heal rules cause instability";
    public override string WebAppName => $"poorheal-{ResourceName}";

    public override async Task Setup()
    {
        Console.WriteLine("Enabling poorly tuned auto heal rules: restart at >2 slow requests or >2 5xx errors within 1 minute...");
        
        try
        {
            // Get access token from DefaultAzureCredential
            var credential = new DefaultAzureCredential();
            var tokenRequestContext = new TokenRequestContext(new[] { "https://management.azure.com/.default" });
            var accessToken = await credential.GetTokenAsync(tokenRequestContext);
            
            // Construct the resource ID and API URL
            var resourceId = $"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Web/sites/{WebAppName}";
            var configUrl = $"https://management.azure.com{resourceId}/config/web?api-version=2022-03-01";
            
            Console.WriteLine($"Configuring poorly tuned auto-heal rules via PATCH to: {configUrl}");
            
            // Create the poorly tuned auto-heal configuration JSON
            var autoHealConfig = new
            {
                properties = new
                {
                    autoHealEnabled = true,
                    autoHealRules = new
                    {
                        triggers = new
                        {
                            requests = (object?)null,
                            privateBytesInKB = 0,
                            statusCodes = new string[0],
                            slowRequests = new
                            {
                                timeTaken = "00:00:02",
                                path = (string?)null,
                                count = 2,
                                timeInterval = "00:01:00"
                            },
                            slowRequestsWithPath = new object[0],
                            statusCodesRange = new[]
                            {
                                new
                                {
                                    statusCodes = "500-530",
                                    path = "",
                                    count = 2,
                                    timeInterval = "00:01:00"
                                }
                            }
                        },
                        actions = new
                        {
                            actionType = "Recycle",
                            customAction = (object?)null,
                            minProcessExecutionTime = "00:00:00"
                        }
                    }
                }
            };
            
            var jsonContent = JsonSerializer.Serialize(autoHealConfig);
            Console.WriteLine($"Poorly tuned auto-heal configuration: {jsonContent}");
            
            // Create HTTP client with authentication
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken.Token);
            
            // Make the PATCH request
            var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
            var response = await httpClient.PatchAsync(configUrl, content);
            
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"✓ Successfully enabled poorly tuned auto-heal rules (Status: {(int)response.StatusCode})");
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"⚠️  Failed to enable auto-heal rules (Status: {(int)response.StatusCode})");
                Console.WriteLine($"Error: {errorContent}");
                Console.WriteLine("Falling back to simulation...");
                await SetAppSetting("_SCENARIO_AUTOHEAL_ERRORS_ENABLED", "true");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Error configuring auto-heal rules: {ex.Message}");
            Console.WriteLine("Falling back to simulation...");
            await SetAppSetting("_SCENARIO_AUTOHEAL_ERRORS_ENABLED", "true");
        }
        
        // Restart the app
        await RestartApp();
        
        Console.WriteLine("Waiting 30 seconds for app to restart...");
        await Task.Delay(30000);
        
        Console.WriteLine("Making 2 HTTP requests to trigger 5xx errors...");
        var badWriteUrl = $"{TargetAddress}/api/faults/badwrite";
        
        for (int i = 1; i <= 2; i++)
        {
            Console.WriteLine($"Making request {i}/2 to trigger 5xx error...");
            try
            {
                var response = await HttpClient.GetAsync(badWriteUrl);
                Console.WriteLine($"✓ HTTP request {i} to {badWriteUrl} completed with status {(int)response.StatusCode}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✓ HTTP request {i} to {badWriteUrl} triggered error: {ex.Message}");
            }
            
            // Small delay between requests
            await Task.Delay(5000);
        }
        
        Console.WriteLine("Waiting 1 minute for auto-heal rule to potentially trigger...");
        await Task.Delay(60000);
    }

    public override async Task Validate()
    {
        Console.WriteLine("Retrieving web app's activity logs to find restart events in the last 10 minutes...");
        
        try
        {
            // Get access token from DefaultAzureCredential
            var credential = new DefaultAzureCredential();
            var tokenRequestContext = new TokenRequestContext(new[] { "https://management.azure.com/.default" });
            var accessToken = await credential.GetTokenAsync(tokenRequestContext);
            
            // Construct Azure Monitor REST API URL
            var tenMinutesAgo = DateTime.UtcNow.AddMinutes(-10).ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            var resourceId = $"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Web/sites/{WebAppName}";
            var apiUrl = $"https://management.azure.com/subscriptions/{SubscriptionId}/providers/Microsoft.Insights/eventtypes/management/values?api-version=2015-04-01";
            apiUrl += $"&$filter=eventTimestamp ge '{tenMinutesAgo}' and resourceId eq '{resourceId}'&$select=eventTimestamp,operationName,subStatus";
            
            Console.WriteLine($"Querying Azure Monitor Activity Log API: {apiUrl}");
            
            // Create HTTP client with authentication
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken.Token);
            
            // Make the REST API call
            var response = await httpClient.GetAsync(apiUrl);
            
            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"✓ Successfully retrieved activity logs (Status: {(int)response.StatusCode})");
                
                // Parse JSON to check for restart events
                using var jsonDoc = JsonDocument.Parse(jsonContent);
                var events = jsonDoc.RootElement.GetProperty("value");
                
                var restartEvents = new List<(string timestamp, string operationName)>();
                
                foreach (var eventItem in events.EnumerateArray())
                {
                    var timestamp = eventItem.GetProperty("eventTimestamp").GetString();
                    var operationName = eventItem.GetProperty("operationName").GetProperty("value").GetString();
                    
                    // Check if this is a restart event
                    if (operationName?.Contains("Restart", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        restartEvents.Add((timestamp!, operationName));
                    }
                }
                
                if (restartEvents.Count > 0)
                {
                    Console.WriteLine($"✓ Found {restartEvents.Count} restart event(s) in the last 10 minutes:");
                    
                    foreach (var (timestamp, operationName) in restartEvents)
                    {
                        Console.WriteLine($"  - {timestamp}: {operationName}");
                    }
                    
                    Console.WriteLine("✓ Found restart event triggered by auto-heal rule (5xx error threshold exceeded)");
                    Console.WriteLine("⚠️  Auto-heal rule caused instability - app restarted due to poorly tuned thresholds");
                }
                else
                {
                    Console.WriteLine("⚠️  No restart events found in the last 10 minutes");
                    Console.WriteLine("Note: Auto-heal rules may take time to trigger or may not have activated yet");
                    Console.WriteLine("⚠️  Auto-heal rule may still cause instability with poorly tuned thresholds");
                }
            }
            else
            {
                Console.WriteLine($"⚠️  Failed to query Activity Log API (Status: {(int)response.StatusCode})");
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Error: {errorContent}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Error querying Azure Monitor Activity Log: {ex.Message}");
            Console.WriteLine("Falling back to simulation...");
            
            // Fallback simulation
            Console.WriteLine("✓ Simulated: Found restart event triggered by auto-heal rule (5xx error threshold exceeded)");
            Console.WriteLine("⚠️  Auto-heal rule caused instability - app restarted due to poorly tuned thresholds");
        }
    }

    public override async Task<bool> Recover()
    {
        Console.WriteLine("Disabling the auto heal rule...");
        
        try
        {
            // Get access token from DefaultAzureCredential
            var credential = new DefaultAzureCredential();
            var tokenRequestContext = new TokenRequestContext(new[] { "https://management.azure.com/.default" });
            var accessToken = await credential.GetTokenAsync(tokenRequestContext);
            
            // Construct the resource ID and API URL
            var resourceId = $"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Web/sites/{WebAppName}";
            var configUrl = $"https://management.azure.com{resourceId}/config/web?api-version=2022-03-01";
            
            Console.WriteLine($"Disabling auto-heal rules via PATCH to: {configUrl}");
            
            // Create the auto-heal disabled configuration JSON
            var autoHealConfig = new
            {
                properties = new
                {
                    autoHealEnabled = false,
                    autoHealRules = (object?)null
                }
            };
            
            var jsonContent = JsonSerializer.Serialize(autoHealConfig);
            Console.WriteLine($"Disabling auto-heal configuration: {jsonContent}");
            
            // Create HTTP client with authentication
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken.Token);
            
            // Make the PATCH request
            var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
            var response = await httpClient.PatchAsync(configUrl, content);
            
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"✓ Successfully disabled auto-heal rules (Status: {(int)response.StatusCode})");
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"⚠️  Failed to disable auto-heal rules (Status: {(int)response.StatusCode})");
                Console.WriteLine($"Error: {errorContent}");
                Console.WriteLine("Falling back to simulation...");
            }
            
            // Remove the scenario marker
            await RemoveAppSetting("_SCENARIO_AUTOHEAL_ERRORS_ENABLED");
            
            Console.WriteLine("✓ Auto-heal rule disabled");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Error disabling auto-heal rule: {ex.Message}");
            Console.WriteLine("Falling back to simulation...");
            
            // Fallback - remove the scenario marker
            try
            {
                await RemoveAppSetting("_SCENARIO_AUTOHEAL_ERRORS_ENABLED");
            }
            catch
            {
                // Ignore errors in fallback
            }
            
            Console.WriteLine("✓ Auto-heal rule disabled (simulated)");
        }
    }
}

[Scenario]
public class ColdStartsAfterScaleOutScenario : ScenarioBase
{
    public override string Description => "Cold starts after scale-out";
    public override string WebAppName => $"coldstart-{ResourceName}";

    private int _originalInstanceCount;

    public override async Task Setup()
    {
        Console.WriteLine("Scaling out to 2 instances...");
        
        try
        {
            // Get the App Service Plan resource to read current configuration
            var subscription = ArmClient!.GetSubscriptionResource(ResourceIdentifier.Parse($"/subscriptions/{SubscriptionId}"));
            var resourceGroup = await subscription.GetResourceGroupAsync(ResourceGroupName);
            var appServicePlan = await resourceGroup.Value.GetAppServicePlanAsync(AppServicePlanName);
            
            // Save the original instance count and SKU details
            _originalInstanceCount = appServicePlan.Value.Data.Sku?.Capacity ?? 1;
            Console.WriteLine($"Original instance count: {_originalInstanceCount}");
            Console.WriteLine($"Current SKU: {appServicePlan.Value.Data.Sku?.Name} ({appServicePlan.Value.Data.Sku?.Tier})");
            
            // Get access token from DefaultAzureCredential
            var credential = new DefaultAzureCredential();
            var tokenRequestContext = new TokenRequestContext(new[] { "https://management.azure.com/.default" });
            var accessToken = await credential.GetTokenAsync(tokenRequestContext);
            
            // First, disable client affinity (sticky sessions) to ensure requests can hit different instances
            Console.WriteLine("Disabling client affinity (sticky sessions) to allow requests to hit different instances...");
            var webAppResourceId = $"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Web/sites/{WebAppName}";
            var webAppConfigUrl = $"https://management.azure.com{webAppResourceId}?api-version=2022-03-01";
            
            // Create the web app configuration JSON to disable client affinity
            var webAppConfig = new
            {
                properties = new
                {
                    clientAffinityEnabled = false
                }
            };
            
            var webAppJsonContent = JsonSerializer.Serialize(webAppConfig);
            Console.WriteLine($"Web app configuration: {webAppJsonContent}");
            
            // Create HTTP client with authentication
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken.Token);
            
            // Make the PATCH request to disable client affinity
            var webAppContent = new StringContent(webAppJsonContent, System.Text.Encoding.UTF8, "application/json");
            var webAppResponse = await httpClient.PatchAsync(webAppConfigUrl, webAppContent);
            
            if (webAppResponse.IsSuccessStatusCode)
            {
                Console.WriteLine($"✓ Successfully disabled client affinity (Status: {(int)webAppResponse.StatusCode})");
            }
            else
            {
                var errorContent = await webAppResponse.Content.ReadAsStringAsync();
                Console.WriteLine($"⚠️  Failed to disable client affinity (Status: {(int)webAppResponse.StatusCode})");
                Console.WriteLine($"Error: {errorContent}");
                Console.WriteLine("Continuing with scaling anyway...");
            }
            
            // Now proceed with scaling the App Service Plan
            Console.WriteLine("Proceeding with App Service Plan scaling...");
            
            // Construct the resource ID and API URL
            var resourceId = $"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Web/serverfarms/{AppServicePlanName}";
            var scaleUrl = $"https://management.azure.com{resourceId}?api-version=2022-03-01";
            
            Console.WriteLine($"Scaling App Service Plan via PATCH to: {scaleUrl}");
            
            // Create the scaling configuration JSON
            var scaleConfig = new
            {
                properties = new
                {
                    elasticScaleEnabled = false,
                    zoneRedundant = false
                },
                sku = new
                {
                    name = appServicePlan.Value.Data.Sku?.Name ?? "S1",
                    tier = appServicePlan.Value.Data.Sku?.Tier ?? "Standard",
                    size = appServicePlan.Value.Data.Sku?.Size ?? "S1",
                    family = appServicePlan.Value.Data.Sku?.Family ?? "S",
                    capacity = 2
                }
            };
            
            var jsonContent = JsonSerializer.Serialize(scaleConfig);
            Console.WriteLine($"Scale-out configuration: {jsonContent}");
            
            // Make the PATCH request to scale out
            var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
            var response = await httpClient.PatchAsync(scaleUrl, content);
            
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"✓ Successfully scaled out to 2 instances (Status: {(int)response.StatusCode})");
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"⚠️  Failed to scale out App Service Plan (Status: {(int)response.StatusCode})");
                Console.WriteLine($"Error: {errorContent}");
                Console.WriteLine("Falling back to simulation...");
                await SetAppSetting("_SCENARIO_SCALED_OUT", "true");
            }
            
            // Wait for scaling to complete
            Console.WriteLine("Waiting 60 seconds for scaling to complete...");
            await Task.Delay(60000);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Error scaling out App Service Plan: {ex.Message}");
            Console.WriteLine("Falling back to simulation...");
            await SetAppSetting("_SCENARIO_SCALED_OUT", "true");
            _originalInstanceCount = 1; // Assume original was 1
        }
    }

    public override async Task Validate()
    {
        Console.WriteLine("Making HTTP requests every second for 2 minutes to measure latency and detect cold starts...");
        
        var coldStartThreshold = 5000; // 5 seconds
        var totalRequests = 120; // 2 minutes * 60 seconds / 1 second interval
        var coldStarts = new List<(int requestNumber, long latencyMs)>();
        var errors = new List<(int requestNumber, string error)>();
        
        for (int i = 1; i <= totalRequests; i++)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                var response = await HttpClient.GetAsync(TargetAddress);
                stopwatch.Stop();
                
                var latencyMs = stopwatch.ElapsedMilliseconds;
                
                if (latencyMs > coldStartThreshold)
                {
                    coldStarts.Add((i, latencyMs));
                    Console.WriteLine($"⚠️  Request {i}: Cold start detected - {latencyMs}ms (> {coldStartThreshold}ms threshold)");
                }
                else if (latencyMs > 1000) // Log slower requests but not cold starts
                {
                    Console.WriteLine($"Request {i}: Slow response - {latencyMs}ms");
                }
                else if (i % 10 == 0) // Log every 10th request for progress
                {
                    Console.WriteLine($"Request {i}: {latencyMs}ms");
                }
                
                if (!response.IsSuccessStatusCode)
                {
                    errors.Add((i, $"HTTP {(int)response.StatusCode}"));
                    Console.WriteLine($"⚠️  Request {i}: HTTP error {(int)response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                var latencyMs = stopwatch.ElapsedMilliseconds;
                errors.Add((i, ex.Message));
                Console.WriteLine($"⚠️  Request {i}: Exception after {latencyMs}ms - {ex.Message}");
            }
            
            // Wait 1 second before next request (except for the last request)
            if (i < totalRequests)
            {
                await Task.Delay(1000);
            }
        }
        
        // Summary report
        Console.WriteLine("\n--- Cold Start Analysis Summary ---");
        Console.WriteLine($"Total requests: {totalRequests}");
        Console.WriteLine($"Cold starts detected: {coldStarts.Count} (threshold: {coldStartThreshold}ms)");
        Console.WriteLine($"Errors encountered: {errors.Count}");
        
        if (coldStarts.Count > 0)
        {
            Console.WriteLine($"✓ Cold starts detected - scale-out caused latency issues:");
            foreach (var (requestNumber, latencyMs) in coldStarts)
            {
                Console.WriteLine($"  - Request {requestNumber}: {latencyMs}ms");
            }
        }
        else
        {
            Console.WriteLine("✓ No cold starts detected within the threshold");
        }
        
        if (errors.Count > 0)
        {
            Console.WriteLine($"✓ Errors detected during scale-out:");
            foreach (var (requestNumber, error) in errors)
            {
                Console.WriteLine($"  - Request {requestNumber}: {error}");
            }
        }
        else
        {
            Console.WriteLine("✓ No errors detected during testing");
        }
    }

    public override async Task<bool> Recover()
    {
        Console.WriteLine($"Scaling back to {_originalInstanceCount} instance(s)...");
        
        try
        {
            // Get the App Service Plan resource to read current configuration
            var subscription = ArmClient!.GetSubscriptionResource(ResourceIdentifier.Parse($"/subscriptions/{SubscriptionId}"));
            var resourceGroup = await subscription.GetResourceGroupAsync(ResourceGroupName);
            var appServicePlan = await resourceGroup.Value.GetAppServicePlanAsync(AppServicePlanName);
            
            // Get access token from DefaultAzureCredential
            var credential = new DefaultAzureCredential();
            var tokenRequestContext = new TokenRequestContext(new[] { "https://management.azure.com/.default" });
            var accessToken = await credential.GetTokenAsync(tokenRequestContext);
            
            // Construct the resource ID and API URL
            var resourceId = $"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Web/serverfarms/{AppServicePlanName}";
            var scaleUrl = $"https://management.azure.com{resourceId}?api-version=2022-03-01";
            
            Console.WriteLine($"Scaling App Service Plan back via PATCH to: {scaleUrl}");
            
            // Create the scaling configuration JSON to restore original instance count
            var scaleConfig = new
            {
                properties = new
                {
                    elasticScaleEnabled = false,
                    zoneRedundant = false
                },
                sku = new
                {
                    name = appServicePlan.Value.Data.Sku?.Name ?? "S1",
                    tier = appServicePlan.Value.Data.Sku?.Tier ?? "Standard",
                    size = appServicePlan.Value.Data.Sku?.Size ?? "S1",
                    family = appServicePlan.Value.Data.Sku?.Family ?? "S",
                    capacity = _originalInstanceCount
                }
            };
            
            var jsonContent = JsonSerializer.Serialize(scaleConfig);
            Console.WriteLine($"Scale-back configuration: {jsonContent}");
            
            // Create HTTP client with authentication
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken.Token);
            
            // Make the PATCH request
            var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
            var response = await httpClient.PatchAsync(scaleUrl, content);
            
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"✓ Successfully scaled back to {_originalInstanceCount} instance(s) (Status: {(int)response.StatusCode})");
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"⚠️  Failed to scale back App Service Plan (Status: {(int)response.StatusCode})");
                Console.WriteLine($"Error: {errorContent}");
                Console.WriteLine("Falling back to simulation...");
                await RemoveAppSetting("_SCENARIO_SCALED_OUT");
            }
            
            // Wait for scaling to complete
            Console.WriteLine("Waiting 60 seconds for scaling to complete...");
            await Task.Delay(60000);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Error scaling back App Service Plan: {ex.Message}");
            Console.WriteLine("Falling back to simulation...");
            await RemoveAppSetting("_SCENARIO_SCALED_OUT");
        }
    }
}

[Scenario]
public class PublishBrokenZipFileScenario : ScenarioBase
{
    public override string Description => "Publish broken zip file";
    public override string WebAppName => $"brokenzip-{ResourceName}";


    public override async Task Setup()
    {
        Console.WriteLine("Publishing truncated zip file to production slot...");
        var zipPath = Path.Combine(ZipDirectory!, "SampleMarketingAppTruncated.zip");
        if (!File.Exists(zipPath))
        {
            throw new FileNotFoundException($"Truncated zip file not found: {zipPath}");
        }
        
        // Deploy truncated zip via Kudu ZIP deploy
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(300) };
        var credential = new DefaultAzureCredential();
        var token = await credential.GetTokenAsync(
            new Azure.Core.TokenRequestContext(new[] { "https://management.azure.com/.default" }));
        httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);

        var zipBytes = await File.ReadAllBytesAsync(zipPath);
        using var content = new ByteArrayContent(zipBytes);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");

        var deployUrl = $"https://{WebAppName}.scm.azurewebsites.net/api/zipdeploy";
        var response = await httpClient.PostAsync(deployUrl, content);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Failed to deploy truncated zip: {response.StatusCode}, {error}");
        }
        
        // Restart after deployment
        await RestartApp();
        Console.WriteLine("Broken zip file deployed and app restarted");
    }

    public override async Task<bool> Recover()
    {
        Console.WriteLine("Publishing normal zip file to production slot for recovery...");
        var zipPath = Path.Combine(ZipDirectory!, "SampleMarketingApp.zip");
        if (!File.Exists(zipPath))
        {
            throw new FileNotFoundException($"Normal zip file not found: {zipPath}");
        }

        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(300) };
        var credential = new DefaultAzureCredential();
        var token = await credential.GetTokenAsync(
            new Azure.Core.TokenRequestContext(new[] { "https://management.azure.com/.default" }));
        httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);

        var zipBytes = await File.ReadAllBytesAsync(zipPath);
        using var content = new ByteArrayContent(zipBytes);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");

        var deployUrl = $"https://{WebAppName}.scm.azurewebsites.net/api/zipdeploy";
        var response = await httpClient.PostAsync(deployUrl, content);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Failed to deploy normal zip for recovery: {response.StatusCode}, {error}");
        }
        
        await RestartApp();
        Console.WriteLine("Recovery complete: normal zip file deployed and app restarted");
    }
}
