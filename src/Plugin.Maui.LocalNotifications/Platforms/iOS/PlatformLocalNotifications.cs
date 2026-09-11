#if IOS
using UserNotifications;

namespace Plugin.Maui.LocalNotifications;

sealed class PlatformLocalNotifications : ILocalNotificationPlatform
{
    public static ILocalNotificationPlatform Create() => new PlatformLocalNotifications();

    public Task EnsureChannelAsync(NotificationChannelRequest channel, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task ShowAsync(NotificationRequest request, CancellationToken cancellationToken)
    {
        var content = new UNMutableNotificationContent
        {
            Title = request.Title,
            Body = request.Body
        };
        UNNotificationTrigger trigger = request.Schedule.Kind switch
        {
            NotificationScheduleKind.At => UNTimeIntervalNotificationTrigger.CreateTrigger(
                Math.Max(1, (request.Schedule.When ?? DateTimeOffset.Now.AddSeconds(1) - DateTimeOffset.Now).TotalSeconds), false),
            NotificationScheduleKind.Interval => UNTimeIntervalNotificationTrigger.CreateTrigger(
                Math.Max(60, request.Schedule.Interval?.TotalSeconds ?? 60), true),
            _ => UNTimeIntervalNotificationTrigger.CreateTrigger(0.1, false)
        };
        var item = UNNotificationRequest.FromIdentifier(request.Id.ToString(), content, trigger);
        UNUserNotificationCenter.Current.AddNotificationRequest(item, null);
        return Task.CompletedTask;
    }

    public Task CancelPlatformAsync(int id, CancellationToken cancellationToken)
    {
        UNUserNotificationCenter.Current.RemovePendingNotificationRequests([id.ToString()]);
        UNUserNotificationCenter.Current.RemoveDeliveredNotifications([id.ToString()]);
        return Task.CompletedTask;
    }
}
#endif
