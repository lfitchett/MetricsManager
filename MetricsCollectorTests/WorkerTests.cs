﻿using DotNetty.Common;
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

            modules[1].value = 2;
            await Scape();
            Assert.Equal(3, scraper.Invocations.Count);
            Assert.Equal(1, storage.Invocations.Count);
            Assert.Contains("module_2", storedValue);
            Assert.Contains("1", storedValue);

            modules[1].value = 3;
            modules[2].value = 3;
            modules[7].value = 3;
            await Scape();
            Assert.Equal(4, scraper.Invocations.Count);
            Assert.Equal(2, storage.Invocations.Count);
            Assert.Contains("module_2", storedValue);
            Assert.Contains("3", storedValue);

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
            Assert.Single(storage.Invocations.Where(s => s.Method.Name == "GetData"));
            Assert.Equal(1, uploader.Invocations.Count);
        }

        [Fact]
        public async Task UploadContent()
        {
            /* test data */
            var metrics = Enumerable.Range(1, 10).Select(i => new Metric(DateTime.Now, "test_namespace", "test_metric", "3", $"tag_{i}")).ToList();

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
            TestUtil.ReflectionEqualCollection(metrics.OrderBy(x => x.Tags), uploadedData.OrderBy(x => x.Tags));
            Assert.Single(storage.Invocations.Where(s => s.Method.Name == "GetData"));
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
            foreach (Metric metric in uploadedData)
            {
                Assert.Equal(numMetrics++ / 10 + 1, metricsCalls);
            }
            Assert.Single(storage.Invocations.Where(s => s.Method.Name == "GetData"));
            Assert.Equal(1, uploader.Invocations.Count);
        }

        [Fact]
        public async Task ScrapeAndUpload()
        {
            /* Setup mocks */
            var systemTime = new Mock<ISystemTime>();
            DateTime fakeTime = new DateTime(100000000);
            systemTime.Setup(x => x.UtcNow).Returns(() => fakeTime);

            var scraper = new Mock<IScraper>();
            string scrapeResults = PrometheousMetrics(Enumerable.Range(1, 10).Select(i => ($"module_{i}", 1.0)));
            scraper.Setup(s => s.Scrape()).ReturnsAsync(() => new Dictionary<string, string> { { "edgeAgent", scrapeResults } });

            var storage = new FileStorage(GetTempDir(), systemTime.Object);

            var uploader = new Mock<IMetricsUpload>();
            IEnumerable<Metric> uploadedData = Enumerable.Empty<Metric>();
            uploader.Setup(u => u.Upload(It.IsAny<IEnumerable<Metric>>())).Callback((Action<IEnumerable<Metric>>)(d => uploadedData = d));

            Worker worker = new Worker(scraper.Object, storage, uploader.Object, systemTime.Object);
            MethodInfo methodInfoScrape = typeof(Worker).GetMethod("Scrape", BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo methodInfoUpload = typeof(Worker).GetMethod("Upload", BindingFlags.NonPublic | BindingFlags.Instance);
            object[] parameters = { };
            Task Scape()
            {
                return methodInfoScrape.Invoke(worker, parameters) as Task;
            }
            Task Upload()
            {
                return methodInfoUpload.Invoke(worker, parameters) as Task;
            }

            /* test without de-duping */
            scrapeResults = PrometheousMetrics(Enumerable.Range(1, 10).Select(i => ($"module_{i}", 1.0)));
            await Scape();
            fakeTime = fakeTime.AddMinutes(10);
            scrapeResults = PrometheousMetrics(Enumerable.Range(1, 10).Select(i => ($"module_{i}", 2.0)));
            await Scape();
            fakeTime = fakeTime.AddMinutes(1);
            await Upload();
            Assert.Equal(20, uploadedData.Count());
            fakeTime = fakeTime.AddMinutes(1);
            await Upload();
            Assert.Empty(uploadedData);

            /* test de-duping */
            fakeTime = fakeTime.AddMinutes(20);
            scrapeResults = PrometheousMetrics(Enumerable.Range(1, 10).Select(i => ($"module_{i}", 5.0)));
            await Scape();
            fakeTime = fakeTime.AddMinutes(10);
            await Scape();
            await Upload();
            fakeTime = fakeTime.AddMinutes(1);
            Assert.Equal(10, uploadedData.Count());
            fakeTime = fakeTime.AddMinutes(1);
            await Upload();
            Assert.Empty(uploadedData);

            /* test mix of de-duping and not */
            fakeTime = fakeTime.AddMinutes(20);
            scrapeResults = PrometheousMetrics(Enumerable.Range(1, 10).Select(i => ($"module_{i}", 7.0)));
            await Scape();
            fakeTime = fakeTime.AddMinutes(10);
            scrapeResults = PrometheousMetrics(Enumerable.Range(1, 10).Select(i => ($"module_{i}", i % 2 == 0 ? 7.0 : 8.0)));
            await Scape();
            fakeTime = fakeTime.AddMinutes(10);
            scrapeResults = PrometheousMetrics(Enumerable.Range(1, 10).Select(i => ($"module_{i}", 7.0)));
            await Scape();
            fakeTime = fakeTime.AddMinutes(1);
            await Upload();
            Assert.Equal(20, uploadedData.Count());
            await Upload();
            fakeTime = fakeTime.AddMinutes(1);
            Assert.Empty(uploadedData);
        }

        [Fact]
        public async Task NoOverlap()
        {
            /* Setup mocks */
            TaskCompletionSource<bool> scrapeTaskSource = new TaskCompletionSource<bool>();
            TaskCompletionSource<bool> uploadTaskSource = new TaskCompletionSource<bool>();

            var scraper = new Mock<IScraper>();
            scraper.Setup(s => s.Scrape()).Returns(async () => {
                await scrapeTaskSource.Task;
                return new Dictionary<string, string> { { "edgeAgent", "" } };
            });

            var storage = new Mock<IFileStorage>();

            var uploader = new Mock<IMetricsUpload>();
            uploader.Setup(u => u.Upload(It.IsAny<IEnumerable<Metric>>())).Returns(async () => await uploadTaskSource.Task);

            Worker worker = new Worker(scraper.Object, storage.Object, uploader.Object);
            MethodInfo methodInfoScrape = typeof(Worker).GetMethod("Scrape", BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo methodInfoUpload = typeof(Worker).GetMethod("Upload", BindingFlags.NonPublic | BindingFlags.Instance);
            object[] parameters = { };
            Task Scape()
            {
                return methodInfoScrape.Invoke(worker, parameters) as Task;
            }
            Task Upload()
            {
                return methodInfoUpload.Invoke(worker, parameters) as Task;
            }

            /* test scraper first */
            var scrapeTask = Scape();
            await Task.Delay(1);
            var uploadTask = Upload();
            await Task.Delay(1);

            uploadTaskSource.SetResult(true);
            await Task.Delay(1);

            Assert.False(scrapeTask.IsCompleted);
            Assert.False(uploadTask.IsCompleted);
            scrapeTaskSource.SetResult(true);
            await Task.Delay(1);

            await Task.WhenAll(scrapeTask, uploadTask);

            /* test uploader first */
            scrapeTaskSource = new TaskCompletionSource<bool>();
            uploadTaskSource = new TaskCompletionSource<bool>();

            uploadTask = Upload();
            await Task.Delay(1);
            scrapeTask = Scape();
            await Task.Delay(1);

            scrapeTaskSource.SetResult(true);
            await Task.Delay(1);

            Assert.False(scrapeTask.IsCompleted);
            Assert.False(uploadTask.IsCompleted);
            uploadTaskSource.SetResult(true);
            await Task.Delay(1);

            await Task.WhenAll(scrapeTask, uploadTask);
        }

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
