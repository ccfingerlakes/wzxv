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
using Android.Graphics;

namespace wzxv
{
    [Activity(Name = ActivityName, Label = "@string/app_name", Theme = "@style/SplashScreen", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ScreenOrientation = ScreenOrientation.Portrait)]
    public class MainActivity : AppCompatActivity
    {
        public const string ActivityName = "wzxv.app.main";
        private const int UI_REFRESH_INTERVAL = 1000;

        private ImageView PhoneButton => FindViewById<ImageView>(Resource.Id.phoneButton);
        private TextView PhoneLink => FindViewById<TextView>(Resource.Id.phoneLink);
        private ImageView MapButton => FindViewById<ImageView>(Resource.Id.mapButton);
        private ImageView MailButton => FindViewById<ImageView>(Resource.Id.mailButton);
        private TextView MailLink => FindViewById<TextView>(Resource.Id.mailLink);
        private ImageView WebsiteButton => FindViewById<ImageView>(Resource.Id.websiteButton);
        private ImageView FacebookButton => FindViewById<ImageView>(Resource.Id.facebookButton);
        private ImageView TwitterButton => FindViewById<ImageView>(Resource.Id.twitterButton);
        private ImageView InstagramButton => FindViewById<ImageView>(Resource.Id.instagramButton);
        private ImageView VolumeMaxButton => FindViewById<ImageView>(Resource.Id.volumeMaxButton);
        private ImageView VolumeMinButton => FindViewById<ImageView>(Resource.Id.volumeMinButton);
        private SeekBar VolumeBar => FindViewById<SeekBar>(Resource.Id.volumeBar);
        private TextView ArtistLabel => FindViewById<TextView>(Resource.Id.artistLabel);
        private TextView TitleLabel => FindViewById<TextView>(Resource.Id.titleLabel);
        private ImageView CoverImage => FindViewById<ImageView>(Resource.Id.coverImage);
        private ImageView PlayButton => FindViewById<ImageView>(Resource.Id.playButton);

        private RadioStationServiceBinder RadioStationServiceBinder { get; set; } = null;
        private Timer _refresh;
        private string _metadataUrl = null;
        private readonly HttpClient _http = new HttpClient();
        
        private AudioManager _audioManager;
        private NetworkStatus _networkStatus;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            _audioManager = (AudioManager)GetSystemService(AudioService);
            _networkStatus = new NetworkStatus(ApplicationContext);
            _networkStatus.Connected += OnNetworkConnected;
            _networkStatus.Disconnected += OnNetworkDisconnected;

            base.Window.RequestFeature(Android.Views.WindowFeatures.ActionBar);
            base.SetTheme(Resource.Style.AppTheme);
            
            base.OnCreate(savedInstanceState);
            
            SetContentView(Resource.Layout.Main);

            TitleLabel.Visibility = Android.Views.ViewStates.Invisible;
            ArtistLabel.Visibility = Android.Views.ViewStates.Invisible;
            CoverImage.Visibility = Android.Views.ViewStates.Invisible;
            CoverImage.Click += (_, __) =>
            {
                if (_metadataUrl != null)
                    LaunchBrowser(_metadataUrl);
            };

            VolumeMinButton.Click += OnVolumeMinButtonClick;
            VolumeMaxButton.Click += OnVolumeMaxButtonClick;

            var volumeBar = VolumeBar;
            volumeBar.Max = _audioManager.GetStreamMaxVolume(Stream.Music);
            volumeBar.SetProgress(_audioManager.GetStreamVolume(Stream.Music), false);
            volumeBar.ProgressChanged += OnVolumeChanged;

            if (RadioStationServiceBinder == null)
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

            if (PackageManager.HasSystemFeature(PackageManager.FeatureTelephony))
            {
                PhoneButton.Click += (_, __) => DialNumber("5853983569");
                PhoneLink.Click += (_, __) => DialNumber("5853983569");
            }

            MapButton.Click += (_, __) => ShowMap(42.9465473, -77.3333895);

            MailButton.Click += (_, __) => SendMail("manager@wzxv.org");
            MailLink.Click += (_, __) => SendMail("manager@wzxv.org");

