using System;
using Metrics.ElasticSearch;
using Metrics.Reports;

namespace Metrics
{
    public static class ElasticSearchConfigExtensions
    {

        public static MetricsReports WithElasticSearch(this MetricsReports reports, ElasticReportsConfig reportConfig, TimeSpan interval)
        {
            return reports.WithReport(new ElasticSearchReport(reportConfig), interval);
        }

        [Obsolete("Use WithElasticSearch(ElasticReportsConfig) instead")]
        public static MetricsReports WithElasticSearch(this MetricsReports reports, string host, int port, string index, TimeSpan interval)
        {
            return reports.WithElasticSearch(new ElasticReportsConfig() { Index = index, Host = host, RollingIndexType = RollingIndexType.None, Port = port }, interval);
        }


    }
}
