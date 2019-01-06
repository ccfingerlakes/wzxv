using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
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
        private static readonly HttpClient __http = new HttpClient();

        public static T Configure<T>(this T control, Action<T> configure)
            where T : Android.Views.View
        {
            configure(control);
            return control;
        }

        public static async Task<T> ConfigureAsync<T>(this T control, Func<T, Task> configure)
            where T : Android.Views.View
        {
            await configure(control);
            return control;
        }

        public static async Task<T> SetBitmapFromUrl<T>(this T view, string url)
            where T : ImageView
        {
            using (var response = await __http.GetAsync(url))
            {
                if (response.IsSuccessStatusCode)
                {
                    using (var stream = await response.Content.ReadAsStreamAsync())
                        view.SetImageBitmap(Android.Graphics.BitmapFactory.DecodeStream(stream));
                }
            }

            return view;
        }
    }
}