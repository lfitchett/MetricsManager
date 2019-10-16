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

        public void WriteToFile(string module, string data)
        {
            string file = Path.Combine(directory, module, DateTime.UtcNow.Ticks.ToString());
            File.WriteAllText(file, data);
        }

        public IEnumerable<string> GetAllModules()
        {
            return Directory.GetDirectories(directory).Select(Path.GetDirectoryName);
        }

        public IDictionary<DateTime, Lazy<string>> GetData(string module, DateTime start, DateTime end)
        {
            return Directory.GetFiles(Path.Combine(directory, module))
                .Select(Path.GetFileName)
                .SelectWhere(fileName => (long.TryParse(fileName, out long timestamp), timestamp))
                .Where(ticks => start.Ticks <= ticks && ticks <= end.Ticks)
                .ToDictionary(
                    ticks => new DateTime(ticks),
                    ticks => new Lazy<string>(() => File.ReadAllText(Path.Combine(directory, module, ticks.ToString())))
                );
        }

        public void RemoveOldEntries(DateTime keepAfter)
        {
            Directory.GetFiles(directory)
                .Where(fileName => long.TryParse(fileName, out long ticks) && ticks < keepAfter.Ticks)
                .ToList()
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
