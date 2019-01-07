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
    class ActivityView<T>
        where T : Activity
    {
        protected ActivityView(T activity)
            => ApplicationContext = activity;

        protected Context ApplicationContext { get; private set; }
    }

    class MainActivityView : ActivityView<MainActivity>
    {
        public const string TAG = "wzxv.app.main.view";
        
        public MainActivityView(MainActivity activity, Action<MainActivityView> configurer)
            : base(activity)
        {
            activity.SetContentView(Resource.Layout.MainActivity);

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

            ScheduleTimeRange.Text = "";
            PlayingProgress.Progress = 0;

            configurer(this);
        }

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

        public async Task Refresh(bool isConnected, bool isPlaying, RadioStationNowPlaying playing)
        {
            UpdateNetworkStatus(isConnected);
            UpdateState(isPlaying);
            await UpdateNowPlaying(playing);
            UpdateProgress(playing);
        }

        public void UpdateNetworkStatus(bool isConnected)
        {
            if (isConnected)
            {
                MediaButton.Alpha = 1.0f;
                MediaButton.Clickable = true;
            }
            else
            {
                MediaButton.Alpha = 0.6f;
                MediaButton.Clickable = false;
                Toast.MakeText(ApplicationContext, "The network connection has been lost", ToastLength.Long);
            }
        }

        public void UpdateProgress(RadioStationNowPlaying playing)
        {
            PlayingProgress.Progress = 0;
            PlayingProgress.Max = 100;
            
            if (playing.Duration.TotalMinutes > 0)
                PlayingProgress.Progress = (int)Math.Ceiling((playing.Position.TotalSeconds / playing.Duration.TotalSeconds) * 100);

            if (playing.Remaining.TotalMinutes >= 1)
                PlayingProgress.ContentDescription = $"{Math.Ceiling(playing.Remaining.TotalMinutes).ToString("#0")} minutes remaining";
            else
                PlayingProgress.ContentDescription = $"{Math.Ceiling(playing.Remaining.TotalSeconds).ToString("#0")} seconds remaining";
        }

        public void UpdateState(bool isPlaying)
        {
            if (isPlaying)
            {
                MediaButton.SetImageResource(Resource.Drawable.pause);
                MediaButton.ContentDescription = "Pause";
            }
            else
            {
                MediaButton.SetImageResource(Resource.Drawable.play);
                MediaButton.ContentDescription = "Play";
            }
        }

        public async Task UpdateNowPlaying(RadioStationNowPlaying playing)
        {
            var slot = playing.Slot;

            ArtistLabel.Text = slot.Artist;
            TitleLabel.Text = slot.Title;
            ScheduleTimeRange.Text = $"{Localization.Today.Add(playing.Slot.TimeOfDay).ToString("h:mm tt")} - {Localization.Today.Add(playing.Slot.TimeOfDay).Add(playing.Duration).ToString("h:mm tt")}";
            CoverImage.SetImageResource(Resource.Drawable.logo);

            if (slot.ImageUrl == null)
            {
                CoverImage.SetImageResource(Resource.Drawable.logo);
                CoverImage.ContentDescription = "Now Playing";
            }
            else
            {
                CoverImage.ContentDescription = $"Visit {slot.Artist} on the Web";
                await CoverImage.TrySetBitmapFromUrl(slot.ImageUrl, Resource.Drawable.logo);
            }
        }
    }
}