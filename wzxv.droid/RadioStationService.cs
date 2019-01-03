using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Android.App;
using Android.Content;
using Android.Media;
using Android.Net;
using Android.Net.Wifi;
using Android.OS;
using Android.Support.V4.App;
using Com.Google.Android.Exoplayer2;
using Com.Google.Android.Exoplayer2.Metadata;
using Com.Google.Android.Exoplayer2.Source;
using Com.Google.Android.Exoplayer2.Trackselection;
using Com.Google.Android.Exoplayer2.Upstream;
using Com.Google.Android.Exoplayer2.Util;

namespace wzxv
{
    [Service(Name = "wzxv.app.audio")]
    [IntentFilter(new [] {  ActionPlay, ActionStop })]
    public class RadioStationService : Service, IPlayerEventListener
    {
        private const int NotificationId = 1;
        private const string ChannelId = "wzxv.app.PLAYBACK";
        private const string StreamUrl = "http://ic2.christiannetcast.com/wzxv-fm";
        private const string ScheduleUrl = "https://drive.google.com/uc?export=download&id=1VHOK768OrBKro49AmfgLzwkSEdm_tWX5";
        private const int ScheduleRefreshInterval = 15000;

        public const string ActionPlay = "wzxv.app.PLAY";
        public const string ActionStop = "wzxv.app.STOP";
        public const string ActionToggle = "wzxv.app.TOGGLE";

        private SimpleExoPlayer _player;
        private NotificationManager _notificationManager;
        private AudioManager _audioManager;
        private WifiManager _wifiManager;
        private WifiManager.WifiLock _wifiLock;
        private PowerManager _powerManager;
        private PowerManager.WakeLock _powerWakeLock;
        private RadioStationServiceBinder _binder;
        private RadioStationSchedule _schedule;
        private Timer _scheduleRefresh;

        public bool IsStarted { get; private set; } = false;
        public bool IsPlaying => _player != null && _player.PlayWhenReady == true && _player.PlaybackState == Player.StateReady;

        public event Action<object, RadioStationServiceScheduleEventArgs> Schedule;
        public event Action<object, EventArgs> StateChanged;
        public event Action<object, RadioStationServiceErrorEventArgs> Error;

        public override void OnCreate()
        {
            base.OnCreate();

            _audioManager = (AudioManager)GetSystemService(AudioService);
            _wifiManager = (WifiManager)GetSystemService(WifiService);
            _notificationManager = (NotificationManager)GetSystemService(NotificationService);
            _powerManager = (PowerManager)GetSystemService(PowerService);
            _schedule = RadioStationSchedule.LoadFrom(ScheduleUrl);
            _scheduleRefresh = new Timer(ScheduleRefreshInterval);
            _scheduleRefresh.Elapsed += (_, __) => OnScheduleRefresh();
        }

        public override IBinder OnBind(Intent intent)
        {
            _binder = new RadioStationServiceBinder(this);
            return _binder;
        }

        public override bool OnUnbind(Intent intent)
        {
            StopNotifications();
            return base.OnUnbind(intent);
        }

        public override void OnDestroy()
        {
            base.OnDestroy();

            if (_scheduleRefresh != null)
            {
                _scheduleRefresh.Stop();
                _scheduleRefresh.Dispose();
                _scheduleRefresh = null;
            }

            if (_player != null)
            {
                if (IsPlaying)
                    _player.Stop();

                _player.Release();
                _player = null;

                StopNotifications();
                StopForeground(true);
                ReleaseLocks();
            }
        }

        public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                if (!IsStarted)
                {
                    RegisterNotificationChannel();
                    
                    var notification = CreateNotificationBuilder()
                                        .Build();

                    StartForeground(NotificationId, notification);

                    IsStarted = true;
                }
            }

            switch (intent.Action)
            {
                case ActionPlay:
                case ActionToggle when !IsPlaying:
                    Play();
                    break;

                case ActionStop:
                case ActionToggle when IsPlaying:
                    Stop();
                    break;
            }

