using DotNetty.Common;
using MetricsCollector;
using Microsoft.Azure.Amqp.Framing;
using Moq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MetricsCollectorTests
{
    public class WorkerTests : TempDirectory
    {
        [Fact]
        public async Task TestScraping()
        {
            /* test data */
            (string name, double value)[] modules = Enumerable.Range(1, 10).Select(i => ($"module_{i}", 1.0)).ToArray();

            /* Setup mocks */
            var scraper = new Mock<IScraper>();
            scraper.Setup(s => s.Scrape()).ReturnsAsync(() => new Dictionary<string, string> { { "edgeAgent", PrometheousMetrics(modules) } });

            var storage = new Mock<IFileStorage>();
            string storedValue = "";
            storage.Setup(s => s.AddScrapeResult(It.IsAny<string>())).Callback((Action<string>)(data => storedValue = data));

            var uploader = new Mock<IMetricsUpload>();

            Worker worker = new Worker(scraper.Object, storage.Object, uploader.Object);
            MethodInfo methodInfo = typeof(Worker).GetMethod("Scrape", BindingFlags.NonPublic | BindingFlags.Instance);
            object[] parameters = { };
            Task Scape()
            {
                return methodInfo.Invoke(worker, parameters) as Task;
            }

            /* test */
            await Scape();
            Assert.Equal(1, scraper.Invocations.Count);
            Assert.Equal(0, storage.Invocations.Count);

            await Scape();
            Assert.Equal(2, scraper.Invocations.Count);
            Assert.Equal(0, storage.Invocations.Count);

            modules[1].value = 5;
            await Scape();
            Assert.Equal(3, scraper.Invocations.Count);
            Assert.Equal(1, storage.Invocations.Count);
            Assert.Contains("module_2", storedValue);
            Assert.Contains("5", storedValue);

            modules[5].value = 2.5;
            modules[2].value = 2;
            modules[7].value = 3;
            await Scape();
            Assert.Equal(4, scraper.Invocations.Count);
            Assert.Equal(2, storage.Invocations.Count);

            await Scape();
            Assert.Equal(5, scraper.Invocations.Count);
            Assert.Equal(2, storage.Invocations.Count);
        }

        [Fact]
        public async Task BasicUploading()
        {
            /* Setup mocks */
            var scraper = new Mock<IScraper>();

            var storage = new Mock<IFileStorage>();
            storage.Setup(s => s.GetData(It.IsAny<DateTime>())).Returns(new Dictionary<DateTime, Func<string>> {
                { DateTime.UtcNow, () => "" }
            });

            var uploader = new Mock<IMetricsUpload>();
            IEnumerable<Metric> uploadedData = Enumerable.Empty<Metric>();
            uploader.Setup(u => u.Upload(It.IsAny<IEnumerable<Metric>>())).Callback((Action<IEnumerable<Metric>>)(data => uploadedData = data));

            Worker worker = new Worker(scraper.Object, storage.Object, uploader.Object);
            MethodInfo methodInfo = typeof(Worker).GetMethod("Upload", BindingFlags.NonPublic | BindingFlags.Instance);
            object[] parameters = { };
            Task Upload()
            {
                return methodInfo.Invoke(worker, parameters) as Task;
            }

            /* test */
            await Upload();
            var _ = uploadedData.ToList();
            Assert.Equal(1, storage.Invocations.Count);
            Assert.Equal(1, uploader.Invocations.Count);
        }

        [Fact]
        public async Task UploadContent()
        {
            /* test data */
            var metrics = Enumerable.Range(1, 1).Select(i => new Metric(DateTime.Now, "1", "2", "3", $"tag_{i}")).ToList();

            /* Setup mocks */
            var scraper = new Mock<IScraper>();

            var storage = new Mock<IFileStorage>();
            storage.Setup(s => s.GetData(It.IsAny<DateTime>())).Returns(new Dictionary<DateTime, Func<string>> {
                { DateTime.UtcNow, () => Newtonsoft.Json.JsonConvert.SerializeObject(metrics) }
            });
            var uploader = new Mock<IMetricsUpload>();
            IEnumerable<Metric> uploadedData = Enumerable.Empty<Metric>();
            uploader.Setup(u => u.Upload(It.IsAny<IEnumerable<Metric>>())).Callback((Action<IEnumerable<Metric>>)(d => uploadedData = d));

            Worker worker = new Worker(scraper.Object, storage.Object, uploader.Object);
            MethodInfo methodInfo = typeof(Worker).GetMethod("Upload", BindingFlags.NonPublic | BindingFlags.Instance);
            object[] parameters = { };
            Task Upload()
            {
                return methodInfo.Invoke(worker, parameters) as Task;
            }

            /* test */
            await Upload();

            var expected = metrics.OrderBy(m => m.Tags).First();
            var actual = uploadedData.OrderBy(m => m.Tags).First();
            Assert.Equal(metrics.Select(x => x.GetHashCode()).OrderBy(x => x), uploadedData.Select(x => x.GetHashCode()).OrderBy(x => x));
            Assert.Equal(1, storage.Invocations.Count);
            Assert.Equal(1, uploader.Invocations.Count);
        }

        [Fact]
        public async Task UploadIsLazy()
        {
            /* test data */
            int metricsCalls = 0;
            string Metrics()
            {
                metricsCalls++;
                return Newtonsoft.Json.JsonConvert.SerializeObject(Enumerable.Range(1, 10).Select(i => new Metric(DateTime.Now, "1", "2", "3", $"{i}")));
            }
            Dictionary<DateTime, Func<string>> data = Enumerable.Range(1, 10).ToDictionary(i => new DateTime(i * 100000000), _ => (Func<string>)Metrics);

            /* Setup mocks */
            var scraper = new Mock<IScraper>();

            var storage = new Mock<IFileStorage>();
            storage.Setup(s => s.GetData(It.IsAny<DateTime>())).Returns(data);

            var uploader = new Mock<IMetricsUpload>();
            IEnumerable<Metric> uploadedData = Enumerable.Empty<Metric>();
            uploader.Setup(u => u.Upload(It.IsAny<IEnumerable<Metric>>())).Callback((Action<IEnumerable<Metric>>)(d => uploadedData = d));

            Worker worker = new Worker(scraper.Object, storage.Object, uploader.Object);
            MethodInfo methodInfo = typeof(Worker).GetMethod("Upload", BindingFlags.NonPublic | BindingFlags.Instance);
            object[] parameters = { };
            Task Upload()
            {
                return methodInfo.Invoke(worker, parameters) as Task;
            }

            /* test */
            await Upload();
            int numMetrics = 0;
            foreach(Metric metric in uploadedData)
            {
                Assert.Equal(numMetrics++ / 10 + 1, metricsCalls);
            }
            Assert.Equal(1, storage.Invocations.Count);
            Assert.Equal(1, uploader.Invocations.Count);
        }


        //    [Fact]
        //    public async Task TestBoth()
        //    {
        //        /* Setup mocks */
        //        var testModules = Enumerable.Range(1, 10).Select(i => $"module_{i}").ToList();
        //        var testData = new List<string>();

        //        var scraper = new Mock<IScraper>();
        //        int numScrapes = 0;
        //        Func<Dictionary<string, string>> getNextData = () =>
        //        {
        //            Dictionary<string, string> next = testModules.ToDictionary(m => m, mod => $"module {mod}\ndata {numScrapes}");
        //            testData.AddRange(next.Values);
        //            numScrapes++;

        //            return next;
        //        };
        //        scraper.Setup(s => s.Scrape()).ReturnsAsync(getNextData);

        //        var storage = new FileStorage(GetTempDir());

        //        var uploader = new Mock<IMetricsUpload>();
        //        List<string> uploadedData = new List<string>();
        //        Action<DateTime, string> onCallback = (time, data) => uploadedData.Add(data);
        //        uploader.Setup(u => u.Upload(It.IsAny<DateTime>(), It.IsAny<string>())).Callback(onCallback);

        //        /* test */
        //        Worker worker = new Worker(scraper.Object, storage, uploader.Object, BaseDelay, BaseDelay * 3.33);

        //        CancellationTokenSource cts = new CancellationTokenSource(BaseDelay * 3.66);
        //        await worker.Start(cts.Token);
        //        await Task.Delay(BaseDelay);

        //        Assert.Equal(3, numScrapes);
        //        Assert.Equal(testModules.Count * numScrapes, uploadedData.Count);
        //        Assert.Equal(testData.OrderBy(x => x), uploadedData.OrderBy(x => x));
        //    }

        private string PrometheousMetrics(IEnumerable<(string name, double value)> modules)
        {
            return $@"
# HELP edgeagent_module_start_total Start command sent to module
# TYPE edgeagent_module_start_total counter
{string.Join("\n", modules.Select(module => $@"
edgeagent_module_start_total{{iothub=""lefitche-hub-3.azure-devices.net"",edge_device=""device4"",instance_number=""1"",module_name=""{module.name}"",module_version=""1.0""}} {module.value}
"
))}
";
        }
    }
}
