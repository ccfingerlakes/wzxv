using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;

namespace wzxv
{
    static class ContactConnector
    {
        private const string TAG = "wzxv.app.contact.connector";
        private const string ExternalLinkType = "Contact";

        public static void OpenDialer(Context context, string number)
        {
            if (context.PackageManager.HasSystemFeature(PackageManager.FeatureTelephony))
            {
                try
                {
                    var intent = new Intent(Intent.ActionDial, Android.Net.Uri.Parse($"tel:{number}"));
                    context.StartActivity(intent);
                }
                catch (Exception ex)
                {
                    Log.Warn(TAG, $"Could not open dialer: {ex.Message}");
                    Log.Debug(TAG, ex.ToString());
                }

                Events.ExternalLink(ExternalLinkType, "dialer");
            }
        }

        public static void OpenMaps(Context context, double latitude, double longitude)
        {
            try
            {
                var intent = new Intent(Intent.ActionView, Android.Net.Uri.Parse($"geo:{latitude},{longitude}?q=Calvary Chapel of the Finger Lakes, 1777 Rochester Rd, Farmington NY 14425"));
                context.StartActivity(intent);
            }
            catch (Exception ex)
            {
                Log.Warn(TAG, $"Could not open maps: {ex.Message}");
                Log.Debug(TAG, ex.ToString());
                Toast.MakeText(context, "1777 Rochester Rd\nFarmington NY 14425", ToastLength.Long).Show();
            }

            Events.ExternalLink(ExternalLinkType, "maps");
        }

        public static void OpenMail(Context context, string to)
        {
            try
            {
                var intent = new Intent(Intent.ActionSend)
                                .PutExtra(Android.Content.Intent.ExtraEmail, new[] { to })
                                .SetType("message/rfc822");
                context.StartActivity(intent);
            }
            catch (Exception ex)
            {
                Log.Warn(TAG, $"Could not open mail: {ex.Message}");
                Log.Debug(TAG, ex.ToString());
            }

            Events.ExternalLink(ExternalLinkType, "mail");
        }
    }
}