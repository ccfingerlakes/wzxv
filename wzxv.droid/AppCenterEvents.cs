using System;
using System.Collections.Generic;
using System.ComponentModel;
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
        public static void Error(Exception ex) => Analytics.TrackEvent("Error", Properties(new { ex.Message, Exception = ex.ToString() }));

        public static void Click(string name) => Analytics.TrackEvent("Click", Properties(new { Name = name }));
        public static void Click<T>(string name, T metadata) => Analytics.TrackEvent("Click", Properties(metadata, new { Name = name }));

        public static void ExternalLink(string type, string url = null) => Analytics.TrackEvent("External Link", Properties(new { Type = type, Url = url }));

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
    }
}