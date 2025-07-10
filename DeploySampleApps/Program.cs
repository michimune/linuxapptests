using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.AppService.Models;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Models;
using Azure.ResourceManager.OperationalInsights;
using Azure.ResourceManager.OperationalInsights.Models;
using Azure.ResourceManager.PostgreSql;
using Azure.ResourceManager.PostgreSql.FlexibleServers;
using Azure.ResourceManager.PostgreSql.FlexibleServers.Models;
using Azure.ResourceManager.PrivateDns;
using Azure.ResourceManager.PrivateDns.Models;
using Azure.ResourceManager.Resources;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace DeploySampleApps;

class Program
{    private static readonly string PrivateDnsZoneName = "privatelink.postgres.database.azure.com";
    private static readonly string DatabaseName = "marketingdb";
    private static readonly string DatabaseUser = "marketinguser";
    private static readonly string DatabasePassword = GenerateSecurePassword();

    private static ArmClient _armClient = null!;
    private static string SubscriptionId = null!;
    private static string Region = null!;
    private static string SampleAppBaseDir = null!;
    private static string ResourceName = null!;
    
    // Dynamic resource names based on user input
    private static string ResourceGroupName => $"rg-{ResourceName}";
    private static string VNetName => $"vnet-{ResourceName}";
    private static string SubnetName => "subnet-appservice";
    private static string PostgreSqlSubnetName => "subnet-postgresql";
    private static string AppServicePlanName => $"asp-{ResourceName}";
    private static string WebAppName => $"webapp-{ResourceName}";
    private static string WebApiAppName => $"webapi-{ResourceName}";
    private static string PostgreSqlServerName => $"psql-{ResourceName}";
    private static string PrivateEndpointName => $"pe-postgresql-{ResourceName}";
    private static string LogAnalyticsWorkspaceName => $"law-{ResourceName}";

    static async Task<int> Main(string[] args)
    {
        try
        {            // Validate command line arguments
            if (args.Length != 4)
            {
                Console.Error.WriteLine("Usage: DeploySampleApps <subscription-id> <region> <sample-app-base-dir> <resource-name>");
                Console.Error.WriteLine("Example: DeploySampleApps 12345678-1234-1234-1234-123456789012 eastus C:\\MyApps mymarketingapp");
                return 1;
            }

            SubscriptionId = args[0];
            Region = args[1];
            SampleAppBaseDir = args[2];
            ResourceName = args[3];            Console.WriteLine($"Starting deployment to subscription {SubscriptionId} in region {Region}");
            Console.WriteLine($"Using sample app base directory: {SampleAppBaseDir}");
            Console.WriteLine($"Using resource name: {ResourceName}");
            Console.WriteLine();
            Console.WriteLine("Generated resource names:");
            Console.WriteLine($"  Resource Group: {ResourceGroupName}");
            Console.WriteLine($"  VNet: {VNetName}");
            Console.WriteLine($"  App Service Plan: {AppServicePlanName}");
            Console.WriteLine($"  Web App: {WebAppName}");
            Console.WriteLine($"  Web API App: {WebApiAppName}");
            Console.WriteLine($"  PostgreSQL Server: {PostgreSqlServerName}");
            Console.WriteLine($"  Private Endpoint: {PrivateEndpointName}");
            Console.WriteLine($"  Log Analytics Workspace: {LogAnalyticsWorkspaceName}");
            Console.WriteLine($"  Database Password: {DatabasePassword}");
            Console.WriteLine();

            // Validate base directory exists
            if (!Directory.Exists(SampleAppBaseDir))
            {
                Console.Error.WriteLine($"Error: Sample app base directory not found: {SampleAppBaseDir}");
                return 1;
            }

            // Validate required directories exist under base directory
            var sampleMarketingAppPath = Path.Combine(SampleAppBaseDir, "SampleMarketingApp");
            var sampleMarketingAppBadPath = Path.Combine(SampleAppBaseDir, "SampleMarketingAppBad");

            if (!Directory.Exists(sampleMarketingAppPath))
            {
                Console.Error.WriteLine($"Error: SampleMarketingApp directory not found at: {sampleMarketingAppPath}");
                return 1;            }

            var zipDir = CreateSampleAppsZipFiles();
            // Add call to generate deploy script
            CreateDeployScript(args);

            // Create the batch script
            CreateBatchScript(zipDir);

            // Initialize Azure client
            _armClient = new ArmClient(new DefaultAzureCredential());

            // Validate authentication before proceeding
            await ValidateAzureAuthentication();

            // Deploy infrastructure
            await DeployInfrastructure();

            Console.WriteLine("Deployment completed successfully!");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }    }

