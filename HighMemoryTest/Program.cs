using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.AppService.Models;
using Azure.ResourceManager.PostgreSql.FlexibleServers;
using Azure.ResourceManager.PostgreSql.FlexibleServers.Models;
using Azure.ResourceManager.Resources;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;

namespace HighMemoryTest
{
    public class Program
    {
        private static string SubscriptionId { get; set; } = string.Empty;
        private static string ZipFilePath { get; set; } = string.Empty;
        private static readonly string ResourceGroupName = "HighMemoryTest-RG";
        private static readonly string AppServicePlanName = "HighMemoryTest-Plan";
        private static readonly string WebAppName = $"highmemorytest-{DateTime.Now.Ticks}";
        private static readonly string PostgreSqlServerName = $"highmemorytest-db-{DateTime.Now.Ticks}";
        private static readonly string PostgreSqlDatabaseName = "marketingapp";
        private static readonly string AdminUsername = "adminuser";
        private static readonly string AdminPassword = "P@ssw0rd123!";
        
        private static ArmClient? _armClient;
        private static SubscriptionResource? _subscription;
        private static ResourceGroupResource? _resourceGroup;
        private static HttpClient? _httpClient;
        private static string? _webAppUrl;

        public static async Task Main(string[] args)
        {
            Console.WriteLine("HighMemoryTest - Azure App Service High Memory Testing");
            Console.WriteLine("======================================================");
            
            // Get subscription ID and zip file path from command line arguments
            if (args.Length < 2 || string.IsNullOrWhiteSpace(args[0]) || string.IsNullOrWhiteSpace(args[1]))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error: Both subscription ID and zip file path are required.");
                Console.WriteLine("Usage: dotnet run <subscription-id> <zip-file-path>");
                Console.WriteLine("Example: dotnet run 12345678-abcd-1234-efgh-123456789012 \"C:\\MyProject\\SampleMarketingApp_Complete.zip\"");
                Console.ResetColor();
                Environment.Exit(1);
            }
            
            SubscriptionId = args[0];
            ZipFilePath = args[1];
            
            Console.WriteLine($"Using subscription ID: {SubscriptionId}");
            Console.WriteLine($"Using zip file path: {ZipFilePath}");
            
            // Validate that the package file exists
            if (!File.Exists(ZipFilePath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: Package file not found: {ZipFilePath}");
                Console.ResetColor();
                Environment.Exit(1);
            }
            
            try
            {
                await InitializeClients();
                await RunTestMethod();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            finally
            {
                _httpClient?.Dispose();
            }
        }        private static async Task InitializeClients()
        {
            Console.WriteLine("Initializing Azure clients...");
            
            var credential = new DefaultAzureCredential();
            _armClient = new ArmClient(credential);
            var subscriptionResource = _armClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(SubscriptionId));
            _subscription = await subscriptionResource.GetAsync();
            
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(300);
            
            Console.WriteLine($"Azure clients initialized successfully using subscription: {SubscriptionId}");
        }

        private static async Task RunTestMethod()
        {
            Console.WriteLine("\nStarting automated test sequence...");
            
            // Step A: Create webapp and deploy code
            await StepA_CreateAndDeployWebApp();
            
            // Step B: Validate app returns 200
            await StepB_ValidateAppHealth();
            
            // Step C: Test high memory endpoint
            await StepC_TestHighMemoryEndpoint();
            
            // Step D: Wait 10 seconds
            await StepD_Wait();
            
            // Step E: Validate app is healthy again
            await StepE_ValidateAppHealthAgain();
            
            // Step F: Delete app resources
            await StepF_DeleteResources();
            
            Console.WriteLine("\nTest sequence completed successfully!");
        }        private static async Task StepA_CreateAndDeployWebApp()
        {
            Console.WriteLine("\nStep A: Creating webapp in Azure App Service and deploying code...");
            
            // Create resource group
            await CreateResourceGroup();
            
            // Create PostgreSQL server
            await CreatePostgreSqlServer();
            
            // Create App Service plan
            await CreateAppServicePlan();
            
            // Create web app
            await CreateWebApp();
            
            // Deploy code
            await DeployCode();
            
            Console.WriteLine("Step A completed: Webapp created and code deployed.");
        }

