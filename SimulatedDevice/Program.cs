// Copyright (c) Microsoft. All rights reserved.
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
    class TelemetryData
    {
        public string deviceId {get; set;}
        public double heartRate {get; set;}
        public int bloodPressureSystolic {get; set;}
        public int bloodPressureDiastolic {get; set;}
        public double bodyTemperature {get; set;}
    }

    internal class Program
    {
        private static DeviceClient s_deviceClient;
        private static readonly TransportType s_transportType = TransportType.Mqtt;
        private static TimeSpan s_telemetryInterval = TimeSpan.FromSeconds(5);

        // The device connection string to authenticate the device with your IoT hub.
        // Using the Azure CLI:
        // az iot hub device-identity show-connection-string --hub-name {YourIoTHubName} --device-id MyDotnetDevice --output table
        private static string s_connectionString = "HostName=ZakDHTempSensorHub.azure-devices.net;DeviceId=zakdh_simdevice;SharedAccessKey=62akxBWSOQ/wCItrYHGM/soDDarf3IB6RCkl2hkdMi8=";
		
        private static async Task Main(string[] args)
        {
            Console.WriteLine("IoT Hub Quickstarts #1 - Simulated device.");

            // This sample accepts the device connection string as a parameter, if present
            ValidateConnectionString(args);

            // Connect to the IoT hub using the MQTT protocol
            s_deviceClient = DeviceClient.CreateFromConnectionString(s_connectionString, s_transportType);

            await s_deviceClient.SetMethodHandlerAsync("SetTelemetryInterval", SetTelemetryInterval, null);

            // Set up a condition to quit the sample
            Console.WriteLine("Press control-C to exit.");
            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cts.Cancel();
                Console.WriteLine("Exiting...");
            };

            // Run the telemetry loop
            await SendDeviceToCloudMessagesAsync(cts.Token);

            s_deviceClient.Dispose();
            Console.WriteLine("Device simulator finished.");
        }

        private static Task<MethodResponse> SetTelemetryInterval(MethodRequest methodRequest, object userContext){
            string data = Encoding.UTF8.GetString(methodRequest.Data);
            if (int.TryParse(data, out int telemetryIntervalInSeconds))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Telemetry interval set to {s_telemetryInterval}");
                Console.ResetColor();

                // Acknowlege the direct method call with a 200 success message.
                string result = $"{{\"result\":\"Executed direct method: {methodRequest.Name}\"}}";
                return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 200));
            }
            else
            {
                // Acknowlege the direct method call with a 400 error message.
                string result = "{\"result\":\"Invalid parameter\"}";
                return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 400));
            }
        }

        private static void ValidateConnectionString(string[] args)
        {
            if (args.Any())
            {
                try
                {
                    var cs = IotHubConnectionStringBuilder.Create(args[0]);
                    s_connectionString = cs.ToString();
                }
                catch (Exception)
                {
                    Console.WriteLine($"Error: Unrecognizable parameter '{args[0]}' as connection string.");
                    Environment.Exit(1);
                }
            }
            else
            {
                try
                {
                    _ = IotHubConnectionStringBuilder.Create(s_connectionString);
                }
                catch (Exception)
                {
                    Console.WriteLine("This sample needs a device connection string to run. Program.cs can be edited to specify it, or it can be included on the command-line as the only parameter.");
                    Environment.Exit(1);
                }
            }
        }

        private static async Task SendDeviceToCloudMessagesAsync(CancellationToken ct)
        {
            var rand = new Random();

            while (!ct.IsCancellationRequested)
            {
                var telemetryDataArray = new[]
                {
                    new TelemetryData
                    {
                        heartRate = (int)Math.Ceiling(50 + rand.NextDouble() * 50),
                        bloodPressureSystolic = rand.Next(90, 140),
                        bloodPressureDiastolic = rand.Next(60, 90),
                        bodyTemperature = Math.Round(36.5 + rand.NextDouble() * 3, 1),
                        deviceId = "zakdh_simdevice"
                    },
                };

                var telemetryJson = JsonConvert.SerializeObject(telemetryDataArray);
                var telemetryMessage = new Message(Encoding.ASCII.GetBytes(telemetryJson));

                telemetryMessage.Properties.Add("heartRateAlert", (telemetryDataArray[0].heartRate > 90) ? "true" : "false");
                telemetryMessage.Properties.Add("bloodPressureSystolicAlert", (telemetryDataArray[0].bloodPressureSystolic > 120) ? "true" : "false");
                telemetryMessage.Properties.Add("bloodPressureDiastolicAlert", (telemetryDataArray[0].bloodPressureDiastolic > 80) ? "true" : "false");
                telemetryMessage.Properties.Add("bodyTemperatureAlert", (telemetryDataArray[0].bodyTemperature > 38) ? "true" : "false");

                await s_deviceClient.SendEventAsync(telemetryMessage);

                Console.WriteLine($"{DateTime.Now} > Sending message: {telemetryJson}");

                await Task.Delay(s_telemetryInterval);
            }
        }
    }
}