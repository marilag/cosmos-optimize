
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
using System.Threading;

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

            Stopwatch stopWatchInsertOperation = new Stopwatch();

            try
            {
                log.LogInformation($"Insert operation started...");
                stopWatchInsertOperation.Start();
                ///COMMENT THIS OUT TO TEST BULK EXECUTION
                _cosmos.CosmosClientOptions.AllowBulkExecution = true;
                await _cosmos.cosmosDatabase.ReplaceThroughputAsync(10000);
                log.LogInformation($"AllowBulkExecution set to true. RU increased to 5000");

                var TaskInsertItem = new List<Task>();

                var maxThrottler = 20;
                var throttler = new SemaphoreSlim(maxThrottler);

                /// Embedded model
                /// 1 collection - Mentor 2
                /// 1 document for Mentor, Class and Registrations
                log.LogInformation($"Setup Embedded Model...");
                var EmbeddedModel = JsonConvert.DeserializeObject<List<Mentor2>>(json);

                double maxEmbeddedRU = 0.00;
                int itemsProcessed = 0;
                EmbeddedModel.ForEach(m =>
               {
                   m.Classes.ForEach(c =>
                   {
                       c.MentorId = m.MentorId;
                       c.MentorName = m.Name;
                       c.MentorEmail = m.Email;
                       c.Registrations.ForEach(r =>
                       {
                           r.ClassId = c.ClassId;
                           r.ClassName = c.ClassName;
                           r.ClassDate = c.Date;
                           r.ClassAddress = c.Address;
                           r.MentorId = c.MentorId;
                           r.MentorName = c.MentorName;
                           r.MentorEmail = c.MentorEmail;
                       });
                   }
               );
                   throttler.Wait();
                   TaskInsertItem.Add(Task.Run(async () =>
                   {
                       await Mentor2.UpsertItemAsync(m).ContinueWith(itemresponse =>
                       {
                           maxEmbeddedRU = (maxEmbeddedRU < itemresponse.Result.RequestCharge) ? itemresponse.Result.RequestCharge : maxEmbeddedRU;
                           itemsProcessed++;
                       });
                       throttler.Release();
                   }));
               });
                Task.WaitAll(TaskInsertItem.ToArray());
                stopWatch.Stop();
                var EmbeddedModelElapsedTime = FormatTime(stopWatch.Elapsed);
                log.LogInformation($"Insert Embedded model completed.  Items: {EmbeddedModel.Count} Process: {itemsProcessed} Max RU: {maxEmbeddedRU} Time elapsed:{EmbeddedModelElapsedTime}");

                /// Reference+Embedded model
                /// 1 collection - Mentor 3
                /// Separate Mentor document,Combined  Class + Registration
                log.LogInformation($"Setup Reference + Embedded Model...");
                var MentorOnly = JsonConvert.DeserializeObject<List<Mentor1>>(json);

                TaskInsertItem = new List<Task>();
                stopWatch.Restart();
                double MentorOnlyRU = 0.00;
                MentorOnly.ForEach(m =>
                {
                    throttler.Wait();
                    TaskInsertItem.Add(Task.Run(async () =>
                   {
                       await Mentor3.UpsertItemAsync(m)
                       .ContinueWith(itemresponse => { MentorOnlyRU = (MentorOnlyRU > itemresponse.Result.RequestCharge) ? MentorOnlyRU : itemresponse.Result.RequestCharge; });
                       throttler.Release();
                   }));
                });

                await Task.WhenAll(TaskInsertItem);
                TaskInsertItem = new List<Task>();
                double classReferenceMaxRU = 0.00;

                var embeddedmodel = (from m in EmbeddedModel
                                     from c in m.Classes
                                     select c).ToList();


                embeddedmodel.ForEach(r =>
                {
                    throttler.Wait();
                    TaskInsertItem.Add(Task.Run(async () =>
                   {
                       await Mentor3.UpsertItemAsync(r)
                       .ContinueWith(itemresponse => { classReferenceMaxRU = (classReferenceMaxRU > itemresponse.Result.RequestCharge) ? classReferenceMaxRU : itemresponse.Result.RequestCharge; });
                       throttler.Release();
                   }));
                });

                await Task.WhenAll(TaskInsertItem);
                stopWatch.Stop();
                var EmbeddedReferenceModelTimeElapsed = FormatTime(stopWatch.Elapsed);

                log.LogInformation($"Insert Reference + Embedded Model completed. Classes: {embeddedmodel.Count} MaxRU: {classReferenceMaxRU}  Mentors: {MentorOnly.Count} MaxRU: {MentorOnlyRU}  Time elapsed:{EmbeddedReferenceModelTimeElapsed}");


                /// Separate Collections
                /// 1 collection each for Mentor, Class, Regisrations
                /// Mentor
                log.LogInformation($"Setup separate collections model...");
                maxThrottler = 10;
                stopWatch.Restart();
                TaskInsertItem = new List<Task>();

                double IndividualMentorMaxRU = 0.00;
                MentorOnly.ForEach(m =>
               {
                   throttler.Wait();
                   TaskInsertItem.Add(Task.Run(async () =>
                   {
                       await Mentor1.UpsertItemAsync(m)
                                    .ContinueWith(itemresponse => { IndividualMentorMaxRU = (IndividualMentorMaxRU > itemresponse.Result.RequestCharge) ? IndividualMentorMaxRU : itemresponse.Result.RequestCharge; });

                       throttler.Release();
                   }));
               });

                await Task.WhenAll(TaskInsertItem);
                stopWatch.Stop();
                var IndividualMentorElapsed = FormatTime(stopWatch.Elapsed);


                stopWatch.Restart();
                double individualClassRU = 0.00;
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

                Model1Classes.ForEach(c =>
               {
                   throttler.Wait();
                   TaskInsertItem.Add(Task.Run(async () =>
                   {
                       await Classes1.UpsertItemAsync(c)
                                   .ContinueWith(itemresponse => { individualClassRU = (individualClassRU > itemresponse.Result.RequestCharge) ? individualClassRU : itemresponse.Result.RequestCharge; });

                       throttler.Release();
                   }));
               });

                await Task.WhenAll(TaskInsertItem);
                stopWatch.Stop();

                var IndividualClassesElapsed = FormatTime(stopWatch.Elapsed);

                ///Registration

                stopWatch.Restart();
                TaskInsertItem = new List<Task>();
                double IndividualRegistrationMaxRU = 0.00;
                var Model1Regisrations = (from m in EmbeddedModel
                                          from c in m.Classes
                                          from r in c.Registrations
                                          select r).ToList();

                log.LogInformation($"Insert {Model1Regisrations.Count} Registrations...");

                Model1Regisrations.ForEach(r =>
               {
                   throttler.Wait();
                   TaskInsertItem.Add(Task.Run(async () =>
                   {
                       await Registration1.UpsertItemAsync(r)
                             .ContinueWith(itemresponse => { IndividualRegistrationMaxRU = (IndividualRegistrationMaxRU > itemresponse.Result.RequestCharge) ? IndividualRegistrationMaxRU : itemresponse.Result.RequestCharge; });

                       throttler.Release();
                   }));
               });

                await Task.WhenAll(TaskInsertItem);
                throttler.Release(maxThrottler);

                stopWatch.Stop();
                stopWatchInsertOperation.Stop();
                var IndividualRegistrationElapsed = FormatTime(stopWatch.Elapsed);



                log.LogInformation($"Insert Embedded model completed. Items: {EmbeddedModel.Count} Max RU: {maxEmbeddedRU} Time elapsed:{EmbeddedModelElapsedTime}");
                log.LogInformation($"Insert Reference + Embedded Model completed. Classes: {embeddedmodel.Count} MaxRU: {classReferenceMaxRU}  Mentors: {MentorOnly.Count} MaxRU: {MentorOnlyRU}  Time elapsed:{EmbeddedReferenceModelTimeElapsed}");
                /*                 log.LogInformation($"Insert Synthetic Key Registration completed. Items: {Model2Registrations.Count} maxRU: {registrationSyntheticMaxRU} Time elapsed:{SyntheticKeyTimeElapsed}");
                 */
                log.LogInformation($"Insert individual Mentor completed. Items: {MentorOnly.Count} MaxRU: {IndividualMentorMaxRU} Time elapsed:{IndividualMentorElapsed}");
                log.LogInformation($"Insert Classes completed. Items: {Model1Classes.Count} MaxRU: {individualClassRU} Time elapsed:{IndividualClassesElapsed}");
                log.LogInformation($"Insert Registration completed.Items: {Model1Regisrations.Count} MaxRU: {IndividualRegistrationMaxRU} Time elapsed:{IndividualRegistrationElapsed}");
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
                log.LogInformation($"Seed completed. Insert operation elapsed time : {FormatTime(stopWatchInsertOperation.Elapsed)}");
            }

            return (ActionResult)new OkResult();
        }

        [FunctionName("getmentors")]
        public async Task<IActionResult> getmentors(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "mentors")] HttpRequest req,
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
            var resultEmbeddedReference = await _cosmos.QueryAsync<Mentor1>(container, query);
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
            var resultEmbeddedReference = await _cosmos.QueryAsync<Mentor1>(container, query);
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

        [FunctionName("mentoritem")]
        public async Task<IActionResult> mentoritem(
                [HttpTrigger(AuthorizationLevel.Function, "get", Route = "mentors/{id}")] HttpRequest req,
                string id,
                ILogger log)
        {
            log.LogInformation("Get mentor.");
            Stopwatch stopWatch = new Stopwatch();
            var container = await _cosmos.GetOrCreateContainerAsync("Mentor2", "/MentorId");
            req.GetQueryParameterDictionary().TryGetValue("mode", out var mode);

            if (mode == "1")
            {
                stopWatch.Start();
                var result = await _cosmos.QueryAsync<Mentor1>(container,
                new QueryDefinition("Select * from c where c.MentorId = @id").WithParameter("@id", id));
                stopWatch.Stop();
                var queryResult = $"Using query. Item: {result.Count} Elapsed: {FormatTime(stopWatch.Elapsed)}";
                log.LogInformation(queryResult);
                return (ActionResult)new OkObjectResult(queryResult);
            }
            else
            {
                stopWatch.Restart();
                var result = await _cosmos.ReadItemAsync<Mentor1>(container, id, id);
                stopWatch.Stop();
                var queryResult = $"Using ReadItemAsync. Item: {result == null} Elapsed: {FormatTime(stopWatch.Elapsed)}";

                log.LogInformation(queryResult);
                return (ActionResult)new OkObjectResult(queryResult);
            }

        }

        [FunctionName("mentorscount")]
        public async Task<IActionResult> mentorscount(
               [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req,
               ILogger log)
        {
            log.LogInformation("Get mentor count.");
            Stopwatch stopWatch = new Stopwatch();

            var query = new QueryDefinition("Select value count(c) from c");
            stopWatch.Start();
            var container = await _cosmos.GetOrCreateContainerAsync("MentorConsistent", "/MentorId");
            var resultcharge = 0.00;
            var count = 0;
            await _cosmos.QueryAsync<int>(container, query).ContinueWith(
                itemresponse =>
                {
                    count = itemresponse.Result.FirstOrDefault();
                }
            );
            stopWatch.Stop();

            log.LogInformation($"Items: {count} Elapsed: {FormatTime(stopWatch.Elapsed)} RU= {resultcharge}");

            return (ActionResult)new OkObjectResult($"Items: {count} Elapsed: {FormatTime(stopWatch.Elapsed)} RU= {resultcharge}");
        }

        [FunctionName("postmentors")]
        public async Task<IActionResult> postmentors(
               [HttpTrigger(AuthorizationLevel.Function, "post", Route = "mentors")] HttpRequest req,
               ILogger log
               )
        {

            req.GetQueryParameterDictionary().TryGetValue("c", out var consistency);
            log.LogInformation("Get mentor count.");
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            var container = await _cosmos.GetOrCreateContainerAsync("MentorConsistent", "/MentorId");
            var mentorId = System.Guid.NewGuid().ToString();
            var requestCharge = 0.00;
            var consistencyLevel = (consistency == "1") ? ConsistencyLevel.Strong : ConsistencyLevel.Eventual;
            await container.CreateItemAsync(new Mentor1()
            {
                MentorId = mentorId,
                Name = "Marilag Dimatulac",
                About = "I'm a software developer",
                Address = "Female"
            }, new PartitionKey(mentorId), new ItemRequestOptions() { ConsistencyLevel = consistencyLevel })
            .ContinueWith(itemResponse => { requestCharge = itemResponse.Result.RequestCharge; });
            stopWatch.Start();

            log.LogInformation($"Insert completed with {consistencyLevel.ToString()} consistency. Elapsed: {FormatTime(stopWatch.Elapsed)} RU= {requestCharge}");
            return (ActionResult)new OkObjectResult($"Insert completed. Elapsed: {FormatTime(stopWatch.Elapsed)} RU= {requestCharge}");

        }

    }
}


