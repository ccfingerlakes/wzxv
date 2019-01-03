using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Support.V4.App;
using Android.Views;
using Android.Widget;

namespace wzxv
{
    class RadioStationNotificationManager
    {
        private const string ChannelId = "wzxv.app.PLAYBACK";
        private readonly Context _context;
        private readonly NotificationManager _manager;

        public RadioStationNotificationManager(Context context)
            : this(context, (NotificationManager)context.GetSystemService(Context.NotificationService))
        { }

        public RadioStationNotificationManager(Context context, NotificationManager manager)
        {
            _context = context;
            _manager = manager;

            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                var channel = new NotificationChannel(ChannelId, "Playback", NotificationImportance.Low)
                {
                    LockscreenVisibility = NotificationVisibility.Public
                };

                channel.SetShowBadge(false);

                _manager.CreateNotificationChannel(channel);
            }
        }

        public NotificationCompat.Builder CreateNotificationBuilder()
        {
            var pendingIntent = PendingIntent.GetActivity(_context, 0, new Intent(_context, typeof(MainActivity)), PendingIntentFlags.UpdateCurrent);

            var builder = new NotificationCompat.Builder(_context)
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

        public NotificationCompat.Action CreateAction(int icon, string title, string action, Action<NotificationCompat.Action.Builder> build = null)
        {
            var intent = new Intent(_context, typeof(RadioStationService)).SetAction(action);

            var flags = PendingIntentFlags.UpdateCurrent;

            if (action.Equals(RadioStationService.ActionStop))
                flags = PendingIntentFlags.CancelCurrent;

            var pendingIntent = PendingIntent.GetService(_context, 1, intent, flags);
            var builder = new NotificationCompat.Action.Builder(icon, title, pendingIntent);

            build?.Invoke(builder);

            return builder.Build();
        }

        public void Notify(int notificationId, Action<NotificationCompat.Builder> builder = null)
        {
            var notification = CreateNotificationBuilder();
            if (builder != null)
            {
                builder(notification);
            }
            _manager.Notify(notificationId, notification.Build());
        }

        public void Stop()
        {
            _manager.CancelAll();
        }
    }
}