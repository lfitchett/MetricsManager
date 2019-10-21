using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace MetricsCollector
{
    public class StdoutUploader : IMetricsUpload
    {
        public Task Upload(IEnumerable<Metric> metrics)
        {
            Console.WriteLine($"\n\n\n{DateTime.UtcNow}Metric Upload");
            foreach (Metric metric in metrics)
            {
                Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(metric));
            }

            return Task.CompletedTask;
        }
    }
}
