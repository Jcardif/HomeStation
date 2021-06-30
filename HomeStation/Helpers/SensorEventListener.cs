using Android.Hardware;

namespace HomeStation.Helpers
{
    public interface IPressureEventListener
    {
        void OnPressureAccuracyChanged(Sensor sensor, SensorStatus accuracy);
        void OnPressureSensorChanged(SensorEvent e);
    }
    public interface ITemperatureEventListener
    {
        void OnTemperatureAccuracyChanged(Sensor sensor, SensorStatus accuracy);
        void OnTemperatureSensorChanged(SensorEvent e);
    }
    public class TemperatureListener : Java.Lang.Object, ISensorEventListener, ITemperatureEventListener
    {
        private ITemperatureEventListener _temperatureEventListener;
        public TemperatureListener(ITemperatureEventListener temperatureEventListener)
        {
            _temperatureEventListener = temperatureEventListener;
        }
        public void OnAccuracyChanged(Sensor sensor, SensorStatus accuracy)
        {
            OnTemperatureAccuracyChanged(sensor, accuracy);
        }

        public void OnSensorChanged(SensorEvent e)
        {
            OnTemperatureSensorChanged(e);
        }

        public void OnTemperatureAccuracyChanged(Sensor sensor, SensorStatus accuracy)
        {
            _temperatureEventListener.OnTemperatureAccuracyChanged(sensor, accuracy);
        }

        public void OnTemperatureSensorChanged(SensorEvent e)
        {
            _temperatureEventListener.OnTemperatureSensorChanged(e);
        }
    }

    public class PressureListener : Java.Lang.Object, IPressureEventListener, ISensorEventListener
    {
        private IPressureEventListener _pressureEventListener;

        public PressureListener(IPressureEventListener pressureEventListener)
        {
            _pressureEventListener = pressureEventListener;
        }

        public void OnAccuracyChanged(Sensor sensor, SensorStatus accuracy)
        {
            OnPressureAccuracyChanged(sensor, accuracy);
        }

        public void OnSensorChanged(SensorEvent e)
        {
            OnPressureSensorChanged(e);
        }

        public void OnPressureAccuracyChanged(Sensor sensor, SensorStatus accuracy)
        {
            _pressureEventListener.OnPressureAccuracyChanged(sensor, accuracy);
        }

        public void OnPressureSensorChanged(SensorEvent e)
        {
            _pressureEventListener.OnPressureSensorChanged(e);
        }
    }
}