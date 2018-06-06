using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualBasic;
using Nest;
using NUnit.Framework;
using Shouldly;

namespace PercolateQuery.IntegrationTests
{
    public class Tests
    {
        private ElasticClient _elasticClient => ElasticClientFactory.ElasticClient();

        [OneTimeSetUp]
        public void SetUpFixture()
        {
            var elasticClient = _elasticClient;

            elasticClient.DeleteIndex(Strings.IndexName);

            //TODO: Create index with proper mapping for percolate qury
            var createIndexResponse = elasticClient.CreateIndex(Strings.IndexName, i => i
                .Mappings(map => map.Map<ShoppingItemEs>(m => m
                    .AutoMap()
                    .Properties(props => props.Percolator(p => p.Name(n => n.Query))))));

            var indexDocument = elasticClient.IndexDocument<ShoppingItemEs>(
                new ShoppingItemEs {Id = "1", Name = "tesla", Price = 100});
        }

        [Test]
        public async Task CorrectVersionOfElasticsearchIsRunning()
        {
            var elasticClient = _elasticClient;

            var response = await elasticClient.RootNodeInfoAsync();
            var actual = response.IsValid;

            Assert.IsTrue(actual, $"Couldn't connect to elasticsearch {response.ServerError}");

            var version = response.Version.Number;

            Assert.AreEqual("6.2.4", version);
        }

        [Test]
        public async Task MappingIsCreatedCorrectly()
        {
            var mappingResponse = await _elasticClient.GetMappingAsync<ShoppingItemEs>(m => m);

            mappingResponse.IsValid.ShouldBe(true);

            var indexProperties = IndexProperties(mappingResponse);
            var queryField = indexProperties["query"];
            queryField.Type.ShouldBe("percolator");
        }

        private IProperties IndexProperties(IGetMappingResponse mappingResponse)
        {
            var indexMapping = mappingResponse.Indices.FirstOrDefault();
            return indexMapping.Value.Mappings.Values.FirstOrDefault().Properties;
        }
    }

    public class CheckAlertsHandler
    {
        private readonly ElasticClient _elasticClient;

        public CheckAlertsHandler(ElasticClient elasticClient)
        {
            _elasticClient = elasticClient;
        }

        public Task<bool> Handle(ShoppingItemUpdated @event)
        {
            return new PricesAlert(_elasticClient).Match(@event);
        }
    }

    public class UpdateShoppingItemCommand
    {
        public string Name { get; set; }
        public double Price { get; set; }
    }

    class DidWeVisitProperQueries : QueryVisitor
    {
        public INumericRangeQuery NumericRangeQuery { get; set; }
        public IMatchQuery MatchQuery { get; set; }

        public override void Visit(INumericRangeQuery query)
        {
            NumericRangeQuery = query;
            base.Visit(query);
        }
        public override void Visit(IMatchQuery query)
        {
            MatchQuery = query;
            base.Visit(query);
        }
    }
}
