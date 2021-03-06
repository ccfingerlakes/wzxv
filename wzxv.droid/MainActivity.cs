﻿using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Media;
using Android.OS;
using Android.Widget;
using System;
using System.Net.Http;
using System.Timers;
using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using System.Collections.Generic;
using Android.Util;
using Android.Content.Res;
using System.Threading.Tasks;
using AndroidX.AppCompat.App;

namespace wzxv
{
    [Activity(Name = ActivityName, Label = "@string/app_name", Theme = "@style/SplashScreen", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize, ScreenOrientation = ScreenOrientation.FullSensor)]
    public class MainActivity : AppCompatActivity
    {
        public const string TAG = "wzxv.app.main";
        public const string ActivityName = "wzxv.app.main";

        private List<IServiceConnection> _connections;
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

            if (_connections == null)
            {
                _connections = new List<IServiceConnection>();

                var intent = new Intent(ApplicationContext, typeof(RadioStationService));
                var serviceConnection = ServiceConnectionFactory.Create<RadioStationService>(service =>
                {
                    if (service != null)
                    {
                        _service = service;
                        _service.Playing += OnRadioStationPlaying;
                        _service.StateChanged += OnRadioStationStateChanged;
                        _service.Error += OnRadioStationError;
                    }
                    else if (_service != null)
                    {
                        _service.Playing -= OnRadioStationPlaying;
                        _service.StateChanged -= OnRadioStationStateChanged;
                        _service.Error -= OnRadioStationError;
                        _service = null;
                    }
                });

                if (BindService(intent, serviceConnection, Bind.AutoCreate))
                {
                    _connections.Add(serviceConnection);
                }

                intent = new Intent(ApplicationContext, typeof(RadioStationScheduleService));
                var scheduleConnection = ServiceConnectionFactory.Create<RadioStationScheduleService>(service =>
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

                if (BindService(intent, scheduleConnection, Bind.AutoCreate))
                {
                    _connections.Add(scheduleConnection);
                }
            }

            SetContentView(Resource.Layout.MainActivity);

            if (_view == null)
                _view = new MainActivityView(this).Attach(OnAttachView);

            if (_networkStatus == null)
            {
                _networkStatus = new NetworkStatus(ApplicationContext, connected: OnNetworkConnected, disconnected: OnNetworkDisconnected);
            }

            _view.UpdateNetworkStatus(_networkStatus.IsConnected);

            if (_service != null)
            {
                _view.UpdateState(_service.IsPlaying);
            }

            if (_schedule != null)
            {
                RunOnUiThread(() => _view.UpdateNowPlaying(_schedule.NowPlaying));
            }
        }

        protected override void OnDestroy()
        {
            if (_view != null)
            {
                _view.Detach(OnDetachView);
                _view = null;
            }

            if (_networkStatus != null)
            {
                _networkStatus.Dispose();
                _networkStatus = null;
            }

            if (_connections != null)
            {
                foreach (var connection in _connections)
                    UnbindService(connection);

                _connections = null;
            }

            _service = null;
            _schedule = null;

            base.OnDestroy();
        }

        private void OnAttachView(MainActivityView.Controls controls)
        {
            controls.Logo.LongClick += OnLogoLongClick;
            // Now Playing
            controls.MediaButton.Click += OnPlayButtonClick;
            controls.CoverImage.Click += OnCoverImageClick;
            // Social
            controls.WebsiteButton.Click += OnWebsiteButtonClick;
            controls.FacebookButton.Click += OnFacebookButtonClick;
            controls.TwitterButton.Click += OnTwitterButtonClick;
            controls.InstagramButton.Click += OnInstagramButtonClick;
            // Contact
            controls.PhoneLink.Click += OnPhoneLinkClick;
            controls.MapButton.Click += OnMapButtonClick;
            controls.MailLink.Click += OnMailLinkClick;
        }

        private void OnDetachView(MainActivityView.Controls controls)
        {
            controls.Logo.LongClick -= OnLogoLongClick;
            // Now Playing
            controls.MediaButton.Click -= OnPlayButtonClick;
            controls.CoverImage.Click -= OnCoverImageClick;
            // Social
            controls.WebsiteButton.Click -= OnWebsiteButtonClick;
            controls.FacebookButton.Click -= OnFacebookButtonClick;
            controls.TwitterButton.Click -= OnTwitterButtonClick;
            controls.InstagramButton.Click -= OnInstagramButtonClick;
            // Contact
            controls.PhoneLink.Click -= OnPhoneLinkClick;
            controls.MapButton.Click -= OnMapButtonClick;
            controls.MailLink.Click -= OnMailLinkClick;
        }

