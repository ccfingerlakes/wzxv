using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Net;
using Android.Net.Wifi;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace wzxv
{
    class RadioStationServiceLock
    {
        private readonly WifiManager _wifiManager;
        private readonly PowerManager _powerManager;

        private WifiManager.WifiLock _wifiLock;
        private PowerManager.WakeLock _powerWakeLock;

        public RadioStationServiceLock(Context context)
        {
            _wifiManager = (WifiManager)context.GetSystemService(Context.WifiService);
            _powerManager = (PowerManager)context.GetSystemService(Context.PowerService);

            _wifiLock = _wifiManager.CreateWifiLock(WifiMode.Full, "wzxv.app");
            _wifiLock.Acquire();

            _powerWakeLock = _powerManager.NewWakeLock(WakeLockFlags.Partial, "wzxv.app");
            _powerWakeLock.Acquire();
        }

        public void Release()
        {
            if (_wifiLock != null)
            {
                _wifiLock.Release();
                _wifiLock = null;
            }

            if (_powerWakeLock != null)
            {
                _powerWakeLock.Release();
                _powerWakeLock = null;
            }
        }
    }
}