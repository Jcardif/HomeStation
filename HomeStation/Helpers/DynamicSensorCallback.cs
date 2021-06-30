using Android.Hardware;
using HomeStation.Interfaces;

namespace HomeStation.Helpers
{
    public class DynamicSensorCallback : SensorManager.DynamicSensorCallback
    {
        private ISensorCallback sensorCallback;

        public DynamicSensorCallback(ISensorCallback sensorCallback)
        {
            this.sensorCallback = sensorCallback;
        }

        public override void OnDynamicSensorConnected(Sensor sensor)
        {
            base.OnDynamicSensorConnected(sensor);
            sensorCallback.OnDynamicSensorConnected(sensor);
        }

        public override void OnDynamicSensorDisconnected(Sensor sensor)
        {
            base.OnDynamicSensorDisconnected(sensor);
            sensorCallback.OnDynamicSensorDisconnected(sensor);
        }
    }
}