    private static string CreateSampleAppsZipFiles()
    {
        // Validate required directories exist under base directory
        var sampleMarketingAppPath = Path.Combine(SampleAppBaseDir, "SampleMarketingApp");
        var sampleMarketingAppBadPath = Path.Combine(SampleAppBaseDir, "SampleMarketingAppBad");

        // Copy SampleMarketingApp to SampleMarketingAppBad and modify requirements.txt
        Console.WriteLine("Preparing SampleMarketingAppBad directory...");
        if (Directory.Exists(sampleMarketingAppBadPath))
        {
            Directory.Delete(sampleMarketingAppBadPath, true);
            Console.WriteLine($"Deleted existing SampleMarketingAppBad directory");
        }
        
        CopyDirectory(sampleMarketingAppPath, sampleMarketingAppBadPath);
        Console.WriteLine($"Copied SampleMarketingApp to SampleMarketingAppBad");
        
        // Modify requirements.txt in SampleMarketingAppBad
        var requirementsBadPath = Path.Combine(sampleMarketingAppBadPath, "requirements.txt");
        if (File.Exists(requirementsBadPath))
        {
            var lines = File.ReadAllLines(requirementsBadPath);
            if (lines.Length > 0)
            {
                // Remove the first line
                var modifiedLines = lines.Skip(1).ToArray();
                File.WriteAllLines(requirementsBadPath, modifiedLines);
                Console.WriteLine($"Removed first line from requirements.txt in SampleMarketingAppBad");
            }
        }

        // Create zip directory
        var zipDir = Path.GetFullPath(Path.Combine(SampleAppBaseDir, "zip"));
        if (!Directory.Exists(zipDir))
        {
            Directory.CreateDirectory(zipDir);
            Console.WriteLine($"Created zip directory: {zipDir}");
        }
        
        // Create zip files
        Console.WriteLine("Creating zip files...");
        var sampleMarketingAppZip = Path.Combine(zipDir, "SampleMarketingApp.zip");
        var sampleMarketingAppBadZip = Path.Combine(zipDir, "SampleMarketingAppBad.zip");
        var webApiAppZip = Path.Combine(zipDir, "WebApiApp.zip");
        
        CreateZipFile(sampleMarketingAppPath, sampleMarketingAppZip);
        CreateZipFile(sampleMarketingAppBadPath, sampleMarketingAppBadZip);
        
        // Create truncated version of SampleMarketingApp.zip
        var sampleMarketingAppTruncatedZip = Path.Combine(zipDir, "SampleMarketingAppTruncated.zip");
        CreateTruncatedZipFile(sampleMarketingAppZip, sampleMarketingAppTruncatedZip);
        
        // Create WebApiApp zip from published output
        var webApiAppPublishPath = Path.Combine(SampleAppBaseDir, "WebApiApp", "bin", "Release", "publish");
        if (Directory.Exists(webApiAppPublishPath))
        {
            CreateZipFile(webApiAppPublishPath, webApiAppZip);
        }
        else
        {
            Console.WriteLine($"Warning: WebApiApp publish directory not found at: {webApiAppPublishPath}");
            Console.WriteLine("Make sure to build and publish WebApiApp before running this deployment");
        }
        
        return zipDir;
    }

    private static void CreateZipFile(string sourceDirectory, string zipFileName)
    {
        if (File.Exists(zipFileName))
        {
            File.Delete(zipFileName);
        }

        ZipFile.CreateFromDirectory(sourceDirectory, zipFileName);
        Console.WriteLine($"Created {zipFileName}");
    }

    private static void CreateTruncatedZipFile(string sourceZipFileName, string truncatedZipFileName)
    {
        if (File.Exists(truncatedZipFileName))
        {
            File.Delete(truncatedZipFileName);
        }

        // Read the original zip file and create a truncated version (first half)
        var originalBytes = File.ReadAllBytes(sourceZipFileName);
        var truncatedBytes = new byte[originalBytes.Length / 2];
        Array.Copy(originalBytes, truncatedBytes, truncatedBytes.Length);
        
        File.WriteAllBytes(truncatedZipFileName, truncatedBytes);
        Console.WriteLine($"Created truncated zip file: {truncatedZipFileName} ({truncatedBytes.Length} bytes from {originalBytes.Length} bytes)");
    }

    private static async Task DeployInfrastructure()
    {
        try
        {
            // Get the subscription using correct targeting pattern
            var subscription = await _armClient.GetSubscriptionResource(
                new ResourceIdentifier($"/subscriptions/{SubscriptionId}")).GetAsync();
            
            // Access via subscription.Value for operations
            var subscriptionResource = subscription.Value;
            Console.WriteLine($"Successfully connected to subscription: {subscriptionResource.Data.DisplayName}");            // Create resource group
            var resourceGroup = await CreateResourceGroup(subscriptionResource);

            // Validate PostgreSQL configuration
            await ValidatePostgreSqlConfiguration(resourceGroup);

            // Create VNet with subnets
            var vnet = await CreateVirtualNetwork(resourceGroup);

            // Create Private DNS Zone
            var privateDnsZone = await CreatePrivateDnsZone(resourceGroup);

            // Create PostgreSQL Flexible Server
            var postgresServer = await CreatePostgreSqlServer(resourceGroup);

            // Create Private Endpoint for PostgreSQL
            await CreatePrivateEndpoint(resourceGroup, vnet, postgresServer, privateDnsZone);

            // Create App Service Plan
            var appServicePlan = await CreateAppServicePlan(resourceGroup);

            // Create Log Analytics Workspace
            var logAnalyticsWorkspace = await CreateLogAnalyticsWorkspace(resourceGroup);

            // Create Web App
            var webApp = await CreateWebApp(resourceGroup, appServicePlan);

            // Create Web API App
            var webApiApp = await CreateWebApiApp(resourceGroup, appServicePlan);

            // Configure diagnostic settings for Web App
            await ConfigureDiagnosticSettings(webApp, logAnalyticsWorkspace);

            // Configure diagnostic settings for Web API App
            await ConfigureDiagnosticSettings(webApiApp, logAnalyticsWorkspace);

            // Configure VNet integration for Web App
            await ConfigureVNetIntegration(webApp, vnet);

            // Configure VNet integration for Web API App
            await ConfigureVNetIntegration(webApiApp, vnet);

            // Deploy applications
            await DeployApplications(webApp);

            // Deploy Web API App
            await DeployWebApiApp(webApiApp);

            // Test production web app connectivity
            await TestProductionWebApp(webApp);

            // Test Web API App connectivity
            await TestWebApiApp(webApiApp);
        }
        catch (Azure.RequestFailedException azureEx)
        {
            Console.Error.WriteLine($"Azure API Error: {azureEx.ErrorCode} - {azureEx.Message}");
            throw;
        }
        catch (HttpRequestException httpEx)
        {
            Console.Error.WriteLine($"HTTP Request Error: {httpEx.Message}");
            throw;
        }
    }

