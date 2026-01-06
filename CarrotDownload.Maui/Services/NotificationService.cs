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
        public Color BackgroundColor => Type switch
        {
            NotificationType.Success => Color.FromArgb("#28a745"), // Green
            NotificationType.Error => Color.FromArgb("#dc3545"), // Red
            NotificationType.Warning => Color.FromArgb("#ffc107"), // Yellow/Amber
            _ => Color.FromArgb("#2196F3") // Blue
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
    }
}
