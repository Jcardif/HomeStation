using System;
using System.Collections.Generic;
using System.Text;
using HomeStation.IoTCentral.Helpers;

namespace HomeStation.IoTCentral.Interfaces
{
    public interface IConnectionMessages
    {
        void OnOperationMessageAvailable(IoTCentralMessageSeverity severity, string message);
        void OnFlashLedsCmdReceived(int[] leds, int flashTimes);
        void OnDisplayMessageCmdReceived(string message);
        void OnTakePhotoCmdReceived();
        void OnOptimalTemperatureReceived(double temperature);
        void OnOptimalPressureReceived(double pressure);
    }
}
