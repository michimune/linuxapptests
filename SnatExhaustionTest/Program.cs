using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.AppService.Models;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Models;
using Azure.ResourceManager.PrivateDns;
using Azure.ResourceManager.PrivateDns.Models;
using Azure.ResourceManager.PostgreSql.FlexibleServers;
using Azure.ResourceManager.PostgreSql.FlexibleServers.Models;
using Azure.ResourceManager.Resources;
using Newtonsoft.Json;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace SnatExhaustionTest;

public class Program
{
    private static string SubscriptionId { get; set; } = string.Empty;
    private static readonly string Location = "brazilsouth";
    private static readonly string ResourceGroupName = $"rg-snat-test-{DateTime.Now:yyyyMMdd-HHmmss}";
    private static readonly string AppServicePlanName = $"asp-snat-test-{DateTime.Now:yyyyMMdd-HHmmss}";
    private static readonly string WebAppName = $"webapp-snat-test-{DateTime.Now:yyyyMMdd-HHmmss}";
    private static readonly string PostgreSqlServerName = $"psql-snat-test-{DateTime.Now:yyyyMMdd-HHmmss}";
    private static readonly string DatabaseName = "snatdb";
    private static readonly string VnetName = $"vnet-snat-test-{DateTime.Now:yyyyMMdd-HHmmss}";
    private static readonly string SubnetName = "app-subnet";
    private static readonly string PostgreSqlSubnetName = "postgresql-subnet";
    private static readonly string AdminUsername = "snatadmin";
    private static readonly string AdminPassword = GeneratePassword();
    private static string ZipFilePath { get; set; } = string.Empty;

    private static ArmClient? _armClient;
    private static ResourceGroupResource? _resourceGroup;
    private static HttpClient? _httpClient;
    private static string? _webAppUrl;
    private static PostgreSqlFlexibleServerResource? _postgreSqlServer;
    private static VirtualNetworkResource? _vnet;
    private static SubnetResource? _appSubnet;
    private static SubnetResource? _postgresSubnet;

    public static async Task Main(string[] args)
    {
        Console.WriteLine("=== SNAT Exhaustion Test ===");
        Console.WriteLine("Starting automated test sequence...\n");

        // Get subscription ID and zip file path from command line arguments
        if (args.Length < 2 || string.IsNullOrWhiteSpace(args[0]) || string.IsNullOrWhiteSpace(args[1]))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Error: Both subscription ID and zip file path are required.");
            Console.WriteLine("Usage: dotnet run <subscription-id> <zip-file-path>");
            Console.WriteLine("Example: dotnet run 12345678-abcd-1234-efgh-123456789012 C:\\MyProject\\SampleMarketingApp_Complete.zip");
            Console.ResetColor();
            Environment.Exit(1);
        }

        SubscriptionId = args[0];
        ZipFilePath = args[1];
        
        Console.WriteLine($"Using subscription ID: {SubscriptionId}");
        Console.WriteLine($"Using package: {ZipFilePath}");
        