    private static async Task<ResourceGroupResource> CreateResourceGroup(SubscriptionResource subscription)
    {
        Console.WriteLine("Creating resource group...");
        
        var resourceGroupData = new ResourceGroupData(Region);
        var resourceGroupLro = await subscription.GetResourceGroups()
            .CreateOrUpdateAsync(Azure.WaitUntil.Completed, ResourceGroupName, resourceGroupData);
        
        Console.WriteLine($"Created resource group: {ResourceGroupName}");
        return resourceGroupLro.Value;
    }

    private static async Task<VirtualNetworkResource> CreateVirtualNetwork(ResourceGroupResource resourceGroup)
    {
        Console.WriteLine("Creating virtual network...");

        var vnetData = new VirtualNetworkData()
        {
            Location = Region,
            AddressPrefixes = { "10.0.0.0/16" }
        };

        // App Service subnet with delegation
        var appSubnetData = new SubnetData()
        {
            Name = SubnetName,
            AddressPrefix = "10.0.1.0/24"
        };
        appSubnetData.Delegations.Add(new ServiceDelegation()
        {
            Name = "Microsoft.Web/serverFarms",
            ServiceName = "Microsoft.Web/serverFarms"
        });
        vnetData.Subnets.Add(appSubnetData);

        // PostgreSQL subnet (no delegation for private endpoints)
        var postgresSubnetData = new SubnetData()
        {
            Name = PostgreSqlSubnetName,
            AddressPrefix = "10.0.2.0/24"
        };
        vnetData.Subnets.Add(postgresSubnetData);

        var vnetLro = await resourceGroup.GetVirtualNetworks()
            .CreateOrUpdateAsync(Azure.WaitUntil.Completed, VNetName, vnetData);

        Console.WriteLine($"Created virtual network: {VNetName}");
        return vnetLro.Value;
    }    private static async Task<PrivateDnsZoneResource> CreatePrivateDnsZone(ResourceGroupResource resourceGroup)
    {
        Console.WriteLine("Creating private DNS zone...");

        var privateDnsZoneData = new PrivateDnsZoneData(new AzureLocation("global"));

        var privateDnsZoneLro = await resourceGroup.GetPrivateDnsZones()
            .CreateOrUpdateAsync(Azure.WaitUntil.Completed, PrivateDnsZoneName, privateDnsZoneData);

        Console.WriteLine($"Created private DNS zone: {PrivateDnsZoneName}");
        return privateDnsZoneLro.Value;
    }

    private static async Task<PostgreSqlFlexibleServerResource> CreatePostgreSqlServer(ResourceGroupResource resourceGroup)
    {
        Console.WriteLine("Creating PostgreSQL Flexible Server...");        var postgresData = new PostgreSqlFlexibleServerData(Region)
        {
            AdministratorLogin = DatabaseUser,
            AdministratorLoginPassword = DatabasePassword,
            Version = PostgreSqlFlexibleServerVersion.Ver14,
            Sku = new PostgreSqlFlexibleServerSku("Standard_B1ms", PostgreSqlFlexibleServerSkuTier.Burstable),
            Storage = new PostgreSqlFlexibleServerStorage()
            {
                StorageSizeInGB = 32
            },
            Network = new PostgreSqlFlexibleServerNetwork()
            {
                PublicNetworkAccess = PostgreSqlFlexibleServerPublicNetworkAccessState.Disabled
            }
        };

        var postgresLro = await resourceGroup.GetPostgreSqlFlexibleServers()
            .CreateOrUpdateAsync(Azure.WaitUntil.Completed, PostgreSqlServerName, postgresData);

        Console.WriteLine($"Created PostgreSQL server: {PostgreSqlServerName}");
        
        var postgresServer = postgresLro.Value;
        
        // Create the marketingdb database
        Console.WriteLine("Creating marketingdb database...");
        var databaseData = new PostgreSqlFlexibleServerDatabaseData()
        {
            Charset = "UTF8",
            Collation = "en_US.utf8"
        };
        
        var databaseLro = await postgresServer.GetPostgreSqlFlexibleServerDatabases()
            .CreateOrUpdateAsync(Azure.WaitUntil.Completed, DatabaseName, databaseData);
            
        Console.WriteLine($"Created database: {DatabaseName}");
        
        return postgresServer;
    }

