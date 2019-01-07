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
using Android.Content.Res;
using System.Threading.Tasks;

namespace wzxv
{
    [Activity(Name = ActivityName, Label = "@string/app_name", Theme = "@style/SplashScreen", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize, ScreenOrientation = ScreenOrientation.FullSensor)]
    public class MainActivity : AppCompatActivity
    {
        public const string TAG = "wzxv.app.main";
        public const string ActivityName = "wzxv.app.main";

        private List<IServiceConnection> _connections = new List<IServiceConnection>();
        private NetworkStatus _networkStatus;
        private RadioStationService _service;
        private RadioStationScheduleService _schedule;
        private MainActivityView _view;
        
        protected override void OnCreate(Bundle savedInstanceState)
        {
            if (!string.IsNullOrEmpty(AppCenterConfig.AppSecret))
                AppCenter.Start(AppCenterConfig.AppSecret, typeof(Analytics), typeof(Crashes));

            base.Window.RequestFeature(Android.Views.WindowFeatures.ActionBar);
            base.SetTheme(Resource.Style.AppTheme);
            
            base.OnCreate(savedInstanceState);

            SetContentView(Resource.Layout.MainActivity);

            if (_view == null)
                _view = new MainActivityView(this, Configure);

            if (_networkStatus == null)
            {
                _networkStatus = new NetworkStatus(ApplicationContext, connected: OnNetworkConnected, disconnected: OnNetworkDisconnected);
            }

            if (_service == null)
            {
                var intent = new Intent(ApplicationContext, typeof(RadioStationService));
                var connection = ServiceConnectionFactory.Create<RadioStationService>(service =>
                {
                    if (service != null)
                    {
                        _service = service;
                        _service.Playing += OnRadioStationPlaying;
                        _service.StateChanged += OnRadioStationStateChanged;
                        _service.Error += OnRadioStationError;
                    }
                    else if(_service != null)
                    {
                        _service.Playing -= OnRadioStationPlaying;
                        _service.StateChanged -= OnRadioStationStateChanged;
                        _service.Error -= OnRadioStationError;
                        _service = null;
                    }
                });

                if (BindService(intent, connection, Bind.AutoCreate))
                {
                    _connections.Add(connection);
                }
            }

            if (_schedule == null)
            {
                var intent = new Intent(ApplicationContext, typeof(RadioStationScheduleService));
                var connection = ServiceConnectionFactory.Create<RadioStationScheduleService>(service =>
                {
                    if (service != null)
                    {
                        _schedule = service;
                        _schedule.Changed += OnRadioStationScheduleChanged;
                    }
                    else if (_service != null)
                    {
                        _schedule.Changed -= OnRadioStationScheduleChanged;
                        _schedule = null;
                    }
                });

                if (BindService(intent, connection, Bind.AutoCreate))
                {
                    _connections.Add(connection);
                }
            }

            _view.UpdateNetworkStatus(_networkStatus.IsConnected);
        }

        void Configure(MainActivityView view)
        {
            // Now Playing
            view.MediaButton.Click += OnPlayButtonClick;
            view.CoverImage.Click += OnCoverImageClick;
            // Social
            view.WebsiteButton.Click += (_, __) => OpenBrowser("http://wzxv.org");
            view.FacebookButton.Click += (_, __) => SocialConnector.OpenFacebook(this, "WZXVTheWord");
            view.TwitterButton.Click += (_, __) => SocialConnector.OpenTwitter(this, "wzxvtheword");
            view.InstagramButton.Click += (_, __) => SocialConnector.OpenInstagram(this, "wzxvtheword");
            // Contact
            if (PackageManager.HasSystemFeature(PackageManager.FeatureTelephony))
                view.PhoneLink.Click += (_, __) => ContactConnector.OpenDialer(this, "15853983569");
            view.MapButton.Click += (_, __) => ContactConnector.OpenMaps(this, 42.9465473, -77.3333895);
            view.MailLink.Click += (_, __) => ContactConnector.OpenMail(this, "manager@wzxv.org");
        }

        protected override void OnStop()
        {
            base.OnStop();
        }

        protected override void OnDestroy()
        {
            if (_connections != null)
            {
                foreach (var connection in _connections)
                    UnbindService(connection);

                _connections = null;
            }

            if (_networkStatus != null)
            {
                _networkStatus.Dispose();
                _networkStatus = null;
            }

            try
            {
                var intent = new Intent(ApplicationContext, typeof(RadioStationService)).SetAction(RadioStationService.ActionStop).PutExtra(RadioStationService.ExtraKeyForce, true);
                StopService(intent);
            }
            catch (Exception ex)
            {
                Log.Error(TAG, $"Failed to stop service: {ex.Message}");
                Log.Debug(TAG, ex.ToString());
            }

            _view = null;

            base.OnDestroy();
        }

        public async override void OnConfigurationChanged(Configuration newConfig)
        {
            base.OnConfigurationChanged(newConfig);
            _view = new MainActivityView(this, Configure);
            await _view.Refresh(_networkStatus.IsConnected, _service.IsPlaying, _schedule.NowPlaying);
        }

        void OnPlayButtonClick(object sender, EventArgs e)
        {
            Events.Click("Media Button", new { _service.IsPlaying });
            
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
            RunOnUiThread(() => _view?.UpdateNetworkStatus(true));
        }

        void OnNetworkDisconnected()
        {
            if (_service != null && _service.IsPlaying)
            {
                _service.Stop();
            }

            RunOnUiThread(() => _view?.UpdateNetworkStatus(false));
        }

        void OpenBrowser(string url)
        {
            var intent = new Intent(Intent.ActionView, Android.Net.Uri.Parse(url));
            StartActivity(intent);
            Events.ExternalLink("Browser", url);
        }

        void OnRadioStationPlaying(object sender, EventArgs e)
        {
            RunOnUiThread(() => _view?.UpdateProgress(_schedule.NowPlaying));
        }

        void OnRadioStationScheduleChanged(object sender, EventArgs e)
        {
            RunOnUiThread(async () => await (_view?.UpdateNowPlaying(_schedule.NowPlaying) ?? Task.CompletedTask));
        }

        void OnRadioStationStateChanged(object sender, EventArgs e)
        {
            RunOnUiThread(() =>
            {
                _view?.UpdateState(_service.IsPlaying);

                if (_service.IsPlaying)
                {
                    Events.Playing();
                }
                else
                {
                    Events.Stopped();
                }
            });
        }

        void OnRadioStationError(object sender, RadioStationErrorEventArgs e)
        {
            Crashes.TrackError(e.Exception);
            Events.Error(e.Exception);
            RunOnUiThread(() => Toast.MakeText(this, "The stream for WZXV - The Word is having \"issues\"... :(", ToastLength.Long).Show());
        }
    }
}