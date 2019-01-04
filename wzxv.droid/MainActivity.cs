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

namespace wzxv
{
    [Activity(Name = ActivityName, Label = "@string/app_name", Theme = "@style/SplashScreen", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize, ScreenOrientation = ScreenOrientation.Portrait)]
    public class MainActivity : AppCompatActivity
    {
        public const string TAG = "wzxv.app.main";
        public const string ActivityName = "wzxv.app.main";

        private RadioStationServiceBinder _service;
        private string _metadataUrl = null;
        private readonly Timer _volumeRefresh = new Timer(1000);
        private readonly HttpClient _http = new HttpClient();
        
        private AudioManager _audioManager;
        private NetworkStatus _networkStatus;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            AppCenter.Start("f5115ef1-a47c-437e-8d62-9899be633736", typeof(Analytics), typeof(Crashes));
            Controls.Register(this);

            _audioManager = (AudioManager)GetSystemService(AudioService);
            _networkStatus = new NetworkStatus(ApplicationContext, connected: OnNetworkConnected, disconnected: OnNetworkDisconnected);

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
                    volumeBar.ProgressChanged += OnVolumeChanged;
                });

                _volumeRefresh.Elapsed += (_, __) => OnVolumeRefresh();
                _volumeRefresh.Start();
            }

            Controls.CoverImage.Click += (_, __) =>
            {
                if (_metadataUrl != null)
                    LaunchBrowser(_metadataUrl);
            };

            Controls.PlayButton.Click += OnPlayButtonClick;

            Controls.WebsiteButton.Click += (_, __) => LaunchBrowser("http://wzxv.org");
            Controls.FacebookButton.Click += (_, __) => LaunchBrowser("https://www.facebook.com/WZXVTheWord/");
            Controls.TwitterButton.Click += (_, __) => LaunchBrowser("https://twitter.com/wzxvtheword");
            Controls.InstagramButton.Click += (_, __) => LaunchBrowser("https://www.instagram.com/wzxvtheword/");

            if (PackageManager.HasSystemFeature(PackageManager.FeatureTelephony))
            {
                Controls.PhoneButton.Click += (_, __) => DialNumber("5853983569");
                Controls.PhoneLink.Click += (_, __) => DialNumber("5853983569");
            }

            Controls.MapButton.Click += (_, __) => ShowMap(42.9465473, -77.3333895);

            Controls.MailButton.Click += (_, __) => SendMail("manager@wzxv.org");
            Controls.MailLink.Click += (_, __) => SendMail("manager@wzxv.org");
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _volumeRefresh.Dispose();
        }

        void OnNetworkConnected()
        {
            RunOnUiThread(() => Controls.PlayButton.Configure(playButton =>
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

            RunOnUiThread(() => Controls.PlayButton.Configure(playButton =>
            {
                playButton.Alpha = 0.6f;
                playButton.Clickable = false;
            }));
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
            OnVolumeRefresh();
        }

        void OnVolumeChanged(object sender, SeekBar.ProgressChangedEventArgs e)
        {
            _audioManager.SetStreamVolume(Stream.Music, e.Progress, VolumeNotificationFlags.RemoveSoundAndVibrate);
        }

        void OnPlayButtonClick(object sender, EventArgs e)
        {
            Controls.PlayButton.Enabled = false;

            if (_service.Service.IsPlaying)
            {
                _service.Service.Stop();
                AppCenterEvents.Stop();
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

                AppCenterEvents.Play();
            }
        }

        void OnVolumeRefresh()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.N)
            {
                RunOnUiThread(() =>
                {
                    Controls.VolumeBar.SetProgress(_audioManager.GetStreamVolume(Stream.Music), false);
                });
            }
        }

        void OnRadioStationMetadataChanged(object sender, RadioStationServiceMetadataChangedEventArgs e)
        {
            RunOnUiThread(() =>
            {
                Controls.ArtistLabel.Text = e.Artist;
                Controls.TitleLabel.Text = e.Title;
                _metadataUrl = e.Url;
                Controls.CoverImage.SetImageResource(Resource.Drawable.logo);

                if (e.ImageUrl != null)
                {
                    try
                    {
                        using (var response = _http.GetAsync(e.ImageUrl).Result)
                        {
                            if (response.IsSuccessStatusCode)
                            {
                                Controls.CoverImage.SetImageBitmap(BitmapFactory.DecodeStream(response.Content.ReadAsStreamAsync().Result));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warn(TAG, $"Failed to retrieve metadata image url '{e.ImageUrl}': {ex.Message}");
                        Log.Debug(TAG, ex.ToString());
                    }
                }
            });
        }

        void OnRadioStationStateChanged(object sender, EventArgs e)
        {
            RunOnUiThread(() =>
            {
                if (_service.Service.IsPlaying)
                {
                    Controls.PlayButton.SetImageResource(Resource.Drawable.pause);
                    AppCenterEvents.Playing();
                }
                else
                {
                    Controls.PlayButton.SetImageResource(Resource.Drawable.play);
                    AppCenterEvents.Stopped();
                }

                Controls.PlayButton.Enabled = true;
            });
        }

        void OnRadioStationError(object sender, RadioStationErrorEventArgs e)
        {
            Crashes.TrackError(e.Exception);
            AppCenterEvents.Error(e.Exception);
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
            public static ImageView PlayButton => __activity?.FindViewById<ImageView>(Resource.Id.playButton);
            public static LinearLayout VolumeControls => __activity?.FindViewById<LinearLayout>(Resource.Id.volumeControls);
        }
    }
}