    private static async Task CreatePrivateEndpoint(ResourceGroupResource resourceGroup, 
        VirtualNetworkResource vnet, PostgreSqlFlexibleServerResource postgresServer, 
        PrivateDnsZoneResource privateDnsZone)
    {
        Console.WriteLine("Creating private endpoint...");

        // Get the PostgreSQL subnet
        var postgresSubnet = await vnet.GetSubnetAsync(PostgreSqlSubnetName);

        var privateEndpointData = new PrivateEndpointData()
        {
            Location = Region,
            Subnet = new SubnetData() { Id = postgresSubnet.Value.Data.Id },
            PrivateLinkServiceConnections =
            {
                new NetworkPrivateLinkServiceConnection()
                {
                    Name = "postgresql-connection",
                    PrivateLinkServiceId = postgresServer.Data.Id,
                    GroupIds = { "postgresqlServer" }
                }
            }
        };        var privateEndpointLro = await resourceGroup.GetPrivateEndpoints()
            .CreateOrUpdateAsync(Azure.WaitUntil.Completed, PrivateEndpointName, privateEndpointData);

        var privateEndpoint = privateEndpointLro.Value;

        // Add A record to private DNS zone with TTL of 3600 seconds
        Console.WriteLine("Creating DNS A record for private endpoint...");
        
        try
        {
            // Get the private IP address from the private endpoint
            var networkInterface = privateEndpoint.Data.NetworkInterfaces?.FirstOrDefault();
            if (networkInterface != null)
            {
                // Get the network interface resource to access the private IP
                var nicResourceId = networkInterface.Id;
                var nicResource = await _armClient.GetNetworkInterfaceResource(nicResourceId).GetAsync();
                
                var privateIpAddress = nicResource.Value.Data.IPConfigurations?.FirstOrDefault()?.PrivateIPAddress;
                
                if (!string.IsNullOrEmpty(privateIpAddress))
                {                    // Create A record with PostgreSQL server name
                    // Note: Using simplified approach due to API changes
                    var aRecordData = new PrivateDnsARecordData()
                    {
                        TtlInSeconds = 3600
                    };
                    
                    // Add the IP address to the A record
                    aRecordData.PrivateDnsARecords.Add(new PrivateDnsARecordInfo() 
                    { 
                        IPv4Address = System.Net.IPAddress.Parse(privateIpAddress) 
                    });

                    await privateDnsZone.GetPrivateDnsARecords()
                        .CreateOrUpdateAsync(Azure.WaitUntil.Completed, PostgreSqlServerName, aRecordData);
                    
                    Console.WriteLine($"Created DNS A record: {PostgreSqlServerName} -> {privateIpAddress} (TTL: 3600s)");
                }
                else
                {
                    Console.WriteLine("Warning: Could not retrieve private IP address for DNS record");
                }
            }
            else
            {
                Console.WriteLine("Warning: No network interface found for private endpoint");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not create DNS A record: {ex.Message}");
        }        // Link private DNS zone to VNet with auto-registration enabled via REST API
        Console.WriteLine("Linking VNet to private DNS zone with auto-registration enabled...");
        
        try
        {
            await LinkVNetToPrivateDnsZone(privateDnsZone, vnet);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not link VNet to private DNS zone: {ex.Message}");
            Console.WriteLine("Manual configuration may be required in the Azure portal.");
        }

        Console.WriteLine($"Created private endpoint: {PrivateEndpointName}");
    }

    private static async Task<AppServicePlanResource> CreateAppServicePlan(ResourceGroupResource resourceGroup)
    {
        Console.WriteLine("Creating App Service Plan...");

        var appServicePlanData = new AppServicePlanData(Region)
        {
            Sku = new AppServiceSkuDescription()
            {
                Name = "S1",
                Tier = "Standard",
                Capacity = 1
            },
            Kind = "linux",
            IsReserved = true // Linux App Service Plan
        };

        var appServicePlanLro = await resourceGroup.GetAppServicePlans()
            .CreateOrUpdateAsync(Azure.WaitUntil.Completed, AppServicePlanName, appServicePlanData);

        Console.WriteLine($"Created App Service Plan: {AppServicePlanName}");
        return appServicePlanLro.Value;
    }

    private static async Task<OperationalInsightsWorkspaceResource> CreateLogAnalyticsWorkspace(ResourceGroupResource resourceGroup)
    {
        Console.WriteLine("Creating Log Analytics Workspace...");

        var workspaceData = new OperationalInsightsWorkspaceData(Region)
        {
            Sku = new OperationalInsightsWorkspaceSku(OperationalInsightsWorkspaceSkuName.PerGB2018),
            RetentionInDays = 30,
            WorkspaceCapping = new OperationalInsightsWorkspaceCapping()
            {
                DailyQuotaInGB = 1.0
            }
        };

        var workspaceLro = await resourceGroup.GetOperationalInsightsWorkspaces()
            .CreateOrUpdateAsync(Azure.WaitUntil.Completed, LogAnalyticsWorkspaceName, workspaceData);

        Console.WriteLine($"Created Log Analytics Workspace: {LogAnalyticsWorkspaceName}");
        return workspaceLro.Value;
    }

    private static async Task<WebSiteResource> CreateWebApp(ResourceGroupResource resourceGroup, 
        AppServicePlanResource appServicePlan)
    {
        Console.WriteLine("Creating Web App...");        var webAppData = new WebSiteData(Region)
        {
            AppServicePlanId = appServicePlan.Data.Id,
            SiteConfig = new SiteConfigProperties()
            {
                LinuxFxVersion = "PYTHON|3.11",
                AppSettings = CreateWebAppSettings()
            },
            IsHttpsOnly = true
        };

        var webAppLro = await resourceGroup.GetWebSites()
            .CreateOrUpdateAsync(Azure.WaitUntil.Completed, WebAppName, webAppData);

        Console.WriteLine($"Created Web App: {WebAppName}");
        return webAppLro.Value;
    }

    private static async Task<WebSiteResource> CreateWebApiApp(ResourceGroupResource resourceGroup, 
        AppServicePlanResource appServicePlan)
    {
        Console.WriteLine("Creating Web API App...");

        var webApiAppData = new WebSiteData(Region)
        {
            AppServicePlanId = appServicePlan.Data.Id,
            SiteConfig = new SiteConfigProperties()
            {
                LinuxFxVersion = "DOTNETCORE|8.0", // .NET 8.0
                AppSettings = CreateWebApiAppSettings()
            },
            IsHttpsOnly = true
        };

        var webApiAppLro = await resourceGroup.GetWebSites()
            .CreateOrUpdateAsync(Azure.WaitUntil.Completed, WebApiAppName, webApiAppData);

        Console.WriteLine($"Created Web API App: {WebApiAppName}");
        return webApiAppLro.Value;
    }

    private static async Task ConfigureDiagnosticSettings(WebSiteResource webApp, OperationalInsightsWorkspaceResource logAnalyticsWorkspace)
    {
        Console.WriteLine($"Configuring diagnostic settings for {webApp.Data.Name}...");

        try
        {
            using var httpClient = new HttpClient();
            
            // Use Azure Management token
            var token = await GetAzureManagementTokenAsync();
            httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Create diagnostic settings data structure
            var diagnosticSettingsData = new
            {
                properties = new
                {
                    workspaceId = logAnalyticsWorkspace.Id.ToString(),
                    logs = new[]
                    {
                        new
                        {
                            categoryGroup = "allLogs",
                            enabled = true,
                            retentionPolicy = new
                            {
                                enabled = false,
                                days = 0
                            }
                        }
                    },
                    metrics = new[]
                    {
                        new
                        {
                            category = "AllMetrics",
                            enabled = true,
                            retentionPolicy = new
                            {
                                enabled = false,
                                days = 0
                            }
                        }
                    }
                }
            };

            var jsonContent = JsonSerializer.Serialize(diagnosticSettingsData);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            // Use the diagnostic settings API endpoint
            var diagnosticSettingsName = "default";
            var apiUrl = $"https://management.azure.com{webApp.Id}/providers/Microsoft.Insights/diagnosticSettings/{diagnosticSettingsName}?api-version=2021-05-01-preview";

            var response = await httpClient.PutAsync(apiUrl, content);

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"✓ Configured diagnostic settings for {webApp.Data.Name}");
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Warning: Failed to configure diagnostic settings for {webApp.Data.Name}");
                Console.WriteLine($"Status: {response.StatusCode}, Error: {errorContent}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not configure diagnostic settings for {webApp.Data.Name}: {ex.Message}");
        }
    }

