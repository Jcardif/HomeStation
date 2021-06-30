using Android.Hardware;

namespace HomeStation.Interfaces
{
    public interface ISensorCallback
    {
        void OnDynamicSensorConnected(Sensor sensor);
        void OnDynamicSensorDisconnected(Sensor sensor);
    }
}