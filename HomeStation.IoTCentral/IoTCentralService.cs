using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HomeStation.IoTCentral.Helpers;
using HomeStation.IoTCentral.Interfaces;
using HomeStation.IoTCentral.Models;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Provisioning.Client;
using Microsoft.Azure.Devices.Provisioning.Client.Transport;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;

namespace HomeStation.IoTCentral
{
    public static class IoTCentralService
    {
        // Telemetry globals.
        const int INTERVAL_IN_MILLISECONDS = 5000;        // Time interval required by wait function.

        // Home Station globals.
        private static string _homeStationIdentification;

        static double _optimalTemperature;          // Setting - can be changed by the operator from IoT Central.
        static double _optimalPressure;          // Setting - can be changed by the operator from IoT Central.
        private static  IConnectionMessages _connectionMessages;

        // User IDs.
        private static string _idScope;
        private static string _deviceId ;
        private static string _primaryKey;

        // IoT Central global variables.
        private static DeviceClient _deviceClient;
        private static CancellationTokenSource _cts;
        private const string GlobalDeviceEndpoint = "global.azure-devices-provisioning.net";
        private static TwinCollection _reportedProperties = new TwinCollection();

        public static void SetUp(string idScope, string deviceId, string primaryKey, 
            string homeStationIdentification, double optimalTemperature, double optimalPressure,
            IConnectionMessages connectionMessages)
        {
            _idScope = idScope;
            _deviceId = deviceId;
            _primaryKey = primaryKey;
            _homeStationIdentification = homeStationIdentification;
            _optimalTemperature = optimalTemperature;
            _optimalPressure = optimalPressure;
            _connectionMessages = connectionMessages;
        }


        public static async Task InitializeIoTCentralService(string connectionString)
        {
            if(_deviceClient!=null)
                return;
            try
            {
                _deviceClient=DeviceClient.CreateFromConnectionString(connectionString);

                _connectionMessages.OnOperationMessageAvailable(IoTCentralMessageSeverity.Information,"Device successfully connected to Azure IoT Central");
                
                _connectionMessages.OnOperationMessageAvailable(IoTCentralMessageSeverity.Text, "Register settings changed handler...");
                await _deviceClient.SetDesiredPropertyUpdateCallbackAsync(OnSettingsChanged, null);
                _connectionMessages.OnOperationMessageAvailable(IoTCentralMessageSeverity.Text, "Done");

                _cts = new CancellationTokenSource();

                // Create a handler for the direct method calls.
                _deviceClient.SetMethodHandlerAsync("FlashLeds", CmdFlashLeds, null).Wait();
                _deviceClient.SetMethodHandlerAsync("DisplayMessage", CmdDisplayMessage, null).Wait();
                _deviceClient.SetMethodHandlerAsync("TakePhoto", CmdTakePhoto, null).Wait();

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw;
            }
        }

        public static async Task SendHomeStationTelemetryAsync(TelemetryDataPoint telemetryDataPoint)
        {
            // Create the telemetry JSON message.
            var telemetryMessageString = JsonConvert.SerializeObject(telemetryDataPoint);
            var telemetryMessage = new Message(Encoding.ASCII.GetBytes(telemetryMessageString));

            _connectionMessages.OnOperationMessageAvailable(IoTCentralMessageSeverity.Text, $"Telemetry data: {telemetryMessageString}");

            // Bail if requested.
            _cts.Token.ThrowIfCancellationRequested();

            // Send the telemetry message.
            await _deviceClient.SendEventAsync(telemetryMessage);
            _connectionMessages.OnOperationMessageAvailable(IoTCentralMessageSeverity.Information,
                $"Telemetry sent {DateTime.Now.ToShortTimeString()}");
        }

        public static async Task SendHomeStationTelemetryAsync(Tap tap)
        {
            // Create the telemetry JSON message.
            var telemetryMessageString = JsonConvert.SerializeObject(tap);
            var telemetryMessage = new Message(Encoding.ASCII.GetBytes(telemetryMessageString));

            _connectionMessages.OnOperationMessageAvailable(IoTCentralMessageSeverity.Text, $"Telemetry data: {telemetryMessageString}");

            // Bail if requested.
            _cts.Token.ThrowIfCancellationRequested();

            // Send the telemetry message.
            await _deviceClient.SendEventAsync(telemetryMessage);
            _connectionMessages.OnOperationMessageAvailable(IoTCentralMessageSeverity.Information,
                $"Telemetry sent {DateTime.Now.ToShortTimeString()}");
        }