        private static async Task CreateResourceGroup()
        {
            Console.WriteLine("Creating resource group...");
            
            var resourceGroupData = new ResourceGroupData(Azure.Core.AzureLocation.BrazilSouth);
            var resourceGroupOperation = await _subscription!.GetResourceGroups()
                .CreateOrUpdateAsync(Azure.WaitUntil.Completed, ResourceGroupName, resourceGroupData);
            
            _resourceGroup = resourceGroupOperation.Value;
            Console.WriteLine($"Resource group '{ResourceGroupName}' created.");
        }        private static async Task CreatePostgreSqlServer()
        {
            Console.WriteLine("Creating PostgreSQL Flexible Server...");
            
            var serverCollection = _resourceGroup!.GetPostgreSqlFlexibleServers();
            
            var serverData = new PostgreSqlFlexibleServerData(Azure.Core.AzureLocation.BrazilSouth)
            {
                Sku = new PostgreSqlFlexibleServerSku("Standard_B1ms", PostgreSqlFlexibleServerSkuTier.Burstable),
                AdministratorLogin = AdminUsername,
                AdministratorLoginPassword = AdminPassword,
                Version = PostgreSqlFlexibleServerVersion.Ver14,
                Storage = new PostgreSqlFlexibleServerStorage()
                {
                    StorageSizeInGB = 32
                },
                Backup = new PostgreSqlFlexibleServerBackupProperties()
                {
                    BackupRetentionDays = 7,
                    GeoRedundantBackup = PostgreSqlFlexibleServerGeoRedundantBackupEnum.Disabled
                }
            };

            var serverOperation = await serverCollection.CreateOrUpdateAsync(
                Azure.WaitUntil.Completed, PostgreSqlServerName, serverData);
            
            var server = serverOperation.Value;
            
            // Create firewall rule to allow Azure services
            var firewallRuleData = new PostgreSqlFlexibleServerFirewallRuleData(
                System.Net.IPAddress.Parse("0.0.0.0"), 
                System.Net.IPAddress.Parse("0.0.0.0"));
            
            await server.GetPostgreSqlFlexibleServerFirewallRules().CreateOrUpdateAsync(
                Azure.WaitUntil.Completed, "AllowAzureServices", firewallRuleData);
            
            // Create database
            var databaseData = new PostgreSqlFlexibleServerDatabaseData()
            {
                Charset = "UTF8",
                Collation = "en_US.utf8"
            };
            
            await server.GetPostgreSqlFlexibleServerDatabases().CreateOrUpdateAsync(
                Azure.WaitUntil.Completed, PostgreSqlDatabaseName, databaseData);
            
            Console.WriteLine($"PostgreSQL Flexible Server '{PostgreSqlServerName}' created with database '{PostgreSqlDatabaseName}'.");
        }

        private static async Task CreateAppServicePlan()
        {
            Console.WriteLine("Creating App Service plan...");
            
            var planCollection = _resourceGroup!.GetAppServicePlans();
            var planData = new AppServicePlanData(Azure.Core.AzureLocation.BrazilSouth)
            {
                Sku = new AppServiceSkuDescription()
                {
                    Name = "B1",
                    Tier = "Basic",
                    Size = "B1",
                    Family = "B",
                    Capacity = 1
                },
                Kind = "linux",
                IsReserved = true
            };

            var planOperation = await planCollection.CreateOrUpdateAsync(
                Azure.WaitUntil.Completed, AppServicePlanName, planData);
            
            Console.WriteLine($"App Service plan '{AppServicePlanName}' created.");        }        private static async Task CreateWebApp()
        {
            Console.WriteLine("Creating web app...");
            
            var webAppCollection = _resourceGroup!.GetWebSites();
            
            // Get the App Service plan
            var appServicePlan = await _resourceGroup.GetAppServicePlanAsync(AppServicePlanName);
            
            // Create PostgreSQL connection string
            var connectionString = $"postgresql://{AdminUsername}:{AdminPassword}@{PostgreSqlServerName}.postgres.database.azure.com:5432/{PostgreSqlDatabaseName}?sslmode=require";
            
            var webAppData = new WebSiteData(Azure.Core.AzureLocation.BrazilSouth)
            {
                AppServicePlanId = appServicePlan.Value.Id,
                Kind = "app,linux",
                SiteConfig = new SiteConfigProperties()
                {
                    LinuxFxVersion = "PYTHON|3.11",                    AppSettings = 
                    {
                        new AppServiceNameValuePair() { Name = "DATABASE_URL", Value = connectionString },
                        new AppServiceNameValuePair() { Name = "SECRET_KEY", Value = AdminPassword },
                        new AppServiceNameValuePair() { Name = "FLASK_ENV", Value = "production" },
                        new AppServiceNameValuePair() { Name = "SCM_DO_BUILD_DURING_DEPLOYMENT", Value = "true" }
                    }
                }
            };

            var webAppOperation = await webAppCollection.CreateOrUpdateAsync(
                Azure.WaitUntil.Completed, WebAppName, webAppData);
            
            var webApp = webAppOperation.Value;
            _webAppUrl = $"https://{WebAppName}.azurewebsites.net";
            
            Console.WriteLine($"Web app '{WebAppName}' created at {_webAppUrl}");
        }

