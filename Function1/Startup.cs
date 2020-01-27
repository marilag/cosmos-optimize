using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

[assembly: FunctionsStartup(typeof(CosmosOptimize.Startup))]
namespace CosmosOptimize
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            var configBuilder = new ConfigurationBuilder()
            .SetBasePath(System.Environment.CurrentDirectory)
            .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();

            var config = configBuilder.Build();

            configBuilder.AddAzureKeyVault(
                $"https://{config["AzureKeyVault:VaultName"]}.vault.azure.net/"
            );

            config = configBuilder.Build();

            builder.Services.Replace(ServiceDescriptor.Singleton(typeof(IConfiguration), config));
            builder.Services.AddSingleton<ICosmosDBSQLService, CosmosDBSQLService>();
            builder.Services.Configure<CosmosDBSQLOptions>(cosmosoptions =>
                        {
                            cosmosoptions.EndpointUri = config["CosmosDb:EndpointUri"];
                            cosmosoptions.Key = config["CosmosKey"];
                            cosmosoptions.Database = config["CosmosDb:Database"];
                        });
        }
    }
}