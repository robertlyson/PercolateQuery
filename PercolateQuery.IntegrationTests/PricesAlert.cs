using System.Linq;
using System.Threading.Tasks;
using Nest;

namespace PercolateQuery.IntegrationTests
{
    public class PricesAlert
    {
        private readonly ElasticClient _elasticClient;

        public PricesAlert(ElasticClient elasticClient)
        {
            _elasticClient = elasticClient;
        }

        public async Task<bool> Register(double price, string itemName)
        {
            //TODO: create query matching price and itemName:
            var query = new QueryContainerDescriptor<ShoppingItemEs>()
                .Bool(b => b
                    .Must(
                        must => must.Match(m => m.Field(f => f.Name).Query(itemName)),
                        must => must.Range(r => r.Field(f => f.Price).LessThanOrEquals(price))));
            var indexResponse = await _elasticClient
                .IndexDocumentAsync(new ShoppingItemEs { Id = "document_with_alert", Query = query });
            var refreshResponse = await _elasticClient.RefreshAsync(_elasticClient.ConnectionSettings.DefaultIndex);

            return indexResponse.IsValid;
        }

        public async Task<bool> Match(ShoppingItemUpdated shoppingItemUpdated)
        {
            //TODO: write query to check for registered percolators
            var searchResponse = await _elasticClient.SearchAsync<ShoppingItemEs>(s => s
                .Query(q => q.Percolate(p => p
                    .Field(f => f.Query)
                    .Document(new ShoppingItemEs
                    {
                        Name = shoppingItemUpdated.Name,
                        Price = shoppingItemUpdated.Price
                    }))));

            return searchResponse.Documents.Any();
        }
    }
}