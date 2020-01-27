using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.Cosmos;
using System.Diagnostics;
using System.Net;

namespace CosmosOptimize
{
    public class Function1
    {
        private readonly IConfiguration _config;
        private readonly ICosmosDBSQLService _cosmos;
        public Function1(IConfiguration config, ICosmosDBSQLService cosmos)
        {
            _config = config;
            _cosmos = cosmos;

        }

        [FunctionName("setup")]
        public async Task<IActionResult> setup(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            var Mentor1 = _cosmos.GetOrCreateContainerAsync("Mentor1", "/MentorId");
            var Classes1 = _cosmos.GetOrCreateContainerAsync("Classes1", "/ClassId");
            var Registration1 = _cosmos.GetOrCreateContainerAsync("Registration1", "/ClassId");
            var Mentor2 = _cosmos.GetOrCreateContainerAsync("Mentor1", "/MentorId");
            var Registration2 = _cosmos.GetOrCreateContainerAsync("Registration1", "/partitionKey");

            await Task.WhenAll(Mentor1, Classes1, Registration1, Mentor2, Registration2);



            stopWatch.Stop();
            TimeSpan ts = stopWatch.Elapsed;

            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                ts.Hours, ts.Minutes, ts.Seconds,
                ts.Milliseconds / 10);



            return (ActionResult)new OkObjectResult(new { Execution = elapsedTime });
        }

        [FunctionName("seed")]
        public async Task<IActionResult> seed(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            var TMentor1 = _cosmos.GetOrCreateContainerAsync("Mentor1", "/MentorId");
            var TClasses1 = _cosmos.GetOrCreateContainerAsync("Classes1", "/ClassId");
            var TRegistration1 = _cosmos.GetOrCreateContainerAsync("Registration1", "/ClassId");
            var TMentor2 = _cosmos.GetOrCreateContainerAsync("Mentor1", "/MentorId");
            var TRegistration2 = _cosmos.GetOrCreateContainerAsync("Registration2", "/partitionKey");

            await Task.WhenAll(TMentor1, TClasses1, TRegistration1, TMentor2, TRegistration2);

            var Mentor1 = TMentor1.Result;
            var Classes1 = TClasses1.Result;
            var Registration1 = TRegistration1.Result;
            var Mentor2 = TMentor2.Result;
            var Registration2 = TRegistration2.Result;

            using (var webclient = new WebClient())
            {
                var json = webclient.DownloadString(_config["AzureStorageDemo:CosmosOptimizeJson"]);
            }

            stopWatch.Stop();
            TimeSpan ts = stopWatch.Elapsed;

            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                ts.Hours, ts.Minutes, ts.Seconds,
                ts.Milliseconds / 10);


            return (ActionResult)new OkObjectResult(new { Execution = elapsedTime });
        }


        [FunctionName("classes1")]
        public async Task<IActionResult> classes1(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            var Classes1 = await _cosmos.GetOrCreateContainerAsync("Classes1", "/ClassId");
            var query = new QueryDefinition("Select * from c where c.MentorId = '1'");
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            var result = await _cosmos.QueryScanAsync<Mentor>(Classes1, query);
            stopWatch.Stop();
            TimeSpan ts = stopWatch.Elapsed;

            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                ts.Hours, ts.Minutes, ts.Seconds,
                ts.Milliseconds / 10);

            return (ActionResult)new OkObjectResult(new { Execution = elapsedTime, Mentor = result });
        }

        [FunctionName("demo2")]
        public async Task<IActionResult> demo2(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            var Class = await _cosmos.GetOrCreateContainerAsync("Class", "/ClassId");
            var result = await _cosmos.ReadItemAsync<Mentor>(Class, "1");

            return (ActionResult)new OkObjectResult(result);
        }

        [FunctionName("class2")]
        public async Task<IActionResult> class2(
          [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req,
          ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            var Mentor1 = await _cosmos.GetOrCreateContainerAsync("Mentor1", "/MentorId");
            var result = await _cosmos.ReadItemAsync<Class>(Mentor1, "1");

            return (ActionResult)new OkObjectResult(result);
        }
    }
}
