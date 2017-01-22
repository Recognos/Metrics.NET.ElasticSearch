using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Text;
using Metrics.Json;
using Metrics.Logging;
using Metrics.MetricData;
using Metrics.Reporters;
using Metrics.Utils;

namespace Metrics.ElasticSearch
{
    public class ElasticSearchReport : BaseReport
    {
        private static readonly ILog log = LogProvider.GetCurrentClassLogger();

        private readonly Uri elasticSearchUri;
        private readonly bool replaceDotsOnFieldNames;
        static readonly string __hostName = System.Net.Dns.GetHostName();
        private readonly ElasticReportsConfig _reportConfig;

        private class ESDocument
        {
            public string Index { get; set; }
            public string Type { get; set; }
            public JsonObject Object { get; set; }
            public string ToJsonString()
            {
                var meta = $"{{ \"index\" : {{ \"_index\" : \"{Index}\", \"_type\" : \"{Type}\"}} }}";
                return meta + Environment.NewLine + this.Object.AsJson(false) + Environment.NewLine;
            }
        }

        private List<ESDocument> data = null;


        public ElasticSearchReport(ElasticReportsConfig reportConfig)
        {
            var uri = new Uri($"http://{reportConfig.Host}:{reportConfig.Port}/_bulk");
            var nodeInfoUri = new Uri($"http://{reportConfig.Host}:{reportConfig.Port}");

            _reportConfig = reportConfig;
            this.elasticSearchUri = uri;

            using (var client = new WebClient())
            {
                try
                {
                    var json = client.DownloadString(nodeInfoUri);
                    var deserializer = new DataContractJsonSerializer(typeof(ElasticSearchNodeInfo));
                    using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                    {
                        var nodeInfo = (ElasticSearchNodeInfo)deserializer.ReadObject(stream);
                        replaceDotsOnFieldNames = nodeInfo.MajorVersionNumber >= 2;
                    }

                }
                catch (Exception ex)
                {
                    log.WarnException("Unable to get ElasticSearch version. Field names with dots won't be replaced.", ex);
                    replaceDotsOnFieldNames = false;
                }
            }
        }

        string GetIndex()
        {
            switch (_reportConfig.RollingIndexType)
            {
                case RollingIndexType.Daily:
                    return $"{_reportConfig.Index}-{DateTime.UtcNow.ToString("yyyy-MM-dd")}";
                case RollingIndexType.Monthly:
                    return $"{_reportConfig.Index}-{DateTime.UtcNow.ToString("yyyy-MM")}";
                case RollingIndexType.None:
                default:
                    return _reportConfig.Index;
            }
        }

        protected override void StartReport(string contextName)
        {
            this.data = new List<ESDocument>();
            base.StartReport(contextName);
        }

        protected override void EndReport(string contextName)
        {
            base.EndReport(contextName);
            using (var client = new WebClient())
            {
                var json = string.Join(string.Empty, this.data.Select(d => d.ToJsonString()));
                client.UploadString(this.elasticSearchUri, json);
            }
        }

        private void Pack(string type, string name, Unit unit, MetricTags tags, IEnumerable<JsonProperty> properties)
        {
            this.data.Add(new ESDocument
            {
                Index = GetIndex(),
                Type = type,
                Object = new JsonObject(new[] {
                         new JsonProperty("Timestamp", Clock.FormatTimestamp(this.CurrentContextTimestamp)),
                         new JsonProperty("Type",type),
                         new JsonProperty("Name",name),
                         new JsonProperty("ServerName",__hostName),
                         new JsonProperty("Unit", unit.ToString()),
                         new JsonProperty("Tags", tags.Tags)
                     }.Concat(properties))
            });
        }

        protected override void ReportGauge(string name, double value, Unit unit, MetricTags tags)
        {
            if (!double.IsNaN(value) && !double.IsInfinity(value))
            {
                Pack("Gauge", name, unit, tags, new[] {
                    new JsonProperty("Value", value),
                });
            }
        }

        protected override void ReportCounter(string name, CounterValue value, Unit unit, MetricTags tags)
        {
            var itemProperties = value.Items.SelectMany(i => new[]
            {
                new JsonProperty(i.Item + " - Count", i.Count),
                new JsonProperty(i.Item + " - Percent", i.Percent),
            });

            Pack("Counter", name, unit, tags, new[] {
                new JsonProperty("Count", value.Count),
            }.Concat(itemProperties));
        }

