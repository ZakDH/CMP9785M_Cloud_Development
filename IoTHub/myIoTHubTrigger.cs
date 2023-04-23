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
    //class that represents the JSON variable getters and setters
    public class TelemetryData
    {
        [JsonProperty("id")]
        public string deviceId {get; set;}
        public double heartRate {get; set;}
        public int bloodPressureSystolic {get; set;}
        public int bloodPressureDiastolic {get; set;}
        public double bodyTemperature {get; set;}
    }
    //class which contains IoT Hub functions
    public class myIoTHubTrigger
    {
        [FunctionName("myIoTHubTrigger")]
        //function triggers on incoming message from simulation program
        //sends incoming data to cosmos db
        public static void Run([IoTHubTrigger("messages/events", Connection = "AzureEventHubConnectionString")] EventData message,
        [CosmosDB(databaseName: "IoTData",
                                 collectionName: "TelemetryData",
                                 ConnectionStringSetting = "cosmosDBConnectionString")] out TelemetryData[] output,
                       ILogger log)
        {
            log.LogInformation($"C# IoT Hub trigger function processed a message: {Encoding.UTF8.GetString(message.Body.Array)}");
            //deserialises the incomign message from JSON to an object
            var jsonBody = Encoding.UTF8.GetString(message.Body);
            dynamic data = JsonConvert.DeserializeObject(jsonBody);
            List<TelemetryData> telemetryDataList = new List<TelemetryData>();
            //loops through each message item
            foreach (var telemetryJson in data)
            {
                //extracts values from telemetry object
                int heartRate1 = telemetryJson.heartRate;
                int bloodPressureSystolic1 = telemetryJson.bloodPressureSystolic;
                int bloodPressureDiastolic1 = telemetryJson.bloodPressureDiastolic;
                double bodyTemperature1 = telemetryJson.bodyTemperature;

                //creates telemetry data object and adds them to a list
                telemetryDataList.Add(new TelemetryData
                {
                    heartRate = heartRate1,
                    bloodPressureSystolic = bloodPressureSystolic1,
                    bloodPressureDiastolic = bloodPressureDiastolic1,
                    bodyTemperature = bodyTemperature1
                });
            }
            //list is converted to an array and assigned to 'output'
            output = telemetryDataList.ToArray();
        }
        //function that takes telemetry data from the cosmos db
        [FunctionName("GetTelemetry")]
        public static async Task<IActionResult> GetTelemetryAsync(
        //uses GET request using the route which holds manditory and optional parameters
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "telemetrydata/{start}/{end}/{filter?}/{sort?}")] HttpRequest req,
        string start, string end, string filter, string sort,
        [CosmosDB(databaseName: "IoTData", 
        collectionName: "TelemetryData",
        ConnectionStringSetting = "cosmosDBConnectionString")]
        DocumentClient client, ILogger log)
        {
        //parses the timestamp from the route parameter
        DateTimeOffset startTimestamp = DateTimeOffset.Parse(start);
        DateTimeOffset endTimestamp = DateTimeOffset.Parse(end);
        //configures a SQL query which is sent to the cosmos db to get the required data
        var telemetryFields = new string[] { "heartRate", "bloodPressureSystolic", "bloodPressureDiastolic", "bodyTemperature" };
        var selectedFields = filter.Split(',');
        //finds out what data intersects between the route parameter and the telemetryfields array
        var validFields = selectedFields.Intersect(telemetryFields);
        //data fields are joined to match cosmos db syntax
        var selectClause = string.Join(",", validFields.Select(f => $"c.{f}"));
        var sortOption = sort?.ToUpper() == "ASC" ? "ASC" : "DESC";
        //constructed sql query which is used to get required data from cosmos db
        var queryText = $"SELECT c.id, c._ts, {selectClause} FROM c WHERE c._ts >= @startTimestamp AND c._ts <= @endTimestamp ORDER BY c._ts {sortOption}";
        //converts timestamps from route parameter to unix timeframe
        var query = new SqlQuerySpec
        {
            QueryText = queryText,
            Parameters = new SqlParameterCollection
            {
                new SqlParameter("@startTimestamp", startTimestamp.ToUnixTimeSeconds()),
                new SqlParameter("@endTimestamp", endTimestamp.ToUnixTimeSeconds())
            }
        };
        //document collection used to parse all the data from cosmos db to more user friendly format
        var collectionLink = UriFactory.CreateDocumentCollectionUri("IoTData", "TelemetryData");
        //removes data taken from cosmos db. eg: system properties
        var queryOptions = new FeedOptions 
        { 
            EnableCrossPartitionQuery = true,
            PopulateQueryMetrics = false,
        };
        //adds the data from cosmos db to 'documents' variable
        var documents = client.CreateDocumentQuery<Document>(collectionLink, query, queryOptions).AsDocumentQuery();

        var result = await documents.ExecuteNextAsync<Document>();
        //configures data results from database
        var telemetryData = result.Select(doc => 
        {
            //adds 'id' and '_ts' fields regardless of the switch method
            var selectedData = new Dictionary<string, object>();
            selectedData.Add("id", doc.GetPropertyValue<string>("id"));
            var timestamp = DateTimeOffset.FromUnixTimeSeconds(doc.GetPropertyValue<long>("_ts"));
            //converts '._ts' data field into more traditional time format
            selectedData.Add("_ts", timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
            //used to add data fields if they are selected by user
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
        //indexes the received data so it is more user friendly
        var settings = new JsonSerializerSettings{
            Formatting = Formatting.Indented
        };
        //serialises the object into JSON
        var json = JsonConvert.SerializeObject(telemetryData, settings);
        return new OkObjectResult(json);
        }
    }
}