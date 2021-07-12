using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Provisioning.Client;
using Microsoft.Azure.Devices.Provisioning.Client.Transport;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace HomeStation.Simulator
{
    class Program
    {
        public enum ButtonTaps
        {
            ButtonA,
            ButtonB,
            ButtonC
        }

        // Telemetry globals.
        const int intervalInMilliseconds = 5000;        // Time interval required by wait function.

        // Home Station globals.
        static int hsNum = 001;
        static string homeStationIdentification = "HS-" + hsNum;

        static double optimalTemperature = -5;          // Setting - can be changed by the operator from IoT Central.
        static double optimalPressure = 700;          // Setting - can be changed by the operator from IoT Central.

        // User IDs.
        static string IDScope = "";
        static string DeviceID = "";
        static string PrimaryKey = "";

        // IoT Central global variables.
        static DeviceClient s_deviceClient;
        static CancellationTokenSource cts;
        static string GlobalDeviceEndpoint = "global.azure-devices-provisioning.net";
        static TwinCollection reportedProperties = new TwinCollection();

        static void Main(string[] args)
        {
            try
            {
                using (var security = new SecurityProviderSymmetricKey(DeviceID, PrimaryKey, null))
                {
                    DeviceRegistrationResult result = RegisterDeviceAsync(security).GetAwaiter().GetResult();
                    if (result.Status != ProvisioningRegistrationStatusType.Assigned)
                    {
                        Console.WriteLine("Failed to register device");
                        return;
                    }
                    IAuthenticationMethod auth = new DeviceAuthenticationWithRegistrySymmetricKey(result.DeviceId, (security as SecurityProviderSymmetricKey).GetPrimaryKey());
                    s_deviceClient = DeviceClient.Create(result.AssignedHub, auth, TransportType.Mqtt);
                }

                greenMessage("Device successfully connected to Azure IoT Central");

                SendDevicePropertiesAsync().GetAwaiter().GetResult();

                Console.Write("Register settings changed handler...");
                s_deviceClient.SetDesiredPropertyUpdateCallbackAsync(OnOptimalTempSettingChanged, null).GetAwaiter().GetResult();
                s_deviceClient.SetDesiredPropertyUpdateCallbackAsync(OnOptimalPressureSettingChanged, null).GetAwaiter().GetResult();
                Console.WriteLine("Done");

                cts = new CancellationTokenSource();

                // Create a handler for the direct method calls.
                s_deviceClient.SetMethodHandlerAsync("FlashLeds", CmdFlashLeds, null).Wait();
                s_deviceClient.SetMethodHandlerAsync("DisplayMessage", CmdDisplayMessage, null).Wait();
                s_deviceClient.SetMethodHandlerAsync("TakePhoto", CmdTakePhoto, null).Wait();

                SendHomeStationTelemetryAsync(cts.Token);

                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                cts.Cancel();

            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine(ex.Message);
            }
        }

        static void colorMessage(string text, ConsoleColor clr)
        {
            Console.ForegroundColor = clr;
            Console.WriteLine(text + "\n");
            Console.ResetColor();
        }
        static void greenMessage(string text)
        {
            colorMessage(text, ConsoleColor.Green);
        }

        static void redMessage(string text)
        {
            colorMessage(text, ConsoleColor.Red);
        }

        static void blueMessage(string text)
        {
            colorMessage(text, ConsoleColor.Blue);
        }

        static string GetButtonTap(Random rand)
        {
            
            var tap = rand.Next(0, 3);
            switch (tap)
            {
                case 0:
                    return ButtonTaps.ButtonA.ToString();
                case 1:
                    return ButtonTaps.ButtonB.ToString();
                case 2:
                    return ButtonTaps.ButtonC.ToString();
                default:
                    return ButtonTaps.ButtonC.ToString();
            }
        }

        static Task<MethodResponse> CmdFlashLeds(MethodRequest methodRequest, object userContext)
        {
            try
            {
                // Pick up variables from the request payload by using the name specified in IoT Central.
                var payloadString = JsonConvert.DeserializeObject<string>(methodRequest.DataAsJson);
                var flashInfo = payloadString.Split("-");

                var ledString = flashInfo.First();
                var ledNos = ledString.Split(",");

                var leds = ledNos.Select(ledNo => Convert.ToInt32(ledNo)).ToArray();
                var flashTimes = Convert.ToInt32(flashInfo.Last());


                blueMessage($"Flash Leds {ledString} for {flashTimes} times");

                // Acknowledge the direct method call with a 200 success message.
                string result = "{\"result\":\"Executed direct method: " + methodRequest.Name + "\"}";
                return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 200));
            }
            catch (Exception ex)
            {
                redMessage($"Invalid payload {ex.Message}");

                // Acknowledge the direct method call with a 400 error message.
                string result = "{\"result\":\"Invalid payload\"}";
                return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 400));
            }
        }

        static Task<MethodResponse> CmdDisplayMessage(MethodRequest methodRequest, object userContext)
        {
            // Pick up variables from the request payload by using the name specified in IoT Central.
            var payloadString = JsonConvert.DeserializeObject<string>(methodRequest.DataAsJson);

            blueMessage($"Display : {payloadString}");

            // Acknowledge the direct method call with a 200 success message.
            string result = "{\"result\":\"Executed direct method: " + methodRequest.Name + "\"}";
            return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 200));

        }

        static Task<MethodResponse> CmdTakePhoto(MethodRequest methodRequest, object userContext)
        {
            blueMessage($"Take photo Executed");

            // Acknowledge the direct method call with a 200 success message.
            string result = "{\"result\":\"Executed direct method: " + methodRequest.Name + "\"}";
            return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 200));
        }



        static async void SendHomeStationTelemetryAsync(CancellationToken token)
        {
            while (true)
            {
                var rand = new Random();

                // Create the telemetry JSON message.
                var telemetryDataPoint = new
                {
                    RoomTemperature = Math.Round((new Random().NextDouble() * (45.0 - 16.5) + 16.5), 2),
                    AtmosphericPressure = Math.Round((new Random().NextDouble() * (1235.5 - 750.9) + 750.9), 2),
                    ButtonTap = GetButtonTap(rand),
                };
                var telemetryMessageString = JsonSerializer.Serialize(telemetryDataPoint);
                var telemetryMessage = new Message(Encoding.ASCII.GetBytes(telemetryMessageString));

                Console.WriteLine($"Telemetry data: {telemetryMessageString}");

                // Bail if requested.
                token.ThrowIfCancellationRequested();

                // Send the telemetry message.
                await s_deviceClient.SendEventAsync(telemetryMessage);
                greenMessage($"Telemetry sent {DateTime.Now.ToShortTimeString()}");

                await Task.Delay(intervalInMilliseconds);
            }
        }


        static async Task OnOptimalTempSettingChanged(TwinCollection desiredProperties, object userContext)
        {
            string setting = "OptimalTemperature";
            if (desiredProperties.Contains(setting))
            {
                optimalTemperature = reportedProperties[setting] = desiredProperties[setting];
                greenMessage($"Optimal temperature updated: {optimalTemperature}");
            }
            await s_deviceClient.UpdateReportedPropertiesAsync(reportedProperties);
        }

        static async Task OnOptimalPressureSettingChanged(TwinCollection desiredProperties, object userContext)
        {
            string setting = "OptimalPressure";
            if (desiredProperties.Contains(setting))
            {
                optimalTemperature = reportedProperties[setting] = desiredProperties[setting];
                greenMessage($"Optimal pressure updated: {optimalPressure}");
            }
            await s_deviceClient.UpdateReportedPropertiesAsync(reportedProperties);
        }

        static async Task SendDevicePropertiesAsync()
        {
            reportedProperties["ATKID"] = homeStationIdentification;
            await s_deviceClient.UpdateReportedPropertiesAsync(reportedProperties);
            greenMessage($"Sent device properties: {reportedProperties["ATKID"]}");
        }

        public static async Task<DeviceRegistrationResult> RegisterDeviceAsync(SecurityProviderSymmetricKey security)
        {
            Console.WriteLine("Register device...");

            using (var transport = new ProvisioningTransportHandlerMqtt(TransportFallbackType.TcpOnly))
            {
                ProvisioningDeviceClient provClient =
                    ProvisioningDeviceClient.Create(GlobalDeviceEndpoint, IDScope, security, transport);

                Console.WriteLine($"RegistrationID = {security.GetRegistrationID()}");

                Console.Write("ProvisioningClient RegisterAsync...");
                DeviceRegistrationResult result = await provClient.RegisterAsync();

                Console.WriteLine($"{result.Status}");

                return result;
            }
        }
    }
}