        protected override void ReportMeter(string name, MeterValue value, Unit unit, TimeUnit rateUnit, MetricTags tags)
        {
            var itemProperties = value.Items.SelectMany(i => new[]
            {
                new JsonProperty(i.Item + " - Count", i.Value.Count),
                new JsonProperty(i.Item + " - Percent", i.Percent),
                new JsonProperty(i.Item + " - Mean Rate", i.Value.MeanRate),
                new JsonProperty(i.Item + " - 1 Min Rate", i.Value.OneMinuteRate),
                new JsonProperty(i.Item + " - 5 Min Rate", i.Value.FiveMinuteRate),
                new JsonProperty(i.Item + " - 15 Min Rate", i.Value.FifteenMinuteRate)
            });

            Pack("Meter", name, unit, tags, new[] {
                new JsonProperty("Count", value.Count),
                new JsonProperty("Mean Rate", value.MeanRate),
                new JsonProperty("1 Min Rate", value.OneMinuteRate),
                new JsonProperty("5 Min Rate", value.FiveMinuteRate),
                new JsonProperty("15 Min Rate", value.FifteenMinuteRate)
            }.Concat(itemProperties));
        }

        protected override void ReportHistogram(string name, HistogramValue value, Unit unit, MetricTags tags)
        {
            Pack("Histogram", name, unit, tags, new[] {
                new JsonProperty("Total Count",value.Count),
                new JsonProperty("Last", value.LastValue),
                new JsonProperty("Last User Value", value.LastUserValue),
                new JsonProperty("Min",value.Min),
                new JsonProperty("Min User Value",value.MinUserValue),
                new JsonProperty("Mean",value.Mean),
                new JsonProperty("Max",value.Max),
                new JsonProperty("Max User Value",value.MaxUserValue),
                new JsonProperty("StdDev",value.StdDev),
                new JsonProperty("Median",value.Median),
                new JsonProperty("Percentile 75%",value.Percentile75),
                new JsonProperty("Percentile 95%",value.Percentile95),
                new JsonProperty("Percentile 98%",value.Percentile98),
                new JsonProperty("Percentile 99%",value.Percentile99),
                new JsonProperty(AdjustDottedFieldNames("Percentile 99.9%"), value.Percentile999),
                new JsonProperty("Sample Size", value.SampleSize)
            });
        }

        protected override void ReportTimer(string name, TimerValue value, Unit unit, TimeUnit rateUnit, TimeUnit durationUnit, MetricTags tags)
        {
            Pack("Timer", name, unit, tags, new[] {
                new JsonProperty("Total Count",value.Rate.Count),
                new JsonProperty("Active Sessions",value.ActiveSessions),
                new JsonProperty("Mean Rate", value.Rate.MeanRate),
                new JsonProperty("1 Min Rate", value.Rate.OneMinuteRate),
                new JsonProperty("5 Min Rate", value.Rate.FiveMinuteRate),
                new JsonProperty("15 Min Rate", value.Rate.FifteenMinuteRate),
                new JsonProperty("Last", value.Histogram.LastValue),
                new JsonProperty("Last User Value", value.Histogram.LastUserValue),
                new JsonProperty("Min",value.Histogram.Min),
                new JsonProperty("Min User Value",value.Histogram.MinUserValue),
                new JsonProperty("Mean",value.Histogram.Mean),
                new JsonProperty("Max",value.Histogram.Max),
                new JsonProperty("Max User Value",value.Histogram.MaxUserValue),
                new JsonProperty("StdDev",value.Histogram.StdDev),
                new JsonProperty("Median",value.Histogram.Median),
                new JsonProperty("Percentile 75%",value.Histogram.Percentile75),
                new JsonProperty("Percentile 95%",value.Histogram.Percentile95),
                new JsonProperty("Percentile 98%",value.Histogram.Percentile98),
                new JsonProperty("Percentile 99%",value.Histogram.Percentile99),
                new JsonProperty(AdjustDottedFieldNames("Percentile 99.9%"), value.Histogram.Percentile999),
                new JsonProperty("Sample Size", value.Histogram.SampleSize)
            });
        }

        private string AdjustDottedFieldNames(string fieldName)
        {
            return replaceDotsOnFieldNames ? fieldName.Replace(".", "_") : fieldName;
        }

        protected override void ReportHealth(HealthStatus status)
        {
            var props = new List<JsonProperty>{
                new JsonProperty("IsHealthy", status.IsHealthy),
                new JsonProperty("RegisteredChecksCount", status.Results.Count())
            };
            if (_reportConfig.ReportingApplication != null)
            {
                props.AddRange(new[] {
                        new JsonProperty("AppVersion",_reportConfig.ReportingApplication.Version),
                        new JsonProperty("UpTime",_reportConfig.ReportingApplication.UpTime.ToString())
                        });
            }

            List<JsonObject> checks = new List<JsonObject>();
            foreach (var healthResult in status.Results)
            {
                checks.Add(new JsonObject(
                    new[] {
                        new JsonProperty("Check",healthResult.Name),
                        new JsonProperty("IsHealthy",healthResult.Check.IsHealthy),
                        new JsonProperty("Message",healthResult.Check.Message) }));
            }
            props.Add(new JsonProperty("HealthChecks", checks));

            Pack("Health", "HealthStatus", Unit.None, MetricTags.None, props);
        }

    }
}
