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
    [Service(Name = "wzxv.app.radio")]
    [IntentFilter(new [] {  ActionPlay, ActionStop })]
    public class RadioStationService : Service
    {
        public const string ActionPlay = "wzxv.app.radio.PLAY";
        public const string ActionStop = "wzxv.app.radio.STOP";
        public const string ActionToggle = "wzxv.app.radio.TOGGLE";

        private const string TAG = "wzxv.app.radio";
        private const int NotificationId = 1;

        private int _startId;
        private RadioStationPlayer _player;
        private RadioStationNotificationManager _notificationManager;
        private RadioStationMediaSession _mediaSession;
        private RadioStationSchedule _schedule;
        private RadioStationServiceLock _lock;
        private RadioStationServiceBinder _binder;

        public event EventHandler<RadioStationServiceMetadataChangedEventArgs> MetadataChanged;
        public event EventHandler StateChanged;
        public event EventHandler<RadioStationErrorEventArgs> Error;

        
        public bool IsPlaying => _player != null && _player.IsPlaying;

        public override void OnCreate()
        {
            base.OnCreate();
            _mediaSession = new RadioStationMediaSession(this);
            _notificationManager = new RadioStationNotificationManager(this);
            _schedule = new RadioStationSchedule(OnScheduleChanged);
            _player = new RadioStationPlayer(this);
            _player.StateChanged += OnPlayerStateChanged;
            _player.Error += OnPlayerError;
        }

        public override IBinder OnBind(Intent intent)
        {
            _binder = new RadioStationServiceBinder(this);
            return _binder;
        }

        public override void OnDestroy()
        {
            base.OnDestroy();

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
                try
                {
                    _lock = new RadioStationServiceLock(this);
                    _player.Start();
                    _schedule.Refresh(force: true);
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

        public void Stop()
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

                StopForeground(false);
                StopSelf(_startId);

                _startId = 0;
            }
        }

        void OnPlayerStateChanged(object sender, EventArgs e)
        {
            if (_player.IsPlaying)
            {
                _mediaSession.SetPlaybackState(PlaybackStateCompat.StatePlaying);
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

        private RadioStationSchedule.Slot _slot;
        void OnScheduleChanged(RadioStationSchedule.Slot slot)
        {
            _slot = slot;

            UpdateNotification();

            if (_mediaSession != null)
            {
                _mediaSession.SetMetadata(slot.Artist, slot.Title, builder =>
                {
                    if (slot.ImageUrl != null)
                        builder.PutString(MediaMetadata.MetadataKeyAlbumArtUri, slot.ImageUrl);
                });
            }

            MetadataChanged?.Invoke(this, new RadioStationServiceMetadataChangedEventArgs(slot.Artist, slot.Title, slot.Url, slot.ImageUrl));
        }

        void UpdateNotification()
        {
            _notificationManager.Notify(NotificationId, builder =>
            {
                if (_slot != null)
                {
                    builder
                        .SetContentTitle(_slot.Title)
                        .SetContentText(_slot.Artist);
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

    public struct RadioStationServiceMetadataChangedEventArgs
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

    public class RadioStationServiceBinder : Binder
    {
        public RadioStationService Service { get; private set; }

        public RadioStationServiceBinder(RadioStationService service)
        {
            Service = service;
        }
    }
}