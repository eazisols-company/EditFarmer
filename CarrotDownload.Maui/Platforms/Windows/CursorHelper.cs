using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using System.Reflection;

namespace CarrotDownload.Maui.Platforms.Windows
{
    public static class CursorHelper
    {
        public static void SetCursor(UIElement element, Behaviors.CursorType cursorType)
        {
            try
            {
                InputSystemCursorShape shape = cursorType switch
                {
                    Behaviors.CursorType.Hand => InputSystemCursorShape.Hand,
                    Behaviors.CursorType.IBeam => InputSystemCursorShape.IBeam,
                    Behaviors.CursorType.Wait => InputSystemCursorShape.Wait,
                    Behaviors.CursorType.Cross => InputSystemCursorShape.Cross,
                    _ => InputSystemCursorShape.Arrow
                };

                var cursor = InputSystemCursor.Create(shape);
                
                // Use reflection to access the protected ProtectedCursor property
                var protectedCursorProperty = typeof(UIElement).GetProperty(
                    "ProtectedCursor",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.SetProperty);

                if (protectedCursorProperty != null && protectedCursorProperty.CanWrite)
                {
                    protectedCursorProperty.SetValue(element, cursor);
                }
            }
            catch
            {
                // Silently fail if reflection doesn't work
            }
        }
    }
}
