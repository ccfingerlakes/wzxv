using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Media;
using Android.OS;
using Android.Support.V7.App;
using Android.Widget;
using System;
using System.Net.Http;
using System.Timers;
using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using System.Collections.Generic;
using Android.Util;
using Android.Arch.Lifecycle;

namespace wzxv
{
    [Activity(Name = ActivityName, Label = "@string/app_name", Theme = "@style/SplashScreen", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize, ScreenOrientation = ScreenOrientation.Portrait)]
    public class MainActivity : AppCompatActivity
    {
        public const string TAG = "wzxv.app.main";
        public const string ActivityName = "wzxv.app.main";

        private RadioStationServiceBinder _service;
        private AudioVolumeObserver _volumeObserver;
        private string _metadataUrl = null;
        private readonly HttpClient _http = new HttpClient();
        
        private AudioManager _audioManager;
        private NetworkStatus _networkStatus;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            AppCenter.Start("f5115ef1-a47c-437e-8d62-9899be633736", typeof(Analytics), typeof(Crashes));
            Controls.Register(this);

            _audioManager = (AudioManager)GetSystemService(AudioService);
            _networkStatus = new NetworkStatus(ApplicationContext, connected: OnNetworkConnected, disconnected: OnNetworkDisconnected);
            _volumeObserver = new AudioVolumeObserver(this, new Handler(), SetVolumeBar);

            base.Window.RequestFeature(Android.Views.WindowFeatures.ActionBar);
            base.SetTheme(Resource.Style.AppTheme);
            
            base.OnCreate(savedInstanceState);
            
            SetContentView(Resource.Layout.MainActivity);

            if (_service == null)
            {
                var intent = new Intent(ApplicationContext, typeof(RadioStationService));
                var connection = new AudioPlayerServiceConnection(this);
                BindService(intent, connection, Bind.AutoCreate);
            }

            InitializeControls();

            if (!_networkStatus.IsConnected)
                OnNetworkDisconnected();
        }

        void InitializeControls()
        {
            if (Build.VERSION.SdkInt < BuildVersionCodes.N)
            {
                Controls.VolumeControls.Visibility = Android.Views.ViewStates.Gone;
            }
            else
            {
                Controls.VolumeMinButton.Click += OnVolumeMinButtonClick;
                Controls.VolumeMaxButton.Click += OnVolumeMaxButtonClick;

                Controls.VolumeBar.Configure(volumeBar =>
                {
                    volumeBar.Max = _audioManager.GetStreamMaxVolume(Stream.Music);
                    volumeBar.SetProgress(_audioManager.GetStreamVolume(Stream.Music), true);
                    volumeBar.ProgressChanged += OnVolumeBarChanged;
                });
            }

            Controls.CoverImage.Click += OnCoverImageClick;
            Controls.MediaButton.Click += OnPlayButtonClick;

            Controls.WebsiteButton.Click += (_, __) => OpenBrowser("http://wzxv.org");
            Controls.FacebookButton.Click += (_, __) => SocialConnector.OpenFacebook(this, "WZXVTheWord");
            Controls.TwitterButton.Click += (_, __) => SocialConnector.OpenTwitter(this, "wzxvtheword");
            Controls.InstagramButton.Click += (_, __) => SocialConnector.OpenInstagram(this, "wzxvtheword");

            if (PackageManager.HasSystemFeature(PackageManager.FeatureTelephony))
            {
                Controls.PhoneButton.Click += (_, __) => ContactConnector.OpenDialer(this, "5853983569");
                Controls.PhoneLink.Click += (_, __) => ContactConnector.OpenDialer(this, "5853983569");
            }

            Controls.MapButton.Click += (_, __) => ContactConnector.OpenMaps(this, 42.9465473, -77.3333895);

            Controls.MailButton.Click += (_, __) => ContactConnector.OpenMail(this, "manager@wzxv.org");
            Controls.MailLink.Click += (_, __) => ContactConnector.OpenMail(this, "manager@wzxv.org");
        }

