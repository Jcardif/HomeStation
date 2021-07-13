using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
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
using HomeStation.IoTCentral;
using HomeStation.IoTCentral.Helpers;
using HomeStation.IoTCentral.Interfaces;
using HomeStation.IoTCentral.Models;
using Java.IO;
using Microsoft.Azure.Devices.Client;
using Xamarin.Essentials;
using Console = System.Console;
using Keycode = Android.Views.Keycode;
using Platform = Xamarin.Essentials.Platform;

namespace HomeStation.Activities
{
    public enum DisplayMode
    {
        Temperature,
        Pressure
    }

    [Activity(Label = "@string/app_name")]
    [IntentFilter(new[] { Intent.ActionMain }, Categories = new[] { Intent.CategoryLauncher })]
    [IntentFilter(new[] { Intent.ActionMain }, Categories = new[] { "android.intent.category.IOT_LAUNCHER" })]
    public class MainActivity : Activity, ISensorCallback, ValueAnimator.IAnimatorUpdateListener, 
        ITemperatureEventListener, IPressureEventListener, IConnectionMessages
    {
        private string idScope = "0ne0032EC78";
        private string deviceId = "HS_ATK-001";
        private string primaryKey = "Wm+AjabCe45V9IzSbw9qij4ZGbZCIDNVw26kaR/EKGI=";
        private string homeStationDeviceId = "HS1_254";
        private double optimalTemperature = 25.0;
        private double optimalPressure = 800;

        private static readonly string Tag = typeof(MainActivity).FullName;

        private SensorManager _sensorManager;
        private ButtonInputDriver _buttonInputDriver;
        private Bmx280SensorDriver _environmentalSensorDriver;
        private AlphanumericDisplay _display;
        private DisplayMode _displayMode = DisplayMode.Temperature;

        private Apa102 _ledStrip;
        private readonly int[] _rainbow = new int[7];
        private static int LEDSTRIP_BRIGHTNESS = 1;
        private static float BAROMETER_RANGE_LOW = 800f;
        private static float BAROMETER_RANGE_HIGH = 1080f;
        private static float BAROMETER_RANGE_SUNNY = 1010f;
        private static float BAROMETER_RANGE_RAINY = 990f;

        private IGpio _led;
        
        private Speaker _speaker;
        private float _lastTemperature;
        private float _lastPressure;

        private TextView _tempValueTxtiew,
            _pressureValueTxtView,
            _optimalTempTxtView,
            _optimalPressureTxtView,
            _tapTxtView,
            _atkIdTxtView,
            _receivedMessageTxTextView;

        private ImageView _imageView;

        private SensorManager.DynamicSensorCallback _dynamicSensorCallback;

        private Random _rand;

        protected override async void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState); 
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.activity_main);

            _tempValueTxtiew = FindViewById<TextView>(Resource.Id.tempValue);
            _pressureValueTxtView = FindViewById<TextView>(Resource.Id.pressureValue);
            _imageView = FindViewById<ImageView>(Resource.Id.weather_image);
            _optimalPressureTxtView = FindViewById<TextView>(Resource.Id.optimalPressureValue);
            _optimalTempTxtView = FindViewById<TextView>(Resource.Id.optimalTempValue);
            _tapTxtView = FindViewById<TextView>(Resource.Id.buttonTapValue);
            _atkIdTxtView = FindViewById<TextView>(Resource.Id.atkIdValue);
            _receivedMessageTxTextView = FindViewById<TextView>(Resource.Id.receivedMessage_text);

            _atkIdTxtView.Text = homeStationDeviceId;


            _sensorManager = (SensorManager)GetSystemService(SensorService);

            _dynamicSensorCallback = new DynamicSensorCallback(this);
            _rand = new Random();

            InitIoTButtons();
            InitEnvironmentalSensors();
            InitDisplay();
            InitLedStrip();
            InitLed();
            InitSpeaker();
       


            IoTCentralService.SetUp(idScope, deviceId, primaryKey, homeStationDeviceId, optimalTemperature,
                optimalTemperature, this);

            await IoTCentralService.InitializeIoTCentralService(Settings.IoTCentralConnectionStringSettings);

            await IoTCentralService.SendDevicePropertiesAsync();
        }

        private void InitSpeaker()
        {
            try
            {
                _speaker = new Speaker(BoardDefaults.GetSpeakerPwmPin());
                ValueAnimator slide = ValueAnimator.OfFloat(440, 440 * 4);
                slide?.SetDuration(50);
                if (slide == null) return;
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

        private void InitLed()
        {
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
        }

        private void InitLedStrip()
        {
            try
            {
                _ledStrip = new Apa102(BoardDefaults.GetSpiBus(), Apa102.Mode.Bgr)
                {
                    Brightness = LEDSTRIP_BRIGHTNESS
                };
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
        }

        private void InitDisplay()
        {
            try
            {
                _display = new AlphanumericDisplay(BoardDefaults.GetI2CBus());
                _display.SetEnabled(true);
                _display.Clear();
                Log.Debug(Tag, "Initialized I2C Display");
            }
            catch (Exception e)
            {
                Log.Error(Tag, "Error initializing display", e);
                Log.Debug(Tag, "Display disabled");
                _display = null;
            }
        }

        private void InitEnvironmentalSensors()
        {
            try
            {
                _environmentalSensorDriver = new Bmx280SensorDriver(BoardDefaults.GetI2CBus());
                _sensorManager?.RegisterDynamicSensorCallback(_dynamicSensorCallback);
                _environmentalSensorDriver.RegisterTemperatureSensor();
                _environmentalSensorDriver.RegisterPressureSensor();
                Log.Debug(Tag, "Initialized I2C BMP280");
            }
            catch (Exception e)
            {
                throw new Exception("Error initializing BMP280", e);
            }
        }

        private void InitIoTButtons()
        {
            try
            {
                _buttonInputDriver = new ButtonInputDriver(BoardDefaults.GetButtonGpioPin(),
                    Google.Android.Things.Contrib.Driver.Button.Button.LogicState.PressedWhenLow,
                    (int)KeyEvent.KeyCodeFromString("KEYCODE_B"));
                _buttonInputDriver.Register();
                Log.Debug(Tag, "Initialized GPIO Button that generates a keypress with KEYCODE_A");
            }
            catch (Exception e)
            {
                throw new Exception("Error initializing GPIO button", e);
            }
        }

        private async void OnUpdateDisplay(float value)
        {
            if (_display == null) return;
            try
            {
                _display.Display(value);
                _tempValueTxtiew.Text = _lastTemperature.ToString("##.##");
                _pressureValueTxtView.Text = _lastPressure.ToString("##.##");

                var current = Xamarin.Essentials.Connectivity.NetworkAccess;
                if (current != NetworkAccess.Internet)
                    return;

                var data = new TelemetryDataPoint()
                {
                    AtmosphericPressure = _lastPressure,
                    RoomTemperature = _lastTemperature
                };

                await IoTCentralService.SendHomeStationTelemetryAsync(data);

            }
            catch (Exception e)
            {
                Log.Error(Tag, "Error setting display", e);
            }
        }

        public override bool OnKeyDown(Keycode keyCode, KeyEvent e)
        {
            Tap tap;
            switch (keyCode)
            {
                case Keycode.A:
                    tap = new Tap(ButtonTaps.ButtonA.ToString());
                    break;

                case Keycode.B:
                    tap = new Tap(ButtonTaps.ButtonB.ToString());
                    break;

                case Keycode.C:
                    tap = new Tap(ButtonTaps.ButtonC.ToString());
                    break;
                default:
                    tap = null;
                    break;
            }

            tap = new Tap(GetButtonTap());
            SendData(tap);
            _tapTxtView.Text = tap.ButtonTap;
            return true;
        }

        private string GetButtonTap()
        {

            var tap = _rand.Next(0, 3);
            switch (tap)
            {
                case 0:
                    return ButtonTaps.ButtonA.ToString();
                case 1:
                    return ButtonTaps.ButtonB.ToString();
                case 2:
                    return ButtonTaps.ButtonC.ToString();
                default:
                    return ButtonTaps.ButtonC.ToString();
            }
        }

        private async void SendData(Tap tap)
        {
            await IoTCentralService.SendHomeStationTelemetryAsync(tap);
        }

        public override bool OnKeyUp(Keycode keyCode, KeyEvent e)
        {
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


        private void OnUpdateBarometer(float pressure)
        {
            if (pressure > BAROMETER_RANGE_SUNNY)
            {
                _imageView.SetImageResource(Resource.Drawable.ic_sunny);
            }
            else if (pressure < BAROMETER_RANGE_RAINY)
            {
                _imageView.SetImageResource(Resource.Drawable.ic_rainy);
            }
            else
            {
                _imageView.SetImageResource(Resource.Drawable.ic_cloudy);
            }
        }

        public void OnDynamicSensorConnected(Sensor sensor)
        {
            if (sensor.Type == SensorType.AmbientTemperature)
            {
                _sensorManager.RegisterListener(new TemperatureListener(this), sensor, SensorDelay.Normal);
            }
            else if (sensor.Type == SensorType.Pressure)
            {
                _sensorManager.RegisterListener(new PressureListener(this), sensor, SensorDelay.Normal);
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
            Log.Debug(Tag, "accuracy changed: " + accuracy);
        }

        public void OnTemperatureSensorChanged(SensorEvent e)
        {
            if (e.Values != null) _lastTemperature = e.Values[0];
            Log.Debug(Tag, "sensor changed: " + _lastTemperature);

            if (_displayMode == DisplayMode.Temperature)
            {
                OnUpdateDisplay(_lastTemperature);
            }
        }

        public void OnPressureAccuracyChanged(Sensor sensor, [GeneratedEnum] SensorStatus accuracy)
        {
            Log.Debug(Tag, "accuracy changed: " + accuracy);
        }

        public void OnPressureSensorChanged(SensorEvent e)
        {
            if (e.Values != null) _lastPressure = e.Values[0];
            Log.Debug(Tag, "sensor changed: " + _lastPressure);

            if (_displayMode == DisplayMode.Pressure)
            {
                OnUpdateDisplay(_lastPressure);
            }
            OnUpdateBarometer(_lastPressure);
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

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
           Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }

        public void OnOperationMessageAvailable(IoTCentralMessageSeverity severity, string message)
        {
            
        }

        public async void OnFlashLedsCmdReceived(int[] leds, int flashTimes)
        {

            if (_ledStrip == null)
            {
                return;
            }

            try
            {
                int[] colors = new int[_rainbow.Length];

                foreach (var led in leds)
                {
                    colors[led] = _rainbow[led];
                }

                for (var i = 0; i < flashTimes; i++)
                {
                    _ledStrip.Write(colors);
                    await Task.Delay(500);
                    _ledStrip.Write(new[] {0, 0, 0, 0, 0, 0, 0});
                    await Task.Delay(500);
                } 
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public void OnDisplayMessageCmdReceived(string message)
        {
            RunOnUiThread(() =>
            {
                _receivedMessageTxTextView.Text = message;
            });
            
        }

        public void OnTakePhotoCmdReceived()
        {
            RunOnUiThread(() =>
            {
                Toast.MakeText(Xamarin.Essentials.Platform.CurrentActivity, "Take photo not implemented", ToastLength.Long)?.Show();
            });
        }

        public void OnOptimalTemperatureReceived(double temperature)
        {
           RunOnUiThread(() =>
           {
               _optimalTempTxtView.Text = temperature.ToString(CultureInfo.InvariantCulture);
           });
        }

        public void OnOptimalPressureReceived(double pressure)
        {
           RunOnUiThread(() =>
           {
               _optimalPressureTxtView.Text = pressure.ToString(CultureInfo.InvariantCulture);
           });
        }
    }
}