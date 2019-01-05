using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Media;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using Com.Google.Android.Exoplayer2;
using Com.Google.Android.Exoplayer2.Source;
using Com.Google.Android.Exoplayer2.Trackselection;
using Com.Google.Android.Exoplayer2.Upstream;
using Com.Google.Android.Exoplayer2.Util;

namespace wzxv
{
    class RadioStationPlayer : Java.Lang.Object, IPlayerEventListener, AudioManager.IOnAudioFocusChangeListener
    {
        private const string TAG = "wzxv.app.radio.player";
        private const string StreamUrl = "http://ic2.christiannetcast.com/wzxv-fm";

        private readonly Handler _handler;
        private readonly Context _context;
        private readonly AudioManager _audioManager;
        private SimpleExoPlayer _player;

        public RadioStationPlayer(Context context)
        {
            _context = context;
            _audioManager = (AudioManager)context.GetSystemService(Context.AudioService);
            _handler = new Handler();
        }

        public event EventHandler StateChanged;
        public event EventHandler<RadioStationErrorEventArgs> Error;

        public bool IsPlaying { get;  private set; }

        protected override void Dispose(bool disposing)
        {
            Stop();
            base.Dispose(disposing);
        }

        public void Start()
        {
            if (_player != null)
            {
                Stop();
            }

            _handler.Post(() =>
            {
                try
                {
                    var defaultBandwidthMeter = new DefaultBandwidthMeter();
                    var adaptiveTrackSelectionFactory = new AdaptiveTrackSelection.Factory(defaultBandwidthMeter);
                    var defaultTrackSelector = new DefaultTrackSelector(adaptiveTrackSelectionFactory);

                    _player = ExoPlayerFactory.NewSimpleInstance(_context, defaultTrackSelector);
                    _player.AddListener(this);
                    _player.PlayWhenReady = true;

                    if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                    {
                        var audioFocusRequest = new AudioFocusRequestClass.Builder(AudioFocus.Gain)
                                                            .SetOnAudioFocusChangeListener(this)
                                                            .SetAudioAttributes(new AudioAttributes.Builder()
                                                                .SetUsage(AudioUsageKind.Media)
                                                                .SetContentType(AudioContentType.Music)
                                                                .Build())
                                                            .Build();

                        if (_audioManager.RequestAudioFocus(audioFocusRequest) == AudioFocusRequest.Granted)
                        {
                            play();
                        }
                    }
                    else
                    {
                        #pragma warning disable CS0618 // Type or member is obsolete
                        if (_audioManager.RequestAudioFocus(this, Stream.Music, AudioFocus.Gain) == AudioFocusRequest.Granted)
                        {
                            play();
                        }
                        #pragma warning restore CS0618 // Type or member is obsolete
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(TAG, $"Could not start player: {ex.Message}");
                    Log.Debug(TAG, ex.ToString());
                    Error?.Invoke(this, new RadioStationErrorEventArgs(ex));
                    IsPlaying = false;
                }
            });

            void play()
            {
                var mediaUri = Android.Net.Uri.Parse(StreamUrl);
                var userAgent = Util.GetUserAgent(_context, "wzxv.app.radio.player");
                var defaultHttpDataSourceFactory = new DefaultHttpDataSourceFactory(userAgent);
                var defaultDataSourceFactory = new DefaultDataSourceFactory(_context, null, defaultHttpDataSourceFactory);
                var mediaSourceFactory = new ExtractorMediaSource.Factory(defaultDataSourceFactory);
                var mediaSource = mediaSourceFactory.CreateMediaSource(mediaUri);

                _player.Prepare(mediaSource);
                IsPlaying = true;
            }
        }

        public void Stop()
        {
            _handler.Post(() =>
            {
                try
                {
                    if (_player != null)
                    {
                        IsPlaying = false;
                        _player.RemoveListener(this);
                        _player.Release();
                        StateChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn(TAG, $"Error during stop of player: {ex.Message}");
                    Log.Debug(TAG, ex.ToString());
                }
                finally
                {
                    _player = null;
                }
            });
        }

        private int? _previousAudioVolume = null;
        void AudioManager.IOnAudioFocusChangeListener.OnAudioFocusChange(AudioFocus focusChange)
        {
            var maxVolume = _audioManager.GetStreamMaxVolume(Stream.Music);

            switch (focusChange)
            {
                case AudioFocus.Gain:
                    Start();

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
            Log.Error(TAG, $"Player error occurred, see debug log for full details");
            Log.Debug(TAG, e.ToString());
            Error?.Invoke(this, new RadioStationErrorEventArgs(e));
        }

        void IPlayerEventListener.OnPlayerStateChanged(bool playWhenReady, int playbackState)
        {
            switch (playbackState)
            {
                case Player.StateBuffering:
                case Player.StateReady:
                    IsPlaying = _player.PlayWhenReady;
                    break;

                case Player.StateIdle:
                case Player.StateEnded:
                    IsPlaying = false;
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

    public struct RadioStationErrorEventArgs
    {
        public Exception Exception { get; private set; }

        public RadioStationErrorEventArgs(Exception ex)
        {
            this.Exception = ex;
        }
    }
}