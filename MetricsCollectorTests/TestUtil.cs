using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Xunit;
using Xunit.Sdk;

namespace MetricsCollectorTests
{
    public class TempDirectory : IDisposable
    {
        private List<string> dirs = new List<string>();

        protected string GetTempDir()
        {
            string newDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(newDir);
            dirs.Add(newDir);

            return newDir;
        }

        public void Dispose()
        {
            foreach (var dir in dirs)
            {
                Directory.Delete(dir, true);
            }
        }
    }

    public static class TestUtil
    {
        public static void ReflectionEqualCollection<T>(IEnumerable<T> expected, IEnumerable<T> actual)
        {
            var expectedEnum = expected.GetEnumerator();
            var actualEnum = actual.GetEnumerator();

            while (expectedEnum.MoveNext() & actualEnum.MoveNext())
            {
                ReflectionEqual(expectedEnum.Current, actualEnum.Current);
            }

            Assert.False(expectedEnum.MoveNext() || actualEnum.MoveNext());
        }

        public static void ReflectionEqual<T>(T expected, T actual)
        {
            foreach (PropertyInfo property in typeof(T).GetProperties())
            {
                Assert.Equal(property.GetValue(expected, null), property.GetValue(actual, null));
            }
        }
    }
}
