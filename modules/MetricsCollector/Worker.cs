using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MetricsCollector
{
    public class Worker
    {
        private readonly Scraper scraper;
        private readonly FileStorage storage;
        private readonly IMetricsUpload uploader;
        private readonly TimeSpan scrapingInterval;
        private readonly TimeSpan uploadInterval;

        private DateTime lastUploadTime = DateTime.MinValue;

        public Worker(Scraper scraper, FileStorage storage, IMetricsUpload uploader, TimeSpan scrapingInterval, TimeSpan uploadInterval)
        {
            this.scraper = scraper;
            this.storage = storage;
            this.uploader = uploader;
            this.scrapingInterval = scrapingInterval;
            this.uploadInterval = uploadInterval;
        }

        public async Task Start(CancellationToken cancellationToken)
        {
            Timer scrapingTimer = new Timer(async _ =>
            {
                Console.WriteLine($"\n\n\nScraping Metrics");

                foreach (var moduleResult in await scraper.Scrape())
                {
                    Console.WriteLine($"Storing metrics for {moduleResult.Key}:\n{moduleResult.Value}");
                    storage.AddScrapeResult(moduleResult.Key, moduleResult.Value);
                }
            }, null, scrapingInterval, scrapingInterval);

            Timer uploadTimer = new Timer(async _ =>
            {
                Console.WriteLine($"\n\n\nUploading Metrics");

                foreach (string module in storage.GetAllModules())
                {
                    foreach (KeyValuePair<DateTime, Lazy<string>> data in storage.GetData(module, lastUploadTime))
                    {
                        await uploader.Upload(data.Key, data.Value.Value);
                    }

                }

                lastUploadTime = DateTime.Now;
            }, null, uploadInterval, uploadInterval);


            await Task.Delay(-1, cancellationToken).ContinueWith(_ => { }, TaskContinuationOptions.OnlyOnCanceled);

            scrapingTimer.Dispose();
            uploadTimer.Dispose();
        }
    }
}
