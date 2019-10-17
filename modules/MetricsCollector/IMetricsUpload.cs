namespace MetricsCollector
{
    using System;
    using System.Threading.Tasks;

    public interface IMetricsUpload
    {
        Task Upload(DateTime scrapedTime, string data);
    }
}
