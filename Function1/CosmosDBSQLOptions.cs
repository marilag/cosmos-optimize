using Microsoft.Azure.Cosmos;

namespace CosmosOptimize
{

    public class CosmosDBSQLOptions
    {
        public string EndpointUri { get; set; }
        public string Key { get; set; }
        public string Database { get; set; }
        public ConnectionMode ConnectionMode { get; set; }
        public CosmosSerializer Serializer { get; set; }
    }
}