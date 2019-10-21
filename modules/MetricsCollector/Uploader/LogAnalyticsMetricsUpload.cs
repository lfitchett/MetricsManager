﻿namespace MetricsCollector
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;

    class LogAnalyticsMetricsUpload : IMetricsUpload
    {
        readonly AzureLogAnalytics logAnalytics;

        public LogAnalyticsMetricsUpload(AzureLogAnalytics logAnalytics)
        {
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