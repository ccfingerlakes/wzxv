using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Database;
using Android.Media;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace wzxv
{
    class AudioVolumeObserver : ContentObserver
    {
        private readonly Context _context;
        private readonly AudioManager _audioManager;

        public AudioVolumeObserver(Context context, Handler handler, Action<int> callback = null)
            : base(handler)
        {
            _context = context;
            _audioManager = (AudioManager)context.GetSystemService(Context.AudioService);

            if (callback != null)
                Changed += (_, e) => callback(e.Volume);

            context.ContentResolver.RegisterContentObserver(Android.Provider.Settings.System.ContentUri, true, this);
        }

        public event EventHandler<AudioVolumeChangedEventArgs> Changed;

        protected override void Dispose(bool disposing)
        {
            _context.ContentResolver.UnregisterContentObserver(this);
            base.Dispose(disposing);
        }

        public override void OnChange(bool selfChange)
        {
            var volume = _audioManager.GetStreamVolume(Stream.Music);
            Changed?.Invoke(this, new AudioVolumeChangedEventArgs(volume));
        }
    }

    public class AudioVolumeChangedEventArgs
    {
        public int Volume { get; private set; }

        public AudioVolumeChangedEventArgs(int volume)
        {
            Volume = volume;
        }
    }
}