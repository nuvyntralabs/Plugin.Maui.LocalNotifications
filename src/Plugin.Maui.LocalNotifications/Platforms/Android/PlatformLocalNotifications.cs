#if ANDROID
using Android.App;
using Android.Content;
using AndroidX.Core.App;
using Application = Android.App.Application;

namespace Plugin.Maui.LocalNotifications;

sealed class PlatformLocalNotifications : ILocalNotificationPlatform
{
    public const string ExtraId = "plugin.maui.localnotifications.id";
    public const string ExtraAction = "plugin.maui.localnotifications.action";
    public const string ExtraTitle = "plugin.maui.localnotifications.title";
    public const string ExtraBody = "plugin.maui.localnotifications.body";
    public const string ExtraChannel = "plugin.maui.localnotifications.channel";

    public static ILocalNotificationPlatform Create() => new PlatformLocalNotifications();

    public Task EnsureChannelAsync(NotificationChannelRequest channel, CancellationToken cancellationToken)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            var manager = (NotificationManager)Application.Context.GetSystemService(Context.NotificationService)!;
            var importance = channel.Importance switch
            {
                Plugin.Maui.LocalNotifications.NotificationImportance.High => Android.App.NotificationImportance.High,
                Plugin.Maui.LocalNotifications.NotificationImportance.Low => Android.App.NotificationImportance.Low,
                Plugin.Maui.LocalNotifications.NotificationImportance.Min => Android.App.NotificationImportance.Min,
                _ => Android.App.NotificationImportance.Default
            };
            manager.CreateNotificationChannel(new NotificationChannel(channel.Id, channel.Name, importance));
        }
        return Task.CompletedTask;
    }

    public Task ShowAsync(NotificationRequest request, CancellationToken cancellationToken)
    {
        var context = Application.Context;
        if (request.Schedule.Kind == NotificationScheduleKind.Immediate)
        {
            Post(context, request);
            return Task.CompletedTask;
        }

        var alarm = (AlarmManager)context.GetSystemService(Context.AlarmService)!;
        var pending = BuildPending(context, request);
        var trigger = request.Schedule.Kind == NotificationScheduleKind.At
            ? request.Schedule.When ?? DateTimeOffset.Now.AddSeconds(5)
            : DateTimeOffset.Now.Add(request.Schedule.Interval ?? TimeSpan.FromMinutes(1));
        var millis = trigger.ToUnixTimeMilliseconds();
        if (request.Schedule.Kind == NotificationScheduleKind.Interval)
        {
            alarm.SetRepeating(AlarmType.RtcWakeup, millis, (long)request.Schedule.Interval!.Value.TotalMilliseconds, pending);
        }
        else if (request.Schedule.Exact && OperatingSystem.IsAndroidVersionAtLeast(31))
        {
            alarm.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, millis, pending);
        }
        else
        {
            alarm.SetAndAllowWhileIdle(AlarmType.RtcWakeup, millis, pending);
        }
        return Task.CompletedTask;
    }

    public Task CancelPlatformAsync(int id, CancellationToken cancellationToken)
    {
        var context = Application.Context;
        NotificationManagerCompat.From(context).Cancel(id);
        var alarm = (AlarmManager)context.GetSystemService(Context.AlarmService)!;
        alarm.Cancel(BuildPending(context, new NotificationRequest { Id = id, Title = "", Body = "" }));
        return Task.CompletedTask;
    }

    internal static void Post(Context context, NotificationRequest request)
    {
        var tap = new Intent(context, typeof(LocalNotificationTapReceiver));
        tap.PutExtra(ExtraId, request.Id);
        tap.PutExtra(ExtraTitle, request.Title);
        tap.PutExtra(ExtraBody, request.Body);
        tap.PutExtra(ExtraChannel, request.ChannelId);
        var tapPending = PendingIntent.GetBroadcast(context, request.Id, tap, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;
        var builder = new NotificationCompat.Builder(context, string.IsNullOrWhiteSpace(request.ChannelId) ? "general" : request.ChannelId)
            .SetContentTitle(request.Title)
            .SetContentText(request.Body)
            .SetSmallIcon(Android.Resource.Drawable.IcDialogInfo)
            .SetAutoCancel(true)
            .SetContentIntent(tapPending);
        foreach (var action in request.Actions)
        {
            var intent = new Intent(context, typeof(LocalNotificationTapReceiver));
            intent.PutExtra(ExtraId, request.Id);
            intent.PutExtra(ExtraAction, action.Id);
            var pending = PendingIntent.GetBroadcast(context, request.Id * 31 + action.Id.GetHashCode(), intent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
            builder.AddAction(0, action.Title, pending);
        }
        NotificationManagerCompat.From(context).Notify(request.Id, builder.Build());
    }

    static PendingIntent BuildPending(Context context, NotificationRequest request)
    {
        var intent = new Intent(context, typeof(LocalNotificationAlarmReceiver));
        intent.PutExtra(ExtraId, request.Id);
        intent.PutExtra(ExtraTitle, request.Title);
        intent.PutExtra(ExtraBody, request.Body);
        intent.PutExtra(ExtraChannel, request.ChannelId);
        return PendingIntent.GetBroadcast(context, request.Id, intent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;
    }
}

[BroadcastReceiver(Enabled = true, Exported = false)]
public sealed class LocalNotificationAlarmReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null || intent is null)
            return;
        var request = new NotificationRequest
        {
            Id = intent.GetIntExtra(PlatformLocalNotifications.ExtraId, 0),
            Title = intent.GetStringExtra(PlatformLocalNotifications.ExtraTitle) ?? "",
            Body = intent.GetStringExtra(PlatformLocalNotifications.ExtraBody) ?? "",
            ChannelId = intent.GetStringExtra(PlatformLocalNotifications.ExtraChannel) ?? "general"
        };
        PlatformLocalNotifications.Post(context, request);
    }
}

[BroadcastReceiver(Enabled = true, Exported = false)]
public sealed class LocalNotificationTapReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (intent is null)
            return;
        try
        {
            LocalNotifications.Current.RaiseTapped(new NotificationTapEventArgs
            {
                Id = intent.GetIntExtra(PlatformLocalNotifications.ExtraId, 0),
                ActionId = intent.GetStringExtra(PlatformLocalNotifications.ExtraAction)
            });
        }
        catch (InvalidOperationException)
        {
            // Plugin not registered yet.
        }
    }
}
#endif
