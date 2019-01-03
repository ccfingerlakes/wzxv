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
    static class ControlExtensions
    {
        public static T Configure<T>(this T control, Action<T> configure)
            where T : Android.Views.View
        {
            configure(control);
            return control;
        }
    }
}