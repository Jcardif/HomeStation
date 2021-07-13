using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HomeStation.IoTCentral.Helpers;
using HomeStation.IoTCentral.Interfaces;

namespace HomeStation.Simulator
{
    public class ConnectionMessages : IConnectionMessages
    {
        static void ColorMessage(string text, ConsoleColor clr)
        {
            Console.ForegroundColor = clr;
            Console.WriteLine(text + "\n");
            Console.ResetColor();
        }
        static void GreenMessage(string text)
        {
            ColorMessage(text, ConsoleColor.Green);
        }

        static void YellowMessage(string text)
        {
            ColorMessage(text, ConsoleColor.Yellow);
        }


        static void RedMessage(string text)
        {
            ColorMessage(text, ConsoleColor.Red);
        }

        static void BlueMessage(string text)
        {
            ColorMessage(text, ConsoleColor.Blue);
        }

        public void OnOperationMessageAvailable(IoTCentralMessageSeverity severity, string message)
        {
            switch (severity)
            {
                case IoTCentralMessageSeverity.Information:
                    GreenMessage(message);
                    break;
                case IoTCentralMessageSeverity.Warning:
                    YellowMessage(message);
                    break;
                case IoTCentralMessageSeverity.Error:
                    RedMessage(message);
                    break;
                case IoTCentralMessageSeverity.Message:
                    BlueMessage(message);
                    break;
                case IoTCentralMessageSeverity.Text:
                    Console.WriteLine(message + "\n");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(severity), severity, null);
            }
        }

        public void OnFlashLedsCmdReceived(int[] leds, int flashTimes)
        {
            throw new NotImplementedException();
        }

        public void OnDisplayMessageCmdReceived(string message)
        {
            throw new NotImplementedException();
        }

        public void OnTakePhotoCmdReceived()
        {
            throw new NotImplementedException();
        }

        public void OnOptimalTemperatureReceived(double temperature)
        {
            throw new NotImplementedException();
        }

        public void OnOptimalPressureReceived(double pressure)
        {
            throw new NotImplementedException();
        }
    }
}
