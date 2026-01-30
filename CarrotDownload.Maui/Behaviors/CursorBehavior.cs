using Microsoft.Maui.Controls;

namespace CarrotDownload.Maui.Behaviors
{
    public static class CursorBehavior
    {
        public static readonly BindableProperty CursorProperty =
            BindableProperty.CreateAttached(
                "Cursor",
                typeof(CursorType),
                typeof(CursorBehavior),
                CursorType.Default,
                propertyChanged: OnCursorChanged);

        public static CursorType GetCursor(BindableObject view) =>
            (CursorType)view.GetValue(CursorProperty);

        public static void SetCursor(BindableObject view, CursorType value) =>
            view.SetValue(CursorProperty, value);

        private static void OnCursorChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is View view && newValue is CursorType cursorType)
            {
                // Add PointerGestureRecognizer to handle hover events
                var pointerGesture = new PointerGestureRecognizer();
                
                pointerGesture.PointerEntered += (s, e) =>
                {
#if WINDOWS
                    SetPlatformCursor(view, cursorType);
#endif
                };

                pointerGesture.PointerExited += (s, e) =>
                {
#if WINDOWS
                    SetPlatformCursor(view, CursorType.Default);
#endif
                };

                view.GestureRecognizers.Add(pointerGesture);
            }
        }

#if WINDOWS
        private static void SetPlatformCursor(View view, CursorType cursorType)
        {
            var handler = view.Handler;
            if (handler?.PlatformView is Microsoft.UI.Xaml.UIElement element)
            {
                Platforms.Windows.CursorHelper.SetCursor(element, cursorType);
            }
        }
#endif
    }

    public enum CursorType
    {
        Default,
        Hand,
        IBeam,
        Wait,
        Cross
    }
}
