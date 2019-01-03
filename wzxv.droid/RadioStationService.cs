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
using Android.Support.V4.Media.Session;
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
    public class RadioStationService : Service, IPlayerEventListener, AudioManager.IOnAudioFocusChangeListener
    {
        private const int NotificationId = 1;
        private const string ChannelId = "wzxv.app.PLAYBACK";
        private const string StreamUrl = "http://ic2.christiannetcast.com/wzxv-fm";
        
        public const string ActionPlay = "wzxv.app.PLAY";
        public const string ActionStop = "wzxv.app.STOP";
        public const string ActionToggle = "wzxv.app.TOGGLE";

        private SimpleExoPlayer _player;
        private AudioManager _audioManager;
        private RadioStationNotificationManager _notificationManager;
        private RadioStationMediaSession _mediaSession;
        private RadioStationSchedule _schedule;
        private RadioStationServiceLock _lock;
        private RadioStationServiceBinder _binder;

        public bool IsStarted { get; private set; } = false;
        public bool IsPlaying => _player != null && _player.PlayWhenReady == true && _player.PlaybackState == Player.StateReady;

        public event EventHandler<RadioStationServiceMetadataChangedEventArgs> Metadata;
        public event EventHandler StateChanged;
        public event EventHandler<RadioStationServiceErrorEventArgs> Error;

        public override void OnCreate()
        {
            base.OnCreate();
            _audioManager = (AudioManager)GetSystemService(AudioService);
            _notificationManager = new RadioStationNotificationManager(ApplicationContext);
            _schedule = new RadioStationSchedule(OnScheduleChanged);
        }

        public override IBinder OnBind(Intent intent)
        {
            _binder = new RadioStationServiceBinder(this);
            return _binder;
        }

        public override void OnDestroy()
        {
            base.OnDestroy();

            if (_schedule != null)
            {
                _schedule.Dispose();
                _schedule = null;
            }

            if (_player != null)
            {
                _player.Release();
                _player = null;
            }

            if (_mediaSession != null)
            {
                _mediaSession.Dispose();
                _mediaSession = null;
            }

            if (_notificationManager != null)
            {
                _notificationManager.Stop();
                _notificationManager = null;
            }

            if (_lock != null)
            {
                _lock.Dispose();
                _lock = null;
            }

            StopForeground(true);
        }

        public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                if (!IsStarted)
                {
                    StartForeground(NotificationId, _notificationManager.CreateNotificationBuilder().Build());
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
                                                    .SetOnAudioFocusChangeListener(this)
                                                    .SetAudioAttributes(new AudioAttributes.Builder()
                                                        .SetUsage(AudioUsageKind.Media)
                                                        .SetContentType(AudioContentType.Music)
                                                        .Build())
                                                    .Build();
                
                if (_audioManager.RequestAudioFocus(audioFocusRequest) == AudioFocusRequest.Granted)
                {
                    try
                    {
                        _mediaSession = new RadioStationMediaSession(ApplicationContext);
                        _lock = new RadioStationServiceLock(ApplicationContext);

                        var mediaUri = Android.Net.Uri.Parse(StreamUrl);
                        var userAgent = Util.GetUserAgent(this, "wzxv.app");
                        var defaultHttpDataSourceFactory = new DefaultHttpDataSourceFactory(userAgent);
                        var defaultDataSourceFactory = new DefaultDataSourceFactory(this, null, defaultHttpDataSourceFactory);
                        var mediaSourceFactory = new ExtractorMediaSource.Factory(defaultDataSourceFactory);
                        var mediaSource = mediaSourceFactory.CreateMediaSource(mediaUri);

                        _player.Prepare(mediaSource);
                        _schedule.Refresh(force: true);
                    }
                    catch
                    {
                        if (_mediaSession != null)
                        {
                            _mediaSession.Dispose();
                            _mediaSession = null;
                        }

                        if (_lock != null)
                        {
                            _lock.Dispose();
                            _lock = null;
                        }

                        throw;
                    }
                }
            }
        }

        public void Stop()
        {
            if (IsPlaying)
            {
                _player.Stop();
                _notificationManager.Stop();
                StopForeground(true);
            }

            if (_lock != null)
            {
                _lock.Dispose();
                _lock = null;
            }

            if (_mediaSession != null)
            {
                _mediaSession.Dispose();
                _mediaSession = null;
            }
        }

        void OnScheduleChanged(RadioStationSchedule.Slot slot)
        {
            _notificationManager.Notify(NotificationId, builder =>
            {
                builder
                    .SetContentTitle(slot.Title)
                    .SetContentText(slot.Artist);

                if (IsPlaying)
                {
                    builder.AddAction(_notificationManager.CreateAction(Android.Resource.Drawable.IcMediaPause, "Pause", ActionStop));
                }
                else
                {
                    builder.AddAction(_notificationManager.CreateAction(Android.Resource.Drawable.IcMediaPlay, "Play", ActionPlay));
                }
            });

            if (_mediaSession != null)
            {
                _mediaSession.SetMetadata(slot.Artist, slot.Title, builder =>
                {
                    if (slot.ImageUrl != null)
                        builder.PutString(MediaMetadata.MetadataKeyAlbumArtUri, slot.ImageUrl);
                });
            }

            Metadata?.Invoke(this, new RadioStationServiceMetadataChangedEventArgs(slot.Artist, slot.Title, slot.Url, slot.ImageUrl));
        }

        private int? _previousAudioVolume = null;
        void AudioManager.IOnAudioFocusChangeListener.OnAudioFocusChange(AudioFocus focusChange)
        {
            var maxVolume = _audioManager.GetStreamMaxVolume(Stream.Music);

            switch (focusChange)
            {
                case AudioFocus.Gain:
                    Play();

                    if (_previousAudioVolume != null)
                    {
                        _audioManager.SetStreamVolume(Stream.Music, _previousAudioVolume.Value, VolumeNotificationFlags.RemoveSoundAndVibrate);
                        _previousAudioVolume = null;
                    }
                    break;

                case AudioFocus.Loss:
                case AudioFocus.LossTransient:
                    Stop();
                    break;

                case AudioFocus.LossTransientCanDuck:
                    _previousAudioVolume = _audioManager.GetStreamVolume(Stream.Music);
                    _audioManager.SetStreamVolume(Stream.Music, (int)Math.Round(maxVolume * 0.1), VolumeNotificationFlags.RemoveSoundAndVibrate);
                    break;
            }
        }

        void IPlayerEventListener.OnPlayerError(ExoPlaybackException e)
        {
            _mediaSession.SetPlaybackState(PlaybackStateCompat.StateError);

            _notificationManager.Stop();

            if (_lock != null)
            {
                _lock.Dispose();
                _lock = null;
            }

            StopForeground(true);

            Error?.Invoke(this, new RadioStationServiceErrorEventArgs(e));
        }

        void IPlayerEventListener.OnPlayerStateChanged(bool playWhenReady, int playbackState)
        {
            switch (playbackState)
            {
                case Player.StateReady:
                    _mediaSession.SetPlaybackState(PlaybackStateCompat.StatePlaying);
                    break;

                case Player.StateBuffering:
                    _mediaSession.SetPlaybackState(PlaybackStateCompat.StateBuffering);
                    break;
            }

            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        void IPlayerEventListener.OnLoadingChanged(bool isLoading)
        {
        }

        void IPlayerEventListener.OnPlaybackParametersChanged(PlaybackParameters playbackParameters)
        {
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

    public class RadioStationServiceMetadataChangedEventArgs : EventArgs
    {
        public string Artist { get; private set; }
        public string Title { get; private set; }
        public string Url { get; private set; }
        public string ImageUrl { get; private set; }

        public RadioStationServiceMetadataChangedEventArgs(string artist, string title, string url, string imageUrl)
        {
            Artist = artist;
            Title = title;
            Url = url;
            ImageUrl = imageUrl;
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
}