using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace wzxv
{
    [BroadcastReceiver]
    [Android.App.IntentFilter(new[] { Intent.ActionMediaButton })]
    public class RadioStationBroadcastReceiver : BroadcastReceiver
    {
        public string ComponentName { get { return this.Class.Name; } }

        public override void OnReceive(Context context, Intent intent)
        {
            if (intent.Action == Intent.ActionMediaButton)
            {
                var key = (KeyEvent)intent.GetParcelableExtra(Intent.ExtraKeyEvent);

                if (key.Action == KeyEventActions.Down)
                {
                    string action = null;

                    switch (key.KeyCode)
                    {
                        case Keycode.Headsethook:
                        case Keycode.MediaPlayPause:
                            action = RadioStationService.ActionToggle;
                            break;
                        case Keycode.MediaPlay:
                            action = RadioStationService.ActionPlay;
                            break;
                        case Keycode.MediaPause:
                        case Keycode.MediaStop:
                            action = RadioStationService.ActionStop;
                            break;
                    }

                    if (action != null)
                    {
                        var remoteIntent = new Intent(context, typeof(RadioStationService)).SetAction(action);

                        if (action == RadioStationService.ActionStop)
                        {
                            context.StopService(remoteIntent);
                        }
                        else
                        {
                            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                            {
                                context.StartForegroundService(remoteIntent);
                            }
                            else
                            {
                                context.StartService(remoteIntent);
                            }
                        }
                    }
                }
            }
        }
    }
}