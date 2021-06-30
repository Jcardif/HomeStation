using System;
using System.Collections.Generic;
using System.Linq;
using Android.OS;
using Android.Things.Pio;

namespace HomeStation.Helpers
{
    public static class BoardDefaults
    {
        private const string DEVICE_EDISON_ARDUINO = "edison_arduino";
        private const string DEVICE_EDISON = "edison";
        private const string DEVICE_RPI3 = "rpi3";
        private const string DEVICE_NXP = "imx6ul";
        private static string _sBoardVariant = "";
        private const string DEVICE_IMX_7D_PICO = "imx7d_pico";

        public static string GetButtonGpioPin()
        {
            switch (GetBoardVariant())
            {
                case DEVICE_EDISON_ARDUINO:
                    return "IO12";
                case DEVICE_EDISON:
                    return "GP44";
                case DEVICE_RPI3:
                    return "BCM21";
                case DEVICE_NXP:
                    return "GPIO4_IO20";
                case DEVICE_IMX_7D_PICO:
                    return "GPIO6_IO14";
                default:
                    throw new Exception("Unknown device: " + Build.Device);
            }
        }

        public static string GetLedGpioPin()
        {
            switch (GetBoardVariant())
            {
                case DEVICE_EDISON_ARDUINO:
                    return "IO13";
                case DEVICE_EDISON:
                    return "GP45";
                case DEVICE_RPI3:
                    return "BCM6";
                case DEVICE_NXP:
                    return "GPIO4_IO21";
                case DEVICE_IMX_7D_PICO:
                    return "GPIO2_IO02";
                default:
                    throw new Exception("Unknown device: " + Build.Device);
            }
        }

        public static string GetI2CBus()
        {
            switch (GetBoardVariant())
            {
                case DEVICE_EDISON_ARDUINO:
                    return "I2C6";
                case DEVICE_EDISON:
                    return "I2C1";
                case DEVICE_RPI3:
                    return "I2C1";
                case DEVICE_NXP:
                    return "I2C2";
                case DEVICE_IMX_7D_PICO:
                    return "I2C1";
                default:
                    throw new Exception("Unknown device: " + Build.Device);
            }
        }

        public static string GetSpiBus()
        {
            switch (GetBoardVariant())
            {
                case DEVICE_EDISON_ARDUINO:
                    return "SPI1";
                case DEVICE_EDISON:
                    return "SPI2";
                case DEVICE_RPI3:
                    return "SPI0.0";
                case DEVICE_NXP:
                    return "SPI3_0";
                case DEVICE_IMX_7D_PICO:
                    return "SPI3.1";
                default:
                    throw new Exception("Unknown device: " + Build.Device);
            }
        }

        public static string GetSpeakerPwmPin()
        {
            switch (GetBoardVariant())
            {
                case DEVICE_EDISON_ARDUINO:
                    return "IO3";
                case DEVICE_EDISON:
                    return "GP13";
                case DEVICE_RPI3:
                    return "PWM1";
                case DEVICE_NXP:
                    return "PWM7";
                case DEVICE_IMX_7D_PICO:
                    return "PWM2";
                default:
                    throw new Exception("Unknown device: " + Build.Device);
            }
        }

        private static string GetBoardVariant()
        {
            if (_sBoardVariant != string.Empty)
            {
                return _sBoardVariant;
            }
            _sBoardVariant = Build.Device;
            // For the edison check the pin prefix
            // to always return Edison Breakout pin name when applicable.
            if (_sBoardVariant.Equals(DEVICE_EDISON))
            {
                PeripheralManager pioService = PeripheralManager.Instance;
                List<string> gpioList = pioService.GpioList.ToList();
                if (gpioList.Count != 0)
                {
                    String pin = gpioList[0];
                    if (pin.StartsWith("IO"))
                    {
                        _sBoardVariant = DEVICE_EDISON_ARDUINO;
                    }
                }
            }
            return _sBoardVariant;
        }


    }
}