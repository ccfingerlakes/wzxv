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
    class RadioStationPlayer : IDisposable
    {
        private const string TAG = "wzxv.app.radio.player";
        private const string StreamUrl = "http://ic2.christiannetcast.com/wzxv-fm";

        private readonly EventListener _listener;
        private readonly Context _context;
        private readonly AudioManager _audioManager;
        private readonly AudioManager.IOnAudioFocusChangeListener _onAudioFocusChangeListener;
        private AudioFocusRequestClass _audioFocusRequest;
        private Handler _handler;
        private SimpleExoPlayer _player;

        public RadioStationPlayer(Context context, AudioManager.IOnAudioFocusChangeListener onAudioFocusChangeListener)
        {
            _onAudioFocusChangeListener = onAudioFocusChangeListener;
            _listener = new EventListener(this);
            _context = context;
            _audioManager = (AudioManager)context.GetSystemService(Context.AudioService);
            _handler = new Handler();
        }

        public event EventHandler StateChanged;
        public event EventHandler<RadioStationErrorEventArgs> Error;

        public bool IsPlaying { get;  private set; }
        public float Volume
        {
            get => _player?.Volume ?? 0;
            set
            {
                if (_player != null)
                    _player.Volume = value;
            }
        }

        public void Dispose()
        {
            Stop();

            if (_handler != null)
            {
                _handler.Dispose();
                _handler = null;
            }
        }

        public void Start()
        {
            if (_handler != null)
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
                        _player.AddListener(_listener);
                        _player.PlayWhenReady = true;

                        if (TryGetAudioFocus())
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
                    catch (Exception ex)
                    {
                        Log.Error(TAG, $"Could not start player: {ex.Message}");
                        Log.Debug(TAG, ex.ToString());
                        Error?.Invoke(this, new RadioStationErrorEventArgs(ex));
                        IsPlaying = false;
                    }
                });
            }

            bool TryGetAudioFocus()
            {
                if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                {
                    _audioFocusRequest = new AudioFocusRequestClass.Builder(AudioFocus.Gain)
                                                        .SetOnAudioFocusChangeListener(_onAudioFocusChangeListener)
                                                        .SetAudioAttributes(new AudioAttributes.Builder()
                                                            .SetUsage(AudioUsageKind.Media)
                                                            .SetContentType(AudioContentType.Music)
                                                            .Build())
                                                        .Build();

                    return _audioManager.RequestAudioFocus(_audioFocusRequest) == AudioFocusRequest.Granted;
                }

                #pragma warning disable CS0618 // Type or member is obsolete
                return _audioManager.RequestAudioFocus(_onAudioFocusChangeListener, Stream.Music, AudioFocus.Gain) == AudioFocusRequest.Granted;
                #pragma warning restore CS0618 // Type or member is obsolete
            }
        }

        public void Stop()
        {
            if (_handler != null)
            {
                _handler.Post(() =>
                {
                    try
                    {
                        if (_player != null)
                        {
                            IsPlaying = false;
                            _player.RemoveListener(_listener);
                            _player.Release();
                            StateChanged?.Invoke(this, EventArgs.Empty);
                        }

                        if (_audioFocusRequest != null)
                        {
                            _audioManager.AbandonAudioFocusRequest(_audioFocusRequest);
                            _audioFocusRequest = null;
                        }
                        else
                        {
                            #pragma warning disable CS0618 // Type or member is obsolete
                            _audioManager.AbandonAudioFocus(_onAudioFocusChangeListener);
                            #pragma warning restore CS0618 // Type or member is obsolete
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
        }

        void OnPlayerError(ExoPlaybackException e)
        {
            Log.Error(TAG, $"Player error occurred, see debug log for full details");
            Log.Debug(TAG, e.ToString());
            Error?.Invoke(this, new RadioStationErrorEventArgs(e));
        }

        void OnPlayerStateChanged(bool playWhenReady, int playbackState)
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

        public class EventListener : Java.Lang.Object, IPlayerEventListener
        {
            private readonly RadioStationPlayer _player;

            public EventListener(RadioStationPlayer player)
            {
                _player = player;
            }

            void IPlayerEventListener.OnPlayerError(ExoPlaybackException e)
                => _player.OnPlayerError(e);

            void IPlayerEventListener.OnPlayerStateChanged(bool playWhenReady, int playbackState)
                => _player.OnPlayerStateChanged(playWhenReady, playbackState);

            void IPlayerEventListener.OnLoadingChanged(bool isLoading) { }
            void IPlayerEventListener.OnPlaybackParametersChanged(PlaybackParameters playbackParameters) { }
            void IPlayerEventListener.OnPositionDiscontinuity(int reason) { }
            void IPlayerEventListener.OnRepeatModeChanged(int reason) { }
            void IPlayerEventListener.OnSeekProcessed() { }
            void IPlayerEventListener.OnShuffleModeEnabledChanged(bool reason) { }
            void IPlayerEventListener.OnTimelineChanged(Timeline timeline, Java.Lang.Object manifest, int reason) { }
            void IPlayerEventListener.OnTracksChanged(TrackGroupArray ignored, TrackSelectionArray trackSelections) { }
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