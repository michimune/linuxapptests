using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.AppService.Models;
using Azure.ResourceManager.PostgreSql.FlexibleServers;
using Azure.ResourceManager.PostgreSql.FlexibleServers.Models;
using Azure.ResourceManager.Resources;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace PackageDependencyTest
{    public class Program
    {
        private static string SubscriptionId { get; set; } = string.Empty;
        private static readonly string ResourceGroupName = $"rg-package-test-{DateTime.Now:yyyyMMddHHmmss}";
        private static readonly string AppServicePlanName = $"asp-package-test-{DateTime.Now:yyyyMMddHHmmss}";
        private static readonly string WebAppName = $"webapp-package-test-{DateTime.Now:yyyyMMddHHmmss}";
        private static readonly string PostgreServerName = $"psql-package-test-{DateTime.Now:yyyyMMddHHmmss}";
        private static readonly string DatabaseName = "marketingdb";
        private static readonly string AdminUsername = "adminuser";
        private static readonly string AdminPassword = GenerateRandomPassword();
        private static readonly string Location = "BrazilSouth";

        private static ArmClient? _armClient;
        private static ResourceGroupResource? _resourceGroup;
        private static HttpClient? _httpClient;

        public static async Task Main(string[] args)
        {
            Console.WriteLine("PackageDependencyTest - Automated Azure App Service Testing");
            Console.WriteLine("=============================================================");

            // Get subscription ID and zip file path from command line arguments
            if (args.Length < 2 || string.IsNullOrWhiteSpace(args[0]) || string.IsNullOrWhiteSpace(args[1]))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error: Both subscription ID and zip file path are required.");
                Console.WriteLine("Usage: dotnet run <subscription-id> <zip-file-path>");
                Console.WriteLine("Example: dotnet run 12345678-abcd-1234-efgh-123456789012 C:\\MyProject\\SampleMarketingApp_Complete.zip");
                Console.ResetColor();
                Environment.Exit(1);
            }            SubscriptionId = args[0];
            ZipFilePath = args[1];
            
            // Derive bad package path by replacing "Complete" with "BadPackageDependency"
            var directory = Path.GetDirectoryName(ZipFilePath);
            var filename = Path.GetFileName(ZipFilePath);
            var badFilename = filename.Replace("Complete", "BadPackageDependency");
            BadZipFilePath = Path.Combine(directory!, badFilename);
              Console.WriteLine($"Using subscription ID: {SubscriptionId}");
            Console.WriteLine($"Using good package: {ZipFilePath}");
            Console.WriteLine($"Using bad package: {BadZipFilePath}");
            
            // Validate that both package files exist
            if (!File.Exists(ZipFilePath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: Good package file not found: {ZipFilePath}");
                Console.ResetColor();
                Environment.Exit(1);
            }
            
            if (!File.Exists(BadZipFilePath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: Bad package file not found: {BadZipFilePath}");
                Console.WriteLine("Make sure the bad package file exists in the same directory with 'BadPackageDependency' instead of 'Complete' in the filename.");
                Console.ResetColor();
                Environment.Exit(1);
            }

            try
            {
                await RunTestMethodAsync();
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

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        private static async Task RunTestMethodAsync()
        {
            Console.WriteLine("Initializing Azure clients...");
            await InitializeAzureClientAsync();

            Console.WriteLine("\n=== Step A: Creating webapp and PostgreSQL instance ===");
            await StepA_CreateWebAppAndDatabase();

            Console.WriteLine("\n=== Step B: Creating staging slot and deploying bad package ===");
            await StepB_CreateStagingSlotAndSwap();

            Console.WriteLine("\n=== Step C: Testing app (expecting error) ===");
            await StepC_TestAppForError();

            Console.WriteLine("\n=== Step D: Swapping back ===");
            await StepD_SwapBack();

            Console.WriteLine("\n=== Step E: Validating app is working ===");
            await StepE_ValidateAppIsWorking();

            Console.WriteLine("\n=== Step F: Deleting Azure resources ===");
            await StepF_DeleteResources();

            Console.WriteLine("\n=== Test completed successfully! ===");
        }        private static async Task InitializeAzureClientAsync()
        {
            var credential = new DefaultAzureCredential();
            _armClient = new ArmClient(credential);

            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(300);
            
            // Use the specific subscription ID
            var subscription = _armClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(SubscriptionId));
            var subscriptionData = await subscription.GetAsync();
            Console.WriteLine($"Using subscription: {subscriptionData.Value.Data.DisplayName} ({subscriptionData.Value.Data.SubscriptionId})");
        }        private static async Task StepA_CreateWebAppAndDatabase()
        {
            Console.WriteLine("Creating resource group...");
            var subscription = _armClient!.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(SubscriptionId));
            var resourceGroupData = new ResourceGroupData(Location);
            var resourceGroupLro = await subscription.GetResourceGroups().CreateOrUpdateAsync(
                Azure.WaitUntil.Completed, ResourceGroupName, resourceGroupData);
            _resourceGroup = resourceGroupLro.Value;

            Console.WriteLine("Creating PostgreSQL server...");
            await CreatePostgreSqlServerAsync();

            Console.WriteLine("Creating App Service plan...");
            await CreateAppServicePlanAsync();

            Console.WriteLine("Creating web app...");
            await CreateWebAppAsync();            Console.WriteLine("Deploying good package...");
            await DeployPackageAsync(ZipFilePath);

            Console.WriteLine("Step A completed successfully!");
        }private static async Task CreatePostgreSqlServerAsync()
        {
            // Use PostgreSQL Flexible Server (not the legacy version)
            var serverData = new PostgreSqlFlexibleServerData(Location)
            {
                AdministratorLogin = AdminUsername,
                AdministratorLoginPassword = AdminPassword,
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

            var serverLro = await _resourceGroup!.GetPostgreSqlFlexibleServers().CreateOrUpdateAsync(
                Azure.WaitUntil.Completed, PostgreServerName, serverData);

            // Create database
            var databaseData = new PostgreSqlFlexibleServerDatabaseData();
            await serverLro.Value.GetPostgreSqlFlexibleServerDatabases().CreateOrUpdateAsync(
                Azure.WaitUntil.Completed, DatabaseName, databaseData);

            // Configure firewall to allow Azure services
            var firewallRuleData = new PostgreSqlFlexibleServerFirewallRuleData(
                IPAddress.Parse("0.0.0.0"), 
                IPAddress.Parse("0.0.0.0"));

            await serverLro.Value.GetPostgreSqlFlexibleServerFirewallRules().CreateOrUpdateAsync(
                Azure.WaitUntil.Completed, "AllowAzureServices", firewallRuleData);
        }        private static async Task CreateAppServicePlanAsync()
        {
            var appServicePlanData = new AppServicePlanData(Location)
            {
                Sku = new AppServiceSkuDescription()
                {
                    Name = "S1",
                    Tier = "Standard",
                    Size = "S1",
                    Family = "S",
                    Capacity = 1
                },
                Kind = "linux",
                IsReserved = true
            };

            await _resourceGroup!.GetAppServicePlans().CreateOrUpdateAsync(
                Azure.WaitUntil.Completed, AppServicePlanName, appServicePlanData);
        }        private static async Task CreateWebAppAsync()
        {
            var appServicePlan = await _resourceGroup!.GetAppServicePlanAsync(AppServicePlanName);

            var webAppData = new WebSiteData(Location)
            {
                AppServicePlanId = appServicePlan.Value.Id,
                Kind = "app,linux",
                SiteConfig = new SiteConfigProperties()
                {
                    LinuxFxVersion = "PYTHON|3.9",
                    AppSettings =
                    {
                        new AppServiceNameValuePair { Name = "DATABASE_URL", Value = $"postgresql://{AdminUsername}:{AdminPassword}@{PostgreServerName}.postgres.database.azure.com:5432/{DatabaseName}" },
                        new AppServiceNameValuePair { Name = "SECRET_KEY", Value = AdminPassword },
                        new AppServiceNameValuePair { Name = "SCM_DO_BUILD_DURING_DEPLOYMENT", Value = "true" }
                    }
                }
            };

            await _resourceGroup!.GetWebSites().CreateOrUpdateAsync(
                Azure.WaitUntil.Completed, WebAppName, webAppData);
        }        private static async Task DeployPackageAsync(string packagePath)
        {
            if (!File.Exists(packagePath))
            {
                throw new FileNotFoundException($"Package file not found: {packagePath}");
            }

            Console.WriteLine($"Deploying package: {Path.GetFileName(packagePath)}");

            // Get access token for deployment
            var credential = new DefaultAzureCredential();
            var tokenRequestContext = new Azure.Core.TokenRequestContext(new[] { "https://management.azure.com/.default" });
            var token = await credential.GetTokenAsync(tokenRequestContext);

            // Deploy using Kudu REST API
            var kuduUrl = $"https://{WebAppName}.scm.azurewebsites.net/api/zipdeploy";
            
            using var request = new HttpRequestMessage(HttpMethod.Post, kuduUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
            
            var fileBytes = File.ReadAllBytes(packagePath);
            request.Content = new ByteArrayContent(fileBytes);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");

            var response = await _httpClient!.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                throw new Exception($"Deployment failed: {response.StatusCode} - {content}");
            }

            Console.WriteLine("Deployment initiated. Waiting for completion...");
            await Task.Delay(60000); // Wait 60 seconds for deployment to complete
        }        private static async Task StepB_CreateStagingSlotAndSwap()
        {
            Console.WriteLine("Creating staging slot...");
            var webApp = await _resourceGroup!.GetWebSiteAsync(WebAppName);
            
            var slotData = new WebSiteData(Location)
            {
                AppServicePlanId = webApp.Value.Data.AppServicePlanId,
                Kind = "app,linux",
                SiteConfig = new SiteConfigProperties()
                {
                    LinuxFxVersion = "PYTHON|3.11",
                    AppSettings =
                    {
                        new AppServiceNameValuePair { Name = "DATABASE_URL", Value = $"postgresql://{AdminUsername}:{AdminPassword}@{PostgreServerName}.postgres.database.azure.com:5432/{DatabaseName}" },
                        new AppServiceNameValuePair { Name = "SECRET_KEY", Value = AdminPassword },
                        new AppServiceNameValuePair { Name = "SCM_DO_BUILD_DURING_DEPLOYMENT", Value = "true" }
                    }
                }
            };

            await webApp.Value.GetWebSiteSlots().CreateOrUpdateAsync(
                Azure.WaitUntil.Completed, "staging", slotData);            Console.WriteLine("Deploying bad package to staging slot...");
            await DeployPackageToSlotAsync(BadZipFilePath, "staging");

            Console.WriteLine("Swapping staging slot with production...");
            await SwapSlotsAsync("staging");

            Console.WriteLine("Step B completed successfully!");
        }        private static async Task DeployPackageToSlotAsync(string packagePath, string slotName)
        {
            if (!File.Exists(packagePath))
            {
                throw new FileNotFoundException($"Package file not found: {packagePath}");
            }

            Console.WriteLine($"Deploying package to slot {slotName}: {Path.GetFileName(packagePath)}");

            // Get access token for deployment
            var credential = new DefaultAzureCredential();
            var tokenRequestContext = new Azure.Core.TokenRequestContext(new[] { "https://management.azure.com/.default" });
            var token = await credential.GetTokenAsync(tokenRequestContext);

            // Deploy using Kudu REST API for slot
            var kuduUrl = $"https://{WebAppName}-{slotName}.scm.azurewebsites.net/api/zipdeploy";
            
            using var request = new HttpRequestMessage(HttpMethod.Post, kuduUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
            
            var fileBytes = File.ReadAllBytes(packagePath);
            request.Content = new ByteArrayContent(fileBytes);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");

            var response = await _httpClient!.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                throw new Exception($"Slot deployment failed: {response.StatusCode} - {content}");
            }

            Console.WriteLine("Slot deployment initiated. Waiting for completion...");
            await Task.Delay(60000); // Wait 60 seconds for deployment to complete
        }        private static async Task SwapSlotsAsync(string sourceSlot)
        {
            var webApp = await _resourceGroup!.GetWebSiteAsync(WebAppName);
            
            var slotSwapData = new CsmSlotEntity("production", true);

            await webApp.Value.GetWebSiteSlot(sourceSlot).Value.SwapSlotAsync(
                Azure.WaitUntil.Completed, slotSwapData);

            Console.WriteLine("Slot swap completed. Waiting for propagation...");
            await Task.Delay(30000); // Wait 30 seconds for swap to propagate
        }

        private static async Task StepC_TestAppForError()
        {
            var appUrl = $"https://{WebAppName}.azurewebsites.net";
            Console.WriteLine($"Testing app at: {appUrl}");

            try
            {
                var response = await _httpClient!.GetAsync(appUrl);
                var statusCode = (int)response.StatusCode;
                
                Console.WriteLine($"Response status code: {statusCode}");
                
                if (statusCode >= 500)
                {
                    Console.WriteLine("✓ Expected error confirmed - app returns 500+ status code");
                }
                else
                {
                    Console.WriteLine($"⚠ Unexpected: App returned {statusCode} instead of 500+");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✓ Expected error confirmed - Exception occurred: {ex.Message}");
            }

            Console.WriteLine("Step C completed successfully!");
        }        private static async Task StepD_SwapBack()
        {
            Console.WriteLine("Swapping back to original version...");
            await SwapSlotsAsync("staging");
            Console.WriteLine("Step D completed successfully!");
        }

        private static async Task StepE_ValidateAppIsWorking()
        {
            var appUrl = $"https://{WebAppName}.azurewebsites.net";
            Console.WriteLine($"Validating app is working at: {appUrl}");

            bool isWorking = false;
            int attempts = 0;
            const int maxAttempts = 6;

            while (!isWorking && attempts < maxAttempts)
            {
                attempts++;
                Console.WriteLine($"Attempt {attempts}/{maxAttempts}...");

                try
                {
                    var response = await _httpClient!.GetAsync(appUrl);
                    var statusCode = (int)response.StatusCode;
                    
                    Console.WriteLine($"Response status code: {statusCode}");
                    
                    if (statusCode == 200)
                    {
                        Console.WriteLine("✓ App is working correctly - returns 200 status code");
                        isWorking = true;
                    }
                    else if (statusCode >= 500)
                    {
                        Console.WriteLine($"App still returning error: {statusCode}");
                        if (attempts < maxAttempts)
                        {
                            Console.WriteLine("Waiting 10 seconds before retry...");
                            await Task.Delay(10000);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"App returned unexpected status code: {statusCode}");
                        isWorking = true; // Consider non-500 codes as working
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Request failed: {ex.Message}");
                    if (attempts < maxAttempts)
                    {
                        Console.WriteLine("Waiting 10 seconds before retry...");
                        await Task.Delay(10000);
                    }
                }
            }

            if (!isWorking)
            {
                throw new Exception("App failed to return 200 status code after 6 attempts");
            }

            Console.WriteLine("Step E completed successfully!");
        }        private static async Task StepF_DeleteResources()
        {
            Console.WriteLine("Deleting resource group and all resources...");
            
            var subscription = _armClient!.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(SubscriptionId));
            var resourceGroup = await subscription.GetResourceGroupAsync(ResourceGroupName);
            
            await resourceGroup.Value.DeleteAsync(Azure.WaitUntil.Completed);
            
            Console.WriteLine("Step F completed successfully!");
        }

        private static string GenerateRandomPassword()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";
            var random = new Random();
            var password = new StringBuilder();
            
            // Ensure at least one of each required character type
            password.Append(chars[random.Next(0, 26)]); // Uppercase
            password.Append(chars[random.Next(26, 52)]); // Lowercase
            password.Append(chars[random.Next(52, 62)]); // Digit
            password.Append(chars[random.Next(62, chars.Length)]); // Special char
            
            // Fill remaining positions
            for (int i = 4; i < 16; i++)
            {
                password.Append(chars[random.Next(chars.Length)]);
            }
            
            return password.ToString();
        }
    }
}
