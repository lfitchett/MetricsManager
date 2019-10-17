namespace MetricsCollector
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    class LogAnalyticsMetricsUpload : IMetricsUpload
    {
        readonly MessageFormatter messageFormatter;
        readonly Scraper scraper;
        readonly AzureLogAnalytics logAnalytics;

        public LogAnalyticsMetricsUpload(MessageFormatter messageFormatter, Scraper scraper, AzureLogAnalytics logAnalytics)
        {
            this.messageFormatter = messageFormatter ?? throw new ArgumentNullException(nameof(messageFormatter));
            this.scraper = scraper ?? throw new ArgumentNullException(nameof(scraper));
            this.logAnalytics = logAnalytics;
        }

        public async Task Upload(DateTime scrapedTime, string prometheusMetrics)
        {
            try
            {
                byte[] message = messageFormatter.BuildJSON(scrapedTime, prometheusMetrics);
                await logAnalytics.Post(message);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error uploading metrics - {e}");
            }
        }
    }
}
