using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;

namespace wzxv
{
#if DEBUG
    [Application(Debuggable = true)]
#else
    [Application(Debuggable=false)]
#endif
    public class WzxvApplication : Application, Application.IActivityLifecycleCallbacks
    {
        public const string TAG = "wzxv.app";

        public WzxvApplication(IntPtr javaReference, Android.Runtime.JniHandleOwnership transfer)
            : base(javaReference, transfer)
        { }

        public override void OnCreate()
        {
            base.OnCreate();
            RegisterActivityLifecycleCallbacks(this);
        }

        public override void OnTerminate()
        {
            this.UnregisterActivityLifecycleCallbacks(this);
            base.OnTerminate();
        }

        void IActivityLifecycleCallbacks.OnActivityDestroyed(Activity activity)
        {
            try
            {
                var intent = new Intent(ApplicationContext, typeof(RadioStationService)).SetAction(RadioStationService.ActionStop).PutExtra(RadioStationService.ExtraKeyForce, true);
                StopService(intent);
            }
            catch (Exception ex)
            {
                Log.Error(TAG, $"Failed to stop service: {ex.Message}");
                Log.Debug(TAG, ex.ToString());
            }
        }

        void IActivityLifecycleCallbacks.OnActivityStopped(Activity activity) { }
        void IActivityLifecycleCallbacks.OnActivityCreated(Activity activity, Bundle savedInstanceState) { }
        void IActivityLifecycleCallbacks.OnActivityPaused(Activity activity) { }
        void IActivityLifecycleCallbacks.OnActivityResumed(Activity activity) { }
        void IActivityLifecycleCallbacks.OnActivitySaveInstanceState(Activity activity, Bundle outState) { }
        void IActivityLifecycleCallbacks.OnActivityStarted(Activity activity) { }
    }
}