        // Validate that package file exists
        if (!File.Exists(ZipFilePath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: Package file not found: {ZipFilePath}");
            Console.ResetColor();
            Environment.Exit(1);
        }

        try
        {
            await InitializeAsync();
            await RunTestSequenceAsync();
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

        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }

    private static async Task InitializeAsync()
    {
        Console.WriteLine("Initializing Azure client...");
        var credential = new DefaultAzureCredential();
        _armClient = new ArmClient(credential);

        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(300);

        Console.WriteLine("Initialization complete.\n");
    }

    private static async Task RunTestSequenceAsync()
    {
        Console.WriteLine("Starting test sequence...\n");

        // Step A: Create and deploy web app
        Console.WriteLine("Step A: Creating webapp in Azure App Service...");
        await CreateAndDeployWebAppAsync();

        // Step B: Test initial app health
        Console.WriteLine("\nStep B: Testing app health...");
        await TestAppHealthAsync();

        // Step C: Trigger SNAT exhaustion
        Console.WriteLine("\nStep C: Triggering SNAT exhaustion...");
        await TriggerSnatExhaustionAsync();

        // Step D: Wait for recovery
        Console.WriteLine("\nStep D: Waiting for 10 seconds...");
        await Task.Delay(10000);

        // Step E: Validate app recovery
        Console.WriteLine("\nStep E: Validating app recovery...");
        await TestAppHealthAsync();

        // Step F: Delete resources
        Console.WriteLine("\nStep F: Deleting app resources...");
        await DeleteResourcesAsync();

        Console.WriteLine("\nTest sequence completed successfully!");
    }

    private static async Task CreateAndDeployWebAppAsync()
    {
        try
        {            var subscription = await _armClient!.GetSubscriptionResource(new Azure.Core.ResourceIdentifier($"/subscriptions/{SubscriptionId}")).GetAsync();

            // Create resource group
            Console.WriteLine($"Creating resource group: {ResourceGroupName}");
            var resourceGroupData = new ResourceGroupData(Location);
            var resourceGroupOperation = await subscription.Value.GetResourceGroups()
                .CreateOrUpdateAsync(Azure.WaitUntil.Completed, ResourceGroupName, resourceGroupData);
            _resourceGroup = resourceGroupOperation.Value;

            // Create VNet and Subnet
            Console.WriteLine($"Creating VNet: {VnetName}");
            await CreateVNetAndSubnetAsync();

            // Create PostgreSQL server
            Console.WriteLine($"Creating PostgreSQL server: {PostgreSqlServerName}");
            await CreatePostgreSqlServerAsync();

            // Create App Service Plan
            Console.WriteLine($"Creating App Service Plan: {AppServicePlanName}");
            await CreateAppServicePlanAsync();

            // Create Web App
            Console.WriteLine($"Creating Web App: {WebAppName}");
            await CreateWebAppAsync();

            // Deploy application
            Console.WriteLine("Deploying application code...");
            await DeployApplicationAsync();

            _webAppUrl = $"https://{WebAppName}.azurewebsites.net";
            Console.WriteLine($"Web app created successfully: {_webAppUrl}");

            // Wait for deployment to complete
            Console.WriteLine("Waiting for deployment to complete...");
            await Task.Delay(60000); // Wait 60 seconds for deployment
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating web app: {ex.Message}");
            throw;
        }
    }

    private static async Task CreateVNetAndSubnetAsync()
    {
        var vnetData = new VirtualNetworkData()
        {
            Location = Location,
            AddressPrefixes = { "10.0.0.0/16" }
        };

        // App Service subnet
        var appSubnetData = new SubnetData()
        {
            Name = SubnetName,
            AddressPrefix = "10.0.1.0/24"
        };
        
        // Add delegation for App Service
        appSubnetData.Delegations.Add(new ServiceDelegation()
        {
            Name = "Microsoft.Web/serverFarms",
            ServiceName = "Microsoft.Web/serverFarms"
        });

        // PostgreSQL subnet - requires delegation to Microsoft.DBforPostgreSQL/flexibleServers
        var postgresSubnetData = new SubnetData()
        {
            Name = PostgreSqlSubnetName,
            AddressPrefix = "10.0.2.0/24"        };

        vnetData.Subnets.Add(appSubnetData);
        vnetData.Subnets.Add(postgresSubnetData);

        var vnetOperation = await _resourceGroup!.GetVirtualNetworks()
            .CreateOrUpdateAsync(Azure.WaitUntil.Completed, VnetName, vnetData);
        _vnet = vnetOperation.Value;        // Get subnet references
        var appSubnetResponse = await _vnet.GetSubnets().GetAsync(SubnetName);
        var postgresSubnetResponse = await _vnet.GetSubnets().GetAsync(PostgreSqlSubnetName);
        _appSubnet = appSubnetResponse.Value;
        _postgresSubnet = postgresSubnetResponse.Value;
        
        Console.WriteLine($"VNet created with app subnet: {_appSubnet.Data.AddressPrefix}");
        Console.WriteLine($"VNet created with PostgreSQL subnet: {_postgresSubnet.Data.AddressPrefix}");    }

    private static async Task CreatePostgreSqlServerAsync()
    {
        var postgreSqlServerData = new PostgreSqlFlexibleServerData(Location)
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
                PublicNetworkAccess = PostgreSqlFlexibleServerPublicNetworkAccessState.Disabled
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
        };        var serverOperation = await _resourceGroup!.GetPostgreSqlFlexibleServers().CreateOrUpdateAsync(
            Azure.WaitUntil.Completed, PostgreSqlServerName, postgreSqlServerData);
        _postgreSqlServer = serverOperation.Value;

        // Create database
        var databaseData = new PostgreSqlFlexibleServerDatabaseData();
        await _postgreSqlServer.GetPostgreSqlFlexibleServerDatabases().CreateOrUpdateAsync(
            Azure.WaitUntil.Completed, DatabaseName, databaseData);

        // Create private endpoint for PostgreSQL
        await CreatePostgreSqlPrivateEndpointAsync();

        Console.WriteLine($"PostgreSQL Flexible Server created with private endpoint: {_postgreSqlServer.Data.FullyQualifiedDomainName}");
    }    private static async Task CreatePostgreSqlPrivateEndpointAsync()
    {        // Create Private DNS Zone for PostgreSQL
        var privateDnsZoneData = new PrivateDnsZoneData("global");
        var privateDnsZoneName = "privatelink.postgres.database.azure.com";
        
        var dnsZoneOperation = await _resourceGroup!.GetPrivateDnsZones()
            .CreateOrUpdateAsync(Azure.WaitUntil.Completed, privateDnsZoneName, privateDnsZoneData);
        var privateDnsZone = dnsZoneOperation.Value;

        // Create Private Endpoint
        var privateEndpointData = new PrivateEndpointData()
        {
            Location = Location,
            Subnet = new SubnetData()
            {
                Id = _postgresSubnet!.Id
            },
            PrivateLinkServiceConnections =
            {
                new NetworkPrivateLinkServiceConnection()
                {
                    Name = $"{PostgreSqlServerName}-connection",
                    PrivateLinkServiceId = _postgreSqlServer!.Id,
                    GroupIds = { "postgresqlServer" }
                }
            }
        };

        var privateEndpointName = $"{PostgreSqlServerName}-pe";
        await _resourceGroup.GetPrivateEndpoints()
            .CreateOrUpdateAsync(Azure.WaitUntil.Completed, privateEndpointName, privateEndpointData);

        Console.WriteLine($"Private endpoint created for PostgreSQL server: {privateEndpointName}");
    }

