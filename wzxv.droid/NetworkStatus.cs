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
using static Android.Net.ConnectivityManager;

namespace wzxv
{
    internal class NetworkStatus : NetworkCallback, IDisposable
    {
        private readonly ConnectivityManager _manager;

        public event EventHandler<EventArgs> Connected;

        public event EventHandler<EventArgs> Disconnected;

        public bool IsConnected => _manager.GetNetworkCapabilities(_manager.ActiveNetwork).HasTransport(TransportType.Wifi);

        public NetworkStatus(Context context, Action connected = null, Action disconnected = null)
        {
            _manager = (ConnectivityManager)context.GetSystemService(Context.ConnectivityService);

            if (connected != null)
                Connected += (_, __) => connected();
            if (disconnected != null)
                Disconnected += (_, __) => disconnected();

            var request = new NetworkRequest.Builder()
                .AddTransportType(TransportType.Cellular)
                .AddTransportType(TransportType.Wifi)
                .Build();

            _manager.RegisterNetworkCallback(request, this);
        }

        void IDisposable.Dispose()
        {
            if (_manager != null)
            {
                _manager.UnregisterNetworkCallback(this);
            }
        }

        public override void OnAvailable(Network network)
        {
            Connected?.Invoke(this, EventArgs.Empty);
        }

        public override void OnUnavailable()
        {
            Disconnected?.Invoke(this, EventArgs.Empty);
        }
    }
}