using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Mimics class from iotedge repo
/// </summary>
namespace MetricsCollector
{
    public interface ISystemTime
    {
        DateTime UtcNow { get; }
    }

    public class SystemTime : ISystemTime
    {
        public static SystemTime Instance { get; } = new SystemTime();

        public DateTime UtcNow => DateTime.UtcNow;
    }
}
