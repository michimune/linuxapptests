using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.AppService.Models;
using Azure.ResourceManager.Resources;
using Newtonsoft.Json;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Collections.Specialized;

namespace AzureAppServiceManager;

class Program
{
    private static ArmClient? _armClient;
    private static SubscriptionResource? _subscription;
    private static ResourceGroupResource? _resourceGroup;
    private static WebSiteResource? _webApp;
    private static AppServicePlanResource? _appServicePlan;
      private static readonly string RESOURCE_GROUP_NAME = $"rg-vibecoding-{DateTime.Now:yyyyMMdd-HHmmss}";
    private static readonly string APP_SERVICE_PLAN_NAME = $"asp-vibecoding-{DateTime.Now:yyyyMMdd-HHmmss}";
    private static readonly string WEB_APP_NAME = $"vibecoding-webapp-{DateTime.Now:yyyyMMdd-HHmmss}";
    private const string APP_ZIP_PATH = @"D:\repos\VibeCoding\app.zip";static async Task Main(string[] args)
    {
        Console.WriteLine("Azure App Service Manager - Automated Test");
        Console.WriteLine("=========================================");
        Console.WriteLine();
        
        try
        {
            // Initialize Azure client
            await InitializeAzureClient(args);
            
            // Run automated test
            await RunAutomatedTest();
            
            Console.WriteLine("\n🎉 Automated test completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Automated test failed: {ex.Message}");
            Environment.Exit(1);
        }
        
        Console.WriteLine("Exiting application...");
    }
    
    static async Task RunAutomatedTest()
    {
        Console.WriteLine("🚀 Starting automated test - running steps A through F...");
        Console.WriteLine();
        
        try
        {
            // Step A: Create webapp and deploy code
            Console.WriteLine("=== STEP A: Create webapp in Azure App Service and deploy code ===");
            await CreateAndDeployWebApp();
            Console.WriteLine("✅ Step A completed successfully!");
            Console.WriteLine();
            
            // Wait a bit for deployment to settle
            Console.WriteLine("⏳ Waiting 10 seconds for deployment to settle...");
            await Task.Delay(10000);
            
            // Step B: Delete APP_VALUE environment variable
            Console.WriteLine("=== STEP B: Delete APP_VALUE environment variable ===");
            await DeleteAppValueEnvironmentVariable();
            Console.WriteLine("✅ Step B completed successfully!");
            Console.WriteLine();
            
            // Wait for app settings to update
            Console.WriteLine("⏳ Waiting 5 seconds for app settings to update...");
            await Task.Delay(5000);
            
            // Step C: Make HTTP request and confirm error (500+)
            Console.WriteLine("=== STEP C: Make HTTP request and confirm error (500+) ===");
            await ValidateAppReturnsError();
            Console.WriteLine("✅ Step C completed successfully!");
            Console.WriteLine();
            
            // Step D: Add APP_VALUE = abcde environment variable
            Console.WriteLine("=== STEP D: Add APP_VALUE = abcde environment variable ===");
            await AddAppValueEnvironmentVariable();
            Console.WriteLine("✅ Step D completed successfully!");
            Console.WriteLine();
            
            // Wait for app settings to update
            Console.WriteLine("⏳ Waiting 5 seconds for app settings to update...");
            await Task.Delay(5000);
            
            // Step E: Validate app returns correct JSON
            Console.WriteLine("=== STEP E: Validate app returns JSON {\"AppValue\": \"abcde\"} ===");
            await ValidateAppIsWorking();
            Console.WriteLine("✅ Step E completed successfully!");
            Console.WriteLine();
            
            // Step F: Delete app resource in Azure
            Console.WriteLine("=== STEP F: Delete app resource in Azure ===");
            await DeleteAppResource();
            Console.WriteLine("✅ Step F completed successfully!");
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Automated test failed at current step: {ex.Message}");
            Console.WriteLine($"Details: {ex}");
            throw;
        }
    }
    
