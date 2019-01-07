using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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
            current = null;
            start = default(DateTimeOffset);
            duration = default(TimeSpan);
            interval = default(TimeSpan);

            if (_schedule.Length > 0)
            {
                var now = DateTimeOffset.Now.ToEasternStandardTime();
                var schedule = _schedule.Select(s => (Slot: s, Schedule: GetNextDateTime(now, s.DayOfWeek, s.TimeOfDay))).OrderBy(i => i.Schedule);
                var last = schedule.LastOrDefault();
                var next = schedule.FirstOrDefault();

                current = last.Slot;
                start = last.Schedule.Subtract(TimeSpan.FromDays(7)).ToLocalTime();
                duration = next.Schedule.Subtract(start);
                interval = next.Schedule.Subtract(now);
            }

            return (current != null);
        }

        static DateTimeOffset GetNextDateTime(DateTimeOffset date, DayOfWeek dow, TimeSpan tod)
        {
            if (date.DayOfWeek != dow)
            {
                date = date.AddDays(((int)dow + 7 - (int)date.DayOfWeek) % 7);
            }
            else if (tod <= date.TimeOfDay)
            {
                date = date.AddDays(7);
            }

            return new DateTimeOffset(date.Year, date.Month, date.Day, tod.Hours, tod.Minutes, tod.Seconds, tod.Milliseconds, date.Offset);
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