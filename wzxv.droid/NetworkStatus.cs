using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Net;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace wzxv
{
    class NetworkStatus
    {
        private readonly ConnectivityManager _manager;
        
        public event EventHandler<EventArgs> Connected;
        public event EventHandler<EventArgs> Disconnected;

        public NetworkStatus(Context context, Action connected = null, Action disconnected = null)
        {
            _manager = (ConnectivityManager)context.GetSystemService(Context.ConnectivityService);

            if (connected != null)
                Connected += (_, __) => connected();
            if (disconnected != null)
                Disconnected += (_, __) => disconnected();
            
            var receiver = new NetworkStatusBroadcastReceiver(OnNetworkStatusChanged);
            context.RegisterReceiver(receiver, new IntentFilter(ConnectivityManager.ConnectivityAction));
        }

        public bool IsConnected => _manager.ActiveNetworkInfo != null && _manager.ActiveNetworkInfo.IsConnected;

        void OnNetworkStatusChanged()
        {
            if (IsConnected)
            {
                Connected?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                Disconnected?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    [BroadcastReceiver()]
    [IntentFilter(new string[] { ConnectivityManager.ConnectivityAction })]
    public class NetworkStatusBroadcastReceiver : BroadcastReceiver
    {
        private readonly Action _callback;

        public NetworkStatusBroadcastReceiver()
        { }

        public NetworkStatusBroadcastReceiver(Action callback)
        {
            _callback = callback;
        }

        public override void OnReceive(Context context, Intent intent)
        {
            _callback?.Invoke();
        }
    }
}