        public override void OnConfigurationChanged(Configuration newConfig)
        {
            Log.Debug(TAG, $"{nameof(MainActivity)}::{nameof(OnConfigurationChanged)}");
            base.OnConfigurationChanged(newConfig);
            _view.Detach(OnDetachView);
            _view = new MainActivityView(this).Attach(OnAttachView);
            _view.Refresh(_networkStatus.IsConnected, _service?.IsPlaying == true, _schedule?.NowPlaying);
        }

        private void OnLogoLongClick(object sender, EventArgs e)
        {
            try
            {
                var versionName = PackageManager.GetPackageInfo(PackageName, 0).VersionName;

                if (!string.IsNullOrEmpty(versionName))
                {
                    var dialog = new AndroidX.AppCompat.App.AlertDialog.Builder(this)
                                    .SetTitle("About")
                                    .SetMessage($"Version {versionName}")
                                    .SetIcon(Android.Resource.Drawable.IcDialogInfo)
                                    .SetCancelable(true)
                                    .Show();
                }
            }
            catch (Exception ex)
            {
                Log.Warn(TAG, $"Could not get package info: {ex.Message}");
                Log.Debug(TAG, ex.ToString());
            }
        }

        private void OnWebsiteButtonClick(object sender, EventArgs e) => OpenBrowser("http://wzxv.org");

        private void OnFacebookButtonClick(object sender, EventArgs e) => SocialConnector.OpenFacebook(this, "WZXVTheWord");

        private void OnTwitterButtonClick(object sender, EventArgs e) => SocialConnector.OpenTwitter(this, "wzxvtheword");

        private void OnInstagramButtonClick(object sender, EventArgs e) => SocialConnector.OpenInstagram(this, "wzxvtheword");

        private void OnPhoneLinkClick(object sender, EventArgs e) => ContactConnector.OpenDialer(this, "15853983569");

        private void OnMapButtonClick(object sender, EventArgs e) => ContactConnector.OpenMaps(this, 42.9465473, -77.3333895);

        private void OnMailLinkClick(object sender, EventArgs e) => ContactConnector.OpenMail(this, "manager@wzxv.org");

        private void OnPlayButtonClick(object sender, EventArgs e)
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

        private void OnCoverImageClick(object sender, EventArgs e)
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

        private void OnNetworkConnected()
        {
            RunOnUiThread(() => _view?.UpdateNetworkStatus(true));
        }

        private void OnNetworkDisconnected()
        {
            if (_service != null && _service.IsPlaying)
            {
                _service.Stop();
            }

            RunOnUiThread(() => _view?.UpdateNetworkStatus(false));
        }

        private void OpenBrowser(string url)
        {
            var intent = new Intent(Intent.ActionView, Android.Net.Uri.Parse(url));
            StartActivity(intent);
            Events.ExternalLink("Browser", url);
        }

        private void OnRadioStationPlaying(object sender, EventArgs e)
        {
            RunOnUiThread(() => _view?.UpdateProgress(_schedule.NowPlaying));
        }

        private void OnRadioStationScheduleChanged(object sender, EventArgs e)
        {
            RunOnUiThread(() => _view?.UpdateNowPlaying(_schedule.NowPlaying));
        }

        private void OnRadioStationStateChanged(object sender, EventArgs e)
        {
            RunOnUiThread(() =>
            {
                _view?.UpdateState(_service?.IsPlaying == true);

                if (_service?.IsPlaying == true)
                {
                    Events.Playing();
                }
                else
                {
                    Events.Stopped();
                }
            });
        }

        private void OnRadioStationError(object sender, RadioStationErrorEventArgs e)
        {
            Log.Error(TAG, $"{nameof(MainActivity)}::{nameof(OnRadioStationError)} {e.Exception.Message ?? "See debug log for full exception detail"}");
            Log.Debug(TAG, $"{nameof(MainActivity)}::{nameof(OnRadioStationError)} {e.Exception}");
            Crashes.TrackError(e.Exception);
            RunOnUiThread(() => Toast.MakeText(this, "The stream for WZXV - The Word is having \"issues\"... :(", ToastLength.Long).Show());
        }
    }
}