    private static async Task CreateAppServicePlanAsync()
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

        await _resourceGroup!.GetAppServicePlans()
            .CreateOrUpdateAsync(Azure.WaitUntil.Completed, AppServicePlanName, appServicePlanData);
    }

    private static async Task CreateWebAppAsync()
    {
        var appServicePlans = _resourceGroup!.GetAppServicePlans();
        var appServicePlan = await appServicePlans.GetAsync(AppServicePlanName);

        var webAppData = new WebSiteData(Location)
        {
            AppServicePlanId = appServicePlan.Value.Id,
            Kind = "app,linux",
            SiteConfig = new SiteConfigProperties()
            {
                LinuxFxVersion = "PYTHON|3.11"
                // Note: VNet integration needs to be configured post-creation via Azure portal or REST API
            }
        };

        webAppData.SiteConfig.AppSettings.Add(new AppServiceNameValuePair() { Name = "SCM_DO_BUILD_DURING_DEPLOYMENT", Value = "true" });
        webAppData.SiteConfig.AppSettings.Add(new AppServiceNameValuePair() { Name = "DATABASE_URL", Value = GetDatabaseConnectionString() });
        webAppData.SiteConfig.AppSettings.Add(new AppServiceNameValuePair() { Name = "SECRET_KEY", Value = AdminPassword });        var webAppOperation = await _resourceGroup!.GetWebSites()
            .CreateOrUpdateAsync(Azure.WaitUntil.Completed, WebAppName, webAppData);
        var webApp = webAppOperation.Value;

        // Configure VNet integration via REST API
        await ConfigureVNetIntegrationAsync(webApp);

        Console.WriteLine($"Web app created and VNet integration configured for subnet: {SubnetName}");
    }

    private static async Task ConfigureVNetIntegrationAsync(WebSiteResource webApp)
    {
        try
        {
            var credential = new DefaultAzureCredential();
            var token = await credential.GetTokenAsync(new Azure.Core.TokenRequestContext(new[] { "https://management.azure.com/.default" }));

            // VNet integration REST API endpoint
            var vnetIntegrationUrl = $"https://management.azure.com{webApp.Id}/config/virtualNetwork?api-version=2022-03-01";

            var vnetConfig = new
            {
                properties = new
                {
                    subnetResourceId = _appSubnet!.Id.ToString(),
                    swiftSupported = true
                }
            };

            var jsonContent = JsonConvert.SerializeObject(vnetConfig);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);

            var response = await httpClient.PutAsync(vnetIntegrationUrl, content);

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("✓ VNet integration configured successfully");
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"⚠ VNet integration configuration failed: {response.StatusCode} - {errorContent}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠ Error configuring VNet integration: {ex.Message}");
        }
    }

    private static async Task DeployApplicationAsync()
    {
        try
        {
            var credential = new DefaultAzureCredential();
            var token = await credential.GetTokenAsync(new Azure.Core.TokenRequestContext(new[] { "https://management.azure.com/.default" }));

            var deployUrl = $"https://{WebAppName}.scm.azurewebsites.net/api/zipdeploy";

            using var fileStream = File.OpenRead(ZipFilePath);
            using var content = new StreamContent(fileStream);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");

            _httpClient!.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

            var response = await _httpClient.PostAsync(deployUrl, content);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"Deployment failed: {response.StatusCode} - {errorContent}");
            }

            Console.WriteLine("Application deployed successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deploying application: {ex.Message}");
            throw;
        }
    }

    private static async Task TestAppHealthAsync()
    {
        int maxRetries = 6;
        int retryDelay = 10000; // 10 seconds

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                Console.WriteLine($"Attempt {attempt}/{maxRetries}: Testing app health...");
                
                var response = await _httpClient!.GetAsync(_webAppUrl);
                
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"✓ App is healthy! Status code: {response.StatusCode}");
                    return;
                }
                else if ((int)response.StatusCode >= 500)
                {
                    Console.WriteLine($"⚠ App returned error status: {response.StatusCode}");
                    
                    if (attempt < maxRetries)
                    {
                        Console.WriteLine($"Waiting {retryDelay / 1000} seconds before retry...");
                        await Task.Delay(retryDelay);
                    }
                }
                else
                {
                    Console.WriteLine($"✓ App responded with status: {response.StatusCode}");
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠ Error testing app health: {ex.Message}");
                
                if (attempt < maxRetries)
                {
                    Console.WriteLine($"Waiting {retryDelay / 1000} seconds before retry...");
                    await Task.Delay(retryDelay);
                }
            }
        }

        throw new Exception("App health check failed after maximum retries");
    }

    private static async Task TriggerSnatExhaustionAsync()
    {
        try
        {
            var snatUrl = $"{_webAppUrl}/api/faults/snat";
            Console.WriteLine($"Making request to SNAT endpoint: {snatUrl}");

            var response = await _httpClient!.GetAsync(snatUrl);
            
            if ((int)response.StatusCode >= 500)
            {
                Console.WriteLine($"✓ SNAT exhaustion triggered successfully! Status code: {response.StatusCode}");
            }
            else
            {
                Console.WriteLine($"⚠ Expected error status (500+), but got: {response.StatusCode}");
                var content = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Response content: {content}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error triggering SNAT exhaustion: {ex.Message}");
            // This might be expected if the SNAT exhaustion causes connection issues
        }
    }

    private static async Task DeleteResourcesAsync()
    {
        try
        {
            Console.WriteLine($"Deleting resource group: {ResourceGroupName}");            var subscription = await _armClient!.GetSubscriptionResource(new Azure.Core.ResourceIdentifier($"/subscriptions/{SubscriptionId}")).GetAsync();
            var resourceGroup = await subscription.Value.GetResourceGroups().GetAsync(ResourceGroupName);
            
            await resourceGroup.Value.DeleteAsync(Azure.WaitUntil.Completed);
            
            Console.WriteLine("✓ Resources deleted successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting resources: {ex.Message}");
        }
    }

    private static string GetDatabaseConnectionString()
    {
        return $"postgresql://{AdminUsername}:{AdminPassword}@{PostgreSqlServerName}.postgres.database.azure.com:5432/{DatabaseName}?sslmode=require";
    }

    private static string GeneratePassword()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%";
        var random = new Random();
        var password = new StringBuilder();
        
        // Ensure password has at least one uppercase, lowercase, digit, and special char
        password.Append(chars[random.Next(0, 26)]); // uppercase
        password.Append(chars[random.Next(26, 52)]); // lowercase
        password.Append(chars[random.Next(52, 62)]); // digit
        password.Append(chars[random.Next(62, chars.Length)]); // special char
        
        // Fill remaining length
        for (int i = 4; i < 16; i++)
        {
            password.Append(chars[random.Next(chars.Length)]);
        }
        
        return password.ToString();
    }
}
