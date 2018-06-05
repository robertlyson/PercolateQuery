using System;
using System.Collections.Generic;
using System.Text;
using Nest;

namespace PercolateQuery.IntegrationTests
{
    public class ShoppingItemEs
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public double Price { get; set; }
        public QueryContainer Query { get; set; }
    }
}
