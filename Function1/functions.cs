
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
using System.Collections.Generic;
using System.Linq;

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


        private static string FormatTime(TimeSpan ts)
        {
            return String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                            ts.Hours, ts.Minutes, ts.Seconds,
                            ts.Milliseconds / 10);
        }

        private async Task TaskInsertItem<T>(T item)
        {

        }

        [FunctionName("delete")]
        public async Task<IActionResult> delete(
               [HttpTrigger(AuthorizationLevel.Function, "delete", Route = null)] HttpRequest req,
               ILogger log)
        {
            await _cosmos.DeleteDatabase();
            return new OkResult();
        }

        [FunctionName("seed")]
        public async Task<IActionResult> seed(
                [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
                ILogger log)
        {
            log.LogInformation("Start see operation");

            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            var TMentor1 = _cosmos.GetOrCreateContainerAsync("Mentor1", "/MentorId");
            var TClasses1 = _cosmos.GetOrCreateContainerAsync("Classes1", "/ClassId");
            var TRegistration1 = _cosmos.GetOrCreateContainerAsync("Registration1", "/ClassId");
            var TMentor2 = _cosmos.GetOrCreateContainerAsync("Mentor2", "/MentorId");
            var TMentor3 = _cosmos.GetOrCreateContainerAsync("Mentor3", "/MentorId");
            var TRegistration2 = _cosmos.GetOrCreateContainerAsync("Registration2", "/partitionKey");

            await Task.WhenAll(TMentor1, TClasses1, TRegistration1, TMentor2, TMentor3, TRegistration2);

            log.LogInformation($"Containers initialized. Time elapsed: {FormatTime(stopWatch.Elapsed)}");

            var Mentor1 = TMentor1.Result;
            var Classes1 = TClasses1.Result;
            var Registration1 = TRegistration1.Result;
            var Mentor2 = TMentor2.Result;
            var Mentor3 = TMentor3.Result;
            var Registration2 = TRegistration2.Result;

            string json = "";

            using (var webclient = new WebClient())
            {
                json = webclient.DownloadString(_config["AzureStorageDemo:CosmosOptimizeJson"]);
                log.LogInformation($"Seed data download. Time elapsed: {FormatTime(stopWatch.Elapsed)}");

            }

            if (string.IsNullOrEmpty(json)) return new OkObjectResult("No data to load");


            try
            {

                ///COMMENT THIS OUT TO TEST BULK EXECUTION
                _cosmos.CosmosClientOptions.AllowBulkExecution = true;
                await _cosmos.cosmosDatabase.ReplaceThroughputAsync(10000);
                log.LogInformation($"AllowBulkExecution set to true. RU increased to 10000");

                var TaskInsertItem = new List<Task>();

                /// Embedded model
                /// 1 collection - Mentor 2
                /// 1 document for Mentor, Class and Registrations
                log.LogInformation($"Setup Embedded Model...");
                var EmbeddedModel = JsonConvert.DeserializeObject<List<Mentor2>>(json);
                EmbeddedModel.ForEach(m =>
                {
                    m.Classes.ForEach(c =>
                    {
                        c.MentorId = m.MentorId;
                        c.Registrations.ForEach(r =>
                        {
                            r.ClassId = c.ClassId;
                            r.MentorId = c.MentorId;
                        });
                    }
                );
                    TaskInsertItem.Add(Mentor2.UpsertItemAsync(m));
                });

                await Task.WhenAll(TaskInsertItem);
                stopWatch.Stop();
                var EmbeddedModelElapsedTime = FormatTime(stopWatch.Elapsed);
                log.LogInformation($"Embedded model completed. Items: {EmbeddedModel.Count} Time elapsed:{EmbeddedModelElapsedTime}");

                /// Reference+Embedded model
                /// 1 collection - Mentor 3
                /// Separate Mentor document,Combined  Class + Registration
                log.LogInformation($"Setup Reference + Embedded Model...");
                var MentorOnly = JsonConvert.DeserializeObject<List<Mentor1>>(json);
                TaskInsertItem = new List<Task>();
                stopWatch.Restart();

                MentorOnly.ForEach(m => TaskInsertItem.Add(Mentor3.UpsertItemAsync(m)));

                //COMMENT THIS TO DEMO THROTTLING
                await Task.WhenAll(TaskInsertItem);
                TaskInsertItem = new List<Task>();

                (from m in EmbeddedModel
                 from c in m.Classes
                 select c).ToList().ForEach(r => TaskInsertItem.Add(Mentor3.UpsertItemAsync(r)));

                await Task.WhenAll(TaskInsertItem);
                stopWatch.Stop();
                var EmbeddedReferenceModelTimeElapsed = FormatTime(stopWatch.Elapsed);
                log.LogInformation($"Reference + Embedded Model completed. Items: {TaskInsertItem.Count} Time elapsed:{EmbeddedReferenceModelTimeElapsed}");

                /// Synthetic Key
                /// 1 Collection - Registration2
                /// PartitionKey = StudenId_MentorId_RegisrationId                
                log.LogInformation($"Setup Synthetic Key...");
                stopWatch.Restart();
                TaskInsertItem = new List<Task>();
                //Add to Synthetic Key
                (from m in EmbeddedModel
                 from c in m.Classes
                 from r in c.Registrations
                 select new Registration2(r.MentorId, r.ClassId)
                 {
                     Age = r.Age,
                     Company = r.Company,
                     Email = r.Email,
                     Gender = r.Gender,
                     Name = r.Name,
                     Phone = r.Phone,
                     RegistrationId = r.RegistrationId
                 }).ToList().ForEach(r => TaskInsertItem.Add(Registration2.UpsertItemAsync(r)));

                await Task.WhenAll(TaskInsertItem);
                stopWatch.Stop();
                var SyntheticKeyTimeElapsed = FormatTime(stopWatch.Elapsed);
                log.LogInformation($"Synthetic Key Registration completed. Items: {TaskInsertItem.Count} Time elapsed:{SyntheticKeyTimeElapsed}");

                /// Separate Collections
                /// 1 collection each for Mentor, Class, Regisrations
                /// Mentor
                log.LogInformation($"Setup separate collections model...");
                Stopwatch stopWatch2 = new Stopwatch();
                stopWatch2.Start();
                stopWatch.Restart();
                TaskInsertItem = new List<Task>();
                MentorOnly.ForEach(m => TaskInsertItem.Add(Mentor1.UpsertItemAsync(m)));
                await Task.WhenAll(TaskInsertItem);
                stopWatch.Stop();
                stopWatch.Restart();
                log.LogInformation($"Insert Mentor completed. Items: {MentorOnly.Count} Time elapsed:{FormatTime(stopWatch.Elapsed)}");

                /// Class
                stopWatch.Restart();
                TaskInsertItem = new List<Task>();

                var Model1Classes = (from m in EmbeddedModel
                                     from c in m.Classes
                                     select new Class1
                                     {
                                         Address = c.Address,
                                         ClassId = c.ClassId,
                                         ClassName = c.ClassName,
                                         Date = c.Date,
                                         MaxMentees = c.MaxMentees,
                                         MentorId = c.MentorId
                                     }).ToList();

                Model1Classes.ForEach(c => TaskInsertItem.Add(Classes1.UpsertItemAsync(c)));

                await Task.WhenAll(TaskInsertItem);
                stopWatch.Stop();
                log.LogInformation($"Insert Classes completed. Items: {TaskInsertItem.Count} Time elapsed:{FormatTime(stopWatch.Elapsed)}");

                ///Registration

                stopWatch.Restart();
                TaskInsertItem = new List<Task>();
                log.LogInformation($"Insert Registrations...");

                var Model1Regisrations = (from m in EmbeddedModel
                                          from c in m.Classes
                                          from r in c.Registrations
                                          select r).ToList();
                Model1Regisrations.ForEach(r => TaskInsertItem.Add(Registration1.UpsertItemAsync(r)));

                await Task.WhenAll(TaskInsertItem);
                stopWatch.Stop();
                log.LogInformation($"Insert Registration completed.Items: {TaskInsertItem.Count} Time elapsed:{FormatTime(stopWatch.Elapsed)}");

                stopWatch2.Stop();
                var SeparateCollectionTimeElapsed = FormatTime(stopWatch2.Elapsed);
                log.LogInformation($"Separate collection completed. Time elapsed:{SeparateCollectionTimeElapsed}");


            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error executing seed");
            }
            finally
            {
                _cosmos.CosmosClientOptions.AllowBulkExecution = false;
                await _cosmos.cosmosDatabase.ReplaceThroughputAsync(800);
                log.LogInformation($"AllowBulkExecution set to false. RU decreased to 600");
                log.LogInformation($"Seed completed");
            }

            return (ActionResult)new OkResult();
        }

        [FunctionName("mentors")]
        public async Task<IActionResult> mentors(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("Get all mentors.");
            Stopwatch stopWatch = new Stopwatch();
            log.LogInformation("Getting mentors list. Query: Select * from c");

            var query = new QueryDefinition("Select * from c");

            Container container = null;
            var result = new List<Mentor1>();

            stopWatch.Start();
            log.LogInformation("Query from Embeddded model...");
            container = await _cosmos.GetOrCreateContainerAsync("Mentor2", "/MentorId");
            result = await _cosmos.QueryAsync<Mentor1>(container, query);
            stopWatch.Stop();
            var mentor2Result = new { Result = result.Count, ElapsedTime = FormatTime(stopWatch.Elapsed) };
            log.LogInformation($"Embeddded model completed. Items: {result.Count}  Time elapsed: {FormatTime(stopWatch.Elapsed)}");

            stopWatch.Restart();
            log.LogInformation("Query from Reference + Embedded model...");
            container = await _cosmos.GetOrCreateContainerAsync("Mentor3", "/MentorId");
            result = await _cosmos.QueryAsync<Mentor1>(container, new QueryDefinition("Select * from c where c.Type = Mentor"));
            stopWatch.Stop();
            var mentor3Result = new { Result = result.Count(), ElapsedTime = FormatTime(stopWatch.Elapsed) };
            log.LogInformation($"Reference + Embedded model completed. Items: {result.Count}  Time elapsed: {FormatTime(stopWatch.Elapsed)}");

            stopWatch.Restart();
            log.LogInformation("Query from individual collection...");
            container = await _cosmos.GetOrCreateContainerAsync("Mentor1", "/MentorId");
            result = await _cosmos.QueryAsync<Mentor1>(container, query);
            stopWatch.Stop();
            var mentor1Result = new { Result = result.Count(), ElapsedTime = FormatTime(stopWatch.Elapsed) };
            log.LogInformation($"Individual collection completed. Items: {result.Count} Time elapsed: {FormatTime(stopWatch.Elapsed)}");

            return (ActionResult)new OkObjectResult(new { Model1 = mentor1Result, Model2 = mentor2Result, Model3 = mentor3Result });
        }

        [FunctionName("mentorclasses")]
        public async Task<IActionResult> mentorsclasses(
                [HttpTrigger(AuthorizationLevel.Function, "get", Route = "mentors/{id}/classes")] HttpRequest req,
                string id,
                ILogger log)
        {
            log.LogInformation("Get all mentor's classes.");
            Stopwatch stopWatch = new Stopwatch();

            Container container = null;
            var result = new List<Mentor1>();

            stopWatch.Start();
            log.LogInformation("Query from Embeddded model...");
            container = await _cosmos.GetOrCreateContainerAsync("Mentor2", "/MentorId");
            var resultEmbedded = await _cosmos.QueryAsync<Mentor1>(container,  
            new QueryDefinition("Select cl.id, cl.MentorId, cl.ClassId, cl.ClassName from c JOIN cl in c.Classes where cl.MentorId = @id").WithParameter("@id", id));

            stopWatch.Stop();
            var mentor2Result = new { Result = result.Count, ElapsedTime = FormatTime(stopWatch.Elapsed) };
            log.LogInformation($"Embeddded model completed. Items: {resultEmbedded.Count}  Time elapsed: {FormatTime(stopWatch.Elapsed)}");

            stopWatch.Restart();
            log.LogInformation("Query from Reference + Embedded model...");
            container = await _cosmos.GetOrCreateContainerAsync("Mentor3", "/MentorId");
            var query = new QueryDefinition("Select c.id, c.MentorId, c.ClassId, c.ClassName, c.Type from c where c.MentorId = @id and c.Type = 'Class'").WithParameter("@id", id);
            var resultEmbeddedReference = await _cosmos.QueryAsync<Mentor1>(container, query );
            stopWatch.Stop();
            var mentor3Result = new { Result = resultEmbeddedReference, ElapsedTime = FormatTime(stopWatch.Elapsed) };
            log.LogInformation($"Reference + Embedded model completed. Items: {resultEmbeddedReference.Count}  Time elapsed: {FormatTime(stopWatch.Elapsed)}");

            stopWatch.Restart();
            log.LogInformation("Query from individual collection...");
            container = await _cosmos.GetOrCreateContainerAsync("Classes1", "/ClassId");
            var resultIndividual = await _cosmos.QueryScanAsync<Mentor1>(container, 
            new QueryDefinition("Select c.id, c.MentorId, c.ClassId, c.ClassName, c.Type from c where c.MentorId = @id").WithParameter("@id", id));
            stopWatch.Stop();
            var mentor1Result = new { Result = resultIndividual, ElapsedTime = FormatTime(stopWatch.Elapsed) };
            log.LogInformation($"Individual collection completed. Items: {resultIndividual.Count} Time elapsed: {FormatTime(stopWatch.Elapsed)}");

            return (ActionResult)new OkObjectResult(new { Embedded = resultIndividual, EmbeddedReference = resultEmbeddedReference, Individual = resultIndividual });
        }

        [FunctionName("mentorsstudents")]
         public async Task<IActionResult> mentorsstudents(
                [HttpTrigger(AuthorizationLevel.Function, "get", Route = "mentors/{id}/classregistrations")] HttpRequest req,
                string id,
                ILogger log)
        {
            log.LogInformation("Get all mentor's registrations.");
            Stopwatch stopWatch = new Stopwatch();
            log.LogInformation("Getting mentors class registrations. Query: Select * from c");

            Container container = null;
            var result = new List<Mentor1>();

            stopWatch.Start();
            log.LogInformation("Query from Embeddded model...");
            container = await _cosmos.GetOrCreateContainerAsync("Mentor2", "/MentorId");
            var resultEmbedded = await _cosmos.QueryAsync<Mentor1>(container,  
            new QueryDefinition("SELECT cl.ClassId, cl.ClassName, cl.Registrations FROM c JOIN cl in c.Classes where c.MentorId = @id")
            .WithParameter("@id", id));

            stopWatch.Stop();
            var mentor2Result = new { Result = result.Count, ElapsedTime = FormatTime(stopWatch.Elapsed) };
            log.LogInformation($"Embeddded model completed. Items: {resultEmbedded.Count}  Time elapsed: {FormatTime(stopWatch.Elapsed)}");

            stopWatch.Restart();
            log.LogInformation("Query from Reference + Embedded model...");
            container = await _cosmos.GetOrCreateContainerAsync("Mentor3", "/MentorId");
            var query = new QueryDefinition("SELECT c.ClassId, c.ClassName, c.Registrations FROM c where c.MentorId = @id and c.Type = 'Class'").WithParameter("@id", id);
            var resultEmbeddedReference = await _cosmos.QueryAsync<Mentor1>(container, query );
            stopWatch.Stop();
            var mentor3Result = new { Result = resultEmbeddedReference, ElapsedTime = FormatTime(stopWatch.Elapsed) };
            log.LogInformation($"Reference + Embedded model completed. Items: {resultEmbeddedReference.Count}  Time elapsed: {FormatTime(stopWatch.Elapsed)}");

            stopWatch.Restart();
            log.LogInformation("Query from individual collection...");
            container = await _cosmos.GetOrCreateContainerAsync("Registration1", "/ClassId");
            var resultIndividual = await _cosmos.QueryScanAsync<Mentor1>(container, 
            new QueryDefinition("SELECT * FROM c where c.MentorId = @id").WithParameter("@id", id));
            stopWatch.Stop();
            var mentor1Result = new { Result = resultIndividual, ElapsedTime = FormatTime(stopWatch.Elapsed) };
            log.LogInformation($"Individual collection completed. Items: {resultIndividual.Count} Time elapsed: {FormatTime(stopWatch.Elapsed)}");

            return (ActionResult)new OkObjectResult(new { Embedded = resultIndividual, EmbeddedReference = resultEmbeddedReference, Individual = resultIndividual });
        }
    }
}


