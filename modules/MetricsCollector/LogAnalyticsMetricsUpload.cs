namespace MetricsCollector
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;

    class LogAnalyticsMetricsUpload : IMetricsUpload
    {
        readonly MetricsParser messageFormatter;
        readonly Scraper scraper;
        readonly AzureLogAnalytics logAnalytics;

        public LogAnalyticsMetricsUpload(MetricsParser messageFormatter, Scraper scraper, AzureLogAnalytics logAnalytics)
        {
            this.messageFormatter = messageFormatter ?? throw new ArgumentNullException(nameof(messageFormatter));
            this.scraper = scraper ?? throw new ArgumentNullException(nameof(scraper));
            this.logAnalytics = logAnalytics;
        }

        public async Task Upload(IEnumerable<Metric> metrics)
        {
            try
            {
                byte[] message = Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(metrics));
                await logAnalytics.Post(message);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error uploading metrics - {e}");
            }
        }
    }
}
