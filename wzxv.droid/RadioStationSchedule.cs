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
        private const string Url = "https://raw.githubusercontent.com/ccfingerlakes/wzxv/master/schedule.csv";

        private readonly object _syncRoot = new object();
        private readonly TimeZoneInfo _est = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        private readonly Timer _timer;
        private IEnumerable<Slot> _slots;
        private Slot _previous;

        public event EventHandler<RadioStationScheduleChangedEventArgs> Changed;

        public RadioStationSchedule(Action<Slot> onChanged = null)
        {
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

            Task.Run(async () =>
            {
                _slots = await Read();
                _timer.Start();
            });
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
                _timer.Interval = 250;
                _timer.Start();
            }
        }

        void OnRefresh(object sender, ElapsedEventArgs e)
        {
            lock (_syncRoot)
            {
                var interval = TimeSpan.FromMinutes(1);

                if (TryGet(out var current, out var next))
                {
                    if (_previous != current)
                    {
                        _previous = current;
                        Changed?.Invoke(this, new RadioStationScheduleChangedEventArgs(current));
                    }

                    interval = next;
                }

                _timer.Interval = interval.TotalMilliseconds;
                _timer.Start();
            }
        }

        public void Dispose()
        {
            _timer.Dispose();
        }

        bool TryGet(out Slot slot, out TimeSpan next)
        {
            var now = TimeZoneInfo.ConvertTime(DateTimeOffset.Now, _est);
            var schedule = _slots
                            .Select(s =>
                            {
                                var n = Next(now, s.DayOfWeek, s.TimeOfDay);
                                return new { Slot = s, Current = n.AddDays(-7), Next = n };
                            })
                            .ToArray();

            slot = schedule
                    .Where(i => i.Current <= now)
                    .OrderByDescending(i => i.Current)
                    .Take(1)
                    .Select(i => i.Slot)
                    .FirstOrDefault();

            next = schedule
                    .Where(i => i.Next > now)
                    .OrderBy(i => i.Next)
                    .Take(1)
                    .Select(i => i.Next)
                    .FirstOrDefault()
                    .Subtract(now)
                    .Add(TimeSpan.FromSeconds(1));

            return (slot != null);
        }

        static DateTimeOffset Next(DateTimeOffset date, DayOfWeek dow, TimeSpan tod)
        {
            if (date.DayOfWeek != dow)
            {
                date = date.AddDays((int)dow + 7 - (int)date.DayOfWeek);
            }
            else if (date.DayOfWeek == dow && tod <= date.TimeOfDay)
            {
                date = date.AddDays(7);
            }

            return new DateTimeOffset(date.Year, date.Month, date.Day, tod.Hours, tod.Minutes, tod.Seconds, tod.Minutes, date.Offset);
        }

        static async Task<IEnumerable<Slot>> Read()
        {
            using (var client = new HttpClient())
            {
                try
                {
                    using (var response = await client.GetAsync(Url))
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            var content = await response.Content.ReadAsStringAsync();

                            if (content != null)
                                return Parse(content);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(TAG, $"Failed to retrieve schedule metadata: {ex.Message}");
                    Log.Debug(TAG, ex.ToString());
                }
            }

            return Array.Empty<Slot>();
        }

        static IEnumerable<Slot> Parse(string content)
        {
            var slots = new List<Slot>();

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

        static bool TryParse(string[] values, CultureInfo culture, out Slot slot)
        {
            slot = null;

            if (Enum.TryParse<DayOfWeek>(values[0], out var dow))
            {
                if (TimeSpan.TryParseExact(values[1], "h\\:mm", culture, out var tod))
                {
                    var url = values.Length >= 5 ? values[4] : null;
                    var imageUrl = values.Length >= 6 ? values[5] : null;

                    slot = new Slot(dow, tod, values[3], values[2], url, imageUrl);

                    return true;
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

            return false;
        }

        public class Slot
        {
            public DayOfWeek DayOfWeek { get; private set; }
            public TimeSpan TimeOfDay { get; private set; }
            public string Artist { get; private set; }
            public string Title { get; private set; }
            public string Url { get; private set; }
            public string ImageUrl { get; private set; }

            public Slot(DayOfWeek dow, TimeSpan tod, string artist, string title, string url, string imageUrl)
            {
                DayOfWeek = dow;
                TimeOfDay = tod;
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