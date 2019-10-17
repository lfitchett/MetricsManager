namespace MetricsCollector
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IMetricsUpload
    {
        Task Upload(IEnumerable<Metric> metrics);
    }
}
