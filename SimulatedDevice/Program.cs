﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// This application uses the Azure IoT Hub device SDK for .NET
// For samples see: https://github.com/Azure/azure-iot-sdk-csharp/tree/master/iothub/device/samples

using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SimulatedDevice
{
    /// reference - https://github.com/Azure/azure-iot-sdk-csharp/blob/main/iothub/device/samples/getting%20started/SimulatedDeviceWithCommand/Program.cs
    /// <summary>
    /// This sample illustrates the very basics of a device app sending telemetry. For a more comprehensive device app sample, please see
    /// <see href="https://github.com/Azure-Samples/azure-iot-samples-csharp/tree/master/iot-hub/Samples/device/DeviceReconnectionSample"/>.
    /// </summary>
    
    //telemetry data declarations
    class TelemetryData
    {
        public string deviceId {get; set;}
        public int heartRate {get; set;}
        public int bloodPressureSystolic {get; set;}
        public int bloodPressureDiastolic {get; set;}
        public double bodyTemperature {get; set;}
    }

    internal class Program
    {
        private static DeviceClient s_deviceClient;
        private static readonly TransportType s_transportType = TransportType.Mqtt;
        private static TimeSpan s_telemetryInterval = TimeSpan.FromSeconds(5); //allows for 5 second delay between each message

        // The device connection string to authenticate the device with your IoT hub.
        private static string s_connectionString = "HostName=ZakDHTempSensorHub.azure-devices.net;DeviceId=zakdh_simdevice;SharedAccessKey=uj+eyQFcn/h2at+H+Q+ifZfWVP8iBKDuhCPsU9MpBEg=";
		
        private static async Task Main(string[] args)
        {
            Console.WriteLine("IoT Hub Quickstarts #1 - Simulated device.");

            // This sample accepts the device connection string as a parameter, if present
            ValidateConnectionString(args);

            // Connect to the IoT hub using the MQTT protocol
            s_deviceClient = DeviceClient.CreateFromConnectionString(s_connectionString, s_transportType);

            //setup method handler to receive telemetry interval
            await s_deviceClient.SetMethodHandlerAsync("SetTelemetryInterval", SetTelemetryInterval, null);

            // Set up a condition to quit the sample
            Console.WriteLine("Press control-C to exit.");
            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cts.Cancel(); //cancel telemetry loop when the user presses control c
                Console.WriteLine("Exiting...");
            };

            // Run the telemetry loop
            await SendDeviceToCloudMessagesAsync(cts.Token);

            //clean up resources when telemetry loop stops
            s_deviceClient.Dispose();
            Console.WriteLine("Device simulator finished.");
        }

        //Used to set an interval between each telemetry message
        private static Task<MethodResponse> SetTelemetryInterval(MethodRequest methodRequest, object userContext){
            string data = Encoding.UTF8.GetString(methodRequest.Data);
            if (int.TryParse(data, out int telemetryIntervalInSeconds))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Telemetry interval set to {s_telemetryInterval}"); //sets the interval time to s_telemetryInterval value (5)
                Console.ResetColor();

                // Acknowledge the direct method call with a 200 success message.
                string result = $"{{\"result\":\"Executed direct method: {methodRequest.Name}\"}}";
                return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 200));
            }
            else
            {
                // Acknowledge the direct method call with a 400 error message.
                string result = "{\"result\":\"Invalid parameter\"}";
                return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 400));
            }
        }


        //Validate the connection string otherwise throw error message.
        private static void ValidateConnectionString(string[] args)
        {
            if (args.Any()) //check if arguments are passed
            {
                try
                {
                    var cs = IotHubConnectionStringBuilder.Create(args[0]); //try create IoT connection from first argument 
                    s_connectionString = cs.ToString(); //store connection string in variable
                }
                catch (Exception)
                {
                    Console.WriteLine($"Error: Unrecognizable parameter '{args[0]}' as connection string.");
                    Environment.Exit(1); //exit program with error message above
                }
            }
            else //if no arguments are passed
            {
                try
                {
                    _ = IotHubConnectionStringBuilder.Create(s_connectionString); //create iot connection from connection string 
                }
                catch (Exception) //if connection string is not valid exit program with error message
                {
                    Console.WriteLine("This sample needs a device connection string to run. Program.cs can be edited to specify it, or it can be included on the command-line as the only parameter.");
                    Environment.Exit(1); 
                }
            }
        }

        //main function to send simulated data to IoT trigger
        private static async Task SendDeviceToCloudMessagesAsync(CancellationToken ct)
        {
            //rand variable to give random telemetry readings
            var rand = new Random();

            while (!ct.IsCancellationRequested)
            {
                //places telemetry data into a new array
                //allows data to be sent as a JSON payload rather than JSON object
                var telemetryDataArray = new[]
                {
                    new TelemetryData
                    {
                        heartRate = (int)Math.Ceiling(50 + rand.NextDouble() * 50), //gives random number between 50 and 100 - rounded to nearest whole number
                        bloodPressureSystolic = rand.Next(90, 140), //gives random number between 90 and 140
                        bloodPressureDiastolic = rand.Next(60, 90), //gives random number between 60 and 90
                        bodyTemperature = Math.Round(36.5 + rand.NextDouble() * 3, 1), //gives random number between 36.5 and 39.5 and rounded to 1 decimal place
                        deviceId = "zakdh_simdevice" //assigns id to simulated device
                    },
                };
                JsonSerializerSettings settings = new JsonSerializerSettings { Formatting = Formatting.Indented };

                var telemetryJson = JsonConvert.SerializeObject(telemetryDataArray, settings); //serialises the JSON payload so it can be transmitted to IoT hub
                var telemetryMessage = new Message(Encoding.ASCII.GetBytes(telemetryJson));

                telemetryMessage.Properties.Add("heartRateAlert", (telemetryDataArray[0].heartRate > 90) ? "true" : "false"); //sets alert to true if heart rate is greater than 90
                telemetryMessage.Properties.Add("bloodPressureSystolicAlert", (telemetryDataArray[0].bloodPressureSystolic > 120) ? "true" : "false"); //alert is equal to true if blood pressure is above 120
                telemetryMessage.Properties.Add("bloodPressureDiastolicAlert", (telemetryDataArray[0].bloodPressureDiastolic > 80) ? "true" : "false");//alert is equal to true if blood pressure is above 80
                telemetryMessage.Properties.Add("bodyTemperatureAlert", (telemetryDataArray[0].bodyTemperature > 38) ? "true" : "false"); //alert is equal to true if body temperature is above 38

                await s_deviceClient.SendEventAsync(telemetryMessage); //sends telemetry data to IoT hub

                Console.WriteLine($"{DateTime.Now} > Sending message: {telemetryJson}"); //displays telemetry data to console 

                await Task.Delay(s_telemetryInterval); //next message is sent after s_telemetryInterval is passed - 5 seconds
            }
        }
    }
}