    private static async Task ConfigureVNetIntegration(WebSiteResource webApp, VirtualNetworkResource vnet)
    {
        Console.WriteLine("Configuring Swift VNet integration...");

        try
        {            // Get the app service subnet
            var appSubnet = await vnet.GetSubnetAsync(SubnetName);

            // Configure Swift VNet integration using Azure Management REST API
            using var httpClient = new HttpClient();
            
            // Use Azure Management token
            var token = await GetAzureManagementTokenAsync();
            httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Use the Swift VNet connection API structure
            var swiftVnetConnectionData = new
            {
                properties = new
                {
                    subnetResourceId = appSubnet.Value.Data.Id.ToString(), // Subnet resource ID is required for Swift connection
                    swiftSupported = true // Flag that specifies if the scale unit supports Swift integration
                }
            };

            var json = JsonSerializer.Serialize(swiftVnetConnectionData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Use the Swift VNet connection endpoint
            var apiUrl = $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Web/sites/{webApp.Data.Name}/networkConfig/virtualNetwork?api-version=2024-11-01";
            var response = await httpClient.PutAsync(apiUrl, content);

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("✓ Swift VNet integration configured successfully");
                Console.WriteLine($"✓ App Service integrated with subnet: {SubnetName}");
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Warning: Swift VNet integration failed. Status: {response.StatusCode}");
                Console.WriteLine($"Error details: {errorContent}");
                Console.WriteLine("Note: Ensure the subnet has delegation to Microsoft.Web/serverFarms");
            }        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not configure Swift VNet integration: {ex.Message}");
        }
    }

    private static async Task DeployApplications(WebSiteResource webApp)
    {
        Console.WriteLine("Deploying applications...");

        // Deploy main application
        var sampleMarketingAppZip = Path.Combine(SampleAppBaseDir, "zip", "SampleMarketingApp.zip");
        await DeployZipFile(webApp, sampleMarketingAppZip, isProduction: true);
        
        // Restart production slot after deployment
        await RestartWebApp(webApp, isProduction: true);

        // Create staging slot
        var stagingSlotData = new WebSiteData(Region)
        {
            AppServicePlanId = webApp.Data.AppServicePlanId,
            SiteConfig = webApp.Data.SiteConfig
        };

        var stagingSlotLro = await webApp.GetWebSiteSlots()
            .CreateOrUpdateAsync(Azure.WaitUntil.Completed, "staging", stagingSlotData);

        Console.WriteLine("Created staging slot");        // Deploy bad application to staging slot
        var sampleMarketingAppBadZip = Path.Combine(SampleAppBaseDir, "zip", "SampleMarketingAppBad.zip");
        await DeployZipFile(webApp, sampleMarketingAppBadZip, isProduction: false);
        
        // Restart staging slot after deployment
        await RestartWebApp(webApp, isProduction: false);

        Console.WriteLine("Applications deployed successfully");
    }

    private static async Task DeployWebApiApp(WebSiteResource webApiApp)
    {
        Console.WriteLine("Deploying Web API App...");

        // Deploy Web API application
        var webApiAppZip = Path.Combine(SampleAppBaseDir, "zip", "WebApiApp.zip");
        if (File.Exists(webApiAppZip))
        {
            await DeployZipFile(webApiApp, webApiAppZip, isProduction: true);
            
            // Restart Web API App after deployment
            await RestartWebApp(webApiApp, isProduction: true);
            
            Console.WriteLine("Web API App deployed successfully");
        }
        else
        {
            Console.WriteLine($"Warning: WebApiApp.zip not found at: {webApiAppZip}");
            Console.WriteLine("Skipping Web API App deployment");
        }
    }

