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

namespace wzxv
{
    static class ContactConnector
    {
        private const string ExternalLinkType = "Contact";

        public static void OpenDialer(Context context, string number)
        {
            var intent = new Intent(Intent.ActionDial, Android.Net.Uri.Parse($"tel:{number}"));
            context.StartActivity(intent);

            Events.ExternalLink(ExternalLinkType, "dialer");
        }

        public static void OpenMaps(Context context, double latitude, double longitude)
        {
            var intent = new Intent(Intent.ActionView, Android.Net.Uri.Parse($"geo:{latitude},{longitude}?q=Calvary Chapel of the Finger Lakes, 1777 Rochester Rd, Farmington NY 14425"));
            context.StartActivity(intent);

            Events.ExternalLink(ExternalLinkType, "maps");
        }

        public static void OpenMail(Context context, string to)
        {
            var intent = new Intent(Intent.ActionSend)
                            .PutExtra(Android.Content.Intent.ExtraEmail, new[] { to })
                            .SetType("message/rfc822");
            context.StartActivity(intent);

            Events.ExternalLink(ExternalLinkType, "mail");
        }
    }
}