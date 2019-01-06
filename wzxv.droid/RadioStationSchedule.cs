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
    public class RadioStationSchedule
    {
        private readonly Slot[] _schedule;

        public RadioStationSchedule(IEnumerable<Slot> slots = null)
        {
            _schedule = slots?.ToArray() ?? Array.Empty<Slot>();
        }

        public bool TryGetCurrent(out Slot current, out DateTimeOffset start, out TimeSpan duration, out TimeSpan interval)
        {
            var now = TimeZoneInfo.ConvertTime(DateTimeOffset.Now, Globalization.EasternStandardTime);

            var schedule = _schedule.Select(s =>
            {
                var n = Next(now, s.DayOfWeek, s.TimeOfDay);
                return (Slot: s, LastPlayed: n.AddDays(-7), NextPlay: n);
            });

            var currentSlot = schedule
                    .Where(i => i.LastPlayed <= now)
                    .OrderByDescending(i => i.LastPlayed)
                    .Take(1)
                    .FirstOrDefault();

            current = currentSlot.Slot;
            start = currentSlot.LastPlayed.ToLocalTime();

            var nextSlot = schedule
                    .Where(i => i.NextPlay > now)
                    .OrderBy(i => i.NextPlay)
                    .Take(1)
                    .Select(i => i.NextPlay)
                    .FirstOrDefault();

            duration = nextSlot.Subtract(start).Add(TimeSpan.FromSeconds(1));
            interval = nextSlot
                    .Subtract(now)
                    .Subtract(TimeSpan.FromSeconds(1));

            return (current != null);
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
}