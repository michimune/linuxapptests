using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.AppService.Models;
using Azure.ResourceManager.Network;
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
        if (args.Length < 2)
        {
            Console.WriteLine("Error: Both subscription ID and resource name are required.");
            Console.WriteLine("Usage: dotnet run <subscription-id> <resource-name>");
            Console.WriteLine("Example: dotnet run 12345678-1234-1234-1234-123456789012 mymarketingapp");
            Environment.Exit(1);
        }

        _subscriptionId = args[0];
        _resourceName = args[1];

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
        scenario.Initialize(_armClient!, _resourceName!, _subscriptionId!);
        
        Console.WriteLine($"\nExecuting scenario: {scenario.Description}");
        Console.WriteLine(new string('-', 50));
        
        try
        {
            Console.WriteLine("Step 1: Prevalidate...");
            await scenario.Prevalidate();
            Console.WriteLine("✓ Prevalidation successful");
            
            Console.WriteLine("Step 2: Setup...");
            await scenario.Setup();
            Console.WriteLine("✓ Setup complete");
            
            Console.WriteLine("Step 3: Validate (expecting failure)...");
            await scenario.Validate();
            Console.WriteLine("✓ Validation complete (failure confirmed)");
            
            Console.WriteLine("Step 4: Recover...");
            await scenario.Recover();
            Console.WriteLine("✓ Recovery complete");
            
            Console.WriteLine("Step 5: Finalize...");
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
    protected HttpClient HttpClient { get; private set; } = new HttpClient { Timeout = TimeSpan.FromSeconds(300) };
    
    public abstract string Description { get; }
    public virtual string TargetAddress => $"https://webapp-{ResourceName}.azurewebsites.net";
    
    // Resource name properties
    protected string ResourceGroupName => $"rg-{ResourceName}";
    protected string VNetName => $"vnet-{ResourceName}";
    protected string SubnetName => "subnet-appservice";
    protected string PostgreSqlSubnetName => "subnet-postgresql";
    protected string AppServicePlanName => $"asp-{ResourceName}";
    protected string WebAppName => $"webapp-{ResourceName}";
    protected string PostgreSqlServerName => $"psql-{ResourceName}";
    protected string PrivateEndpointName => $"pe-postgresql-{ResourceName}";

    public void Initialize(ArmClient armClient, string resourceName, string subscriptionId)
    {
        ArmClient = armClient;
        ResourceName = resourceName;
        SubscriptionId = subscriptionId;
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

    public virtual async Task Recover()
    {
        // Default implementation - do nothing
        await Task.CompletedTask;
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
            }
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
}

// Scenario implementations

[Scenario]
public class MissingEnvironmentVariableScenario : ScenarioBase
{
    public override string Description => "Missing environment variable (SECRET_KEY)";
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

    public override async Task Recover()
    {
        if (_savedValue != null)
        {
            await UpdateAppSetting("SECRET_KEY", _savedValue);
            Console.WriteLine("Restored SECRET_KEY app setting");
            await Task.Delay(30000); // Wait 30 seconds
        }
    }
}

[Scenario]
public class MissingConnectionStringScenario : ScenarioBase
{
    public override string Description => "Missing connection string (DATABASE_URL)";
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

    public override async Task Recover()
    {
        if (_savedValue != null)
        {
            await UpdateAppSetting("DATABASE_URL", _savedValue);
            Console.WriteLine("Restored DATABASE_URL app setting");
            await Task.Delay(30000); // Wait 30 seconds
        }
    }
}

[Scenario]
public class HighMemoryScenario : ScenarioBase
{
    public override string Description => "High memory usage";

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

    public override async Task Recover()
    {
        Console.WriteLine("Waiting 60 seconds for memory to stabilize...");
        await Task.Delay(60000);
    }
}

[Scenario]
public class SnatPortExhaustionScenario : ScenarioBase
{
    public override string Description => "SNAT port exhaustion";

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

    public override async Task Recover()
    {
        Console.WriteLine("Waiting 60 seconds for SNAT ports to be released...");
        await Task.Delay(60000);
    }
}

[Scenario]
public class IncorrectStartupCommandScenario : ScenarioBase
{
    public override string Description => "Incorrect startup command";

    public override async Task Setup()
    {
        await UpdateAppSetting("AZURE_WEBAPP_STARTUP_COMMAND", "bogus_command_line");
        Console.WriteLine("Added incorrect startup command");
        await Task.Delay(30000); // Wait 30 seconds
    }

    public override async Task Recover()
    {
        await UpdateAppSetting("AZURE_WEBAPP_STARTUP_COMMAND", null);
        Console.WriteLine("Removed incorrect startup command");
        await Task.Delay(30000); // Wait 30 seconds
    }
}

[Scenario]
public class HighCpuScenario : ScenarioBase
{
    public override string Description => "High CPU usage";

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

    public override async Task Recover()
    {
        Console.WriteLine("Waiting 60 seconds for CPU to stabilize...");
        await Task.Delay(60000);
    }
}

[Scenario]
public class MisconfiguredConnectionStringScenario : ScenarioBase
{
    public override string Description => "Misconfigured connection string (wrong host name)";
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

    public override async Task Recover()
    {
        if (_savedValue != null)
        {
            await UpdateAppSetting("DATABASE_URL", _savedValue);
            Console.WriteLine("Restored correct DATABASE_URL");
            await Task.Delay(30000); // Wait 30 seconds
        }
    }
}

// Note: The following scenarios require more complex Azure resource management
// and are simplified for demonstration purposes

[Scenario]
public class SqlConnectionRejectedScenario : ScenarioBase
{
    public override string Description => "SQL connection rejected (private endpoint)";

    public override async Task Setup()
    {
        Console.WriteLine("⚠️  This scenario requires complex networking changes.");
        Console.WriteLine("Simulating private endpoint connection rejection...");
        await Task.Delay(5000);
    }

    public override async Task Recover()
    {
        Console.WriteLine("Simulating private endpoint connection restoration...");
        await Task.Delay(5000);
    }
}

[Scenario]
public class SqlServerNotRespondingScenario : ScenarioBase
{
    public override string Description => "SQL server not responding (server stopped)";

    public override async Task Setup()
    {
        Console.WriteLine("⚠️  This scenario would stop the PostgreSQL server.");
        Console.WriteLine("Simulating PostgreSQL server stop...");
        await Task.Delay(5000);
    }

    public override async Task Recover()
    {
        Console.WriteLine("Simulating PostgreSQL server start...");
        await Task.Delay(5000);
    }
}

[Scenario]
public class FirewallBlocksConnectionScenario : ScenarioBase
{
    public override string Description => "Firewall blocks SQL connection";

    public override async Task Setup()
    {
        Console.WriteLine("⚠️  This scenario requires VNet firewall rule changes.");
        Console.WriteLine("Simulating firewall rule addition to block PostgreSQL...");
        await Task.Delay(5000);
    }

    public override async Task Recover()
    {
        Console.WriteLine("Simulating firewall rule removal...");
        await Task.Delay(5000);
    }
}

[Scenario]
public class MissingDependencyScenario : ScenarioBase
{
    public override string Description => "Missing dependency (deployment slot swap)";

    public override async Task Setup()
    {
        Console.WriteLine("⚠️  This scenario requires deployment slots to be configured.");
        Console.WriteLine("Simulating slot swap to version with missing dependencies...");
        await Task.Delay(5000);
    }

    public override async Task Recover()
    {
        Console.WriteLine("Simulating slot swap back to working version...");
        await Task.Delay(5000);
    }
}
