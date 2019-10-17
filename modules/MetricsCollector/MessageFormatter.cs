namespace MetricsCollector
{
    using Microsoft.Azure.Devices.Client;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    public class MessageFormatter
    {
        const string IdentifierPropertyName = "MessageIdentifier";
        const string PrometheusMetricSchema = @"^(?<metricname>[^#\{\}]+)(\{((?<tagname>[^="",]+)=(\""(?<tagvalue>[^="",]+)\"")(,(?<tagname>[^="",]+)=(\""(?<tagvalue>[^="",]+)\""))*)\})?\s(?<metricvalue>.+)$";
        static readonly Regex PrometheusSchemaRegex = new Regex(PrometheusMetricSchema, RegexOptions.Compiled);
        readonly MetricsFormat format;
        readonly string identifier;

        public MessageFormatter(MetricsFormat format, string identifier)
        {
            this.format = format;
            this.identifier = identifier;
        }

        public byte[] BuildJSON(DateTime timeGeneratedUtc, string prometheusMessage)
        {
            return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(GetMetricsDataList(timeGeneratedUtc, prometheusMessage)));
        }

        public IList<Message> Build(DateTime timeGeneratedUtc, string prometheusMessage)
        {
            IList<Message> messages = this.format == MetricsFormat.Prometheus
                ? BuildMessagesInPrometheusFormat(prometheusMessage)
                : BuildMessagesInJsonFormat(timeGeneratedUtc, prometheusMessage);
            return messages;
        }

        IList<Message> BuildMessagesInJsonFormat(DateTime timeGeneratedUtc, string prometheusMessage)
        {
            var message = new Message(BuildJSON(timeGeneratedUtc, prometheusMessage));
            message.Properties[IdentifierPropertyName] = this.identifier;
            message.MessageSchema = this.format.ToString();
            message.ContentEncoding = "UTF-8";
            message.ContentType = "application/json";

            return new List<Message> { message };
        }

        IList<MetricsData> GetMetricsDataList(DateTime timeGeneratedUtc, string prometheusMessage)
        {
            var metricsDataList = new List<MetricsData>();
            using (StringReader sr = new StringReader(prometheusMessage))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (line.Trim().StartsWith('#'))
                    {
                        continue;
                    }

                    Match match = PrometheusSchemaRegex.Match(line.Trim());
                    if (match.Success)
                    {
                        var metricName = string.Empty;
                        var metricValue = string.Empty;
                        var tagNames = new List<string>();
                        var tagValues = new List<string>();

                        var name = match.Groups["metricname"];
                        if (name?.Length > 0)
                        {
                            metricName = name.Value;
                        }

                        var value = match.Groups["metricvalue"];
                        if (value?.Length > 0)
                        {
                            metricValue = value.Value;
                        }

                        var tagnames = match.Groups["tagname"];
                        if (tagnames.Length > 0)
                        {
                            for (int i = 0; i < tagnames.Captures.Count; i++)
                            {
                                tagNames.Add(tagnames.Captures[i].Value);
                            }
                        }

                        var tagvalues = match.Groups["tagvalue"];
                        if (tagvalues.Length > 0)
                        {
                            for (int i = 0; i < tagvalues.Captures.Count; i++)
                            {
                                tagValues.Add(tagvalues.Captures[i].Value);
                            }
                        }

                        var tags = tagNames.Zip(tagValues, (k, v) => new { k, v })
                            .ToDictionary(x => x.k, x => x.v);
                        var metricsData = new MetricsData(
                            timeGeneratedUtc,
                            "prometheus",
                            metricName,
                            metricValue,
                            JsonConvert.SerializeObject(tags));

                        metricsDataList.Add(metricsData);
                    }
                }
            }
            return metricsDataList;
        }

        IList<Message> BuildMessagesInPrometheusFormat(string prometheusMessage)
        {
            byte[] messageBytes = Encoding.UTF8.GetBytes(prometheusMessage);
            var message = new Message(messageBytes);
            message.Properties[IdentifierPropertyName] = this.identifier;
            message.MessageSchema = this.format.ToString();
            return new[] { message };
        }
    }

    public class MetricsData
    {
        public MetricsData(DateTime timeGeneratedUtc, string ns, string name, string value, string tags)
        {
            this.TimeGeneratedUtc = timeGeneratedUtc;
            this.Namespace = ns;
            this.Name = name;
            this.Value = value;
            this.Tags = tags;
        }

        public DateTime TimeGeneratedUtc { get; }
        public string Namespace { get; }
        public string Name { get; }
        public string Value { get; }
        public string Tags { get; }
    }
}