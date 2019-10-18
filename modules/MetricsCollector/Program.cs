namespace MetricsCollector
{
    using System;
    using System.Linq;
    using System.Runtime.Loader;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.WindowsAzure.Storage.Auth;
    using Newtonsoft.Json;

    internal class Program
    {
        private static readonly Version ExpectedSchemaVersion = new Version("1.0");

        private static void Main(string[] args)
        {
            // Wait until the app unloads or is cancelled
            var cts = new CancellationTokenSource();
            AssemblyLoadContext.Default.Unloading += ctx => cts.Cancel();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();

            Init(cts.Token).Wait();
        }

        /// <summary>
        ///     Initializes the ModuleClient and sets up the callback to receive
        ///     messages containing temperature information
        /// </summary>
        private static async Task<Worker> Init(CancellationToken cancellationToken)
        {
            var mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            ITransportSettings[] settings = { mqttSetting };

            // Open a connection to the Edge runtime
            var ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
            await ioTHubModuleClient.OpenAsync();
            Console.WriteLine("IoT Hub module client initialized.");

            var configuration = await GetConfiguration(ioTHubModuleClient);
            Console.WriteLine($"Obtained configuration: {configuration}");

            var identifier = Environment.GetEnvironmentVariable("MessageIdentifier") ?? "IoTEdgeMetrics";
            Console.WriteLine($"Using message identifier {identifier}");

            var messageFormatter = new MetricsParser();
            var scraper = new Scraper(configuration.Endpoints.Values.ToList());
            var storage = new FileStorage(@"\data");

            IMetricsUpload metricsSync;
            if (configuration.SyncTarget == SyncTarget.AzureLogAnalytics)
            {
                string workspaceId = Environment.GetEnvironmentVariable("AzMonWorkspaceId") ??
                    Environment.GetEnvironmentVariable("azMonWorkspaceId") ?? // Workaround for IoT Edge k8s bug
                    throw new Exception("AzMonWorkspaceId env var not set!");

                string wKey = Environment.GetEnvironmentVariable("AzMonWorkspaceKey") ??
                    Environment.GetEnvironmentVariable("azMonWorkspaceKey") ??
                    throw new Exception("AzMonWorkspaceKey env var not set!");

                string clName = Environment.GetEnvironmentVariable("AzMonCustomLogName") ??
                    Environment.GetEnvironmentVariable("azMonCustomLogName") ??
                    "promMetrics";

                metricsSync = new LogAnalyticsMetricsUpload(messageFormatter, scraper, new AzureLogAnalytics(workspaceId, wKey, clName));
            }
            else
            {
                metricsSync = new IoTHubMetricsUpload(messageFormatter, scraper, ioTHubModuleClient);
            }

            // for testing
            metricsSync = new StdoutUploader();

            var scrapingInterval = TimeSpan.FromSeconds(configuration.ScrapeFrequencySecs);
            var uploadInterval = TimeSpan.FromSeconds(configuration.ScrapeFrequencySecs * 2);

            var worker = new Worker(scraper, storage, metricsSync);
            await worker.Start(scrapingInterval, uploadInterval, cancellationToken);
        }

        private static async Task<Configuration> GetConfiguration(ModuleClient ioTHubModuleClient)
        {
            var twin = await ioTHubModuleClient.GetTwinAsync();
            var desiredPropertiesJson = twin.Properties.Desired.ToJson();
            var configuration = JsonConvert.DeserializeObject<Configuration>(desiredPropertiesJson);
            if (ExpectedSchemaVersion.CompareMajorVersion(configuration.SchemaVersion, "logs upload request schema") !=
                0)
                throw new InvalidOperationException($"Payload schema version is not valid - {desiredPropertiesJson}");
            return configuration;
        }
    }
}