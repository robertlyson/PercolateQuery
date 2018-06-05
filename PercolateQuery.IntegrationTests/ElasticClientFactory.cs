using System;
using Nest;

namespace PercolateQuery.IntegrationTests
{
    public class ElasticClientFactory
    {
        public static ElasticClient ElasticClient()
        {
            var uri = new Uri("http://localhost:9200");
            var settings = new ConnectionSettings(uri);
            settings.DefaultIndex(Strings.IndexName);
            settings.EnableDebugMode();
            var elasticClient =
                new ElasticClient(settings);

            return elasticClient;
        }
    }
}