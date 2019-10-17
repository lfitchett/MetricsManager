using MetricsCollector;
using Moq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace MetricsCollectorTests
{
    public class FileStorageTests : TempDirectory
    {
        private DateTime fakeTime;
        public FileStorageTests()
        {
            var systemTime = new Mock<ISystemTime>();
            fakeTime = new DateTime(100000000);
            systemTime.Setup(x => x.UtcNow).Returns(() => fakeTime);
            FileStorage.systemTime = systemTime.Object;
        }

        [Fact]
        public void Storage()
        {
            string directory = GetTempDir();
            FileStorage storage = new FileStorage(directory);

            storage.AddScrapeResult("test_module", string.Join(", ", Enumerable.Range(0, 10)));

            Assert.NotEmpty(Directory.GetFiles(Path.Combine(directory, "test_module")));
        }

        [Fact]
        public void GetModules()
        {
            var testModules = Enumerable.Range(1, 10).Select(i => $"module_{i}").ToList();
            FileStorage storage = new FileStorage(GetTempDir());

            testModules.ForEach(m => storage.AddScrapeResult(m, string.Join(", ", Enumerable.Range(0, 10))));

            var actual = storage.GetAllModules();
            Assert.Equal(testModules.OrderBy(f => f), actual.OrderBy(f => f));
        }

        [Fact]
        public void GetDataSingleEntry()
        {
            FileStorage storage = new FileStorage(GetTempDir());

            string testData = string.Join(", ", Enumerable.Range(0, 10));
            storage.AddScrapeResult("test_module", testData);

            IDictionary<DateTime, Lazy<string>> actual = storage.GetData("test_module");
            Assert.Single(actual);
            Assert.Equal(testData, actual.Single().Value.Value);
        }

        [Fact]
        public void GetDataByTime()
        {
            FileStorage storage = new FileStorage(GetTempDir());

            storage.AddScrapeResult("test_module", "data1");

            IDictionary<DateTime, Lazy<string>> actual = storage.GetData("test_module");
            Assert.Single(actual);
            Assert.Equal("data1", actual.Single().Value.Value);

            DateTime break1 = fakeTime.AddMinutes(5);
            fakeTime = fakeTime.AddMinutes(10);
            storage.AddScrapeResult("test_module", "data2");

            actual = storage.GetData("test_module");
            Assert.Equal(2, actual.Count);
            actual = storage.GetData("test_module", break1);
            Assert.Single(actual);
            Assert.Equal("data2", actual.Single().Value.Value);

            DateTime break2 = fakeTime.AddMinutes(5);
            fakeTime = fakeTime.AddMinutes(10);
            storage.AddScrapeResult("test_module", "data3");

            actual = storage.GetData("test_module");
            Assert.Equal(3, actual.Count);
            actual = storage.GetData("test_module", break1, break2);
            Assert.Single(actual);
            Assert.Equal("data2", actual.Single().Value.Value);
        }

        [Fact]
        public void GetDataMany()
        {
            FileStorage storage = new FileStorage(GetTempDir());

            /* add a variable number of scrape results to 10 different modules */
            Dictionary<string, List<string>> testData = Enumerable.Range(1, 10)
                .ToDictionary(i => $"module_{i}", i => Enumerable.Range(i, i).Select(j => $"test data {i}-{j}").ToList());
            testData.ToList().ForEach(d => d.Value.ForEach(data =>
            {
                storage.AddScrapeResult(d.Key, data);
                fakeTime = fakeTime.AddMinutes(10);
            }));

            foreach (var d in testData)
            {
                /* get all stored data and sort by timestamp (key) */
                var actual = storage.GetData(d.Key).Select(x => x.Value.Value);
                Assert.Equal(d.Value.OrderBy(x => x), actual.OrderBy(x => x));
            }
        }

        [Fact]
        public void RemoveOld()
        {
            FileStorage storage = new FileStorage(GetTempDir());

            storage.AddScrapeResult("test_module", "data1");

            IDictionary<DateTime, Lazy<string>> actual = storage.GetData("test_module");
            Assert.Single(actual);
            Assert.Equal("data1", actual.Single().Value.Value);

            DateTime break1 = fakeTime.AddMinutes(5);
            fakeTime = fakeTime.AddMinutes(10);
            storage.AddScrapeResult("test_module", "data2");

            actual = storage.GetData("test_module");
            Assert.Equal(2, actual.Count);
            storage.RemoveOldEntries(break1);
            actual = storage.GetData("test_module");
            Assert.Single(actual);

            DateTime break2 = fakeTime.AddMinutes(5);
            fakeTime = fakeTime.AddMinutes(10);
            storage.AddScrapeResult("test_module", "data3");
            fakeTime = fakeTime.AddMinutes(10);
            storage.AddScrapeResult("test_module", "data4");
            fakeTime = fakeTime.AddMinutes(10);
            storage.AddScrapeResult("test_module", "data5");
            fakeTime = fakeTime.AddMinutes(10);

            actual = storage.GetData("test_module");
            Assert.Equal(4, actual.Count);
            storage.RemoveOldEntries(break2);
            actual = storage.GetData("test_module");
            Assert.Equal(new[] { "data3", "data4", "data5" }, actual.OrderBy(x => x.Key).Select(x => x.Value.Value));

            storage.RemoveOldEntries(DateTime.UtcNow);
            actual = storage.GetData("test_module");
            Assert.Empty(actual);
        }
    }
}