    private static async Task DeployZipFile(WebSiteResource webApp, string zipFileName, bool isProduction)
    {
        Console.WriteLine($"Deploying {zipFileName} to {(isProduction ? "production" : "staging")}...");

        const int maxRetries = 5;
        const int retryDelaySeconds = 30;
        
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                using var httpClient = new HttpClient();
                // 300-second timeout for large deployments
                httpClient.Timeout = TimeSpan.FromSeconds(300);

                // Use Azure Management token instead of basic auth
                var token = await GetAzureManagementTokenAsync();
                httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                // Read zip file
                var zipBytes = await File.ReadAllBytesAsync(zipFileName);
                var content = new ByteArrayContent(zipBytes);
                // Set Content-Type to application/zip
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");

                // Deploy using ZIP API
                var webAppName = isProduction ? webApp.Data.Name : webApp.Data.Name + "-staging";
                var deployUrl = $"https://{webAppName}.scm.azurewebsites.net/api/zipdeploy";

                Console.WriteLine($"Deploying to: {deployUrl} (attempt {attempt}/{maxRetries})");
                var response = await httpClient.PostAsync(deployUrl, content);
                
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"✓ Successfully deployed {zipFileName}");
                    return; // Success - exit the retry loop
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Deployment failed with status {response.StatusCode}: {errorContent}");
                    
