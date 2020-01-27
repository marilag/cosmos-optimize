using System.IO;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;

namespace CosmosOptimize
{
    public static class Utils
    {
        public static async Task<string> DownloadFileFromBlobStorageAsJsonString(string ConnectionString, string ContainerName, string filename )
        {
            var storageAccount = CloudStorageAccount.Parse(ConnectionString);
            var myClient = storageAccount.CreateCloudBlobClient();
            var container = myClient.GetContainerReference(ContainerName);

            //lines modified
            var blockBlob = container.GetBlockBlobReference(filename);
            Stream stream = new MemoryStream();
            await blockBlob.DownloadToStreamAsync(stream);
            stream.Position = 0;
            using (StreamReader reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

    }
}