        protected override void OnDestroy()
        {
            if (_volumeObserver != null)
            {
                _volumeObserver.Dispose();
                _volumeObserver = null;
            }

            Controls.Register(null);

            base.OnDestroy();
        }

        void OnPlayButtonClick(object sender, EventArgs e)
        {
            Events.Click("Media Button", new { _service.Service.IsPlaying });
            Controls.MediaButton.Enabled = false;

            if (_service.Service.IsPlaying)
            {
                _service.Service.Stop();
            }
            else
            {
                var intent = new Intent(ApplicationContext, typeof(RadioStationService)).SetAction(RadioStationService.ActionPlay);

                if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                {
                    StartForegroundService(intent);
                }
                else
                {
                    StartService(intent);
                }
            }
        }

        void OnCoverImageClick(object sender, EventArgs e)
        {
            if (_metadataUrl != null)
            {
                var uri = Android.Net.Uri.Parse(_metadataUrl);
                var intent = new Intent(Intent.ActionView, uri);
                StartActivity(intent);
                Events.Click("Cover", new { Url = _metadataUrl });
            }
        }

        void OnNetworkConnected()
        {
            RunOnUiThread(() => Controls.MediaButton.Configure(playButton =>
                {
                    playButton.Alpha = 1.0f;
                    playButton.Clickable = true;
                }));
        }

        void OnNetworkDisconnected()
        {
            if (_service != null && _service.Service.IsPlaying)
            {
                _service.Service.Stop();
            }

            RunOnUiThread(() => Controls.MediaButton.Configure(playButton =>
            {
                playButton.Alpha = 0.6f;
                playButton.Clickable = false;
                Toast.MakeText(this, "The network connection has been lost", ToastLength.Long);
            }));
        }

        void OpenBrowser(string url)
        {
            var intent = new Intent(Intent.ActionView, Android.Net.Uri.Parse(url));
            StartActivity(intent);
            Events.ExternalLink("Browser", url);
        }

        void OnVolumeMinButtonClick(object sender, EventArgs e)
        {
            _audioManager.SetStreamVolume(Stream.Music, 0, VolumeNotificationFlags.RemoveSoundAndVibrate);
            SetVolumeBar(0);
        }

        void OnVolumeMaxButtonClick(object sender, EventArgs e)
        {
            var max = _audioManager.GetStreamMaxVolume(Stream.Music);
            _audioManager.SetStreamVolume(Stream.Music, max, VolumeNotificationFlags.RemoveSoundAndVibrate);
            SetVolumeBar(max);
        }

        void OnVolumeBarChanged(object sender, SeekBar.ProgressChangedEventArgs e)
        {
            _audioManager.SetStreamVolume(Stream.Music, e.Progress, VolumeNotificationFlags.RemoveSoundAndVibrate);
        }

        void SetVolumeBar(int volume)
        {
            if (Controls.VolumeControls.Visibility == Android.Views.ViewStates.Visible)
            {
                RunOnUiThread(() =>
                {
                    Controls.VolumeBar.SetProgress(volume, false);
                });
            }
        }

