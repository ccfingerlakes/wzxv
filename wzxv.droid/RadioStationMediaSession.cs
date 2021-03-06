﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Media;
using Android.Media.Session;
using Android.OS;
using Android.Runtime;
using Android.Support.V4.Media;
using Android.Support.V4.Media.Session;
using Android.Views;
using Android.Widget;

namespace wzxv
{
    internal class RadioStationMediaSession : IDisposable
    {
        private readonly MediaSessionCompat _session;

        public MediaSessionCompat.Token SessionToken => _session.SessionToken;

        public RadioStationMediaSession(Context context)
        {
            var intent = new Intent(context, typeof(MainActivity));
            var pendingIntent = PendingIntent.GetActivity(context, 0, intent, 0);
            var componentName = new ComponentName(context.PackageName, new RadioStationBroadcastReceiver().ComponentName);

            _session = new MediaSessionCompat(context, "wzxv.app", componentName, pendingIntent)
            {
                Active = true
            };
            _session.SetCallback(new MediaSessionCallback(context));
            _session.SetFlags((int)(MediaSessionFlags.HandlesMediaButtons | MediaSessionFlags.HandlesTransportControls));
        }

        public void Dispose()
        {
            _session.Release();
        }

        public RadioStationMediaSession SetMetadata(string artist, string title, TimeSpan duration, Action<MediaMetadataCompat.Builder> build = null)
        {
            var builder = new MediaMetadataCompat.Builder()
                                .PutString(MediaMetadata.MetadataKeyArtist, artist)
                                .PutString(MediaMetadata.MetadataKeyTitle, title)
                                .PutLong(MediaMetadata.MetadataKeyDuration, (long)Math.Ceiling(duration.TotalMilliseconds));

            build?.Invoke(builder);

            _session.SetMetadata(builder.Build());

            return this;
        }

        public RadioStationMediaSession SetPlaybackState(int state, TimeSpan position = default(TimeSpan), Action<PlaybackStateCompat.Builder> configure = null)
        {
            var positionMS = position == default(TimeSpan) ? -1 : (long)Math.Ceiling(position.TotalMilliseconds);
            var builder = new PlaybackStateCompat.Builder()
                            .SetActions(PlaybackStateCompat.ActionPlay | PlaybackStateCompat.ActionPause)
                            .SetState(state, positionMS, 1.0f, SystemClock.ElapsedRealtime());

            configure?.Invoke(builder);

            _session.SetPlaybackState(builder.Build());

            return this;
        }

        private class MediaSessionCallback : MediaSessionCompat.Callback
        {
            private readonly Context _context;

            public MediaSessionCallback(Context context)
            {
                _context = context;
            }

            public override void OnPause()
            {
                var intent = new Intent(_context, typeof(RadioStationService)).SetAction(RadioStationService.ActionStop);
                _context.StopService(intent);
                base.OnPause();
            }

            public override void OnPlay()
            {
                var intent = new Intent(_context, typeof(RadioStationService)).SetAction(RadioStationService.ActionPlay);

                if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                {
                    _context.StartForegroundService(intent);
                }
                else
                {
                    _context.StartService(intent);
                }

                base.OnPlay();
            }

            public override void OnStop()
            {
                var intent = new Intent(_context, typeof(RadioStationService)).SetAction(RadioStationService.ActionStop);
                _context.StopService(intent);
                base.OnStop();
            }
        }
    }
}