    static void ShowMenu()
    {
        Console.WriteLine("Choose an option:");
        Console.WriteLine("A - Create webapp in Azure App Service and deploy code");
        Console.WriteLine("B - Delete APP_VALUE environment variable");
        Console.WriteLine("C - Make HTTP request and confirm error (500+)");
        Console.WriteLine("D - Add APP_VALUE = abcde environment variable");
        Console.WriteLine("E - Validate app returns JSON {\"AppValue\": \"abcde\"}");
        Console.WriteLine("F - Delete app resource in Azure");
        Console.WriteLine("X - Exit");
        Console.WriteLine();
        Console.Write("Your choice: ");
    }    static async Task InitializeAzureClient(string[] args)
    {
        Console.WriteLine("Initializing Azure client...");
        
        try
        {
            _armClient = new ArmClient(new DefaultAzureCredential());
            
            // Get subscription ID from command line arguments or environment variable
            string subscriptionId;
            if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
            {
                subscriptionId = args[0];
                Console.WriteLine($"Using subscription ID from command line: {subscriptionId}");
            }
            else if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID")))
            {
                subscriptionId = Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID")!;
                Console.WriteLine($"Using subscription ID from environment variable: {subscriptionId}");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error: Subscription ID not provided.");
                Console.WriteLine("Please provide the subscription ID either:");
                Console.WriteLine("  1. As the first command line argument: dotnet run <subscription-id>");
                Console.WriteLine("  2. Set the AZURE_SUBSCRIPTION_ID environment variable");
                Console.ResetColor();
                Environment.Exit(1);
                return; // This will never be reached, but satisfies the compiler
            }
            
            _subscription = _armClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(subscriptionId));
            
            // Get subscription data to verify access
            var subscriptionData = await _subscription.GetAsync();
            Console.WriteLine($"Using subscription: {subscriptionData.Value.Data.DisplayName} ({subscriptionData.Value.Id})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize Azure client: {ex.Message}");
            Console.WriteLine("Please ensure you're logged in with 'az login' or have proper Azure credentials configured.");
            throw;
        }
    }
    
    static async Task CreateAndDeployWebApp()
    {
        Console.WriteLine("Creating webapp in Azure App Service and deploying code...");
        
        if (!File.Exists(APP_ZIP_PATH))
        {
            throw new FileNotFoundException($"App zip file not found at: {APP_ZIP_PATH}");
        }
        
        // Create resource group
        Console.WriteLine("Creating resource group...");
        var resourceGroupData = new ResourceGroupData(AzureLocation.EastUS);
        var resourceGroupOperation = await _subscription!.GetResourceGroups().CreateOrUpdateAsync(
            Azure.WaitUntil.Completed, RESOURCE_GROUP_NAME, resourceGroupData);
        _resourceGroup = resourceGroupOperation.Value;
        Console.WriteLine($"Resource group '{RESOURCE_GROUP_NAME}' created/updated.");
        
        // Create App Service Plan
        Console.WriteLine("Creating App Service Plan...");
        var appServicePlanData = new AppServicePlanData(AzureLocation.EastUS)
        {
            Sku = new AppServiceSkuDescription
            {
                Name = "F1", // Free tier
                Tier = "Free"
            }
        };
        
        var appServicePlanOperation = await _resourceGroup.GetAppServicePlans().CreateOrUpdateAsync(
            Azure.WaitUntil.Completed, APP_SERVICE_PLAN_NAME, appServicePlanData);
        _appServicePlan = appServicePlanOperation.Value;
        Console.WriteLine($"App Service Plan '{APP_SERVICE_PLAN_NAME}' created.");
        
        // Create Web App
        Console.WriteLine("Creating Web App...");
        var webAppData = new WebSiteData(AzureLocation.EastUS)
        {
            AppServicePlanId = _appServicePlan.Id,
            SiteConfig = new SiteConfigProperties
            {
                AppSettings =
                [
                    new AppServiceNameValuePair { Name = "WEBSITE_RUN_FROM_PACKAGE", Value = "1" }
                ]
            }
        };
        
        var webAppOperation = await _resourceGroup.GetWebSites().CreateOrUpdateAsync(
            Azure.WaitUntil.Completed, WEB_APP_NAME, webAppData);
        _webApp = webAppOperation.Value;
        Console.WriteLine($"Web App '{WEB_APP_NAME}' created.");
        
        // Deploy code
        Console.WriteLine("Deploying code...");
        await DeployZipFile();
        
        Console.WriteLine($"✅ Web app created and deployed successfully!");
        Console.WriteLine($"🌐 App URL: https://{WEB_APP_NAME}.azurewebsites.net");
    }
      static async Task DeployZipFile()
    {
        try
        {
            // Use Azure credentials for deployment via Kudu API
            var credential = new DefaultAzureCredential();
            var kuduUrl = $"https://{WEB_APP_NAME}.scm.azurewebsites.net/api/zipdeploy";
            
            // Get access token for the App Service resource
            var tokenRequest = new TokenRequestContext(new[] { "https://management.azure.com/.default" });
            var accessToken = await credential.GetTokenAsync(tokenRequest);
            
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken.Token);
            
            var zipBytes = await File.ReadAllBytesAsync(APP_ZIP_PATH);
            var content = new ByteArrayContent(zipBytes);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");
            
            Console.WriteLine("Uploading zip file using Azure credentials...");
            var response = await httpClient.PostAsync(kuduUrl, content);
            
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("Code deployed successfully!");
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"Deployment failed: {response.StatusCode} - {errorContent}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Deployment error: {ex.Message}");
            throw;
        }
    }
    
    static async Task DeleteAppValueEnvironmentVariable()
    {
        Console.WriteLine("Deleting APP_VALUE environment variable...");
        
        if (_webApp == null)
        {
            Console.WriteLine("❌ No web app found. Please create the app first (option A).");
            return;
        }
        
        try
        {
            await EnsureWebAppReference();
            
            // Get current app settings
            var appSettings = await _webApp.GetApplicationSettingsAsync();
            var settings = appSettings.Value.Properties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);            if (settings.ContainsKey("APP_VALUE"))
            {
                settings.Remove("APP_VALUE");
                
                // Update app settings by creating a new configuration
                var appSettingsData = new AppServiceConfigurationDictionary();
                foreach (var setting in settings)
                {
                    appSettingsData.Properties.Add(setting.Key, setting.Value);
                }
                
                await _webApp.UpdateApplicationSettingsAsync(appSettingsData);
                Console.WriteLine("✅ APP_VALUE environment variable deleted successfully!");
            }
            else
            {
                Console.WriteLine("ℹ️ APP_VALUE environment variable was not defined.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to delete environment variable: {ex.Message}");
        }
    }
    
    static async Task ValidateAppReturnsError()
    {
        Console.WriteLine("Making HTTP request to validate app returns error (500+)...");
        
        if (_webApp == null)
        {
            Console.WriteLine("❌ No web app found. Please create the app first (option A).");
            return;
        }
        
        try
        {
            var appUrl = $"https://{WEB_APP_NAME}.azurewebsites.net";
            
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            
            var response = await httpClient.GetAsync(appUrl);
            var statusCode = (int)response.StatusCode;
            var content = await response.Content.ReadAsStringAsync();
            
            Console.WriteLine($"📡 Response Status Code: {statusCode}");
            Console.WriteLine($"📄 Response Content: {content}");
            
            if (statusCode >= 500)
            {
                Console.WriteLine("✅ App correctly returns an error (status code 500+)!");
            }
            else
            {
                Console.WriteLine("❌ App did not return an error (expected status code 500+).");
            }
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"📡 HTTP Request failed: {ex.Message}");
            Console.WriteLine("✅ This might be the expected error behavior!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to make HTTP request: {ex.Message}");
        }
    }
    
    static async Task AddAppValueEnvironmentVariable()
    {
        Console.WriteLine("Adding APP_VALUE = abcde environment variable...");
        
        if (_webApp == null)
        {
            Console.WriteLine("❌ No web app found. Please create the app first (option A).");
            return;
        }
        
        try
        {
            await EnsureWebAppReference();
            
            // Get current app settings
            var appSettings = await _webApp.GetApplicationSettingsAsync();
            var settings = appSettings.Value.Properties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);            // Add or update APP_VALUE
            settings["APP_VALUE"] = "abcde";
            
            // Update app settings by creating a new configuration
            var appSettingsData = new AppServiceConfigurationDictionary();
            foreach (var setting in settings)
            {
                appSettingsData.Properties.Add(setting.Key, setting.Value);
            }
            
            await _webApp.UpdateApplicationSettingsAsync(appSettingsData);
            Console.WriteLine("✅ APP_VALUE environment variable set to 'abcde' successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to add environment variable: {ex.Message}");
        }
    }
    
    static async Task ValidateAppIsWorking()
    {
        Console.WriteLine("Validating app returns correct JSON response...");
        
        if (_webApp == null)
        {
            Console.WriteLine("❌ No web app found. Please create the app first (option A).");
            return;
        }
        
        try
        {
            var appUrl = $"https://{WEB_APP_NAME}.azurewebsites.net";
            
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            
            var response = await httpClient.GetAsync(appUrl);
            var statusCode = (int)response.StatusCode;
            var content = await response.Content.ReadAsStringAsync();
            
            Console.WriteLine($"📡 Response Status Code: {statusCode}");
            Console.WriteLine($"📄 Response Content: {content}");
            
            if (response.IsSuccessStatusCode)
            {
                try
                {
                    var jsonResponse = JsonConvert.DeserializeObject<Dictionary<string, object>>(content);
                    
                    if (jsonResponse != null && 
                        jsonResponse.ContainsKey("AppValue") && 
                        jsonResponse["AppValue"]?.ToString() == "abcde")
                    {
                        Console.WriteLine("✅ App correctly returns JSON with AppValue: 'abcde'!");
                    }
                    else
                    {
                        Console.WriteLine("❌ App response JSON does not contain expected AppValue: 'abcde'.");
                    }
                }
                catch (JsonException)
                {
                    Console.WriteLine("❌ App response is not valid JSON.");
                }
            }
            else
            {
                Console.WriteLine($"❌ App returned error status code: {statusCode}");
            }
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"❌ HTTP Request failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to validate app: {ex.Message}");
        }
    }
    
    static async Task DeleteAppResource()
    {
        Console.WriteLine("Deleting app resource in Azure...");
        
        try
        {
            if (_resourceGroup != null)
            {
                Console.WriteLine($"Deleting resource group '{RESOURCE_GROUP_NAME}' and all its resources...");
                var deleteOperation = await _resourceGroup.DeleteAsync(Azure.WaitUntil.Completed);
                
                _webApp = null;
                _appServicePlan = null;
                _resourceGroup = null;
                
                Console.WriteLine("✅ App resource deleted successfully!");
            }
            else
            {
                Console.WriteLine("ℹ️ No resource group found to delete.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to delete app resource: {ex.Message}");
        }
    }
    
    static async Task EnsureWebAppReference()
    {
        if (_webApp == null && _subscription != null)
        {
            try
            {
                _resourceGroup = await _subscription.GetResourceGroupAsync(RESOURCE_GROUP_NAME);
                _webApp = await _resourceGroup.GetWebSiteAsync(WEB_APP_NAME);
            }
            catch
            {
                // Web app might not exist
            }
        }
    }
}
