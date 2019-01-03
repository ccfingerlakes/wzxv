using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;

namespace wzxv
{
    public class RadioStationSchedule : IDisposable
    {
        private const string TAG = "wzxv.app.radio.schedule";
        private const string SlotsUrl = "https://drive.google.com/uc?export=download&id=1VHOK768OrBKro49AmfgLzwkSEdm_tWX5";
        private const string ArtistsUrl = "https://raw.githubusercontent.com/ccfingerlakes/wzxv/android/artists.csv";

        private readonly object _syncRoot = new object();
        private readonly TimeZoneInfo _est = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        private readonly Timer _timer;
        private readonly IEnumerable<Slot> _slots;
        private Slot _previous;

        public event EventHandler<RadioStationScheduleChangedEventArgs> Changed;

        public RadioStationSchedule(Action<Slot> onChanged = null)
        {
            _slots = ReadSlots();

            if (onChanged != null)
            {
                Changed += (_, e) => onChanged(e.Current);
            }

            _timer = new Timer()
            {
                Interval = 250,
                AutoReset = false
            };
            _timer.Elapsed += OnRefresh;
            _timer.Start();
        }

        public void Refresh(bool force = false)
        {
            lock (_syncRoot)
            {
                if (force)
                {
                    _previous = null;
                }

                _timer.Stop();
                _timer.Interval = 100;
                _timer.Start();
            }
        }

        void OnRefresh(object sender, ElapsedEventArgs e)
        {
            lock (_syncRoot)
            {
                (var current, var next) = Get();

                if (_previous != current)
                {
                    _previous = current;
                    Changed?.Invoke(this, new RadioStationScheduleChangedEventArgs(current));
                }

                var now = TimeZoneInfo.ConvertTime(DateTimeOffset.Now, _est);
                TimeSpan interval = TimeSpan.FromMinutes(1);

                if (next != Slot.DefaultSlot)
                {
                    if (next.TimeOfDay < current.TimeOfDay)
                    {
                        interval = new DateTimeOffset(now.Year, now.Month, now.Day, next.TimeOfDay.Hours, next.TimeOfDay.Minutes, 0, 0, now.Offset).AddDays(1).Subtract(now).Add(TimeSpan.FromSeconds(1));
                    }
                    else
                    {
                        interval = new DateTimeOffset(now.Year, now.Month, now.Day, next.TimeOfDay.Hours, next.TimeOfDay.Minutes, 0, 0, now.Offset).Subtract(now).Add(TimeSpan.FromSeconds(1));
                    }
                }

                _timer.Interval = interval.TotalMilliseconds;
                _timer.Start();
            }
        }

        public void Dispose()
        {
            _timer.Dispose();
        }

        (Slot current, Slot next) Get()
        {
            var time = TimeZoneInfo.ConvertTime(DateTimeOffset.Now, _est);

            if (time.DayOfWeek >= DayOfWeek.Monday && time.DayOfWeek <= DayOfWeek.Friday)
            {
                var slots = _slots
                            .Select(s => new { s, d = time.TimeOfDay - s.TimeOfDay })
                            .OrderBy(i => i.d);

                var current = slots
                                .Where(i => i.d.TotalSeconds >= 0)
                                .Select(i => i.s)
                                .FirstOrDefault();

                if (current == null)
                    current = _slots.First();

                var next = slots
                                .Where(i => i.d.TotalSeconds < 0)
                                .Select(i => i.s)
                                .LastOrDefault();

                if (next == null)
                    current = _slots.First();

                return (current, next);
            }

            return (Slot.DefaultSlot, Slot.DefaultSlot);
        }

        static IEnumerable<Slot> ReadSlots()
        {
            (var slots, var artists) = GetHttpContents();

            if (slots != null)
                return ParseSlots(slots, artists);

            return Array.Empty<Slot>();
        }

        static (string slots, string artists) GetHttpContents()
        {
            string slots = null;
            string artists = null;

            using (var client = new HttpClient())
            {
                Task.WhenAll(
                    Task.Run(async () =>
                    {
                        try
                        {
                            using (var response = await client.GetAsync(SlotsUrl))
                            {
                                if (response.IsSuccessStatusCode)
                                {
                                    slots = await response.Content.ReadAsStringAsync();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error(TAG, $"Failed to retrieve slots metadata: {ex.Message}");
                            Log.Debug(TAG, ex.ToString());
                        }
                    }),
                    Task.Run(async () =>
                    {
                        try
                        {
                            using (var response = await client.GetAsync(ArtistsUrl))
                            {
                                if (response.IsSuccessStatusCode)
                                {
                                    artists = await response.Content.ReadAsStringAsync();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error(TAG, $"Failed to retrieve slots metadata: {ex.Message}");
                            Log.Debug(TAG, ex.ToString());
                        }
                    })
                ).Wait();
            }

            return (slots, artists);
        }

        static IEnumerable<Slot> ParseSlots(string slotsAsString, string artistsAsString)
        {
            var slots = new List<Slot>();
            var artists = new Dictionary<string, (string url, string imageUrl)>(StringComparer.InvariantCultureIgnoreCase);

            if (!string.IsNullOrEmpty(artistsAsString))
            {
                foreach(var line in artistsAsString.Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = line.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries);

                    if (parts.Length >= 3)
                    {
                        artists[parts[0]] = (parts[1], parts[2]);
                    }
                }
            }

            if (!string.IsNullOrEmpty(slotsAsString))
            {
                var parts = slotsAsString.Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
                var culture = CultureInfo.GetCultureInfo("en-US");

                for (var i = 0; i + 2 <= parts.Length; i += 3)
                {
                    var time = parts[i].TrimStart('@').TrimEnd(';').Trim();
                    var artist = parts[i + 1].TrimEnd(';').Trim();
                    var title = parts[i + 2].TrimEnd(';').Trim();
                    string url = null;
                    string imageUrl = null;

                    if (artists.ContainsKey(artist))
                    {
                        (url, imageUrl) = artists[artist];
                    }

                    if (DateTime.TryParseExact(time, "h:mmtt", culture, DateTimeStyles.None, out var date))
                    {
                        slots.Add(new Slot(date.TimeOfDay, artist, title, url, imageUrl));
                    }
                }
            }

            return slots;
        }

        public class Slot
        {
            public readonly static Slot DefaultSlot = new Slot(TimeSpan.Zero, "WZXV", "The Word", "http://wzxv.org", null);

            public TimeSpan TimeOfDay { get; private set; }
            public string Artist { get; private set; }
            public string Title { get; private set; }
            public string Url { get; private set; }
            public string ImageUrl { get; private set; }

            public Slot(TimeSpan timeOfDay, string artist, string title, string url, string imageUrl)
            {
                TimeOfDay = timeOfDay;
                Artist = artist;
                Title = title;
                Url = url;
                ImageUrl = imageUrl;
            }
        }
    }

    public class RadioStationScheduleChangedEventArgs : EventArgs
    {
        public RadioStationSchedule.Slot Current { get; private set; }

        public RadioStationScheduleChangedEventArgs(RadioStationSchedule.Slot current)
        {
            Current = current;
        }
    }
}