            if (!_networkStatus.IsConnected)
                OnNetworkDisconnected(this, EventArgs.Empty);
        }

        private void OnNetworkConnected(object sender, EventArgs e)
        {
            PlayButton.Alpha = 1.0f;
            PlayButton.Clickable = true;
        }

        private void OnNetworkDisconnected(object sender, EventArgs e)
        {
            if (RadioStationServiceBinder != null && RadioStationServiceBinder.Service.IsPlaying)
            {
                RadioStationServiceBinder.Service.Stop();
            }

            PlayButton.Alpha = 0.6f;
            PlayButton.Clickable = false;
        }

        void LaunchBrowser(string url)
        {
            var uri = Android.Net.Uri.Parse(url);
            var intent = new Intent(Intent.ActionView, uri);
            StartActivity(intent);
        }

        void DialNumber(string number)
        {
            var uri = Android.Net.Uri.Parse($"tel:{number}");
            var intent = new Intent(Intent.ActionDial, uri);
            StartActivity(intent);
        }

        void ShowMap(double latitude, double longitude)
        {
            var uri = Android.Net.Uri.Parse($"geo:{latitude},{longitude}?q=Calvary Chapel of the Finger Lakes, 1777 Rochester Rd, Farmington NY 14425");
            var intent = new Intent(Intent.ActionView, uri);
            StartActivity(intent);
        }

        void SendMail(string to)
        {
            var intent = new Intent(Intent.ActionSend)
                            .PutExtra(Android.Content.Intent.ExtraEmail, new[] { to })
                            .SetType("message/rfc822");
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

        void OnRadioStationSchedule(object sender, RadioStationServiceMetadataChangedEventArgs e)
        {
            RunOnUiThread(() =>
            {
                ArtistLabel.Text = e.Artist;
                TitleLabel.Text = e.Title;
                _metadataUrl = e.Url;
                CoverImage.SetImageResource(Resource.Drawable.logo);

                if (e.ImageUrl != null)
                {
                    try
                    {
                        using (var response = _http.GetAsync(e.ImageUrl).Result)
                        {
                            if (response.IsSuccessStatusCode)
                            {
                                CoverImage.SetImageBitmap(BitmapFactory.DecodeStream(response.Content.ReadAsStreamAsync().Result));
                            }
                        }
                    }
                    catch
                    {
                    }
                }
            });
        }

        void OnVolumeChanged(object sender, SeekBar.ProgressChangedEventArgs e)
        {
            _audioManager.SetStreamVolume(Stream.Music, e.Progress, VolumeNotificationFlags.RemoveSoundAndVibrate);
        }

        void OnPlayButtonClick(object sender, EventArgs e)
        {
            if (RadioStationServiceBinder.Service.IsPlaying)
            {
                RadioStationServiceBinder.Service.Stop();
            }
            else
            {
                StartForegroundService(new Intent(ApplicationContext, typeof(RadioStationService)).SetAction(RadioStationService.ActionPlay));
            }
        }

        void OnRadioStationStateChanged(object sender, EventArgs e)
        {
            if (RadioStationServiceBinder.Service.IsPlaying)
            {
                PlayButton.SetImageResource(Resource.Drawable.pause);
                TitleLabel.Visibility = Android.Views.ViewStates.Visible;
                ArtistLabel.Visibility = Android.Views.ViewStates.Visible;
                CoverImage.Visibility = Android.Views.ViewStates.Visible;
            }
            else
            {
                PlayButton.SetImageResource(Resource.Drawable.play);
                TitleLabel.Visibility = Android.Views.ViewStates.Invisible;
                ArtistLabel.Visibility = Android.Views.ViewStates.Invisible;
                CoverImage.Visibility = Android.Views.ViewStates.Invisible;
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
                    _instance.RadioStationServiceBinder = binder;
                    binder.Service.StateChanged += _instance.OnRadioStationStateChanged;
                    binder.Service.Error += _instance.OnRadioStationError;
                    binder.Service.Metadata += _instance.OnRadioStationSchedule;
                }
            }

            public void OnServiceDisconnected(ComponentName name)
            {
                _instance.RadioStationServiceBinder = null;
            }
        }
    }
}