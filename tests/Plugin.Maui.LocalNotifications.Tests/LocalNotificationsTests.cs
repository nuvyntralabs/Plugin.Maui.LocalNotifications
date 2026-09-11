using Plugin.Maui.LocalNotifications;

namespace Plugin.Maui.LocalNotifications.Tests;

sealed class FakePlatform : ILocalNotificationPlatform
{
    public List<int> Shown { get; } = [];
    public List<int> Canceled { get; } = [];
    public List<string> Channels { get; } = [];
    public Task EnsureChannelAsync(NotificationChannelRequest channel, CancellationToken cancellationToken)
    {
        Channels.Add(channel.Id);
        return Task.CompletedTask;
    }
    public Task ShowAsync(NotificationRequest request, CancellationToken cancellationToken)
    {
        Shown.Add(request.Id);
        return Task.CompletedTask;
    }
    public Task CancelPlatformAsync(int id, CancellationToken cancellationToken)
    {
        Canceled.Add(id);
        return Task.CompletedTask;
    }
}

public sealed class LocalNotificationsTests
{
    [Fact]
    public async Task Schedule_cancel_pending_and_tap()
    {
        var platform = new FakePlatform();
        var api = new LocalNotificationsImplementation(new LocalNotificationOptions(), platform);
        await api.ScheduleAsync(new NotificationRequest
        {
            Id = 1001,
            Title = "Upload",
            Body = "3 photos",
            Schedule = NotificationSchedule.At(DateTimeOffset.Now.AddMinutes(30)),
            Payload = new Dictionary<string, string> { ["route"] = "//uploads" },
            Actions = { new NotificationAction("open", "Open"), new NotificationAction("later", "Later") }
        });
        Assert.Single(await api.GetPendingAsync());
        NotificationTapEventArgs? tap = null;
        api.NotificationTapped += (_, e) => tap = e;
        api.RaiseTapped(new NotificationTapEventArgs { Id = 1001, ActionId = "open", Payload = new Dictionary<string, string> { ["route"] = "//uploads" } });
        Assert.Equal("open", tap!.ActionId);
        await api.CancelAsync(1001);
        Assert.Empty(await api.GetPendingAsync());
        Assert.Contains(1001, platform.Canceled);
    }

    [Fact]
    public async Task Immediate_is_not_pending()
    {
        var api = new LocalNotificationsImplementation(new LocalNotificationOptions(), new FakePlatform());
        await api.ScheduleAsync(new NotificationRequest { Id = 1, Title = "Now", Body = "x" });
        Assert.Empty(await api.GetPendingAsync());
    }

    [Fact]
    public async Task Interval_below_60s_is_rejected()
    {
        var api = new LocalNotificationsImplementation(new LocalNotificationOptions(), new FakePlatform());
        await Assert.ThrowsAsync<ArgumentException>(() => api.ScheduleAsync(new NotificationRequest
        {
            Id = 2,
            Title = "t",
            Schedule = NotificationSchedule.Every(TimeSpan.FromSeconds(30))
        }));
    }

    [Fact]
    public async Task Zero_id_and_too_many_actions_are_rejected()
    {
        var api = new LocalNotificationsImplementation(new LocalNotificationOptions(), new FakePlatform());
        await Assert.ThrowsAsync<ArgumentException>(() => api.ScheduleAsync(new NotificationRequest { Id = 0, Title = "t" }));
        await Assert.ThrowsAsync<ArgumentException>(() => api.ScheduleAsync(new NotificationRequest
        {
            Id = 3,
            Title = "t",
            Actions =
            {
                new NotificationAction("a", "A"),
                new NotificationAction("b", "B"),
                new NotificationAction("c", "C"),
                new NotificationAction("d", "D")
            }
        }));
    }
}
