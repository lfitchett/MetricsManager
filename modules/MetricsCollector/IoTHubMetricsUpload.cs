namespace MetricsCollector
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;

    public class IoTHubMetricsUpload : IMetricsUpload
    {
        readonly MessageFormatter messageFormatter;
        readonly Scraper scraper;
        readonly ModuleClient moduleClient;

        public IoTHubMetricsUpload(MessageFormatter messageFormatter, Scraper scraper, ModuleClient moduleClient)
        {
            this.messageFormatter = messageFormatter ?? throw new ArgumentNullException(nameof(messageFormatter));
            this.scraper = scraper ?? throw new ArgumentNullException(nameof(scraper));
            this.moduleClient = moduleClient;
        }

        public async Task Upload(DateTime scrapedTime, string prometheusMetrics)
        {
            try
            {
                IList<Message> messages = messageFormatter.Build(scrapedTime, prometheusMetrics);
                await moduleClient.SendEventBatchAsync(messages);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error uploading metrics - {e}");
            }
        }
    }
}