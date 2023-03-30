using IoTHubTrigger = Microsoft.Azure.WebJobs.EventHubTriggerAttribute;

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.EventHubs;
using System.Text;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Collections.Generic;

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
        private static HttpClient client = new HttpClient();
        
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
    }
}