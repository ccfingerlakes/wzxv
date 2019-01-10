using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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
    static class Events
    {
        public static void Playing() => Analytics.TrackEvent("Playing");
        public static void Stopped() => Analytics.TrackEvent("Stopped");

        public static void Click(string name) => Analytics.TrackEvent("Click", Properties(new { Name = name }));
        public static void Click<T>(string name, T metadata)
            where T : class 
            => Analytics.TrackEvent("Click", Properties(metadata, new { Name = name }));

        public static void ExternalLink(string type, string url = null) => Analytics.TrackEvent("External Link", Properties(new { Type = type, Url = url }));

        public static void Performance<T>(string name, TimeSpan elapsed, T metadata = null)
            where T : class
            => Analytics.TrackEvent("Performance", Properties(metadata, new { Name = name, Elapsed = elapsed.TotalMilliseconds }));

        public static IDisposable Performance<T>(string name, T metadata = null)
            where T : class
            => new PerformanceEvent<T>(name, metadata);

        static IDictionary<string, string> Properties(params object[] args)
        {
            var properties = new Dictionary<string, string>();

            foreach (var o in args)
            {
                if (o != null)
                {
                    foreach (PropertyDescriptor prop in TypeDescriptor.GetProperties(o))
                    {
                        var val = prop.GetValue(o);

                        if (val != null)
                            properties[prop.Name] = val.ToString();
                    }
                }
            }

            return properties;
        }

        class PerformanceEvent<T> : IDisposable
            where T : class
        {
            private readonly string _name;
            private readonly T _metadata;
            private readonly Stopwatch _sw;

            public PerformanceEvent(string name, T metadata)
            {
                _name = name;
                _metadata = metadata;
                _sw = Stopwatch.StartNew();
            }

            public void Dispose()
            {
                _sw.Stop();
                Performance(_name, _sw.Elapsed, _metadata);
            }
        }
    }
}