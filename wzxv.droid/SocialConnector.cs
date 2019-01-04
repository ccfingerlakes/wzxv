using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;

namespace wzxv
{
    static class SocialConnector
    {
        private const string TAG = "wzxv.app.social.connector";
        private const string ExternalLinkType = "Social";

        public static void OpenFacebook(Context context, string id)
        {
            string uri = null;

            try
            {
                var versionCode = context.PackageManager.GetPackageInfo("com.facebook.katana", 0).VersionCode;

                if (versionCode >= 3002850)
                {
                    uri = $"fb://facewebmodal/f?href=https://facebook.com/{id}";
                }
                else
                {
                    uri = $"fb://page/{id}";
                }
            }
            catch (Exception ex)
            {
                Log.Warn(TAG, $"Facebook App could not be detected: {ex.Message}");
                Log.Debug(TAG, ex.ToString());
                uri = $"https://facebook.com/{id}/";
            }

            var intent = new Intent(Intent.ActionView, Android.Net.Uri.Parse(uri));
            context.StartActivity(intent);

            Events.ExternalLink(ExternalLinkType, uri);
        }

        public static void OpenTwitter(Context context, string id)
        {
            var uri = $"https://twitter.com/{id}/";
            var intent = new Intent(Intent.ActionView, Android.Net.Uri.Parse(uri));
            context.StartActivity(intent);

            Events.ExternalLink(ExternalLinkType, uri);
        }

        public static void OpenInstagram(Context context, string id)
        {
            var uri = $"https://instagram.com/{id}/";
            var intent = new Intent(Intent.ActionView, Android.Net.Uri.Parse(uri));
            context.StartActivity(intent);

            Events.ExternalLink(ExternalLinkType, uri);
        }
    }
}