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
    public class IoTCentralService
    {
        // Telemetry globals.
        const int intervalInMilliseconds = 5000;        // Time interval required by wait function.

        // Home Station globals.
        private string homeStationIdentification;

        double optimalTemperature;          // Setting - can be changed by the operator from IoT Central.
        double optimalPressure;          // Setting - can be changed by the operator from IoT Central.
        private readonly IConnectionMessages _connectionMessages;

        // User IDs.
        private string IDScope;
        private string DeviceID ;
        private string PrimaryKey;

        // IoT Central global variables.
        private DeviceClient s_deviceClient;
        private CancellationTokenSource cts;
        private string GlobalDeviceEndpoint = "global.azure-devices-provisioning.net";
        private TwinCollection reportedProperties = new TwinCollection();

        public IoTCentralService(string idScope, string deviceId, string primaryKey, 
            string homeStationIdentification, double optimalTemperature, double optimalPressure,
            IConnectionMessages connectionMessages)
        {
            IDScope = idScope;
            DeviceID = deviceId;
            PrimaryKey = primaryKey;
            this.homeStationIdentification = homeStationIdentification;
            this.optimalTemperature = optimalTemperature;
            this.optimalPressure = optimalPressure;
            _connectionMessages = connectionMessages;
        }


        public void InitializeIoTCentralService()
        {
            try
            {
                using (var security = new SecurityProviderSymmetricKey(DeviceID, PrimaryKey, null))
                {
                    DeviceRegistrationResult result = RegisterDeviceAsync(security).GetAwaiter().GetResult();
                    if (result.Status != ProvisioningRegistrationStatusType.Assigned)
                    {
                        _connectionMessages.OnOperationMessageAvailable(IoTCentralMessageSeverity.Error,
                            "Failed to register device");
                        return;
                    }
                    IAuthenticationMethod auth = new DeviceAuthenticationWithRegistrySymmetricKey(result.DeviceId, (security as SecurityProviderSymmetricKey).GetPrimaryKey());
                    s_deviceClient = DeviceClient.Create(result.AssignedHub, auth, TransportType.Mqtt);
                }

                _connectionMessages.OnOperationMessageAvailable(IoTCentralMessageSeverity.Information,
                    "Device successfully connected to Azure IoT Central");

                SendDevicePropertiesAsync().GetAwaiter().GetResult();

                _connectionMessages.OnOperationMessageAvailable(IoTCentralMessageSeverity.Information,
                    "Register settings changed handler...");
                s_deviceClient.SetDesiredPropertyUpdateCallbackAsync(OnOptimalTempSettingChanged, null).GetAwaiter().GetResult();
                s_deviceClient.SetDesiredPropertyUpdateCallbackAsync(OnOptimalPressureSettingChanged, null).GetAwaiter().GetResult();
                _connectionMessages.OnOperationMessageAvailable(IoTCentralMessageSeverity.Information, "Done");

                cts = new CancellationTokenSource();

                // Create a handler for the direct method calls.
                s_deviceClient.SetMethodHandlerAsync("FlashLeds", CmdFlashLeds, null).Wait();
                s_deviceClient.SetMethodHandlerAsync("DisplayMessage", CmdDisplayMessage, null).Wait();
                s_deviceClient.SetMethodHandlerAsync("TakePhoto", CmdTakePhoto, null).Wait();
                
                cts.Cancel();

            }
            catch (Exception ex)
            {
                _connectionMessages.OnOperationMessageAvailable(IoTCentralMessageSeverity.Error, ex.Message);
            }
        }

        private async Task<DeviceRegistrationResult> RegisterDeviceAsync(SecurityProviderSymmetricKey security)
        {
            _connectionMessages.OnOperationMessageAvailable(IoTCentralMessageSeverity.Information,
                "Register device...");

            using (var transport = new ProvisioningTransportHandlerMqtt(TransportFallbackType.TcpOnly))
            {
                ProvisioningDeviceClient provClient =
                    ProvisioningDeviceClient.Create(GlobalDeviceEndpoint, IDScope, security, transport);

                _connectionMessages.OnOperationMessageAvailable(IoTCentralMessageSeverity.Information,
                    $"RegistrationID = {security.GetRegistrationID()}");

                _connectionMessages.OnOperationMessageAvailable(IoTCentralMessageSeverity.Information,
                    "ProvisioningClient RegisterAsync...");

                DeviceRegistrationResult result = await provClient.RegisterAsync();

                _connectionMessages.OnOperationMessageAvailable(IoTCentralMessageSeverity.Information,
                    $"{result.Status}");

                return result;
            }
        }

        private async Task OnOptimalTempSettingChanged(TwinCollection desiredProperties, object userContext)
        {
            string setting = "OptimalTemperature";
            if (desiredProperties.Contains(setting))
            {
                optimalTemperature = reportedProperties[setting] = desiredProperties[setting];
                _connectionMessages.OnOperationMessageAvailable(IoTCentralMessageSeverity.Information,
                    $"Optimal temperature updated: {optimalTemperature}");
            }
            await s_deviceClient.UpdateReportedPropertiesAsync(reportedProperties);
        }

        private async Task OnOptimalPressureSettingChanged(TwinCollection desiredProperties, object userContext)
        {
            string setting = "OptimalPressure";
            if (desiredProperties.Contains(setting))
            {
                optimalTemperature = reportedProperties[setting] = desiredProperties[setting];
                _connectionMessages.OnOperationMessageAvailable(IoTCentralMessageSeverity.Information,
                    $"Optimal pressure updated: {optimalPressure}");
            }
            await s_deviceClient.UpdateReportedPropertiesAsync(reportedProperties);
        }

        private async Task SendDevicePropertiesAsync()
        {
            reportedProperties["ATKID"] = homeStationIdentification;
            await s_deviceClient.UpdateReportedPropertiesAsync(reportedProperties);
            _connectionMessages.OnOperationMessageAvailable(IoTCentralMessageSeverity.Information,
                $"Sent device properties: {reportedProperties["ATKID"]}");
        }

        private Task<MethodResponse> CmdFlashLeds(MethodRequest methodRequest, object userContext)
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

        private Task<MethodResponse> CmdDisplayMessage(MethodRequest methodRequest, object userContext)
        {
            // Pick up variables from the request payload by using the name specified in IoT Central.
            var payloadString = JsonConvert.DeserializeObject<string>(methodRequest.DataAsJson);

            _connectionMessages.OnDisplayMessageCmdReceived(payloadString);
            
            // Acknowledge the direct method call with a 200 success message.
            string result = "{\"result\":\"Executed direct method: " + methodRequest.Name + "\"}";
            return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 200));

        }

        private Task<MethodResponse> CmdTakePhoto(MethodRequest methodRequest, object userContext)
        {
            _connectionMessages.OnTakePhotoCmdReceived();

            // Acknowledge the direct method call with a 200 success message.
            string result = "{\"result\":\"Executed direct method: " + methodRequest.Name + "\"}";
            return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 200));
        }

        private async void SendHomeStationTelemetryAsync(TelemetryDataPoint telemetryDataPoint)
        {
            while (true)
            {
                var rand = new Random();

                // Create the telemetry JSON message.
                var telemetryMessageString = JsonConvert.SerializeObject(telemetryDataPoint);
                var telemetryMessage = new Message(Encoding.ASCII.GetBytes(telemetryMessageString));

                Console.WriteLine($"Telemetry data: {telemetryMessageString}");

                // Bail if requested.
                cts.Token.ThrowIfCancellationRequested();

                // Send the telemetry message.
                await s_deviceClient.SendEventAsync(telemetryMessage);
                _connectionMessages.OnOperationMessageAvailable(IoTCentralMessageSeverity.Information,
                    $"Telemetry sent {DateTime.Now.ToShortTimeString()}");

                await Task.Delay(intervalInMilliseconds);
            }
        }

    }
}
