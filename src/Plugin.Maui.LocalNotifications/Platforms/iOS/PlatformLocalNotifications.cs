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
        var delaySeconds = request.Schedule.Kind switch
        {
            NotificationScheduleKind.At => SecondsUntil(request.Schedule.When),
            NotificationScheduleKind.Interval => Math.Max(60, request.Schedule.Interval?.TotalSeconds ?? 60),
            _ => 0.1
        };
        var trigger = UNTimeIntervalNotificationTrigger.CreateTrigger(
            delaySeconds, request.Schedule.Kind == NotificationScheduleKind.Interval);
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

    static double SecondsUntil(DateTimeOffset? when)
    {
        var target = when ?? DateTimeOffset.Now.AddSeconds(1);
        return Math.Max(1, (target - DateTimeOffset.Now).TotalSeconds);
    }
}
#endif
