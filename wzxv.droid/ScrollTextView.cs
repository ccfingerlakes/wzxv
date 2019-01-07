using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;

namespace wzxv
{
    [DesignTimeVisible(true)]
    public class ScrollTextView : TextView
    {
        public ScrollTextView(Context context)
            : base(context)
        { }

        public ScrollTextView(Context context, IAttributeSet attrs) 
            : base(context, attrs)
        { }

        public ScrollTextView(Context context, IAttributeSet attrs, int defStyle)
            : base(context, attrs, defStyle)
        { }

        public ScrollTextView(Context context, IAttributeSet attrs, int defStyle, int defStylesRes)
            : base(context, attrs, defStyle, defStylesRes)
        { }

        public ScrollTextView(IntPtr javaReference, JniHandleOwnership transfer)
            : base(javaReference, transfer)
        { }

        protected override void OnFocusChanged(bool gainFocus, [GeneratedEnum] FocusSearchDirection direction, Rect previouslyFocusedRect)
        {
            if (gainFocus)
                base.OnFocusChanged(gainFocus, direction, previouslyFocusedRect);
        }

        public override void OnWindowFocusChanged(bool hasWindowFocus)
        {
            if (hasWindowFocus)
                base.OnWindowFocusChanged(hasWindowFocus);
        }

        public override bool IsFocused => true;
    }
}