        void OnRadioStationMetadataChanged(object sender, RadioStationServiceMetadataChangedEventArgs e)
        {
            RunOnUiThread(() =>
            {
                _metadataUrl = e.Url;

                Controls.ArtistLabel.Text = e.Artist;
                Controls.TitleLabel.Text = e.Title;
                Controls.CoverImage.SetImageResource(Resource.Drawable.logo);

                if (e.ImageUrl == null)
                {
                    Controls.CoverImage.Configure(coverImage =>
                    {
                        coverImage.SetImageResource(Resource.Drawable.logo);
                        coverImage.ContentDescription = "Now Playing";
                    });
                }
                else
                {
                    Controls.CoverImage.Configure(coverImage =>
                    {
                        coverImage.ContentDescription = $"Visit {e.Artist} on the Web";

                        try
                        {
                            using (var response = _http.GetAsync(e.ImageUrl).Result)
                            {
                                if (response.IsSuccessStatusCode)
                                {
                                    coverImage.SetImageBitmap(BitmapFactory.DecodeStream(response.Content.ReadAsStreamAsync().Result));
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Warn(TAG, $"Failed to retrieve metadata image url '{e.ImageUrl}': {ex.Message}");
                            Log.Debug(TAG, ex.ToString());
                            coverImage.SetImageResource(Resource.Drawable.logo);
                        }
                    });
                }
            });
        }

        void OnRadioStationStateChanged(object sender, EventArgs e)
        {
            RunOnUiThread(() =>
            {
                if (_service.Service.IsPlaying)
                {
                    Controls.MediaButton.Configure(mediaButton =>
                    {
                        mediaButton.SetImageResource(Resource.Drawable.pause);
                        mediaButton.ContentDescription = "Pause";
                    });
                    Events.Playing();
                }
                else
                {
                    Controls.MediaButton.Configure(mediaButton =>
                    {
                        mediaButton.SetImageResource(Resource.Drawable.play);
                        mediaButton.ContentDescription = "Play";
                    });
                    Events.Stopped();
                }

                Controls.MediaButton.Enabled = true;
            });
        }

        void OnRadioStationError(object sender, RadioStationErrorEventArgs e)
        {
            Crashes.TrackError(e.Exception);
            Events.Error(e.Exception);
            RunOnUiThread(() => Toast.MakeText(this, "The stream for WZXV - The Word is having \"issues\"... :(", ToastLength.Long).Show());
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
                    _instance._service = binder;
                    binder.Service.StateChanged += _instance.OnRadioStationStateChanged;
                    binder.Service.Error += _instance.OnRadioStationError;
                    binder.Service.MetadataChanged += _instance.OnRadioStationMetadataChanged;
                }
            }

            public void OnServiceDisconnected(ComponentName name)
            {
                _instance._service = null;
            }
        }

        static class Controls
        {
            private static MainActivity __activity;

            public static void Register(MainActivity activity)
                => __activity = activity;

            public static ImageView PhoneButton => __activity?.FindViewById<ImageView>(Resource.Id.phoneButton);
            public static TextView PhoneLink => __activity?.FindViewById<TextView>(Resource.Id.phoneLink);
            public static ImageView MapButton => __activity?.FindViewById<ImageView>(Resource.Id.mapButton);
            public static ImageView MailButton => __activity?.FindViewById<ImageView>(Resource.Id.mailButton);
            public static TextView MailLink => __activity?.FindViewById<TextView>(Resource.Id.mailLink);
            public static ImageView WebsiteButton => __activity?.FindViewById<ImageView>(Resource.Id.websiteButton);
            public static ImageView FacebookButton => __activity?.FindViewById<ImageView>(Resource.Id.facebookButton);
            public static ImageView TwitterButton => __activity?.FindViewById<ImageView>(Resource.Id.twitterButton);
            public static ImageView InstagramButton => __activity?.FindViewById<ImageView>(Resource.Id.instagramButton);
            public static ImageView VolumeMaxButton => __activity?.FindViewById<ImageView>(Resource.Id.volumeMaxButton);
            public static ImageView VolumeMinButton => __activity?.FindViewById<ImageView>(Resource.Id.volumeMinButton);
            public static SeekBar VolumeBar => __activity?.FindViewById<SeekBar>(Resource.Id.volumeBar);
            public static TextView ArtistLabel => __activity?.FindViewById<TextView>(Resource.Id.artistLabel);
            public static TextView TitleLabel => __activity?.FindViewById<TextView>(Resource.Id.titleLabel);
            public static ImageView CoverImage => __activity?.FindViewById<ImageView>(Resource.Id.coverImage);
            public static ImageView MediaButton => __activity?.FindViewById<ImageView>(Resource.Id.mediaButton);
            public static LinearLayout VolumeControls => __activity?.FindViewById<LinearLayout>(Resource.Id.volumeControls);
        }
    }
}