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
using Android.Util;
using Com.Google.Android.Exoplayer2;
using Com.Google.Android.Exoplayer2.Metadata;
using Com.Google.Android.Exoplayer2.Source;
using Com.Google.Android.Exoplayer2.Trackselection;
using Com.Google.Android.Exoplayer2.Upstream;
using Com.Google.Android.Exoplayer2.Util;

namespace wzxv
{
    [Service]
    [IntentFilter(new [] {  ActionPlay, ActionStop })]
    public class RadioStationService : Service
    {
        public const string ExtraKeyForce = "wzxv.app.radio.FORCE";
        public const string ActionPlay = "wzxv.app.radio.PLAY";
        public const string ActionStop = "wzxv.app.radio.STOP";
        public const string ActionToggle = "wzxv.app.radio.TOGGLE";

        private const string TAG = "wzxv.app.radio";
        private const int NotificationId = 1;

        public event EventHandler Playing;
        public event EventHandler StateChanged;
        public event EventHandler<RadioStationErrorEventArgs> Error;

        private int _startId;
        private RadioStationPlayer _player;
        private RadioStationNotificationManager _notificationManager;
        private RadioStationMediaSession _mediaSession;
        private RadioStationScheduleService _schedule;
        private RadioStationServiceLock _lock;
        private Handler _playingHandler = new Handler();
        
        public bool IsPlaying => _player != null && _player.IsPlaying;

        public override void OnCreate()
        {
            base.OnCreate();
            _mediaSession = new RadioStationMediaSession(this);
            _notificationManager = new RadioStationNotificationManager(this);

            if (_schedule == null)
            {
                var intent = new Intent(ApplicationContext, typeof(RadioStationScheduleService));
                var connection = new ServiceConnection<RadioStationScheduleServiceBinder>(binder =>
                {
                    if (binder == null)
                    {
                        if (_schedule != null)
                        {
                            _schedule.Changed -= OnScheduleChanged;
                        }

                        _schedule = null;
                    }
                    else
                    {
                        _schedule = binder.Service;
                        _schedule.Changed += OnScheduleChanged;
                    }
                });
                BindService(intent, connection, Bind.AutoCreate);
            }   

            _player = new RadioStationPlayer(this);
            _player.StateChanged += OnPlayerStateChanged;
            _player.Error += OnPlayerError;
            _playingHandler.Post(OnPlaying);
        }

        public override IBinder OnBind(Intent intent)
        {
            return new RadioStationServiceBinder(this);
        }

        public override void OnDestroy()
        {
            base.OnDestroy();

            if (_playingHandler != null)
            {
                _playingHandler.Dispose();
                _playingHandler = null;
            }

            if (_player != null)
            {
                _player.Dispose();
                _player = null;
            }

            if (_schedule != null)
            {
                _schedule.Dispose();
                _schedule = null;
            }

            if (_notificationManager != null)
            {
                _notificationManager.Stop();
                _notificationManager = null;
            }

            if (_mediaSession != null)
            {
                _mediaSession.Dispose();
                _mediaSession = null;
            }
        }

        public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                if (_startId == 0)
                {
                    StartForeground(NotificationId, _notificationManager.CreateNotificationBuilder().Build());
                }
            }

            if (_startId == 0)
            {
                _startId = startId;
            }

            switch (intent?.Action)
            {
                case ActionPlay:
                case ActionToggle when !IsPlaying:
                    Play();
                    break;

                case ActionStop:
                case ActionToggle when IsPlaying:
                    Stop(intent.HasExtra(ExtraKeyForce));
                    break;
            }

            return StartCommandResult.Sticky;
        }

        void Play()
        {
            if (!IsPlaying)
            {
                try
                {
                    _lock = new RadioStationServiceLock(this);
                    _player.Start();
                }
                catch (Exception ex)
                {
                    Log.Error(TAG, $"Failed to play: {ex.Message}");
                    Log.Debug(TAG, ex.ToString());

                    if (_lock != null)
                    {
                        _lock.Release();
                        _lock = null;
                    }

                    Error?.Invoke(this, new RadioStationErrorEventArgs(ex));
                }
            }
        }

        public void Stop(bool force = false)
        {
            try
            {
                if (IsPlaying)
                {
                    _player.Stop();
                }
            }
            catch (Exception ex)
            {
                Log.Error(TAG, $"Error during stop: {ex.Message}");
                Log.Debug(TAG, ex.ToString());
            }
            finally
            {
                if (_lock != null)
                {
                    _lock.Release();
                    _lock = null;
                }

                StopForeground(force);
                StopSelf(_startId);

                _startId = 0;
            }
        }

        void OnPlaying()
        {
            if (_playingHandler != null)
            {
                if (_schedule?.NowPlaying != null)
                    Playing?.Invoke(this, EventArgs.Empty);

                _playingHandler.PostDelayed(OnPlaying, 500);
            }
        }

        void OnPlayerStateChanged(object sender, EventArgs e)
        {
            if (_player.IsPlaying)
            {
                _mediaSession.SetPlaybackState(PlaybackStateCompat.StatePlaying, (_schedule?.NowPlaying?.Position ?? default(TimeSpan)));
            }
            else
            {
                _mediaSession.SetPlaybackState(PlaybackStateCompat.StateStopped);
            }

            UpdateNotification();

            StateChanged?.Invoke(this, e);
        }

        void OnPlayerError(object sender, RadioStationErrorEventArgs e)
        {
            _mediaSession.SetPlaybackState(PlaybackStateCompat.StateError);
            Stop();
            Error?.Invoke(this, e);
        }

        void OnScheduleChanged(object sender, EventArgs e)
        {
            UpdateNotification();
        }

        void UpdateNotification()
        {
            var playing = _schedule?.NowPlaying;
            var slot = playing?.Slot;

            if (_mediaSession != null && slot != null)
            {
                _mediaSession.SetMetadata(slot.Artist, slot.Title, playing.Duration, builder =>
                {
                    if (slot.ImageUrl != null)
                        builder.PutString(MediaMetadata.MetadataKeyAlbumArtUri, slot.ImageUrl);
                });

                if (_player.IsPlaying)
                {
                    _mediaSession.SetPlaybackState(PlaybackStateCompat.StatePlaying, playing.Position);
                }
            }

            _notificationManager.Notify(NotificationId, builder =>
            {
                if (slot != null)
                {
                    builder
                        .SetContentTitle(slot.Title)
                        .SetContentText(slot.Artist);
                }

                if (IsPlaying)
                {
                    builder.AddAction(_notificationManager.CreateAction(Android.Resource.Drawable.IcMediaPause, "Pause", ActionStop));
                }
                else
                {
                    builder.AddAction(_notificationManager.CreateAction(Android.Resource.Drawable.IcMediaPlay, "Play", ActionPlay));
                }
            });
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