            return StartCommandResult.Sticky;
        }

        void RegisterNotificationChannel()
        {
            var channel = new NotificationChannel(ChannelId, "Playback", NotificationImportance.Low)
            {
                LockscreenVisibility = NotificationVisibility.Public
            };

            channel.SetShowBadge(false);

            _notificationManager.CreateNotificationChannel(channel);
        }

        NotificationCompat.Builder CreateNotificationBuilder()
        {
            var pendingIntent = PendingIntent.GetActivity(ApplicationContext, 0, new Intent(ApplicationContext, typeof(MainActivity)), PendingIntentFlags.UpdateCurrent);
            
            var builder = new NotificationCompat.Builder(ApplicationContext)
                .SetSmallIcon(Resource.Drawable.headset)
                .SetContentIntent(pendingIntent)
                .SetOngoing(true)
                .SetVisibility(NotificationCompat.VisibilityPublic)
                .SetContentTitle("WZXV - The Word");

            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                builder.SetChannelId(ChannelId);
            }
            
            return builder;
        }

        void StopNotifications()
        {
            _notificationManager.CancelAll();
        }

        void AquireLocks()
        {
            if (_wifiLock == null)
            {
                _wifiLock = _wifiManager.CreateWifiLock(WifiMode.Full, "wzxv.app");
            }
            _wifiLock.Acquire();

            if (_powerWakeLock == null)
            {
                _powerWakeLock = _powerManager.NewWakeLock(WakeLockFlags.Full, "wzxv.app");
            }
            _powerWakeLock.Acquire();
        }

        void ReleaseLocks()
        {
            if (_wifiLock != null)
            {
                _wifiLock.Release();
                _wifiLock = null;
            }

            if (_powerWakeLock != null)
            {
                _powerWakeLock.Release();
                _powerWakeLock = null;
            }
        }

        void Play()
        {
            if (!IsPlaying)
            {
                if (_player == null)
                {
                    var defaultBandwidthMeter = new DefaultBandwidthMeter();
                    var adaptiveTrackSelectionFactory = new AdaptiveTrackSelection.Factory(defaultBandwidthMeter);
                    var defaultTrackSelector = new DefaultTrackSelector(adaptiveTrackSelectionFactory);

                    _player = ExoPlayerFactory.NewSimpleInstance(this, defaultTrackSelector);
                    _player.AddListener(this);
                    _player.PlayWhenReady = true;
                }

                var audioFocusRequest = new AudioFocusRequestClass.Builder(AudioFocus.Gain)
                                                    .SetAudioAttributes(new AudioAttributes.Builder()
                                                        .SetUsage(AudioUsageKind.Media)
                                                        .SetContentType(AudioContentType.Music)
                                                        .Build())
                                                    .Build();

                if (_audioManager.RequestAudioFocus(audioFocusRequest) == AudioFocusRequest.Granted)
                {
                    AquireLocks();

                    var mediaUri = Android.Net.Uri.Parse(StreamUrl);
                    var userAgent = Util.GetUserAgent(this, "wzxv.app");
                    var defaultHttpDataSourceFactory = new DefaultHttpDataSourceFactory(userAgent);
                    var defaultDataSourceFactory = new DefaultDataSourceFactory(this, null, defaultHttpDataSourceFactory);
                    var mediaSourceFactory = new ExtractorMediaSource.Factory(defaultDataSourceFactory);
                    var mediaSource = mediaSourceFactory.CreateMediaSource(mediaUri);
                    
                    _player.Prepare(mediaSource);

                    OnScheduleRefresh();
                    _scheduleRefresh.Start();
                }
            }
        }

        public void Stop()
        {
            if (IsPlaying)
            {
                _scheduleRefresh.Stop();
                _player.Stop();
                StopNotifications();
                StopForeground(true);
                ReleaseLocks();
            }
        }

        void OnScheduleRefresh()
        {
            (var artist, var title) = _schedule.GetCurrent();

            _notificationManager.Notify(NotificationId, CreateNotificationBuilder()
                                                            .SetContentTitle(title)
                                                            .SetContentText(artist)
                                                            .Build());

            Schedule?.Invoke(this, new RadioStationServiceScheduleEventArgs(artist, title));
        }

        void IPlayerEventListener.OnLoadingChanged(bool isLoading)
        {
        }

        void IPlayerEventListener.OnPlaybackParametersChanged(PlaybackParameters playbackParameters)
        {
        }

        void IPlayerEventListener.OnPlayerError(ExoPlaybackException e)
        {
            _scheduleRefresh.Stop();
            StopNotifications();
            StopForeground(true);
            ReleaseLocks();
            Error?.Invoke(this, new RadioStationServiceErrorEventArgs(e));
        }

        void IPlayerEventListener.OnPlayerStateChanged(bool playWhenReady, int playbackState)
        {
            string contentTitle = null;

            switch (playbackState)
            {
                case Player.StateReady:
                    contentTitle = "Playing...";
                    break;

                case Player.StateBuffering:
                    contentTitle = "Loading...";
                    break;
            }

            var builder = CreateNotificationBuilder();

            if (contentTitle != null)
            {
                builder.SetContentTitle(contentTitle);
            }

            _notificationManager.Notify(NotificationId, builder.Build());

            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        void IPlayerEventListener.OnPositionDiscontinuity(int reason)
        {
        }

        void IPlayerEventListener.OnRepeatModeChanged(int reason)
        {
        }

        void IPlayerEventListener.OnSeekProcessed()
        {
        }

        void IPlayerEventListener.OnShuffleModeEnabledChanged(bool reason)
        {
        }

        void IPlayerEventListener.OnTimelineChanged(Timeline timeline, Java.Lang.Object manifest, int reason)
        {
        }

        void IPlayerEventListener.OnTracksChanged(TrackGroupArray ignored, TrackSelectionArray trackSelections)
        {
        }
    }

    public class RadioStationServiceScheduleEventArgs : EventArgs
    {
        public string Artist { get; private set; }
        public string Title { get; private set; }

        public RadioStationServiceScheduleEventArgs(string artist, string title)
        {
            Artist = artist;
            Title = title;
        }
    }

    public class RadioStationServiceErrorEventArgs : EventArgs
    {
        public ExoPlaybackException Exception { get; private set; }

        public RadioStationServiceErrorEventArgs(ExoPlaybackException ex)
        {
            this.Exception = ex;
        }
    }

    public class RadioStationServiceBinder : Binder
    {
        public RadioStationService Service { get; private set; }

        public RadioStationServiceBinder(RadioStationService service)
        {
            Service = service;
        }
    }

    //    private IExoPlayer _player = null;
    //    private IMediaSource _mediaSource = null;

    //    public bool IsPlaying => _player.PlayWhenReady == true && _player.PlaybackState == Player.StateReady;

    //    private NotificationManager Notification => (NotificationManager)GetSystemService(NotificationService);

    //    public override void OnCreate()
    //    {
    //        base.OnCreate();

    //        var mediaUrl = "http://ic2.christiannetcast.com/wzxv-fm";
    //        var mediaUri = Android.Net.Uri.Parse(mediaUrl);
    //        var userAgent = Util.GetUserAgent(this, "wzxv.app");
    //        var defaultHttpDataSourceFactory = new DefaultHttpDataSourceFactory(userAgent);
    //        var defaultDataSourceFactory = new DefaultDataSourceFactory(this, null, defaultHttpDataSourceFactory);
    //        var mediaSourceFactory = new ExtractorMediaSource.Factory(defaultDataSourceFactory);

    //        _mediaSource = mediaSourceFactory.CreateMediaSource(mediaUri);

    //        var defaultBandwidthMeter = new DefaultBandwidthMeter();
    //        var adaptiveTrackSelectionFactory = new AdaptiveTrackSelection.Factory(defaultBandwidthMeter);
    //        var defaultTrackSelector = new DefaultTrackSelector(adaptiveTrackSelectionFactory);

    //        _player = ExoPlayerFactory.NewSimpleInstance(this, defaultTrackSelector);
    //        _player.AddListener(this);
    //        _player.PlayWhenReady = true;
    //    }

    //    public override IBinder OnBind(Intent intent)
    //    {
    //        return null;
    //    }

    //    public override void OnDestroy()
    //    {
    //        if (IsPlaying)
    //        {
    //            _player.Stop();
    //        }

    //        _player.Release();

    //        Notification.Cancel(NotificationId);

    //        base.OnDestroy();
    //    }

    //    public override StartCommandResult OnStartCommand(Intent intent, [GeneratedEnum] StartCommandFlags flags, int startId)
    //    {
    //        switch (intent.Action)
    //        {
    //            case Play:
    //                if (!IsPlaying)
    //                {
    //                    var channel = new NotificationChannel(NotificationChannelId, "Audio Player", NotificationImportance.Default);

    //                    channel.LockscreenVisibility = NotificationVisibility.Public;

    //                    Notification.CreateNotificationChannel(channel);

    //                    var notification = new Notification.Builder(this, NotificationChannelId)
    //                                        .SetContentTitle(Resources.GetString(Resource.String.app_name))
    //                                        .SetContentText("Playing")
    //                                        .SetSmallIcon(Resource.Drawable.ic_stat_audio)
    //                                        .SetContentIntent(BuildIntentToShowMainActivity())
    //                                        .SetOngoing(true)
    //                                        .Build();

    //                    StartForeground(NotificationId, notification);

    //                    _player.Prepare(_mediaSource);
    //                }
    //                break;

    //            case Stop:
    //                if (IsPlaying)
    //                {
    //                    _player.Stop();
    //                    StopForeground(true);
    //                    StopSelf();
    //                }
    //                break;
    //        }

    //        return StartCommandResult.Sticky;
    //    }

    //    PendingIntent BuildIntentToShowMainActivity()
    //    {
    //        var notificationIntent = new Intent(this, typeof(MainActivity));
    //        notificationIntent.SetAction(MainActivity.ActivityName);
    //        notificationIntent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTask);

    //        var pendingIntent = PendingIntent.GetActivity(this, 0, notificationIntent, PendingIntentFlags.UpdateCurrent);
    //        return pendingIntent;
    //    }

    //    public void OnLoadingChanged(bool p0)
    //    {
    //    }

    //    public void OnPlaybackParametersChanged(PlaybackParameters p0)
    //    {
    //    }

    //    public void OnPlayerError(ExoPlaybackException p0)
    //    {
    //    }

    //    public void OnPlayerStateChanged(bool p0, int p1)
    //    {
    //    }

    //    public void OnPositionDiscontinuity(int p0)
    //    {
    //    }

    //    public void OnRepeatModeChanged(int p0)
    //    {
    //    }

    //    public void OnSeekProcessed()
    //    {
    //    }

    //    public void OnShuffleModeEnabledChanged(bool p0)
    //    {
    //    }

    //    public void OnTimelineChanged(Timeline p0, Java.Lang.Object p1, int p2)
    //    {
    //    }

    //    public void OnTracksChanged(TrackGroupArray p0, TrackSelectionArray p1)
    //    {
    //    }
    //}
}