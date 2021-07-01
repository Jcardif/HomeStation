using System;
using Android.Animation;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Hardware;
using Android.OS;
using Android.Runtime;
using Android.Things.Pio;
using Android.Util;
using Android.Views;
using Android.Views.Animations;
using Android.Widget;
using Google.Android.Things.Contrib.Driver.Apa102;
using Google.Android.Things.Contrib.Driver.Bmx280;
using Google.Android.Things.Contrib.Driver.Button;
using Google.Android.Things.Contrib.Driver.Ht16k33;
using Google.Android.Things.Contrib.Driver.Pwmspeaker;
using HomeStation.Helpers;
using HomeStation.Interfaces;
using Java.IO;
using Xamarin.Essentials;
using Console = System.Console;
using Keycode = Android.Views.Keycode;

namespace HomeStation.Activities
{
    public enum DisplayMode
    {
        TEMPERATURE,
        PRESSURE
    }

    [Activity(Label = "@string/app_name")]
    [IntentFilter(new[] { Intent.ActionMain }, Categories = new[] { Intent.CategoryLauncher })]
    [IntentFilter(new[] { Intent.ActionMain }, Categories = new[] { "android.intent.category.IOT_LAUNCHER" })]
    public class MainActivity : Activity, ISensorCallback, ValueAnimator.IAnimatorUpdateListener, ITemperatureEventListener, IPressureEventListener
    {
        private static readonly string TAG = typeof(MainActivity).FullName;

        private SensorManager _sensorManager;
        private ButtonInputDriver _buttonInputDriver;
        private Bmx280SensorDriver _environmentalSensorDriver;
        private AlphanumericDisplay _display;
        private DisplayMode _displayMode = DisplayMode.TEMPERATURE;

        private Apa102 _ledStrip;
        private readonly int[] _rainbow = new int[7];
        private static int LEDSTRIP_BRIGHTNESS = 1;
        private static float BAROMETER_RANGE_LOW = 965f;
        private static float BAROMETER_RANGE_HIGH = 1035f;
        private static float BAROMETER_RANGE_SUNNY = 1010f;
        private static float BAROMETER_RANGE_RAINY = 990f;

        private IGpio _led;

        //private static  int SPEAKER_READY_DELAY_MS = 300;

        private Speaker _speaker;
        private float _lastTemperature;
        private float _lastPressure;

        private TextView _tempValueTxtiew, _pressureValueTxtView;

        private SensorManager.DynamicSensorCallback _dynamicSensorCallback;


        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.activity_main);

            _tempValueTxtiew = FindViewById<TextView>(Resource.Id.tempValue);
            _pressureValueTxtView = FindViewById<TextView>(Resource.Id.pressureValue);

            _sensorManager = (SensorManager)GetSystemService(SensorService);

            _dynamicSensorCallback = new DynamicSensorCallback(this);

            try
            {
                _buttonInputDriver = new ButtonInputDriver(BoardDefaults.GetButtonGpioPin(),
                    Google.Android.Things.Contrib.Driver.Button.Button.LogicState.PressedWhenLow,
                    (int)KeyEvent.KeyCodeFromString("KEYCODE_A"));
                _buttonInputDriver.Register();
                Log.Debug(TAG, "Initialized GPIO Button that generates a keypress with KEYCODE_A");
            }
            catch (Exception e)
            {
                throw new Exception("Error initializing GPIO button", e);
            }

            try
            {
                _environmentalSensorDriver = new Bmx280SensorDriver(BoardDefaults.GetI2CBus());
                _sensorManager.RegisterDynamicSensorCallback(_dynamicSensorCallback);
                _environmentalSensorDriver.RegisterTemperatureSensor();
                _environmentalSensorDriver.RegisterPressureSensor();
                Log.Debug(TAG, "Initialized I2C BMP280");
            }
            catch (Exception e)
            {
                throw new Exception("Error initializing BMP280", e);
            }

