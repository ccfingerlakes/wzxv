using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Microsoft.AppCenter.Analytics;

namespace wzxv
{
    static class AppCenterEvents
    {
        public static void Play() => Analytics.TrackEvent("Play");
        public static void Playing() => Analytics.TrackEvent("Playing");
        public static void Stop() => Analytics.TrackEvent("Stop");
        public static void Stopped() => Analytics.TrackEvent("Stopped");
        public static void Error(Exception ex) => Analytics.TrackEvent("Error", new Dictionary<string, string> { { "Message", ex?.Message } });
    }
}