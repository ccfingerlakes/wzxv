using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace wzxv
{
    class RadioStationSchedule
    {
        private const string DefaultArtist = "WZXV";
        private const string DefaultTitle = "The Word";

        private readonly List<Slot> _slots;
        private readonly TimeZoneInfo _est = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

        private RadioStationSchedule(IEnumerable<Slot> slots)
        {
            _slots = new List<Slot>(slots);
        }

        public (string artist, string title) GetCurrent()
        {
            return Get(DateTimeOffset.Now);
        }

        public (string artist, string title) Get(DateTimeOffset value)
        {
            var time = TimeZoneInfo.ConvertTime(value, _est);

            if (time.DayOfWeek >= DayOfWeek.Monday && time.DayOfWeek <= DayOfWeek.Friday)
            {
                var slot = _slots
                            .Select(s => new { s, d = time.TimeOfDay - s.TimeOfDay })
                            .Where(i => i.d.TotalSeconds >= 0)
                            .OrderBy(i => i.d)
                            .Select(i => i.s)
                            .FirstOrDefault();

                if (slot == null)
                    slot = _slots.First();

                return (slot.Artist, slot.Title);
            }

            return (DefaultArtist, DefaultTitle);
        }

        public static RadioStationSchedule LoadFrom(string uri)
        {
            var slots = new List<Slot>();

            using (var client = new HttpClient())
            {
                using (var response = client.GetAsync(uri).GetAwaiter().GetResult())
                {
                    if (response.IsSuccessStatusCode)
                    {
                        var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                        if (!string.IsNullOrEmpty(content))
                        {
                            var parts = content.Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
                            var culture = CultureInfo.GetCultureInfo("en-US");

                            for (var i = 0; i + 2 <= parts.Length; i += 3)
                            {
                                var time = parts[i].TrimStart('@').TrimEnd(';').Trim();
                                var artist = parts[i + 1].TrimEnd(';').Trim();
                                var title = parts[i + 2].TrimEnd(';').Trim();

                                if (DateTime.TryParseExact(time, "h:mmtt", culture, DateTimeStyles.None, out var date))
                                {
                                    slots.Add(new Slot
                                    {
                                        TimeOfDay = date.TimeOfDay,
                                        Artist = artist,
                                        Title = title
                                    });
                                }
                            }
                        }
                    }
                }
            }

            return new RadioStationSchedule(slots.OrderBy(s => s.TimeOfDay));
        }

        class Slot
        {
            public TimeSpan TimeOfDay;
            public string Artist;
            public string Title;
        }
    }
}