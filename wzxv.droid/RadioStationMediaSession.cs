using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Media;
using Android.OS;
using Android.Runtime;
using Android.Support.V4.Media;
using Android.Support.V4.Media.Session;
using Android.Views;
using Android.Widget;

namespace wzxv
{
    class RadioStationMediaSession : IDisposable
    {
        private readonly AudioManager _audioManager;
        private readonly MediaSessionCompat _session;
        private readonly MediaControllerCompat _controller;

        public RadioStationMediaSession(Context context)
        {
            _audioManager = (AudioManager)context.GetSystemService(Context.AudioService);

            var intent = new Intent(context, typeof(MainActivity));
            var pendingIntent = PendingIntent.GetActivity(context, 0, intent, 0);
            var componentName = new ComponentName(context.PackageName, new RadioStationBroadcastReceiver().ComponentName);

            _session = new MediaSessionCompat(context, "wzxv.app", componentName, pendingIntent);
            _controller = new MediaControllerCompat(context, _session.SessionToken);
            
            _session.Active = true;
            _session.SetCallback(new MediaSessionCallback(context));
            _session.SetFlags(MediaSessionCompat.FlagHandlesMediaButtons | MediaSessionCompat.FlagHandlesTransportControls);
        }

        public void Dispose()
        {
            _session.Release();
        }

        public void SetMetadata(string artist, string title, Action<MediaMetadataCompat.Builder> build = null)
        {
            var builder = new MediaMetadataCompat.Builder()
                                .PutString(MediaMetadata.MetadataKeyArtist, artist)
                                .PutString(MediaMetadata.MetadataKeyTitle, title);

            build?.Invoke(builder);

            _session.SetMetadata(builder.Build());
        }

        public void SetPlaybackState(int state, Action<PlaybackStateCompat.Builder> configure = null)
        {
            var builder = new PlaybackStateCompat.Builder()
                            .SetActions(PlaybackStateCompat.ActionPlay | PlaybackStateCompat.ActionStop)
                            .SetState(state, -1, 1.0f, SystemClock.ElapsedRealtime());

            configure?.Invoke(builder);

            _session.SetPlaybackState(builder.Build());
        }

        class MediaSessionCallback : MediaSessionCompat.Callback
        {
            private readonly Context _context;

            public MediaSessionCallback(Context context)
            {
                _context = context;
            }

            public override void OnPause()
            {
                var intent = new Intent(RadioStationService.ActionStop);
                _context.StartService(intent);
                base.OnPause();
            }

            public override void OnPlay()
            {
                var intent = new Intent(RadioStationService.ActionPlay);
                _context.StartService(intent);
                base.OnPlay();
            }

            public override void OnStop()
            {
                var intent = new Intent(RadioStationService.ActionStop);
                _context.StartService(intent);
                base.OnStop();
            }
        }
    }
}