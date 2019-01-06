using System;
using System.Collections.Generic;
using System.Globalization;
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
    static class RadioStationScheduleReader
    {
        const string TAG = "wzxv.app.radio.schedule.reader";

        public static async Task<RadioStationSchedule> Read(string url)
        {
            using (var client = new HttpClient())
            {
                try
                {
                    using (var response = await client.GetAsync(url))
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            var content = await response.Content.ReadAsStringAsync();

                            if (content != null)
                                return new RadioStationSchedule(Parse(content));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(TAG, $"Failed to retrieve schedule metadata: {ex.Message}");
                    Log.Debug(TAG, ex.ToString());
                }
            }

            return new RadioStationSchedule();
        }

        static IEnumerable<RadioStationSchedule.Slot> Parse(string content)
        {
            var slots = new List<RadioStationSchedule.Slot>();

            if (!string.IsNullOrEmpty(content))
            {
                var culture = CultureInfo.GetCultureInfo("en-US");

                foreach (var row in CsvReader.Parse(content))
                {
                    if (row.Length < 4)
                    {
                        Log.Warn(TAG, $"Invalid row length ({row.Length}) detected; the row will be skipped");
                    }
                    else if (row[0].Equals("Weekday", StringComparison.InvariantCultureIgnoreCase))
                    {
                        row[0] = DayOfWeek.Monday.ToString();
                        if (TryParse(row, culture, out var monday))
                            slots.Add(monday);

                        row[0] = DayOfWeek.Tuesday.ToString();
                        if (TryParse(row, culture, out var tuesday))
                            slots.Add(tuesday);

                        row[0] = DayOfWeek.Wednesday.ToString();
                        if (TryParse(row, culture, out var wednesday))
                            slots.Add(wednesday);

                        row[0] = DayOfWeek.Thursday.ToString();
                        if (TryParse(row, culture, out var thursday))
                            slots.Add(thursday);

                        row[0] = DayOfWeek.Friday.ToString();
                        if (TryParse(row, culture, out var friday))
                            slots.Add(friday);
                    }
                    else
                    {
                        if (TryParse(row, culture, out var slot))
                            slots.Add(slot);
                    }
                }
            }

            return slots;
        }

        static bool TryParse(string[] values, CultureInfo culture, out RadioStationSchedule.Slot slot)
        {
            slot = null;

            if (Enum.TryParse<DayOfWeek>(values[0], out var dow))
            {
                if (TimeSpan.TryParseExact(values[1], "h\\:mm", culture, out var tod))
                {
                    var url = values.Length >= 5 ? values[4] : null;
                    var imageUrl = values.Length >= 6 ? values[5] : null;

                    slot = new RadioStationSchedule.Slot(dow, tod, values[3], values[2], url, imageUrl);
                }
                else
                {
                    Log.Warn(TAG, $"Invalid TimeOfDay '{values[1]}'; the row will be skipped");
                }
            }
            else
            {
                Log.Warn(TAG, $"Invalid DayOfWeek '{values[0]}'; the row will be skipped");
            }

            return (slot != null);
        }
    }
}