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
    class ServiceConnection<T> : Java.Lang.Object, IServiceConnection
        where T : IBinder
    {
        private Action<T> _callback;

        public ServiceConnection(Action<T> callback)
        {
            _callback = callback;
        }

        public void OnServiceConnected(ComponentName name, IBinder service)
        {
            if (service is T obj)
                _callback(obj);
        }

        public void OnServiceDisconnected(ComponentName name)
        {
            _callback(default);
        }
    }
}