            try
            {
                _display = new AlphanumericDisplay(BoardDefaults.GetI2CBus());
                _display.SetEnabled(true);
                _display.Clear();
                Log.Debug(TAG, "Initialized I2C Display");
            }
            catch (Exception e)
            {
                Log.Error(TAG, "Error initializing display", e);
                Log.Debug(TAG, "Display disabled");
                _display = null;
            }

            try
            {
                _ledStrip = new Apa102(BoardDefaults.GetSpiBus(), Apa102.Mode.Bgr);
                _ledStrip.Brightness = LEDSTRIP_BRIGHTNESS;
                for (int i = 0; i < _rainbow.Length; i++)
                {
                    float[] hsv = { i * 360f / _rainbow.Length, 1.0f, 1.0f };
                    _rainbow[i] = Color.HSVToColor(255, hsv);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                _ledStrip = null;
            }

            try
            {
                PeripheralManager pioService = PeripheralManager.Instance;
                _led = pioService.OpenGpio(BoardDefaults.GetLedGpioPin());
                _led.SetEdgeTriggerType(Gpio.EdgeNone);
                _led.SetDirection(Gpio.DirectionOutInitiallyLow);
                _led.SetActiveType(Gpio.ActiveHigh);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            try
            {
                _speaker = new Speaker(BoardDefaults.GetSpeakerPwmPin());
                ValueAnimator slide = ValueAnimator.OfFloat(440, 440 * 4);
                slide.SetDuration(50);
                slide.RepeatCount = 5;
                slide.SetInterpolator(new LinearInterpolator());
                slide.AddUpdateListener(this);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        private async void UpdateDisplay(float value)
        {
            if (_display != null)
            {
                try
                {
                    _display.Display(value);
                    _tempValueTxtiew.Text = _lastTemperature.ToString("##.##");
                    _pressureValueTxtView.Text = _lastPressure.ToString("##.##");

                    var current = Connectivity.NetworkAccess;
                    if (current != NetworkAccess.Internet)
                        return;
                    
                    // TODO: Init IoT Hub and send messages


                }
                catch (Exception e)
                {
                    Log.Error(TAG, "Error setting display", e);
                }
            }
        }

        public override bool OnKeyDown(Keycode keyCode, KeyEvent e)
        {
            if (keyCode == Keycode.A)
            {
                _displayMode = DisplayMode.PRESSURE;
                UpdateDisplay(_lastPressure);
                try
                {
                    _led.Value = true;
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception);
                }
                return true;
            }
            return base.OnKeyUp(keyCode, e);
        }

        public override bool OnKeyUp(Keycode keyCode, KeyEvent e)
        {
            if (keyCode == Keycode.A)
            {
                _displayMode = DisplayMode.TEMPERATURE;
                UpdateDisplay(_lastTemperature);
                try
                {
                    _led.Value = false;
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception);
                }
                return true;
            }

            return base.OnKeyUp(keyCode, e);
        }


        protected override void OnDestroy()
        {
            base.OnDestroy();
            // Clean up sensor registrations
            _sensorManager.UnregisterListener(new TemperatureListener(this));
            _sensorManager.UnregisterListener(new PressureListener(this));

            // Clean up peripheral.
            if (_environmentalSensorDriver != null)
            {
                try
                {
                    _environmentalSensorDriver.Close();
                }
                catch (IOException e)
                {
                    e.PrintStackTrace();
                }

                _environmentalSensorDriver = null;
            }

            if (_buttonInputDriver != null)
            {
                try
                {
                    _buttonInputDriver.Close();
                }
                catch (IOException e)
                {
                    e.PrintStackTrace();
                }

                _buttonInputDriver = null;
            }

            if (_display != null)
            {
                try
                {
                    _display.Clear();
                    _display.SetEnabled(false);
                    _display.Close();
                }
                catch (IOException e)
                {
                    e.PrintStackTrace();
                }
                finally
                {
                    _display = null;
                }
            }

            if (_ledStrip != null)
            {
                try
                {
                    _ledStrip.Brightness = 0;
                    _ledStrip.Write(new int[7]);
                    _ledStrip.Close();
                }
                catch (IOException e)
                {
                    e.PrintStackTrace();
                }
                finally
                {
                    _ledStrip = null;
                }
            }

            if (_led != null)
            {
                try
                {
                    _led.Value = false;
                    _led.Close();
                }
                catch (IOException e)
                {
                    e.PrintStackTrace();
                }
                finally
                {
                    _led = null;
                }
            }

            //TODO https://github.com/androidthings/weatherstation/blob/master/app/src/main/java/com/example/androidthings/weatherstation/WeatherStationActivity.java#L304
        }


        private void UpdateBarometer(float pressure)
        {
            if (pressure > BAROMETER_RANGE_SUNNY)
            {
                //imageView.SetImageResource(Resource.Mipmap.ic_sunny);
            }
            else if (pressure < BAROMETER_RANGE_RAINY)
            {
                //imageView.SetImageResource(Resource.Drawable.ic_rainy);
            }
            else
            {
                //imageView.SetImageResource(Resource.Drawable.ic_cloudy);
            }

            if (_ledStrip == null)
            {
                return;
            }

            float t = (pressure - BAROMETER_RANGE_LOW) / (BAROMETER_RANGE_HIGH - BAROMETER_RANGE_LOW);
            int n = (int)Math.Ceiling(_rainbow.Length * t);
            n = Math.Max(0, Math.Min(n, _rainbow.Length));
            int[] colors = new int[_rainbow.Length];
            for (int i = 0; i < n; i++)
            {
                int ri = _rainbow.Length - 1 - i;
                colors[ri] = _rainbow[ri];
            }

            try
            {
                _ledStrip.Write(colors);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public void OnDynamicSensorConnected(Sensor sensor)
        {
            if (sensor.Type == SensorType.AmbientTemperature)
            {
                _sensorManager.RegisterListener(new TemperatureListener(this), sensor, SensorDelay.Normal);
                //if (mPubsubPublisher != null)
                //{
                //    mSensorManager.registerListener(mPubsubPublisher.getTemperatureListener(), sensor,
                //            SensorManager.SENSOR_DELAY_NORMAL);
                //}
            }
            else if (sensor.Type == SensorType.Pressure)
            {
                _sensorManager.RegisterListener(new PressureListener(this), sensor, SensorDelay.Normal);
                //if (mPubsubPublisher != null)
                //{
                //    mSensorManager.registerListener(mPubsubPublisher.getTemperatureListener(), sensor,
                //            SensorManager.SENSOR_DELAY_NORMAL);
                //}
            }
        }

        public void OnDynamicSensorDisconnected(Sensor sensor)
        {

        }

        public void OnAnimationUpdate(ValueAnimator animation)
        {
            try
            {
                float v = (float)animation.AnimatedValue;
                _speaker.Play(v);
            }
            catch (Exception e)
            {
                throw new Exception("Error sliding speaker", e);
            }
        }

        public void OnTemperatureAccuracyChanged(Sensor sensor, [GeneratedEnum] SensorStatus accuracy)
        {
            Log.Debug(TAG, "accuracy changed: " + accuracy);
        }

        public void OnTemperatureSensorChanged(SensorEvent e)
        {
            _lastTemperature = e.Values[0];
            Log.Debug(TAG, "sensor changed: " + _lastTemperature);

            if (_displayMode == DisplayMode.TEMPERATURE)
            {
                UpdateDisplay(_lastTemperature);
            }
        }

        public void OnPressureAccuracyChanged(Sensor sensor, [GeneratedEnum] SensorStatus accuracy)
        {
            Log.Debug(TAG, "accuracy changed: " + accuracy);
        }

        public void OnPressureSensorChanged(SensorEvent e)
        {
            _lastPressure = e.Values[0];
            Log.Debug(TAG, "sensor changed: " + _lastPressure);

            if (_displayMode == DisplayMode.PRESSURE)
            {
                UpdateDisplay(_lastPressure);
            }
            UpdateBarometer(_lastPressure);
        }


        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }
    }
}