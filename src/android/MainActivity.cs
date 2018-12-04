using Android.App;
using Android.OS;
using Android.Support.V7.App;
using Android.Runtime;
using Android.Widget;
using Android.Content.PM;
using System;
using Com.Google.Android.Exoplayer2.Source;
using Com.Google.Android.Exoplayer2.Extractor;
using Com.Google.Android.Exoplayer2.Upstream;
using Com.Google.Android.Exoplayer2;
using Com.Google.Android.Exoplayer2.Trackselection;
using Com.Google.Android.Exoplayer2.Util;
using Android.Media.Session;

namespace wzxv
{
    [Activity(Name = "wzxv.app.main", Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true, ScreenOrientation = ScreenOrientation.Portrait)]
    public class MainActivity : AppCompatActivity
    {
        private Button _button => FindViewById<Button>(Resource.Id.button);
        private IExoPlayer _player = null;
        private ExtractorMediaSource _extractorMediaSource = null;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.Main);

            var mediaUrl = "http://ic2.christiannetcast.com/wzxv-fm";
            var mediaUri = Android.Net.Uri.Parse(mediaUrl);
            var userAgent = Util.GetUserAgent(this, "wzxv.app");
            var defaultHttpDataSourceFactory = new DefaultHttpDataSourceFactory(userAgent);
            var defaultDataSourceFactory = new DefaultDataSourceFactory(this, null, defaultHttpDataSourceFactory);

            _extractorMediaSource = new ExtractorMediaSource(mediaUri, defaultDataSourceFactory, new DefaultExtractorsFactory(), null, null);

            var defaultBandwidthMeter = new DefaultBandwidthMeter();
            var adaptiveTrackSelectionFactory = new AdaptiveTrackSelection.Factory(defaultBandwidthMeter);
            var defaultTrackSelector = new DefaultTrackSelector(adaptiveTrackSelectionFactory);

            _player = ExoPlayerFactory.NewSimpleInstance(this, defaultTrackSelector);
            _player.PlayWhenReady = true;

            _button.Click += OnButtonClick;
        }

        protected override void OnDestroy()
        {
            _player.Release();
            base.OnDestroy();
        }

        private void OnButtonClick(object sender, EventArgs e)
        {
            _button.Enabled = false;

            var isPlaying = _player.PlayWhenReady == true && _player.PlaybackState == Player.StateReady;

            if (isPlaying)
            {
                _player.Stop();
                _button.Text = "Start";
            }
            else
            {
                _player.Prepare(_extractorMediaSource);
                _button.Text = "Stop";
            }

            _button.Enabled = true;
        }
    }
}