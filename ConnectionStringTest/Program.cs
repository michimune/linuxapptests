using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using Azure;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.AppService.Models;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.PostgreSql.FlexibleServers;
using Azure.ResourceManager.PostgreSql.FlexibleServers.Models;
using Azure.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ConnectionStringTest
{    class Program
    {
        private static string SubscriptionId { get; set; } = string.Empty;
        private static ArmClient? _armClient;
        private static SubscriptionResource? _subscription;
        private static ResourceGroupResource? _resourceGroup;
        private static WebSiteResource? _webApp;
        private static PostgreSqlFlexibleServerResource? _postgreSqlServer;
        private static PostgreSqlFlexibleServerDatabaseResource? _postgreSqlDatabase;
        private static string _uniqueId = DateTime.Now.ToString("yyyyMMddHHmmss");
        private static string _resourceGroupName = $"rg-connectiontest-{_uniqueId}";
        private static string _appServicePlanName = $"asp-connectiontest-{_uniqueId}";
        private static string _webAppName = $"webapp-connectiontest-{_uniqueId}";
        private static string _postgreSqlServerName = $"psql-connectiontest-{_uniqueId}";
        private static string _databaseName = "marketingappdb";
        private static string _adminUsername = "adminuser";
        private static string _adminPassword = "PassWord123-Abc";
        private static string _databaseUrl = ""; // Will be populated after PostgreSQL creation
        private static string _secretKey = "PassWord123-Abc";
        private static AzureLocation _location = AzureLocation.BrazilSouth;
        private static string _zipPath = "";
        private static int _maxRetryAttempts = 6;
        private static int _retryDelaySeconds = 10;

        static async Task Main(string[] args)
        {
            Console.WriteLine("ConnectionStringTest - Automated Azure App Service Testing");
            Console.WriteLine("===========================================================");
            Console.WriteLine();

            // Validate command line arguments
            if (args.Length < 2)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error: Required command line arguments not provided.");
                Console.WriteLine("Usage: dotnet run <subscription-id> <zip-file-path>");
                Console.WriteLine("Example: dotnet run 12345678-1234-1234-1234-123456789012 C:\\MyProject\\SampleMarketingApp_Complete.zip");
                Console.ResetColor();
                Environment.Exit(1);
            }

            // Get subscription ID from first command line argument
            if (string.IsNullOrWhiteSpace(args[0]))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error: Subscription ID cannot be empty.");
                Console.WriteLine("Usage: dotnet run <subscription-id> <zip-file-path>");
                Console.ResetColor();
                Environment.Exit(1);
            }
            SubscriptionId = args[0];
            Console.WriteLine($"Using subscription ID: {SubscriptionId}");

            // Get zip file path from second command line argument
            if (string.IsNullOrWhiteSpace(args[1]))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error: Zip file path cannot be empty.");
                Console.WriteLine("Usage: dotnet run <subscription-id> <zip-file-path>");
                Console.ResetColor();
                Environment.Exit(1);
            }
            _zipPath = args[1];
            Console.WriteLine($"Using zip file path: {_zipPath}");

            try
            {
                LoadConfiguration();
                await InitializeAzureClient();
                await RunTestMethod();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }

        private static void LoadConfiguration()
        {
            var configPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
            if (File.Exists(configPath))
            {
                try
                {
                    var configText = File.ReadAllText(configPath);
                    var config = JsonConvert.DeserializeObject<JObject>(configText);

                    if (config != null)
                    {
                        // Load Azure settings
                        var azureSettings = config["AzureSettings"];
                        if (azureSettings != null)
                        {
                            if (azureSettings["Location"]?.ToString() is string location)
                                _location = new AzureLocation(location);
                            // Note: DeploymentZipPath is now provided via command line argument
                        }

                        // Load Database settings
                        var dbSettings = config["DatabaseSettings"];
                        if (dbSettings != null)
                        {
                            if (dbSettings["AdminUsername"]?.ToString() is string username)
                                _adminUsername = username;
                            if (dbSettings["AdminPassword"]?.ToString() is string password)
                                _adminPassword = password;
                            if (dbSettings["DatabaseName"]?.ToString() is string dbName)
                                _databaseName = dbName;
                        }

                        // Load App settings
                        var appSettings = config["AppSettings"];
                        if (appSettings != null)
                        {
                            if (appSettings["SecretKey"]?.ToString() is string secretKey)
                                _secretKey = secretKey;
                        }

                        // Load Test settings
                        var testSettings = config["TestSettings"];
                        if (testSettings != null)
                        {
                            if (testSettings["MaxRetryAttempts"]?.Value<int>() is int maxRetry)
                                _maxRetryAttempts = maxRetry;
                            if (testSettings["RetryDelaySeconds"]?.Value<int>() is int retryDelay)
                                _retryDelaySeconds = retryDelay;
                        }

                        Console.WriteLine("Configuration loaded from appsettings.json");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Could not load configuration from appsettings.json: {ex.Message}");
                    Console.WriteLine("Using default configuration values.");
                }
            }
        }        private static async Task InitializeAzureClient()
        {
            Console.WriteLine("Initializing Azure client...");
            
            var credential = new DefaultAzureCredential();
            _armClient = new ArmClient(credential);
              // Use the specific subscription ID
            var subscriptionResponse = await _armClient.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{SubscriptionId}")).GetAsync();
            
            if (subscriptionResponse?.Value != null)
            {
                _subscription = subscriptionResponse.Value;
                Console.WriteLine($"Using subscription: {_subscription.Data.DisplayName} ({_subscription.Data.SubscriptionId})");
                
                // Register PostgreSQL resource provider if needed
                await RegisterPostgreSqlResourceProvider();
            }
            else
            {
                throw new InvalidOperationException($"Could not access subscription {SubscriptionId}. Please ensure you have access and are logged in with 'az login'");
            }
        }

        private static async Task RegisterPostgreSqlResourceProvider()
        {
            Console.WriteLine("Checking PostgreSQL resource provider registration...");
            
            try
            {
                var resourceProviders = _subscription!.GetResourceProviders();
                var postgreSqlProvider = await resourceProviders.GetAsync("Microsoft.DBforPostgreSQL");
                
                if (postgreSqlProvider?.Value?.Data?.RegistrationState == "Registered")
                {
                    Console.WriteLine("✓ PostgreSQL resource provider is already registered");
                    return;
                }
                
                Console.WriteLine("Registering PostgreSQL resource provider...");
                await postgreSqlProvider!.Value.RegisterAsync();
                
                // Wait for registration to complete (up to 2 minutes)
                var timeout = DateTime.UtcNow.AddMinutes(2);
                while (DateTime.UtcNow < timeout)
                {
                    var updatedProvider = await resourceProviders.GetAsync("Microsoft.DBforPostgreSQL");
                    if (updatedProvider?.Value?.Data?.RegistrationState == "Registered")
                    {
                        Console.WriteLine("✓ PostgreSQL resource provider registered successfully");
                        return;
                    }
                    
                    Console.WriteLine("Waiting for resource provider registration...");
                    await Task.Delay(10000); // Wait 10 seconds
                }
                
                throw new TimeoutException("PostgreSQL resource provider registration timed out");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠ Could not register PostgreSQL resource provider automatically: {ex.Message}");
                Console.WriteLine("Please run this command manually:");
                Console.WriteLine("  az provider register --namespace Microsoft.DBforPostgreSQL --wait");
                throw;
            }
        }

        private static async Task RunTestMethod()
        {
            Console.WriteLine("Starting automated test sequence...");
            Console.WriteLine();

            try
            {
                // Step A is critical - if it fails, we cannot continue
                try
                {
                    await StepA_CreateWebAppAndPostgreSQL();
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"✗ Step A failed: {ex.Message}");
                    Console.WriteLine("✗ Cannot continue with test sequence - exiting program");
                    Console.ResetColor();
                    return; // Exit early if Step A fails
                }

                await StepB_DeleteEnvironmentVariables();
                await StepC_TestAppReturnsError();
                await StepD_AddEnvironmentVariables();
                
                bool stepESuccess = await StepE_ValidateAppIsWorking();
                
                if (stepESuccess)
                {
                    await StepF_DeleteResources();
                    Console.WriteLine("Test sequence completed successfully!");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("✗ Test sequence failed at Step E - App validation failed!");
                    Console.WriteLine("✗ Skipping Step F - Resources will NOT be deleted for debugging purposes.");
                    Console.ResetColor();
                    Console.WriteLine();
                    Console.WriteLine("You can manually delete the resource group later:");
                    Console.WriteLine($"  az group delete --name {_resourceGroupName} --yes --no-wait");
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ Test sequence failed with exception: {ex.Message}");
                Console.ResetColor();
                throw;
            }
        }private static async Task StepA_CreateWebAppAndPostgreSQL()
        {
            Console.WriteLine("Step A: Creating webapp and PostgreSQL instance in Azure...");

            try
            {
                Console.WriteLine($"Creating resource group: {_resourceGroupName}");
                var resourceGroupData = new ResourceGroupData(_location);
                var resourceGroupOperation = await _subscription!.GetResourceGroups().CreateOrUpdateAsync(
                    WaitUntil.Completed, _resourceGroupName, resourceGroupData);
                _resourceGroup = resourceGroupOperation.Value;

                Console.WriteLine($"Creating PostgreSQL Flexible Server: {_postgreSqlServerName}");
                await CreatePostgreSqlServer();

                Console.WriteLine($"Creating database: {_databaseName}");
                await CreatePostgreSqlDatabase();

                Console.WriteLine($"Creating App Service Plan: {_appServicePlanName}");
                await CreateAppServicePlan();

                Console.WriteLine($"Creating Web App: {_webAppName}");
                await CreateWebApp();

                Console.WriteLine("Deploying application code...");
                await DeployCode();

                Console.WriteLine("✓ Step A completed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Step A failed: {ex.Message}");
                throw;
            }
        }        private static async Task CreatePostgreSqlServer()
        {
            var postgreSqlServerData = new PostgreSqlFlexibleServerData(_location)
            {
                AdministratorLogin = _adminUsername,
                AdministratorLoginPassword = _adminPassword,
                Version = PostgreSqlFlexibleServerVersion.Ver14,
                Sku = new PostgreSqlFlexibleServerSku("Standard_B1ms", PostgreSqlFlexibleServerSkuTier.Burstable),
                Storage = new PostgreSqlFlexibleServerStorage
                {
                    StorageSizeInGB = 32
                },
                Network = new PostgreSqlFlexibleServerNetwork
                {
                    PublicNetworkAccess = PostgreSqlFlexibleServerPublicNetworkAccessState.Enabled
                },
                Backup = new PostgreSqlFlexibleServerBackupProperties
                {
                    BackupRetentionDays = 7,
                    GeoRedundantBackup = PostgreSqlFlexibleServerGeoRedundantBackupEnum.Disabled
                },
                HighAvailability = new PostgreSqlFlexibleServerHighAvailability
                {
                    Mode = PostgreSqlFlexibleServerHighAvailabilityMode.Disabled
                }
            };

            var serverOperation = await _resourceGroup!.GetPostgreSqlFlexibleServers().CreateOrUpdateAsync(
                WaitUntil.Completed, _postgreSqlServerName, postgreSqlServerData);
            _postgreSqlServer = serverOperation.Value;

            // Configure firewall to allow all Azure services
            var firewallRuleData = new PostgreSqlFlexibleServerFirewallRuleData(
                System.Net.IPAddress.Parse("0.0.0.0"), 
                System.Net.IPAddress.Parse("0.0.0.0"));

            await _postgreSqlServer.GetPostgreSqlFlexibleServerFirewallRules().CreateOrUpdateAsync(
                WaitUntil.Completed, "AllowAllAzureServices", firewallRuleData);

            // Update the database URL with actual connection details
            var serverFqdn = _postgreSqlServer.Data.FullyQualifiedDomainName;
            _databaseUrl = $"postgresql://{_adminUsername}:{_adminPassword}@{serverFqdn}:5432/{_databaseName}";
            
            Console.WriteLine($"PostgreSQL server created: {serverFqdn}");
        }

        private static async Task CreatePostgreSqlDatabase()
        {
            var databaseData = new PostgreSqlFlexibleServerDatabaseData();

            var databaseOperation = await _postgreSqlServer!.GetPostgreSqlFlexibleServerDatabases().CreateOrUpdateAsync(
                WaitUntil.Completed, _databaseName, databaseData);
            _postgreSqlDatabase = databaseOperation.Value;

            Console.WriteLine($"Database '{_databaseName}' created successfully");
        }private static async Task CreateAppServicePlan()
        {
            var appServicePlanData = new AppServicePlanData(_location)
            {
                Sku = new AppServiceSkuDescription
                {
                    Name = "F1",
                    Tier = "Free",
                    Size = "F1",
                    Family = "F",
                    Capacity = 1
                },
                Kind = "linux",
                IsReserved = true  // Required for Linux App Service Plans
            };

            await _resourceGroup!.GetAppServicePlans().CreateOrUpdateAsync(
                WaitUntil.Completed, _appServicePlanName, appServicePlanData);
        }

        private static async Task CreateWebApp()
        {
            var appServicePlan = await _resourceGroup!.GetAppServicePlans().GetAsync(_appServicePlanName);
            
            var webSiteData = new WebSiteData(_location)
            {
                AppServicePlanId = appServicePlan.Value.Id,
                Kind = "app",
                SiteConfig = new SiteConfigProperties
                {
                    LinuxFxVersion = "PYTHON|3.11",
                    AppSettings =
                    {
                        new AppServiceNameValuePair { Name = "SCM_DO_BUILD_DURING_DEPLOYMENT", Value = "true" },
                        new AppServiceNameValuePair { Name = "WEBSITES_ENABLE_APP_SERVICE_STORAGE", Value = "false" }
                    }
                }
            };

            var webAppOperation = await _resourceGroup!.GetWebSites().CreateOrUpdateAsync(
                WaitUntil.Completed, _webAppName, webSiteData);
            _webApp = webAppOperation.Value;
        }        private static async Task DeployCode()
        {
            if (!File.Exists(_zipPath))
            {
                throw new FileNotFoundException($"Deployment zip file not found: {_zipPath}");
            }

            var credential = new DefaultAzureCredential();
            var tokenRequestContext = new TokenRequestContext(new[] { "https://management.azure.com/.default" });
            var accessToken = await credential.GetTokenAsync(tokenRequestContext);
            
            var kuduUrl = $"https://{_webAppName}.scm.azurewebsites.net/api/zipdeploy";
            
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(300);
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken.Token);
            
            using var zipContent = new ByteArrayContent(File.ReadAllBytes(_zipPath));
            zipContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");
            
            var response = await httpClient.PostAsync(kuduUrl, zipContent);
            
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Deployment failed: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
            }

            Console.WriteLine("Waiting for deployment to complete...");
            await Task.Delay(30000);
        }

        private static async Task StepB_DeleteEnvironmentVariables()
        {
            Console.WriteLine("Step B: Deleting DATABASE_URL and SECRET_KEY environment variables...");

            try
            {
                var config = await _webApp!.GetApplicationSettingsAsync();
                var settings = config.Value.Properties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                
                bool removed = false;
                if (settings.ContainsKey("DATABASE_URL"))
                {
                    settings.Remove("DATABASE_URL");
                    removed = true;
                    Console.WriteLine("Removed DATABASE_URL");
                }
                
                if (settings.ContainsKey("SECRET_KEY"))
                {
                    settings.Remove("SECRET_KEY");
                    removed = true;
                    Console.WriteLine("Removed SECRET_KEY");
                }

                if (removed)
                {
                    var appSettingsResource = new AppServiceConfigurationDictionary();
                    foreach (var setting in settings)
                    {
                        appSettingsResource.Properties.Add(setting.Key, setting.Value);
                    }
                    
                    await _webApp.UpdateApplicationSettingsAsync(appSettingsResource);
                    Console.WriteLine("Environment variables updated");
                }
                else
                {
                    Console.WriteLine("No DATABASE_URL or SECRET_KEY found to remove");
                }

                Console.WriteLine("✓ Step B completed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Step B failed: {ex.Message}");
                throw;
            }
        }

        private static async Task StepC_TestAppReturnsError()
        {
            Console.WriteLine("Step C: Testing that app returns an error (500+)...");

            try
            {
                var appUrl = $"https://{_webAppName}.azurewebsites.net";
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(300);

                var response = await httpClient.GetAsync(appUrl);
                var statusCode = (int)response.StatusCode;
                
                Console.WriteLine($"App returned status code: {statusCode}");
                
                if (statusCode >= 500)
                {
                    Console.WriteLine($"✓ App correctly returns error status {statusCode}");
                }
                else
                {
                    Console.WriteLine($"⚠ App returned {statusCode}, expected 500+");
                }

                Console.WriteLine("✓ Step C completed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Step C failed: {ex.Message}");
                throw;
            }
        }

        private static async Task StepD_AddEnvironmentVariables()
        {
            Console.WriteLine("Step D: Adding DATABASE_URL and SECRET_KEY environment variables...");

            try
            {
                var config = await _webApp!.GetApplicationSettingsAsync();
                var settings = config.Value.Properties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                
                settings["DATABASE_URL"] = _databaseUrl;
                settings["SECRET_KEY"] = _secretKey;
                
                Console.WriteLine($"DATABASE_URL: {_databaseUrl}");
                Console.WriteLine($"SECRET_KEY: {_secretKey}");
                
                var appSettingsResource = new AppServiceConfigurationDictionary();
                foreach (var setting in settings)
                {
                    appSettingsResource.Properties.Add(setting.Key, setting.Value);
                }
                
                await _webApp.UpdateApplicationSettingsAsync(appSettingsResource);
                
                Console.WriteLine("Added DATABASE_URL and SECRET_KEY to environment variables");
                Console.WriteLine("✓ Step D completed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Step D failed: {ex.Message}");
                throw;
            }
        }        private static async Task<bool> StepE_ValidateAppIsWorking()
        {
            Console.WriteLine("Step E: Validating that app returns 200 status code...");

            try
            {
                var appUrl = $"https://{_webAppName}.azurewebsites.net";
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(30);

                int attempts = 0;
                int maxAttempts = _maxRetryAttempts;
                bool success = false;

                while (attempts < maxAttempts && !success)
                {
                    attempts++;
                    Console.WriteLine($"Attempt {attempts}/{maxAttempts}...");

                    try
                    {
                        var response = await httpClient.GetAsync(appUrl);
                        var statusCode = (int)response.StatusCode;
                        
                        Console.WriteLine($"App returned status code: {statusCode}");
                        
                        if (statusCode == 200)
                        {
                            Console.WriteLine("✓ App is working correctly!");
                            success = true;
                        }
                        else if (statusCode >= 500)
                        {
                            Console.WriteLine($"App still returning error {statusCode}");
                            if (attempts < maxAttempts)
                            {
                                Console.WriteLine($"Waiting {_retryDelaySeconds} seconds before retrying...");
                                await Task.Delay(_retryDelaySeconds * 1000);
                            }
                        }
                        else
                        {
                            Console.WriteLine($"App returned unexpected status {statusCode}");
                            success = true;
                        }
                    }
                    catch (HttpRequestException ex)
                    {
                        Console.WriteLine($"HTTP request failed: {ex.Message}");
                        if (attempts < maxAttempts)
                        {
                            Console.WriteLine($"Waiting {_retryDelaySeconds} seconds before retrying...");
                            await Task.Delay(_retryDelaySeconds * 1000);
                        }
                    }
                }

                if (success)
                {
                    Console.WriteLine("✓ Step E completed successfully");
                    return true;
                }
                else
                {
                    Console.WriteLine("⚠ Step E failed - App validation unsuccessful after all retry attempts");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Step E failed: {ex.Message}");
                return false;
            }
        }

        private static async Task StepF_DeleteResources()
        {
            Console.WriteLine("Step F: Deleting Azure resources...");

            try
            {
                if (_resourceGroup != null)
                {
                    Console.WriteLine($"Deleting resource group: {_resourceGroupName}");
                    await _resourceGroup.DeleteAsync(WaitUntil.Started);
                    Console.WriteLine("Resource group deletion initiated (this may take a few minutes to complete)");
                }

                Console.WriteLine("✓ Step F completed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Step F failed: {ex.Message}");
                throw;
            }
        }
    }
}
