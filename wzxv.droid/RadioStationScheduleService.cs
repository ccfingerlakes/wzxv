﻿using System;
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
    [Service]
    public class RadioStationScheduleService : Service
    {
        private const string TAG = "wzxv.app.radio.schedule";
        private const string Url = "https://raw.githubusercontent.com/ccfingerlakes/wzxv/master/schedule.csv";

        public event EventHandler Changed;

        private Timer _timer;
        private RadioStationSchedule _schedule;

        public RadioStationNowPlaying NowPlaying { get; private set; }

        public RadioStationScheduleService()
            : base()
        { }

        public RadioStationScheduleService(IntPtr javaReference, JniHandleOwnership transfer)
            : base(javaReference, transfer)
        { }

        public override void OnCreate()
        {
            _timer = new Timer()
            {
                Interval = 1,
                AutoReset = false
            };
            _timer.Elapsed += OnRefresh;

            Task.Run(async () =>
            {
                _schedule = await RadioStationScheduleReader.Read(Url);
                _timer.Start();
            });

            base.OnCreate();
        }

        public override void OnDestroy()
        {
            if (_timer != null)
            {
                _timer.Dispose();
            }
            base.OnDestroy();
        }

        public override IBinder OnBind(Intent intent)
        {
            return new ServiceBinder<RadioStationScheduleService>(this);
        }

        void OnRefresh(object sender, ElapsedEventArgs e)
        {
            var interval = TimeSpan.FromMinutes(1);

            if (_schedule.TryGetCurrent(out var current, out var start, out var duration, out interval))
            {
                if (NowPlaying?.Slot != current)
                {
                    NowPlaying = new RadioStationNowPlaying(current, start, duration);
                    Task.Run(() => Changed?.Invoke(this, EventArgs.Empty));
                }
            }

            if (interval.TotalMilliseconds >= 0)
                _timer.Interval = interval.TotalMilliseconds;
            else
                _timer.Interval = 0;

            _timer.Start();
        }
    }

    public class RadioStationNowPlaying
    {
        private readonly DateTimeOffset _start;

        public RadioStationSchedule.Slot Slot { get; private set; }
        public TimeSpan Duration { get; private set; }
        public TimeSpan Position => DateTimeOffset.Now.Subtract(_start);
        public TimeSpan Remaining => Duration.Subtract(Position);

        public RadioStationNowPlaying(RadioStationSchedule.Slot slot, DateTimeOffset start, TimeSpan duration)
        {
            Slot = slot;
            _start = start;
            Duration = duration;
        }
    }
}