using CarrotDownload.Maui.Services;
using System.Globalization;

namespace CarrotDownload.Maui.Controls
{
    public partial class NotificationOverlay : ContentView
    {
        public NotificationOverlay()
        {
            InitializeComponent();
        }
    }

    public class TypeToIconConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is NotificationType type)
            {
                return type switch
                {
                    NotificationType.Success => "✓",
                    NotificationType.Error => "!",
                    NotificationType.Warning => "⚠",
                    _ => "ℹ"
                };
            }
            return "";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