                    if (attempt < maxRetries)
                    {
                        Console.WriteLine($"Retrying in {retryDelaySeconds} seconds... (attempt {attempt + 1}/{maxRetries})");
                        await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds));
                    }
                    else
                    {
                        Console.WriteLine($"Warning: Deployment failed after {maxRetries} attempts for {zipFileName}");
                    }
                }
            }
            catch (TaskCanceledException tcEx) when (tcEx.InnerException is TimeoutException)
            {
                Console.WriteLine($"Deployment timed out for {zipFileName} on attempt {attempt}");
                
                if (attempt < maxRetries)
                {
                    Console.WriteLine($"Retrying in {retryDelaySeconds} seconds... (attempt {attempt + 1}/{maxRetries})");
                    await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds));
                }
                else
                {
                    Console.WriteLine($"Warning: Deployment timed out after {maxRetries} attempts. This may be normal for large applications.");
                }
            }
            catch (HttpRequestException httpEx)
            {
                Console.WriteLine($"HTTP error during deployment of {zipFileName} on attempt {attempt}: {httpEx.Message}");
                
                if (attempt < maxRetries)
                {
                    Console.WriteLine($"Retrying in {retryDelaySeconds} seconds... (attempt {attempt + 1}/{maxRetries})");
                    await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds));
                }
                else
                {
                    Console.WriteLine($"Warning: HTTP error after {maxRetries} attempts for {zipFileName}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during deployment of {zipFileName} on attempt {attempt}: {ex.Message}");
                
                if (attempt < maxRetries)
                {
                    Console.WriteLine($"Retrying in {retryDelaySeconds} seconds... (attempt {attempt + 1}/{maxRetries})");
                    await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds));
                }
                else
                {
                    Console.WriteLine($"Warning: Could not deploy {zipFileName} after {maxRetries} attempts");
                }
            }
        }    }

    private static void CreateBatchScript(string zipDir)
    {
        Console.WriteLine("Creating batch script...");
          var batchScript = $@"@echo off
echo Running BadScenarioLinux for {ResourceName}
dotnet run --project BadScenarioLinux\BadScenarioLinux.csproj {SubscriptionId} {ResourceName} {zipDir}
";

        var batchScriptPath = Path.Combine(SampleAppBaseDir, "badapps.bat");
        File.WriteAllText(batchScriptPath, batchScript);
        Console.WriteLine($"Created badapps.bat at: {batchScriptPath}");
    }

    private static void CreateDeployScript(string[] args)
    {
        Console.WriteLine("Creating deploy script...");
        var deployScript = $@"@echo off
" +
                        $"echo Deploying infrastructure for {ResourceName}\r\n" +
                        $"dotnet run --project DeploySampleApps\\DeploySampleApps.csproj {string.Join(" ", args)}\r\n";
        var deployScriptPath = Path.Combine(SampleAppBaseDir, "deploy.bat");
        File.WriteAllText(deployScriptPath, deployScript);
        Console.WriteLine($"Created deploy.bat at: {deployScriptPath}");
    }

    private static async Task<string> GetAzureManagementTokenAsync()
    {
        try
        {
            // Authentication Pattern - DefaultAzureCredential with proper token scope
            var credential = new DefaultAzureCredential();
            var token = await credential.GetTokenAsync(
                new Azure.Core.TokenRequestContext(new[] { "https://management.azure.com/.default" }));
            
            return token.Token;
        }
        catch (Azure.Identity.AuthenticationFailedException authEx)
        {
            Console.Error.WriteLine($"Authentication failed: {authEx.Message}");
            Console.Error.WriteLine("Please ensure you are logged in via Azure CLI: az login");
            throw;
        }
    }

    private static async Task LinkVNetToPrivateDnsZone(PrivateDnsZoneResource privateDnsZone, VirtualNetworkResource vnet)
    {
        Console.WriteLine($"Creating VNet link to private DNS zone via REST API...");
        
        try
        {
            var token = await GetAzureManagementTokenAsync();
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            
            var linkName = $"link-{VNetName}";
            var requestUri = $"https://management.azure.com{privateDnsZone.Id}/virtualNetworkLinks/{linkName}?api-version=2020-06-01";
            
            var linkData = new
            {
                location = "Global",
                properties = new
                {
                    virtualNetwork = new
                    {
                        id = vnet.Data.Id.ToString()
                    },
                    registrationEnabled = true  // Enables auto-registration and fallback to internet
                }
            };
            
            var jsonContent = JsonSerializer.Serialize(linkData);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            
            var response = await httpClient.PutAsync(requestUri, content);
            
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"✓ VNet '{VNetName}' successfully linked to Private DNS Zone '{PrivateDnsZoneName}'");
                Console.WriteLine("✓ Auto-registration enabled (enables fallback to internet DNS resolution)");
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Failed to create VNet link. Status: {response.StatusCode}");
                Console.WriteLine($"Error: {errorContent}");
                throw new Exception($"VNet linking failed with status {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error linking VNet to private DNS zone: {ex.Message}");
            throw;
        }
    }

    private static async Task ValidateAzureAuthentication()
    {
        Console.WriteLine("Validating Azure authentication...");
        try
        {
            var token = await GetAzureManagementTokenAsync();
            Console.WriteLine("✓ Azure authentication successful");
        }
        catch (Exception)
        {
            Console.Error.WriteLine("✗ Azure authentication failed");
            throw;
        }
    }

    private static string GetDatabaseConnectionString()
    {
        // Environment Variables pattern - build connection string
        return $"postgresql://{DatabaseUser}:{DatabasePassword}@{PostgreSqlServerName}.privatelink.postgres.database.azure.com:5432/{DatabaseName}";
    }

    private static List<AppServiceNameValuePair> CreateWebAppSettings()
    {
        // Environment Variables configuration using proper pattern
        var appSettings = new List<AppServiceNameValuePair>();
        
        appSettings.Add(new AppServiceNameValuePair() 
        { 
            Name = "DATABASE_URL", 
            Value = GetDatabaseConnectionString() 
        });
        
        appSettings.Add(new AppServiceNameValuePair() 
        { 
            Name = "SECRET_KEY", 
            Value = DatabasePassword 
        });
        
        appSettings.Add(new AppServiceNameValuePair() 
        { 
            Name = "SCM_DO_BUILD_DURING_DEPLOYMENT", 
            Value = "true" 
        });

        // Additional settings for better error handling
        appSettings.Add(new AppServiceNameValuePair() 
        { 
            Name = "WEBSITES_ENABLE_APP_SERVICE_STORAGE", 
            Value = "false" 
        });

        appSettings.Add(new AppServiceNameValuePair() 
        { 
            Name = "PRODUCTS_ENABLED", 
            Value = "1" 
        });

        appSettings.Add(new AppServiceNameValuePair() 
        { 
            Name = "WEBAPI_URL", 
            Value = $"https://{WebApiAppName}.azurewebsites.net" 
        });

        return appSettings;
    }

    private static List<AppServiceNameValuePair> CreateWebApiAppSettings()
    {
        // Environment Variables configuration for Web API App
        var appSettings = new List<AppServiceNameValuePair>();
        
        appSettings.Add(new AppServiceNameValuePair() 
        { 
            Name = "DATABASE_URL", 
            Value = GetDatabaseConnectionString() 
        });
        
        appSettings.Add(new AppServiceNameValuePair() 
        { 
            Name = "ASPNETCORE_ENVIRONMENT", 
            Value = "Production" 
        });

        appSettings.Add(new AppServiceNameValuePair() 
        { 
            Name = "WEBSITES_ENABLE_APP_SERVICE_STORAGE", 
            Value = "false" 
        });

        appSettings.Add(new AppServiceNameValuePair() 
        { 
            Name = "APP_VALUE", 
            Value = "abcde" 
        });

        return appSettings;
    }

    private static async Task ValidatePostgreSqlConfiguration(ResourceGroupResource resourceGroup)
    {
        Console.WriteLine("Validating PostgreSQL configuration...");
        
        try
        {
            // Check if Private DNS zone exists to avoid "Empty Private DNS Zone Error"
            var privateDnsZones = resourceGroup.GetPrivateDnsZones();
            var dnsZoneExists = await privateDnsZones.ExistsAsync(PrivateDnsZoneName);
            
            if (!dnsZoneExists.Value)
            {
                Console.WriteLine($"Private DNS zone {PrivateDnsZoneName} will be created for private endpoint connectivity");
            }
            else
            {
                Console.WriteLine($"Private DNS zone {PrivateDnsZoneName} already exists");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not validate PostgreSQL configuration: {ex.Message}");
        }
    }

    // Note: CS1998 warning can be safely ignored for initialization methods
    // that may not require async operations in all implementations
    #pragma warning disable CS1998
    private static async Task<bool> ValidateResourceNaming()
    {
        // Build Warning: CS1998 - Async method without await
        // This is expected for initialization methods that may not require async operations
        Console.WriteLine("Validating resource naming conventions...");
        
        var resourceNames = new[]
        {
            ResourceGroupName,
            VNetName, 
            AppServicePlanName,
            WebAppName,
            PostgreSqlServerName
        };

        foreach (var name in resourceNames)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 60)
            {
                Console.Error.WriteLine($"Invalid resource name: {name}");
                return false;
            }
        }        
        return true;
    }    private static async Task RestartWebApp(WebSiteResource webApp, bool isProduction)
    {
        try
        {
            var targetName = isProduction ? "production" : "staging";
            Console.WriteLine($"Restarting {targetName} slot...");

            if (isProduction)
            {
                // Restart the main web app
                await webApp.RestartAsync();
                Console.WriteLine("✓ Production slot restarted successfully");
            }
            else
            {
                // Restart the staging slot using REST API
                await RestartStagingSlotViaApi(webApp);
                Console.WriteLine("✓ Staging slot restarted successfully");
            }
        }
        catch (Exception ex)
        {
            var targetName = isProduction ? "production" : "staging";
            Console.WriteLine($"Warning: Could not restart {targetName} slot: {ex.Message}");
        }
    }

    private static async Task RestartStagingSlotViaApi(WebSiteResource webApp)
    {
        using var httpClient = new HttpClient();
        
        // Use Azure Management token
        var token = await GetAzureManagementTokenAsync();
        httpClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Restart staging slot using Azure Management API
        var restartUrl = $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Web/sites/{webApp.Data.Name}/slots/staging/restart?api-version=2024-11-01";
        
        var response = await httpClient.PostAsync(restartUrl, null);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"Failed to restart staging slot. Status: {response.StatusCode}, Error: {errorContent}");        }
    }

    private static async Task TestProductionWebApp(WebSiteResource webApp)
    {
        Console.WriteLine("Testing production web app connectivity...");
        
        var webAppUrl = $"https://{webApp.Data.DefaultHostName}";
        const int maxAttempts = 5;
        const int delaySeconds = 10;
        
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(30);
        
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                Console.WriteLine($"Attempt {attempt}/{maxAttempts}: Testing {webAppUrl}");
                
                var response = await httpClient.GetAsync(webAppUrl);
                
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"✓ Production web app is responding successfully (Status: {response.StatusCode})");
                    return;
                }
                else
                {
                    Console.WriteLine($"⚠ Web app responded with status: {response.StatusCode}");
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"⚠ HTTP request failed on attempt {attempt}: {ex.Message}");
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                Console.WriteLine($"⚠ Request timed out on attempt {attempt}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠ Unexpected error on attempt {attempt}: {ex.Message}");
            }
            
            if (attempt < maxAttempts)
            {
                Console.WriteLine($"Waiting {delaySeconds} seconds before next attempt...");
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
            }
        }
        
        Console.WriteLine($"⚠ Production web app did not respond successfully after {maxAttempts} attempts");
        Console.WriteLine("Note: This may be normal if the application is still starting up");
    }

    private static async Task TestWebApiApp(WebSiteResource webApiApp)
    {
        Console.WriteLine("Testing Web API App connectivity...");
        
        var webApiAppUrl = $"https://{webApiApp.Data.DefaultHostName}";
        const int maxAttempts = 5;
        const int delaySeconds = 10;
        
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(30);
        
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                Console.WriteLine($"Attempt {attempt}/{maxAttempts}: Testing {webApiAppUrl}");
                
                var response = await httpClient.GetAsync(webApiAppUrl);
                
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"✓ Web API App is responding successfully (Status: {response.StatusCode})");
                    return;
                }
                else
                {
                    Console.WriteLine($"⚠ Web API App responded with status: {response.StatusCode}");
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"⚠ HTTP request failed on attempt {attempt}: {ex.Message}");
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                Console.WriteLine($"⚠ Request timed out on attempt {attempt}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠ Unexpected error on attempt {attempt}: {ex.Message}");
            }
            
            if (attempt < maxAttempts)
            {
                Console.WriteLine($"Waiting {delaySeconds} seconds before next attempt...");
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
            }
        }
        
        Console.WriteLine($"⚠ Web API App did not respond successfully after {maxAttempts} attempts");
        Console.WriteLine("Note: This may be normal if the application is still starting up");
    }

    private static string GenerateSecurePassword()
    {
        // Generate a secure random password for PostgreSQL
        // Requirements: at least 8 characters, contains upper, lower, digit, and special character
        const string upperCase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string lowerCase = "abcdefghijklmnopqrstuvwxyz";
        const string digits = "0123456789";
        const string specialChars = "!#$%^&*";
        
        var random = new Random();
        var password = new StringBuilder();
        
        // Ensure at least one character from each category
        password.Append(upperCase[random.Next(upperCase.Length)]);
        password.Append(lowerCase[random.Next(lowerCase.Length)]);
        password.Append(digits[random.Next(digits.Length)]);
        password.Append(specialChars[random.Next(specialChars.Length)]);
        
        // Fill the rest with random characters from all categories
        const string allChars = upperCase + lowerCase + digits + specialChars;
        for (int i = 4; i < 16; i++) // Total length: 16 characters
        {
            password.Append(allChars[random.Next(allChars.Length)]);
        }
        
        // Shuffle the password to avoid predictable pattern
        var chars = password.ToString().ToCharArray();
        for (int i = chars.Length - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
          return new string(chars);
    }

    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        // Create target directory if it doesn't exist
        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        // Copy all files
        foreach (string file in Directory.GetFiles(sourceDir))
        {
            string fileName = Path.GetFileName(file);
            string targetFile = Path.Combine(targetDir, fileName);
            File.Copy(file, targetFile, true);
        }

        // Copy all subdirectories recursively
        foreach (string subDir in Directory.GetDirectories(sourceDir))
        {
            string dirName = Path.GetFileName(subDir);
            string targetSubDir = Path.Combine(targetDir, dirName);
            CopyDirectory(subDir, targetSubDir);
        }
    }
}
