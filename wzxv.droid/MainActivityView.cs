using System;
using System.Collections.Generic;
using System.Linq;
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
    class ActivityView<TActivity>
        where TActivity : Activity
    {
        protected ActivityView(TActivity activity)
            => Activity = activity;

        protected TActivity Activity { get; private set; }
    }

    class MainActivityView : ActivityView<MainActivity>
    {
        public const string TAG = "wzxv.app.main.view";

        private Controls _controls;

        public MainActivityView(MainActivity activity)
            : base(activity)
        {
            activity.SetContentView(Resource.Layout.MainActivity);

            _controls = new Controls(activity);
            
            _controls.ScheduleTimeRange.Text = string.Empty;
            _controls.PlayingProgress.Progress = 0;
        }

        public MainActivityView Attach(Action<Controls> action)
        {
            action(_controls);
            return this;
        }

        public MainActivityView Detach(Action<Controls> action)
        {
            action(_controls);
            return this;
        }

        public async Task Refresh(bool isConnected, bool isPlaying, RadioStationNowPlaying playing)
        {
            base.Activity.SetContentView(Resource.Layout.MainActivity);

            _controls = new Controls(base.Activity);
            
            UpdateNetworkStatus(isConnected);
            UpdateState(isPlaying);

            if (playing != null)
            {
                await UpdateNowPlaying(playing);
                UpdateProgress(playing);
            }
        }

        public void UpdateNetworkStatus(bool isConnected)
        {
            if (isConnected)
            {
                _controls.MediaButton.Alpha = 1.0f;
                _controls.MediaButton.Clickable = true;
            }
            else
            {
                _controls.MediaButton.Alpha = 0.6f;
                _controls.MediaButton.Clickable = false;
                Toast.MakeText(base.Activity, "The network connection has been lost", ToastLength.Long);
            }
        }

        public void UpdateProgress(RadioStationNowPlaying playing)
        {
            _controls.PlayingProgress.Progress = 0;
            _controls.PlayingProgress.Max = 100;
            
            if (playing.Duration.TotalMinutes > 0)
                _controls.PlayingProgress.Progress = (int)Math.Ceiling((playing.Position.TotalSeconds / playing.Duration.TotalSeconds) * 100);

            if (playing.Remaining.TotalMinutes >= 1)
                _controls.PlayingProgress.ContentDescription = $"{Math.Ceiling(playing.Remaining.TotalMinutes).ToString("#0")} minutes remaining";
            else
                _controls.PlayingProgress.ContentDescription = $"{Math.Ceiling(playing.Remaining.TotalSeconds).ToString("#0")} seconds remaining";
        }

        public void UpdateState(bool isPlaying)
        {
            if (isPlaying)
            {
                _controls.MediaButton.SetImageResource(Resource.Drawable.pause);
                _controls.MediaButton.ContentDescription = "Pause";
            }
            else
            {
                _controls.MediaButton.SetImageResource(Resource.Drawable.play);
                _controls.MediaButton.ContentDescription = "Play";
            }
        }

        public async Task UpdateNowPlaying(RadioStationNowPlaying playing)
        {
            var slot = playing.Slot;

            _controls.ArtistLabel.Text = slot.Artist;
            _controls.TitleLabel.Text = slot.Title;
            _controls.ScheduleTimeRange.Text = $"{Localization.Today.Add(playing.Slot.TimeOfDay).ToString("h:mm tt")} - {Localization.Today.Add(playing.Slot.TimeOfDay).Add(playing.Duration).ToString("h:mm tt")}";
            _controls.CoverImage.SetImageResource(Resource.Drawable.logo);

            if (slot.ImageUrl == null)
            {
                _controls.CoverImage.SetImageResource(Resource.Drawable.logo);
                _controls.CoverImage.ContentDescription = "Now Playing";
            }
            else
            {
                _controls.CoverImage.ContentDescription = $"Visit {slot.Artist} on the Web";
                await _controls.CoverImage.TrySetBitmapFromUrl(slot.ImageUrl, Resource.Drawable.logo);
            }
        }

        public class Controls
        {
            // Media Controls
            public ImageView MediaButton { get; private set; }

            // Contact Controls
            public TextView PhoneLink { get; private set; }
            public ImageView MapButton { get; private set; }
            public TextView MailLink { get; private set; }

            // Social Buttons
            public ImageView WebsiteButton { get; private set; }
            public ImageView FacebookButton { get; private set; }
            public ImageView TwitterButton { get; private set; }
            public ImageView InstagramButton { get; private set; }

            // Now Playing
            public ImageView CoverImage { get; private set; }
            public TextView ArtistLabel { get; private set; }
            public TextView TitleLabel { get; private set; }
            public TextView ScheduleTimeRange { get; private set; }
            public ProgressBar PlayingProgress { get; private set; }

            public Controls(MainActivity activity)
            {
                MediaButton = activity.FindViewById<ImageView>(Resource.Id.mediaButton);
                PhoneLink = activity.FindViewById<TextView>(Resource.Id.phoneLink);
                MapButton = activity.FindViewById<ImageView>(Resource.Id.mapButton);
                MailLink = activity.FindViewById<TextView>(Resource.Id.mailLink);

                WebsiteButton = activity.FindViewById<ImageView>(Resource.Id.websiteButton);
                FacebookButton = activity.FindViewById<ImageView>(Resource.Id.facebookButton);
                TwitterButton = activity.FindViewById<ImageView>(Resource.Id.twitterButton);
                InstagramButton = activity.FindViewById<ImageView>(Resource.Id.instagramButton);

                CoverImage = activity.FindViewById<ImageView>(Resource.Id.coverImage);
                ArtistLabel = activity.FindViewById<TextView>(Resource.Id.artistLabel);
                TitleLabel = activity.FindViewById<TextView>(Resource.Id.titleLabel);
                ScheduleTimeRange = activity.FindViewById<TextView>(Resource.Id.scheduleTimeRange);
                PlayingProgress = activity.FindViewById<ProgressBar>(Resource.Id.playingProgress);
            }
        }
    }
}