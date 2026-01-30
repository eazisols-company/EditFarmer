using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;

namespace CarrotDownload.Maui.Platforms.Windows
{
    public static class ButtonCursorHandler
    {
        public static void Initialize()
        {
            // Set hand cursor for Button
            ButtonHandler.Mapper.AppendToMapping("HandCursor", (handler, view) =>
            {
                AddCursorBehavior(handler, view);
            });

            // Set hand cursor for ImageButton
            ImageButtonHandler.Mapper.AppendToMapping("HandCursor", (handler, view) =>
            {
                AddCursorBehavior(handler, view);
            });

            // Set hand cursor for Label - only if it has gesture recognizers (is clickable)
            LabelHandler.Mapper.AppendToMapping("HandCursor", (handler, view) =>
            {
                if (view is View controlView && controlView.GestureRecognizers.Count > 0)
                {
                    AddCursorBehavior(handler, view);
                }
            });
        }

        private static void AddCursorBehavior(IElementHandler handler, IElement view)
        {
            if (view is not View controlView) return;

            // Add PointerGestureRecognizer to detect hover
            var pointerGesture = new PointerGestureRecognizer();

            pointerGesture.PointerEntered += (s, e) =>
            {
                if (handler.PlatformView is Microsoft.UI.Xaml.UIElement element)
                {
                    CursorHelper.SetCursor(element, Behaviors.CursorType.Hand);
                }
            };

            pointerGesture.PointerExited += (s, e) =>
            {
                if (handler.PlatformView is Microsoft.UI.Xaml.UIElement element)
                {
                    CursorHelper.SetCursor(element, Behaviors.CursorType.Default);
                }
            };

            controlView.GestureRecognizers.Add(pointerGesture);
        }
    }
}
