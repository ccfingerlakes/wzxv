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
using Android.Util;
using Android.Views;
using Android.Widget;

namespace wzxv
{
    static class ControlExtensions
    {
        private const string TAG = "wzxv.app.controls.extensions";

        private static readonly HttpClient __http = new HttpClient();

        public static async Task<T> TrySetBitmapFromUrl<T>(this T view, string url, int resId)
            where T : ImageView
        {
            try
            {
                using (var response = await __http.GetAsync(url))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        using (var stream = await response.Content.ReadAsStreamAsync())
                            view.SetImageBitmap(Android.Graphics.BitmapFactory.DecodeStream(stream));
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn(TAG, $"Could not load image from url '{url}': {ex.Message}");
                Log.Debug(TAG, ex.ToString());
                view.SetImageResource(resId);
            }

            return view;
        }
    }
}