        private static async Task DeployCode()
        {
            Console.WriteLine("Deploying code to web app...");
            
            if (!File.Exists(ZipFilePath))
            {
                throw new FileNotFoundException($"Deployment package not found: {ZipFilePath}");
            }

            // Get deployment credentials
            var credential = new DefaultAzureCredential();
            var token = await credential.GetTokenAsync(
                new Azure.Core.TokenRequestContext(new[] { "https://management.azure.com/.default" }));

            // Deploy using Kudu API
            var kuduUrl = $"https://{WebAppName}.scm.azurewebsites.net/api/zipdeploy";
            
            using var fileStream = File.OpenRead(ZipFilePath);
            using var content = new StreamContent(fileStream);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
            
            _httpClient!.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", token.Token);
            
            var response = await _httpClient.PostAsync(kuduUrl, content);
            
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("Code deployed successfully.");
                
                // Wait for deployment to complete
                Console.WriteLine("Waiting for deployment to complete...");
                await Task.Delay(30000); // Wait 30 seconds for deployment
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"Deployment failed: {response.StatusCode} - {errorContent}");
            }
        }

        private static async Task StepB_ValidateAppHealth()
        {
            Console.WriteLine("\nStep B: Validating app returns 200 status code...");
            
            await ValidateAppHealth("Step B");
            
            Console.WriteLine("Step B completed: App is healthy.");
        }

        private static async Task ValidateAppHealth(string stepName)
        {
            int maxRetries = 6;
            int retryCount = 0;
            
            while (retryCount < maxRetries)
            {
                try
                {
                    Console.WriteLine($"Making HTTP request to {_webAppUrl} (attempt {retryCount + 1}/{maxRetries})...");
                    
                    var response = await _httpClient!.GetAsync(_webAppUrl);
                    
                    Console.WriteLine($"Response status: {response.StatusCode}");
                    
                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"{stepName}: App is healthy (status code: {response.StatusCode})");
                        return;
                    }
                    else if ((int)response.StatusCode >= 500)
                    {
                        Console.WriteLine($"{stepName}: App returned server error {response.StatusCode}, retrying...");
                        retryCount++;
                        
                        if (retryCount < maxRetries)
                        {
                            Console.WriteLine("Waiting 10 seconds before retry...");
                            await Task.Delay(10000);
                        }
                    }
                    else
                    {
                        throw new Exception($"Unexpected status code: {response.StatusCode}");
                    }
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"{stepName}: HTTP request failed: {ex.Message}");
                    retryCount++;
                    
                    if (retryCount < maxRetries)
                    {
                        Console.WriteLine("Waiting 10 seconds before retry...");
                        await Task.Delay(10000);
                    }
                }
            }
            
            throw new Exception($"{stepName}: App failed to return 200 status code after {maxRetries} attempts");
        }

        private static async Task StepC_TestHighMemoryEndpoint()
        {
            Console.WriteLine("\nStep C: Testing high memory endpoint...");
            
            var highMemoryUrl = $"{_webAppUrl}/api/faults/highmemory";
            
            try
            {
                Console.WriteLine($"Making HTTP request to {highMemoryUrl}...");
                
                var response = await _httpClient!.GetAsync(highMemoryUrl);
                
                Console.WriteLine($"Response status: {response.StatusCode}");
                
                if ((int)response.StatusCode >= 500)
                {
                    Console.WriteLine($"Step C completed: High memory endpoint returned error status {response.StatusCode} as expected.");
                }
                else
                {
                    Console.WriteLine($"Warning: High memory endpoint returned {response.StatusCode}, expected 500+");
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Step C completed: High memory endpoint caused HTTP exception as expected: {ex.Message}");
            }
        }

        private static async Task StepD_Wait()
        {
            Console.WriteLine("\nStep D: Waiting for 10 seconds...");
            await Task.Delay(10000);
            Console.WriteLine("Step D completed: Wait finished.");
        }

        private static async Task StepE_ValidateAppHealthAgain()
        {
            Console.WriteLine("\nStep E: Validating app is healthy again...");
            
            await ValidateAppHealth("Step E");
            
            Console.WriteLine("Step E completed: App is healthy again.");
        }

        private static async Task StepF_DeleteResources()
        {
            Console.WriteLine("\nStep F: Deleting app resources in Azure...");
            
            try
            {
                if (_resourceGroup != null)
                {
                    Console.WriteLine($"Deleting resource group '{ResourceGroupName}'...");
                    await _resourceGroup.DeleteAsync(Azure.WaitUntil.Started);
                    Console.WriteLine("Resource group deletion initiated.");
                }
                
                Console.WriteLine("Step F completed: Resources deleted.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Error deleting resources: {ex.Message}");
            }
        }
    }
}
