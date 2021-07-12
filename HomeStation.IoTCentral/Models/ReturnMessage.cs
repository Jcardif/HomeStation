using System;
using System.Collections.Generic;
using System.Text;
using HomeStation.IoTCentral.Helpers;

namespace HomeStation.IoTCentral.Models
{
    public class ReturnMessage
    {
        public ReturnMessage(IoTCentralMessageSeverity messageSeverity, string message)
        {
            MessageSeverity = messageSeverity;
            Message = message;
        }

        public IoTCentralMessageSeverity MessageSeverity { get; set; }
        public string Message { get; set; }
    }
}
