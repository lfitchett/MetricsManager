using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MetricsCollector
{
    public class Worker
    {

        private readonly IScraper scraper;
        private readonly IFileStorage storage;
        private readonly IMetricsUpload uploader;
        private readonly ISystemTime systemTime;
        private readonly MetricsParser metricsParser = new MetricsParser();

        private DateTime lastUploadTime = DateTime.MinValue;
        private Dictionary<int, Metric> metrics = new Dictionary<int, Metric>();

        public Worker(IScraper scraper, IFileStorage storage, IMetricsUpload uploader, ISystemTime systemTime = null)
        {
            this.scraper = scraper;
            this.storage = storage;
            this.uploader = uploader;
            this.systemTime = systemTime ?? SystemTime.Instance;
        }

        public async Task Start(TimeSpan scrapingInterval, TimeSpan uploadInterval, CancellationToken cancellationToken)
        {
            Timer scrapingTimer = new Timer(async _ => await Scrape(), null, scrapingInterval, scrapingInterval);
            Timer uploadTimer = new Timer(async _ => await Upload(), null, uploadInterval, uploadInterval);

            await Task.Delay(-1, cancellationToken).ContinueWith(_ => { }, TaskContinuationOptions.OnlyOnCanceled);

            scrapingTimer.Dispose();
            uploadTimer.Dispose();
        }

        private async Task Scrape()
        {
            Console.WriteLine($"\n\n\nScraping Metrics");
            List<Metric> metricsToPersist = new List<Metric>();

            foreach (var moduleResult in await scraper.Scrape())
            {
                var scrapedMetrics = metricsParser.ParseMetrics(systemTime.UtcNow, moduleResult.Value);

                foreach (Metric scrapedMetric in scrapedMetrics)
                {
                    if (metrics.TryGetValue(scrapedMetric.GetValuelessHash(), out Metric oldMetric))
                    {
                        if (oldMetric.Value.Equals(scrapedMetric.Value))
                        {
                            continue;
                        }
                        metricsToPersist.Add(oldMetric);
                    }
                    metrics[scrapedMetric.GetValuelessHash()] = scrapedMetric;
                }

            }

            if (metricsToPersist.Count != 0)
            {
                Console.WriteLine($"Storing metrics");
                storage.AddScrapeResult(Newtonsoft.Json.JsonConvert.SerializeObject(metricsToPersist));
            }
        }

        private async Task Upload()
        {
            Console.WriteLine($"\n\n\nUploading Metrics");
            await uploader.Upload(GetMetricsToUpload(lastUploadTime));
            lastUploadTime = systemTime.UtcNow;
        }

        private IEnumerable<Metric> GetMetricsToUpload(DateTime lastUploadTime)
        {
            foreach (KeyValuePair<DateTime, Func<string>> data in storage.GetData(lastUploadTime))
            {
                var temp = data.Value();
                var fileMetrics = Newtonsoft.Json.JsonConvert.DeserializeObject<Metric[]>(temp) ?? Enumerable.Empty<Metric>();
                foreach (Metric metric in fileMetrics)
                {
                    yield return metric;
                }
            }

            foreach (Metric metric in metrics.Values)
            {
                yield return metric;
            }
            metrics.Clear();
        }
    }
}
