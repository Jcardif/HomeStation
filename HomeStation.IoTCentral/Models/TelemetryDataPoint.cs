using System;
using System.Collections.Generic;
using System.Text;

namespace HomeStation.IoTCentral.Models
{
    public class TelemetryDataPoint
    {
        public string ButtonTap { get; set; }
        public double RoomTemperature { get; set; }
        public double AtmosphericPressure { get; set; }
    }
}
