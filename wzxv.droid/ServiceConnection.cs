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
    static class ServiceConnectionFactory
    {
        public static ServiceConnection<ServiceBinder<T>, T> Create<T>(Action<T> callback)
            where T : Service
            => new ServiceConnection<ServiceBinder<T>, T>(callback);
    }

    class ServiceConnection<TBinder, T> : Java.Lang.Object, IServiceConnection
        where T : Service
        where TBinder : ServiceBinder<T>
    {
        private Action<T> _callback;

        public ServiceConnection(Action<T> callback)
        {
            _callback = callback;
        }

        public void OnServiceConnected(ComponentName name, IBinder service)
        {
            if (service is TBinder obj)
                _callback(obj.Service);
        }

        public void OnServiceDisconnected(ComponentName name)
        {
            _callback(null);
        }
    }
}