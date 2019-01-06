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

        private NetworkStatus _networkStatus;
        private RadioStationService _service;
        private RadioStationScheduleService _schedule;
        
        protected override void OnCreate(Bundle savedInstanceState)
        {
            AppCenter.Start("f5115ef1-a47c-437e-8d62-9899be633736", typeof(Analytics), typeof(Crashes));
            Controls.Register(this);

            _networkStatus = new NetworkStatus(ApplicationContext, connected: OnNetworkConnected, disconnected: OnNetworkDisconnected);
            
            base.Window.RequestFeature(Android.Views.WindowFeatures.ActionBar);
            base.SetTheme(Resource.Style.AppTheme);
            
            base.OnCreate(savedInstanceState);
            
            SetContentView(Resource.Layout.MainActivity);

            InitializeControls();

            if (_service == null)
            {
                var intent = new Intent(ApplicationContext, typeof(RadioStationService));
                var connection = new ServiceConnection<RadioStationServiceBinder>(binder =>
                {
                    if (binder != null)
                    {
                        _service = binder.Service;
                        _service.Playing += OnRadioStationPlaying;
                        _service.StateChanged += OnRadioStationStateChanged;
                        _service.Error += OnRadioStationError;
                    }
                    else
                    {
                        _service.Playing -= OnRadioStationPlaying;
                        _service.StateChanged -= OnRadioStationStateChanged;
                        _service.Error -= OnRadioStationError;
                        _service = null;
                    }
                });

                BindService(intent, connection, Bind.AutoCreate);
            }

            if (_schedule == null)
            {
                var intent = new Intent(ApplicationContext, typeof(RadioStationScheduleService));
                var connection = new ServiceConnection<RadioStationScheduleServiceBinder>(binder =>
                {
                    if (binder != null)
                    {
                        _schedule = binder.Service;
                        _schedule.Changed += OnRadioStationScheduleChanged;
                    }
                    else
                    {
                        _schedule.Changed -= OnRadioStationScheduleChanged;
                        _schedule = null;
                    }
                });

                BindService(intent, connection, Bind.AutoCreate);
            }

            if (!_networkStatus.IsConnected)
                OnNetworkDisconnected();
        }

        void InitializeControls()
        {
            // Now Playing
            Controls.PlayingProgress.Progress = 0;
            Controls.CoverImage.Click += OnCoverImageClick;
            Controls.ScheduleTimeRange.Text = "";
            Controls.MediaButton.Click += OnPlayButtonClick;

            // Social
            Controls.WebsiteButton.Click += (_, __) => OpenBrowser("http://wzxv.org");
            Controls.FacebookButton.Click += (_, __) => SocialConnector.OpenFacebook(this, "WZXVTheWord");
            Controls.TwitterButton.Click += (_, __) => SocialConnector.OpenTwitter(this, "wzxvtheword");
            Controls.InstagramButton.Click += (_, __) => SocialConnector.OpenInstagram(this, "wzxvtheword");

            // Contact
            if (PackageManager.HasSystemFeature(PackageManager.FeatureTelephony))
            {
                Controls.PhoneLink.Click += (_, __) => ContactConnector.OpenDialer(this, "15853983569");
            }

            Controls.MapButton.Click += (_, __) => ContactConnector.OpenMaps(this, 42.9465473, -77.3333895);
            Controls.MailLink.Click += (_, __) => ContactConnector.OpenMail(this, "manager@wzxv.org");
        }

        protected override void OnStop()
        {
            var intent = new Intent(ApplicationContext, typeof(RadioStationService)).SetAction(RadioStationService.ActionStop).PutExtra(RadioStationService.ExtraKeyForce, true);
            StopService(intent);
            base.OnStop();
        }

        protected override void OnDestroy()
        {
            Controls.Register(null);
            base.OnDestroy();
        }

        void OnPlayButtonClick(object sender, EventArgs e)
        {
            Events.Click("Media Button", new { _service.IsPlaying });
            Controls.MediaButton.Enabled = false;

            if (_service.IsPlaying)
            {
                _service.Stop();
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
            var url = _schedule?.NowPlaying?.Slot?.Url;

            if (url != null)
            {
                var uri = Android.Net.Uri.Parse(url);
                var intent = new Intent(Intent.ActionView, uri);
                StartActivity(intent);
                Events.Click("Cover", new { Url = url });
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
            if (_service != null && _service.IsPlaying)
            {
                _service.Stop();
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

        void OnRadioStationPlaying(object sender, EventArgs e)
        {
            RunOnUiThread(() =>
            {
                var playing = _schedule.NowPlaying;

                Controls.PlayingProgress.Configure(c =>
                {
                    c.Progress = 0;
                    c.Max = (int)Math.Ceiling(playing.Duration.TotalSeconds);
                    c.Progress = (int)Math.Floor(playing.Position.TotalSeconds);
                });
            });
        }

        void OnRadioStationScheduleChanged(object sender, EventArgs e)
        {
            RunOnUiThread(async () =>
            {
                var playing = _schedule.NowPlaying;
                var slot = playing.Slot;

                Controls.ArtistLabel.Text = slot.Artist;
                Controls.TitleLabel.Text = slot.Title;
                Controls.ScheduleTimeRange.Text = $"{Globalization.Today.Add(playing.Slot.TimeOfDay).ToString("h:mm tt")} - {Globalization.Today.Add(playing.Slot.TimeOfDay).Add(playing.Duration).ToString("h:mm tt")}";
                Controls.CoverImage.SetImageResource(Resource.Drawable.logo);

                if (slot.ImageUrl == null)
                {
                    Controls.CoverImage.Configure(coverImage =>
                    {
                        coverImage.SetImageResource(Resource.Drawable.logo);
                        coverImage.ContentDescription = "Now Playing";
                    });
                }
                else
                {
                    await Controls.CoverImage.ConfigureAsync(async coverImage =>
                    {
                        coverImage.ContentDescription = $"Visit {slot.Artist} on the Web";

                        try
                        {
                            await coverImage.SetBitmapFromUrl(slot.ImageUrl);
                        }
                        catch (Exception ex)
                        {
                            Log.Warn(TAG, $"Failed to retrieve metadata image url '{slot.ImageUrl}': {ex.Message}");
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
                if (_service.IsPlaying)
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

        static class Controls
        {
            private static MainActivity __activity;

            public static void Register(MainActivity activity)
                => __activity = activity;

            public static ImageView MediaButton => __activity?.FindViewById<ImageView>(Resource.Id.mediaButton);

            // Contact Controls
            public static TextView PhoneLink => __activity?.FindViewById<TextView>(Resource.Id.phoneLink);
            public static ImageView MapButton => __activity?.FindViewById<ImageView>(Resource.Id.mapButton);
            public static TextView MailLink => __activity?.FindViewById<TextView>(Resource.Id.mailLink);

            // Social Buttons
            public static ImageView WebsiteButton => __activity?.FindViewById<ImageView>(Resource.Id.websiteButton);
            public static ImageView FacebookButton => __activity?.FindViewById<ImageView>(Resource.Id.facebookButton);
            public static ImageView TwitterButton => __activity?.FindViewById<ImageView>(Resource.Id.twitterButton);
            public static ImageView InstagramButton => __activity?.FindViewById<ImageView>(Resource.Id.instagramButton);

            // Now Playing
            public static ImageView CoverImage => __activity?.FindViewById<ImageView>(Resource.Id.coverImage);
            public static TextView ArtistLabel => __activity?.FindViewById<TextView>(Resource.Id.artistLabel);
            public static TextView TitleLabel => __activity?.FindViewById<TextView>(Resource.Id.titleLabel);
            public static TextView ScheduleTimeRange => __activity?.FindViewById<TextView>(Resource.Id.scheduleTimeRange);
            public static ProgressBar PlayingProgress => __activity?.FindViewById<ProgressBar>(Resource.Id.playingProgress);
        }
    }
}