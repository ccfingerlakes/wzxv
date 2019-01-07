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
    class ServiceBinder<T> : Binder
        where T : Service
    {
        public T Service { get; private set; }

        public ServiceBinder(T service)
        {
            Service = service;
        }
    }
}