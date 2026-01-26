using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace CarrotDownload.Maui.Services
{
    public enum NotificationType
    {
        Success,
        Error,
        Info,
        Warning
    }

    public class NotificationMessage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Message { get; set; } = string.Empty;
        public NotificationType Type { get; set; }
        
        public Command CloseCommand => new Command(() => NotificationService.RemoveNotification(this));
        
        // Softer, more friendly background colors
        public Color BackgroundColor => Type switch
        {
            NotificationType.Success => Color.FromArgb("#F0F9F4"), // Light green
            NotificationType.Error => Color.FromArgb("#FEF2F2"), // Light red
            NotificationType.Warning => Color.FromArgb("#FFFBEB"), // Light yellow
            _ => Color.FromArgb("#EFF6FF") // Light blue
        };
        
        // Border colors for subtle definition
        public Color BorderColor => Type switch
        {
            NotificationType.Success => Color.FromArgb("#D1FAE5"),
            NotificationType.Error => Color.FromArgb("#FEE2E2"),
            NotificationType.Warning => Color.FromArgb("#FEF3C7"),
            _ => Color.FromArgb("#DBEAFE")
        };
        
        // Icon background colors
        public Color IconBackgroundColor => Type switch
        {
            NotificationType.Success => Color.FromArgb("#10B981"), // Green
            NotificationType.Error => Color.FromArgb("#EF4444"), // Red
            NotificationType.Warning => Color.FromArgb("#F59E0B"), // Amber
            _ => Color.FromArgb("#3B82F6") // Blue
        };
        
        // Icon colors
        public Color IconColor => Colors.White;
        
        // Text colors for better readability
        public Color TextColor => Type switch
        {
            NotificationType.Success => Color.FromArgb("#065F46"),
            NotificationType.Error => Color.FromArgb("#991B1B"),
            NotificationType.Warning => Color.FromArgb("#92400E"),
            _ => Color.FromArgb("#1E40AF")
        };
        
        // Close button color
        public Color CloseButtonColor => Type switch
        {
            NotificationType.Success => Color.FromArgb("#059669"),
            NotificationType.Error => Color.FromArgb("#DC2626"),
            NotificationType.Warning => Color.FromArgb("#D97706"),
            _ => Color.FromArgb("#2563EB")
        };
    }

    public static class NotificationService
    {
        public static ObservableCollection<NotificationMessage> Notifications { get; } = new ObservableCollection<NotificationMessage>();

        public static async Task ShowSuccess(string message)
        {
            await ShowNotification(message, NotificationType.Success);
        }

        public static async Task ShowError(string message)
        {
            await ShowNotification(message, NotificationType.Error);
        }

        public static async Task ShowInfo(string message)
        {
            await ShowNotification(message, NotificationType.Info);
        }

        public static async Task ShowWarning(string message)
        {
            await ShowNotification(message, NotificationType.Warning);
        }

        private static async Task ShowNotification(string message, NotificationType type)
        {
            var notification = new NotificationMessage
            {
                Message = message,
                Type = type
            };

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Notifications.Insert(0, notification);
            });

            // Auto-remove after 10 seconds
            _ = Task.Run(async () =>
            {
                await Task.Delay(10000);
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    Notifications.Remove(notification);
                });
            });
        }

        public static void RemoveNotification(NotificationMessage notification)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (Notifications.Contains(notification))
                {
                    Notifications.Remove(notification);
                }
            });
        }
    }
}
