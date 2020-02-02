using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

namespace CosmosOptimize
{
    public interface ICosmosDBSQLService
    {
        CosmosClientOptions CosmosClientOptions { get; set; }
        Database cosmosDatabase { get; set; }

        Task<T> DeleteItemAysnc<T>(Container container, string Id, dynamic partitionkey);
        Task<Container> GetOrCreateContainerAsync(string container, string partitionPath);
        Task<List<T>> QueryAsync<T>(Container container, QueryDefinition queryDefinition);
        Task<List<T>> QueryAsync<T>(Container container, QueryDefinition queryDefinition, dynamic partitionkey);
        Task<List<T>> QueryScanAsync<T>(Container container, QueryDefinition queryDefinition);
        Task<T> ReadItemAsync<T>(Container container, string Id);
        Task<T> ReadItemAsync<T>(Container container, string Id, string partitionkey);
        Task<T> UpsertItemAsync<T>(Container container, T entity, ItemRequestOptions requestoptions = null);
        Task DeleteDatabase();
    }

    public class CosmosDBSQLService : ICosmosDBSQLService
    {
        private static CosmosClient _client;
        public Database cosmosDatabase { get; set; }
        private readonly CosmosDBSQLOptions _options;
        public CosmosClientOptions CosmosClientOptions { get; set; }


        public CosmosDBSQLService(IOptions<CosmosDBSQLOptions> options)
        {
            _options = options.Value;
            CosmosClientOptions = new CosmosClientOptions
            {
                ConnectionMode = _options.ConnectionMode,
                GatewayModeMaxConnectionLimit = 10,
                Serializer = _options.Serializer
            };
            _client = new CosmosClient(_options.EndpointUri, _options.Key, CosmosClientOptions);

        }
        public async Task DeleteDatabase()
        {
            cosmosDatabase = _client.GetDatabase(_options.Database);
            var requestResponse = await cosmosDatabase.DeleteAsync();
        }

        public async Task<Container> GetOrCreateContainerAsync(string container, string partitionPath)
        {
            cosmosDatabase = await _client.CreateDatabaseIfNotExistsAsync(_options.Database, _options.DefaultRU);

            ContainerProperties containerProperties = new ContainerProperties(id: container, partitionKeyPath: partitionPath);

            return await cosmosDatabase.CreateContainerIfNotExistsAsync(
                containerProperties: containerProperties);
        }

        public async Task<T> UpsertItemAsync<T>(Container container, T entity, ItemRequestOptions requestoptions = null)

        {

            T item = default(T);
            try
            {

                item = await container.UpsertItemAsync(entity);
            }
            catch (CosmosException cosmosEx)
            {
                throw cosmosEx;
            }
            catch (Exception)
            {

                throw;
            }

            return item;
        }


        public async Task<T> ReadItemAsync<T>(Container container, string Id)

        {
            T item = default(T);
            try
            {
                item = await container.ReadItemAsync<T>(Id, new PartitionKey(Id));

            }
            catch (CosmosException cosEx)
            {
                if (cosEx.SubStatusCode == 1003)
                {
                    throw new Exception("Resource doesn't exists in the database");
                }
            }
            catch (Exception)
            {

                throw;
            }

            return item;
        }

        public async Task<T> ReadItemAsync<T>(Container container, string Id, string partitionkey)
        {
            T item = default(T);
            try
            {
                item = await container.ReadItemAsync<T>(Id, new PartitionKey(partitionkey));

            }
            catch (CosmosException cosEx)
            {
                if (cosEx.SubStatusCode == 1003)
                {
                    throw new Exception("Resource doesn't exists in the database");
                }
            }
            catch (Exception)
            {

                throw;
            }

            return item;
        }

        public async Task<T> DeleteItemAysnc<T>(Container container, string Id, dynamic partitionkey)
        {
            T item = default(T);
            try
            {
                item = await container.DeleteItemAsync<T>(Id, new PartitionKey(partitionkey));

            }
            catch (CosmosException cosEx)
            {
                if (cosEx.SubStatusCode == 1003)
                {
                    throw new Exception("Resource doesn't exists in the database");
                }
            }
            catch (Exception)
            {

                throw;
            }

            return item;
        }

        public async Task<List<T>> QueryAsync<T>(Container container, QueryDefinition queryDefinition)
        {
            var results = new List<T>();

            try
            {

                FeedIterator<T> resultSetIterator = container.GetItemQueryIterator<T>(queryDefinition, requestOptions: new QueryRequestOptions());
                while (resultSetIterator.HasMoreResults)
                {
                    results.AddRange((await resultSetIterator.ReadNextAsync()));
                }

            }
            catch (CosmosException cosEx)
            {
                if (cosEx.SubStatusCode == 1003)
                {
                    throw new Exception("Resource doesn't exists in the database");
                }
            }
            catch (Exception)
            {
                throw;
            }

            return results;
        }


        public async Task<List<T>> QueryAsync<T>(Container container, QueryDefinition queryDefinition, dynamic partitionkey)
        {
            List<T> results = new List<T>();

            try
            {

                FeedIterator<T> resultSetIterator = container.GetItemQueryIterator<T>(queryDefinition, requestOptions: new QueryRequestOptions() { PartitionKey = new PartitionKey(partitionkey) });
                while (resultSetIterator.HasMoreResults)
                {
                    results.AddRange((await resultSetIterator.ReadNextAsync()));
                }

            }
            catch (CosmosException cosEx)
            {
                if (cosEx.SubStatusCode == 1003)
                {
                    throw new Exception("Resource doesn't exists in the database");
                }
            }
            catch (Exception)
            {
                throw;
            }

            return results;
        }

        public async Task<List<T>> QueryScanAsync<T>(Container container, QueryDefinition queryDefinition)
        {
            List<T> results = new List<T>();
            try
            {

                FeedIterator<T> resultSetIterator = container.GetItemQueryIterator<T>(queryDefinition, requestOptions: new QueryRequestOptions() { EnableScanInQuery = true });
                while (resultSetIterator.HasMoreResults)
                {
                    results.AddRange((await resultSetIterator.ReadNextAsync()));
                }

            }
            catch (CosmosException cosEx)
            {
                if (cosEx.SubStatusCode == 1003)
                {
                    throw new Exception("Resource doesn't exists in the database");
                }
            }
            catch (Exception)
            {

                throw;
            }

            return results;
        }
    }
}
