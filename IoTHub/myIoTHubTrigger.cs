using IoTHubTrigger = Microsoft.Azure.WebJobs.EventHubTriggerAttribute;

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.EventHubs;
using System.Text;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Collections;
using System;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Azure.Documents.Linq;


namespace Uni.Assignment
{
    public class TelemetryData
    {
        [JsonProperty("id")]
        public string deviceId {get; set;}
        public double heartRate {get; set;}
        public int bloodPressureSystolic {get; set;}
        public int bloodPressureDiastolic {get; set;}
        public double bodyTemperature {get; set;}
    }
    public class myIoTHubTrigger
    {
        [FunctionName("myIoTHubTrigger")]
        public static void Run([IoTHubTrigger("messages/events", Connection = "AzureEventHubConnectionString")] EventData message,
        [CosmosDB(databaseName: "IoTData",
                                 collectionName: "TelemetryData",
                                 ConnectionStringSetting = "cosmosDBConnectionString")] out TelemetryData[] output,
                       ILogger log)
        {
            log.LogInformation($"C# IoT Hub trigger function processed a message: {Encoding.UTF8.GetString(message.Body.Array)}");
        
            var jsonBody = Encoding.UTF8.GetString(message.Body);
            dynamic data = JsonConvert.DeserializeObject(jsonBody);
            List<TelemetryData> telemetryDataList = new List<TelemetryData>();
            foreach (var telemetryJson in data)
            {
                //extracts values from telemetry JSON
                int heartRate1 = telemetryJson.heartRate;
                int bloodPressureSystolic1 = telemetryJson.bloodPressureSystolic;
                int bloodPressureDiastolic1 = telemetryJson.bloodPressureDiastolic;
                double bodyTemperature1 = telemetryJson.bodyTemperature;

                //creates telemetry data object 
                telemetryDataList.Add(new TelemetryData
                {
                    heartRate = heartRate1,
                    bloodPressureSystolic = bloodPressureSystolic1,
                    bloodPressureDiastolic = bloodPressureDiastolic1,
                    bodyTemperature = bodyTemperature1
                });
            }
            output = telemetryDataList.ToArray();
        }
        
        [FunctionName("GetTelemetry")]
        public static async Task<IActionResult> GetTelemetryAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "telemetrydata/{start}/{end}/{filter?}/{sort?}")] HttpRequest req,
        string start, string end, string filter, string sort,
        [CosmosDB(databaseName: "IoTData", 
        collectionName: "TelemetryData",
        ConnectionStringSetting = "cosmosDBConnectionString")]
        DocumentClient client, ILogger log)
        {

        DateTimeOffset startTimestamp = DateTimeOffset.Parse(start);
        DateTimeOffset endTimestamp = DateTimeOffset.Parse(end);

        var telemetryFields = new string[] { "heartRate", "bloodPressureSystolic", "bloodPressureDiastolic", "bodyTemperature" };
        var selectedFields = filter.Split(',');
        var validFields = selectedFields.Intersect(telemetryFields);
        var selectClause = string.Join(",", validFields.Select(f => $"c.{f}"));
        var sortOption = sort?.ToUpper() == "ASC" ? "ASC" : "DESC";
        var queryText = $"SELECT c.id, c._ts, {selectClause} FROM c WHERE c._ts >= @startTimestamp AND c._ts <= @endTimestamp ORDER BY c._ts {sortOption}";
        var query = new SqlQuerySpec
        {
            QueryText = queryText,
            Parameters = new SqlParameterCollection
            {
                new SqlParameter("@startTimestamp", startTimestamp.ToUnixTimeSeconds()),
                new SqlParameter("@endTimestamp", endTimestamp.ToUnixTimeSeconds())
            }
        };
        var collectionLink = UriFactory.CreateDocumentCollectionUri("IoTData", "TelemetryData");
        
        var queryOptions = new FeedOptions 
        { 
            EnableCrossPartitionQuery = true,
            PopulateQueryMetrics = false,
        };
        var documents = client.CreateDocumentQuery<Document>(collectionLink, query, queryOptions).AsDocumentQuery();

        var result = await documents.ExecuteNextAsync<Document>();
        var telemetryData = result.Select(doc => 
        {
            var selectedData = new Dictionary<string, object>();
            selectedData.Add("id", doc.GetPropertyValue<string>("id"));
            var timestamp = DateTimeOffset.FromUnixTimeSeconds(doc.GetPropertyValue<long>("_ts"));
            selectedData.Add("_ts", timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
            foreach (var field in telemetryFields)
            {
                if (selectedFields.Contains(field))
                {
                    switch (field)
                    {
                        case "heartRate":
                            selectedData.Add(field, doc.GetPropertyValue<int>("heartRate"));
                            break;
                        case "bloodPressureSystolic":
                            selectedData.Add(field, doc.GetPropertyValue<int>("bloodPressureSystolic"));
                            break;
                        case "bloodPressureDiastolic":
                            selectedData.Add(field, doc.GetPropertyValue<int>("bloodPressureDiastolic"));
                            break;
                        case "bodyTemperature":
                            selectedData.Add(field, doc.GetPropertyValue<double>("bodyTemperature"));
                            break;
                    }
                }
            }
            return selectedData;
        });
        var settings = new JsonSerializerSettings{
            Formatting = Formatting.Indented
        };
        var json = JsonConvert.SerializeObject(telemetryData, settings);
        return new OkObjectResult(json);
        }
    }
}