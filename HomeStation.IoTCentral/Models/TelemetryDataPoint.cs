using System;
using System.Collections.Generic;
using System.Text;

namespace HomeStation.IoTCentral.Models
{
    public class TelemetryDataPoint
    {
        public double RoomTemperature { get; set; }
        public double AtmosphericPressure { get; set; }
    }

    public class Tap
    {
        public Tap(string buttonTap)
        {
            ButtonTap = buttonTap;
        }

        public string ButtonTap { get; set; }
    }
}
