using DotNetty.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;

namespace MetricsCollector
{
    public class FileStorage
    {
        private string directory;

        public FileStorage(string directory)
        {
            this.directory = directory;
        }

        public void AddScrapeResult(string module, string data)
        {
            Directory.CreateDirectory(Path.Combine(directory, module));
            string file = Path.Combine(directory, module, DateTime.UtcNow.Ticks.ToString());
            File.WriteAllText(file, data);
        }

        public IEnumerable<string> GetAllModules()
        {
            return Directory.GetDirectories(directory).Select(Path.GetFileName);
        }

        public IDictionary<DateTime, Lazy<string>> GetData(string module)
        {
            return GetData(module, _ => true);
        }
        public IDictionary<DateTime, Lazy<string>> GetData(string module, DateTime start)
        {
            return GetData(module, ticks => start.Ticks <= ticks);
        }
        public IDictionary<DateTime, Lazy<string>> GetData(string module, DateTime start, DateTime end)
        {
            return GetData(module, ticks => start.Ticks <= ticks && ticks <= end.Ticks);
        }
        private IDictionary<DateTime, Lazy<string>> GetData(string module, Func<long, bool> inTimeRange)
        {
            return Directory.GetFiles(Path.Combine(directory, module))
                .Select(Path.GetFileName)
                .SelectWhere(fileName => (long.TryParse(fileName, out long timestamp), timestamp))
                .Where(inTimeRange)
                .ToDictionary(
                    ticks => new DateTime(ticks),
                    ticks => new Lazy<string>(() =>
                    {
                        string file = Path.Combine(directory, module, ticks.ToString());
                        if (File.Exists(file))
                        {
                            return File.ReadAllText(file);
                        }
                        return "";
                    })
                );
        }

        public void RemoveOldEntries(DateTime keepAfter)
        {
            GetAllModules()
                .SelectMany(module =>
                    GetData(module)
                    .Select(d => d.Key)
                    .Where(timestamp => timestamp < keepAfter)
                    .Select(timestamp => Path.Combine(directory, module, timestamp.Ticks.ToString()))
                ).ToList()
                .ForEach(File.Delete);
        }
    }
    public static class SelectWhereClass
    {
        public static IEnumerable<TResult> SelectWhere<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, (bool, TResult)> selector)
        {
            foreach (TSource s in source)
            {
                (bool include, TResult result) = selector(s);
                if (include)
                {
                    yield return result;
                }
            }
        }
    }
}