        private static Task<MethodResponse> CmdFlashLeds(MethodRequest methodRequest, object userContext)
        {
            try
            {
                // Pick up variables from the request payload by using the name specified in IoT Central.
                var payloadString = JsonConvert.DeserializeObject<string>(methodRequest.DataAsJson);
                var flashInfo = payloadString.Split('-');

                var ledString = flashInfo.First();
                var ledNos = ledString.Split(',');

                var leds = ledNos.Select(ledNo => Convert.ToInt32(ledNo)).ToArray();
                var flashTimes = Convert.ToInt32(flashInfo.Last());

                _connectionMessages.OnFlashLedsCmdReceived(leds, flashTimes);

                // Acknowledge the direct method call with a 200 success message.
                string result = "{\"result\":\"Executed direct method: " + methodRequest.Name + "\"}";
                return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 200));
            }
            catch (Exception ex)
            {
                _connectionMessages.OnOperationMessageAvailable(IoTCentralMessageSeverity.Error,
                    $"Invalid payload {ex.Message}");

                // Acknowledge the direct method call with a 400 error message.
                string result = "{\"result\":\"Invalid payload\"}";
                return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 400));
            }
        }

        private static Task<MethodResponse> CmdDisplayMessage(MethodRequest methodRequest, object userContext)
        {
            // Pick up variables from the request payload by using the name specified in IoT Central.
            var payloadString = JsonConvert.DeserializeObject<string>(methodRequest.DataAsJson);

            _connectionMessages.OnDisplayMessageCmdReceived(payloadString);

            // Acknowledge the direct method call with a 200 success message.
            string result = "{\"result\":\"Executed direct method: " + methodRequest.Name + "\"}";
            return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 200));

        }

        private static Task<MethodResponse> CmdTakePhoto(MethodRequest methodRequest, object userContext)
        {
            _connectionMessages.OnTakePhotoCmdReceived();

            // Acknowledge the direct method call with a 200 success message.
            string result = "{\"result\":\"Executed direct method: " + methodRequest.Name + "\"}";
            return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 200));
        }



        static async Task OnSettingsChanged(TwinCollection desiredProperties, object userContext)
        {
            string optimalPressureSetting = "OptimalPressure";
            string optimalTemperatureSetting = "OptimalTemperature";
            if (desiredProperties.Contains(optimalPressureSetting))
            {
                _optimalPressure = _reportedProperties[optimalPressureSetting] = desiredProperties[optimalPressureSetting];
                 _connectionMessages.OnOperationMessageAvailable(IoTCentralMessageSeverity.Information,$"Optimal pressure updated: {_optimalPressure}");

                 await _deviceClient.UpdateReportedPropertiesAsync(_reportedProperties);
                 _connectionMessages.OnOptimalPressureReceived(_optimalPressure);
            }

            if (desiredProperties.Contains(optimalTemperatureSetting))
            {
                _optimalTemperature = _reportedProperties[optimalTemperatureSetting] = desiredProperties[optimalTemperatureSetting];
                _connectionMessages.OnOperationMessageAvailable(IoTCentralMessageSeverity.Information, $"Optimal temperature updated: {_optimalTemperature}");

                await _deviceClient.UpdateReportedPropertiesAsync(_reportedProperties);
                _connectionMessages.OnOptimalTemperatureReceived(_optimalTemperature);
            }
        }

        public static async Task SendDevicePropertiesAsync()
        {
            _reportedProperties["ATKID"] = _homeStationIdentification;
            await _deviceClient.UpdateReportedPropertiesAsync(_reportedProperties);
            _connectionMessages.OnOperationMessageAvailable(IoTCentralMessageSeverity.Information,$"Sent device properties: {_reportedProperties["ATKID"]}");
        }

    }
}
