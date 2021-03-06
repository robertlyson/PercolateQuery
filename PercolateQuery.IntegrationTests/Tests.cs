﻿using System.Collections.Generic;
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
        public async Task RegisterPriceAlertQuery()
        {
            var elasticClient = _elasticClient;
            
            var registered = await new PricesAlert(elasticClient).Register(100, "tesla");
            registered.ShouldBe(true);

            var getDocument = await elasticClient.GetAsync<ShoppingItemEs>("document_with_alert");

            var queryVisitor = new DidWeVisitProperQueries { };
            getDocument.Source.Query.Accept(queryVisitor);

            queryVisitor.NumericRangeQuery.LessThanOrEqualTo.ShouldBe(100);
            queryVisitor.MatchQuery.Query.ShouldBe("tesla");
        }

        [Test]
        public async Task UpdatingTeslaItemTo90ShouldRiseAlert()
        {
            //register alert
            var registered = await new PricesAlert(_elasticClient).Register(100, "tesla");

            var @event = new ShoppingItemUpdated {Id = 1, Name = "tesla", Price = 90};

            await new UpdateShoppingItemInElasticSearchHandler(_elasticClient).Handle(@event);

            var updatedDocument = await _elasticClient.GetAsync<ShoppingItemEs>(@event.Id.ToString());
            updatedDocument.Source.Price.ShouldBe(90);

            var alertsFound = await new CheckAlertsHandler(_elasticClient).Handle(@event);

            alertsFound.ShouldBe(true);
        }

        [Test]
        public async Task UpdatingTeslaItemTo110ShouldNotRiseAlert()
        {
            //register alert
            var registered = await new PricesAlert(_elasticClient).Register(100, "tesla");

            var @event = new ShoppingItemUpdated { Id = 1, Name = "tesla", Price = 110 };

            await new UpdateShoppingItemInElasticSearchHandler(_elasticClient).Handle(@event);

            var updatedDocument = await _elasticClient.GetAsync<ShoppingItemEs>(@event.Id.ToString());
            updatedDocument.Source.Price.ShouldBe(110);

            var alertsFound = await new CheckAlertsHandler(_elasticClient).Handle(@event);

            alertsFound.ShouldBe(false);
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
