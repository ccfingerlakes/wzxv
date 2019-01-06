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
    static class Globalization
    {
        public static readonly TimeZoneInfo EasternStandardTime = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

        public static DateTimeOffset Today => DateTimeOffset.Now.ToToday();

        public static DateTimeOffset ToToday(this DateTimeOffset date)
            => new DateTimeOffset(date.Year, date.Month, date.Day, 0, 0, 0, date.Offset);
    }
}