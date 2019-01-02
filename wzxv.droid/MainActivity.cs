using Android.App;
using Android.OS;
using Android.Support.V7.App;
using Android.Runtime;
using Android.Widget;
using Android.Content.PM;
using System;
using System.Linq;
using Com.Google.Android.Exoplayer2.Source;
using Com.Google.Android.Exoplayer2.Extractor;
using Com.Google.Android.Exoplayer2.Upstream;
using Com.Google.Android.Exoplayer2;
using Com.Google.Android.Exoplayer2.Trackselection;
using Com.Google.Android.Exoplayer2.Util;
using Android.Media.Session;
using Java.Lang;
using Java.IO;
using Android.Content;
using System.Net.Http;
using System.Globalization;
using System.Collections.Generic;
using System.Timers;
using Android.Media;

namespace wzxv
{
    [Activity(Name = ActivityName, Label = "@string/app_name", Theme = "@style/SplashScreen", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ScreenOrientation = ScreenOrientation.Portrait)]
    public class MainActivity : AppCompatActivity
    {
        public const string ActivityName = "wzxv.app.main";
        private const int UI_REFRESH_INTERVAL = 1000;

        private ImageView WebsiteButton => FindViewById<ImageView>(Resource.Id.websiteButton);
        private ImageView FacebookButton => FindViewById<ImageView>(Resource.Id.facebookButton);
        private ImageView TwitterButton => FindViewById<ImageView>(Resource.Id.twitterButton);
        private ImageView InstagramButton => FindViewById<ImageView>(Resource.Id.instagramButton);
        private ImageView VolumeMaxButton => FindViewById<ImageView>(Resource.Id.volumeMaxButton);
        private ImageView VolumeMinButton => FindViewById<ImageView>(Resource.Id.volumeMinButton);
        private SeekBar VolumeBar => FindViewById<SeekBar>(Resource.Id.volumeBar);
        private TextView ArtistLabel => FindViewById<TextView>(Resource.Id.artistLabel);
        private TextView TitleLabel => FindViewById<TextView>(Resource.Id.titleLabel);
        private ImageView PlayButton => FindViewById<ImageView>(Resource.Id.playButton);
        private RadioStationServiceBinder AudioPlayerServiceBinder { get; set; } = null;
        private Timer _refresh;

        private AudioManager _audioManager;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            _audioManager = (AudioManager)GetSystemService(AudioService);
            
            base.Window.RequestFeature(Android.Views.WindowFeatures.ActionBar);
            base.SetTheme(Resource.Style.AppTheme);
            
            base.OnCreate(savedInstanceState);
            
            SetContentView(Resource.Layout.Main);

            TitleLabel.Visibility = Android.Views.ViewStates.Invisible;
            ArtistLabel.Visibility = Android.Views.ViewStates.Invisible;

            VolumeMinButton.Click += OnVolumeMinButtonClick;
            VolumeMaxButton.Click += OnVolumeMaxButtonClick;

            var volumeBar = VolumeBar;
            volumeBar.Max = _audioManager.GetStreamMaxVolume(Stream.Music);
            volumeBar.SetProgress(_audioManager.GetStreamVolume(Stream.Music), false);
            volumeBar.ProgressChanged += OnVolumeChanged;

            if (AudioPlayerServiceBinder == null)
            {
                var intent = new Intent(ApplicationContext, typeof(RadioStationService));
                var connection = new AudioPlayerServiceConnection(this);
                BindService(intent, connection, Bind.AutoCreate);
            }

            PlayButton.Click += OnPlayButtonClick;

            _refresh = new Timer(UI_REFRESH_INTERVAL);
            _refresh.Elapsed += (_, __) => OnRefresh();
            _refresh.Start();

            WebsiteButton.Click += (_, __) => LaunchBrowser("http://wzxv.org");
            FacebookButton.Click += (_, __) => LaunchBrowser("https://www.facebook.com/WZXVTheWord/");
            TwitterButton.Click += (_, __) => LaunchBrowser("https://twitter.com/wzxvtheword");
            InstagramButton.Click += (_, __) => LaunchBrowser("https://www.instagram.com/wzxvtheword/");
        }

        void LaunchBrowser(string url)
        {
            var uri = Android.Net.Uri.Parse(url);
            var intent = new Intent(Intent.ActionView, uri);
            StartActivity(intent);
        }

        void OnVolumeMinButtonClick(object sender, EventArgs e)
        {
            _audioManager.SetStreamVolume(Stream.Music, 0, VolumeNotificationFlags.RemoveSoundAndVibrate);
        }

        void OnVolumeMaxButtonClick(object sender, EventArgs e)
        {
            var max = _audioManager.GetStreamMaxVolume(Stream.Music);
            _audioManager.SetStreamVolume(Stream.Music, max, VolumeNotificationFlags.RemoveSoundAndVibrate);
            OnRefresh();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _refresh.Stop();
            _refresh.Dispose();
        }

        void OnRefresh()
        {
            RunOnUiThread(() =>
            {
                VolumeBar.SetProgress(_audioManager.GetStreamVolume(Stream.Music), false);
            });
        }

        void OnRadioStationSchedule(object sender, RadioStationServiceScheduleEventArgs e)
        {
            RunOnUiThread(() =>
            {
                ArtistLabel.Text = e.Artist;
                TitleLabel.Text = e.Title;
            });
        }

        void OnVolumeChanged(object sender, SeekBar.ProgressChangedEventArgs e)
        {
            _audioManager.SetStreamVolume(Stream.Music, e.Progress, VolumeNotificationFlags.RemoveSoundAndVibrate);
        }

        void OnPlayButtonClick(object sender, EventArgs e)
        {
            if (AudioPlayerServiceBinder.Service.IsPlaying)
            {
                AudioPlayerServiceBinder.Service.Stop();
            }
            else
            {
                StartForegroundService(new Intent(ApplicationContext, typeof(RadioStationService)).SetAction(RadioStationService.ACTION_PLAY));
            }
        }

        void OnRadioStationStateChanged(object sender, EventArgs e)
        {
            if (AudioPlayerServiceBinder.Service.IsPlaying)
            {
                PlayButton.SetImageResource(Resource.Drawable.pause);
                TitleLabel.Visibility = Android.Views.ViewStates.Visible;
                ArtistLabel.Visibility = Android.Views.ViewStates.Visible;
            }
            else
            {
                PlayButton.SetImageResource(Resource.Drawable.play);
                TitleLabel.Visibility = Android.Views.ViewStates.Invisible;
                ArtistLabel.Visibility = Android.Views.ViewStates.Invisible;
            }
        }

        void OnRadioStationError(object sender, RadioStationServiceErrorEventArgs e)
        {
            Toast.MakeText(ApplicationContext, "The stream for WZXV - The Word was interrupted", ToastLength.Long).Show();
        }

        class AudioPlayerServiceConnection : Java.Lang.Object, IServiceConnection
        {
            MainActivity _instance;

            public AudioPlayerServiceConnection(MainActivity instance)
            {
                _instance = instance;
            }

            public void OnServiceConnected(ComponentName name, IBinder service)
            {
                if (service is RadioStationServiceBinder binder)
                {
                    _instance.AudioPlayerServiceBinder = binder;
                    binder.Service.StateChanged += _instance.OnRadioStationStateChanged;
                    binder.Service.Error += _instance.OnRadioStationError;
                    binder.Service.Schedule += _instance.OnRadioStationSchedule;
                }
            }

            public void OnServiceDisconnected(ComponentName name)
            {
                _instance.AudioPlayerServiceBinder = null;
            }
        }
    }
}