namespace Plugin.Maui.LocalNotifications;

public enum NotificationImportance { Min, Low, Default, High }
public enum NotificationScheduleKind { Immediate, At, Interval }

public sealed class LocalNotificationOptions
{
    public string AndroidDefaultChannelId { get; set; } = "general";
    public string AndroidDefaultChannelName { get; set; } = "General";
    public bool RequestPermissionOnRegister { get; set; }
}

public sealed class NotificationChannelRequest
{
    public string Id { get; set; } = "general";
    public string Name { get; set; } = "General";
    public NotificationImportance Importance { get; set; } = NotificationImportance.Default;
}

public sealed class NotificationAction
{
    public NotificationAction() { }
    public NotificationAction(string id, string title) { Id = id; Title = title; }
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
}

public sealed class NotificationSchedule
{
    public NotificationScheduleKind Kind { get; init; } = NotificationScheduleKind.Immediate;
    public DateTimeOffset? When { get; init; }
    public TimeSpan? Interval { get; init; }
    public bool Exact { get; init; }
    public static NotificationSchedule Immediate() => new() { Kind = NotificationScheduleKind.Immediate };
    public static NotificationSchedule At(DateTimeOffset when, bool exact = false) =>
        new() { Kind = NotificationScheduleKind.At, When = when, Exact = exact };
    public static NotificationSchedule Every(TimeSpan interval) =>
        new() { Kind = NotificationScheduleKind.Interval, Interval = interval };
}

public sealed class NotificationRequest
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string ChannelId { get; set; } = "";
    public NotificationSchedule Schedule { get; set; } = NotificationSchedule.Immediate();
    public Dictionary<string, string> Payload { get; set; } = [];
    public List<NotificationAction> Actions { get; set; } = [];
}

public sealed class NotificationTapEventArgs : EventArgs
{
    public int Id { get; init; }
    public string? ActionId { get; init; }
    public IReadOnlyDictionary<string, string> Payload { get; init; } = new Dictionary<string, string>();
}

public interface ILocalNotificationPlatform
{
    Task EnsureChannelAsync(NotificationChannelRequest channel, CancellationToken cancellationToken);
    Task ShowAsync(NotificationRequest request, CancellationToken cancellationToken);
    Task CancelPlatformAsync(int id, CancellationToken cancellationToken);
}

public interface ILocalNotifications
{
    event EventHandler<NotificationTapEventArgs>? NotificationTapped;
    Task EnsureChannelAsync(NotificationChannelRequest channel, CancellationToken cancellationToken = default);
    Task ScheduleAsync(NotificationRequest request, CancellationToken cancellationToken = default);
    Task CancelAsync(int id, CancellationToken cancellationToken = default);
    Task CancelAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NotificationRequest>> GetPendingAsync(CancellationToken cancellationToken = default);
    Task<bool> AreEnabledAsync(CancellationToken cancellationToken = default);
    void RaiseTapped(NotificationTapEventArgs args);
}

public static class LocalNotifications
{
    static ILocalNotifications? current;
    public static ILocalNotifications Current =>
        current ?? throw new InvalidOperationException("LocalNotifications is not initialized. Call builder.UseLocalNotifications().");
    public static void SetDefault(ILocalNotifications implementation) =>
        current = implementation ?? throw new ArgumentNullException(nameof(implementation));

    public static ILocalNotifications Create(LocalNotificationOptions? options = null, ILocalNotificationPlatform? platform = null)
    {
        var instance = new LocalNotificationsImplementation(options ?? new LocalNotificationOptions(), platform ?? PlatformLocalNotifications.Create());
        SetDefault(instance);
        return instance;
    }
}

public static class MauiAppBuilderExtensions
{
    public static MauiAppBuilder UseLocalNotifications(this MauiAppBuilder builder, Action<LocalNotificationOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var options = new LocalNotificationOptions();
        configure?.Invoke(options);
        builder.Services.AddSingleton(LocalNotifications.Create(options));
        return builder;
    }
}

sealed class LocalNotificationsImplementation : ILocalNotifications
{
    readonly LocalNotificationOptions options;
    readonly ILocalNotificationPlatform platform;
    readonly Dictionary<int, NotificationRequest> pending = [];
    readonly HashSet<string> channels = [];

    public LocalNotificationsImplementation(LocalNotificationOptions options, ILocalNotificationPlatform platform)
    {
        this.options = options;
        this.platform = platform;
    }

    public event EventHandler<NotificationTapEventArgs>? NotificationTapped;

    public async Task EnsureChannelAsync(NotificationChannelRequest channel, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(channel);
        if (string.IsNullOrWhiteSpace(channel.Id))
            throw new ArgumentException("Channel id is required.", nameof(channel));
        await platform.EnsureChannelAsync(channel, cancellationToken).ConfigureAwait(false);
        channels.Add(channel.Id);
    }

    public async Task ScheduleAsync(NotificationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Id == 0)
            throw new ArgumentException("Notification id must be non-zero.", nameof(request));
        if (request.Actions.Count > 3)
            throw new ArgumentException("A maximum of 3 actions is supported.", nameof(request));
        if (request.Schedule.Kind == NotificationScheduleKind.Interval)
        {
            var interval = request.Schedule.Interval ?? TimeSpan.Zero;
            if (interval < TimeSpan.FromSeconds(60))
                throw new ArgumentException("Repeating intervals must be at least 60 seconds.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.ChannelId))
            request.ChannelId = options.AndroidDefaultChannelId;
        if (!channels.Contains(request.ChannelId))
        {
            await EnsureChannelAsync(new NotificationChannelRequest
            {
                Id = request.ChannelId,
                Name = options.AndroidDefaultChannelName
            }, cancellationToken).ConfigureAwait(false);
        }

        pending[request.Id] = Clone(request);
        await platform.ShowAsync(request, cancellationToken).ConfigureAwait(false);
        if (request.Schedule.Kind == NotificationScheduleKind.Immediate)
            pending.Remove(request.Id);
    }

    public async Task CancelAsync(int id, CancellationToken cancellationToken = default)
    {
        pending.Remove(id);
        await platform.CancelPlatformAsync(id, cancellationToken).ConfigureAwait(false);
    }

    public async Task CancelAllAsync(CancellationToken cancellationToken = default)
    {
        foreach (var id in pending.Keys.ToArray())
            await CancelAsync(id, cancellationToken).ConfigureAwait(false);
        pending.Clear();
    }

    public Task<IReadOnlyList<NotificationRequest>> GetPendingAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<NotificationRequest>>(pending.Values.Select(Clone).ToList());

    public Task<bool> AreEnabledAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

    public void RaiseTapped(NotificationTapEventArgs args) => NotificationTapped?.Invoke(this, args);

    static NotificationRequest Clone(NotificationRequest request) => new()
    {
        Id = request.Id,
        Title = request.Title,
        Body = request.Body,
        ChannelId = request.ChannelId,
        Schedule = request.Schedule,
        Payload = new Dictionary<string, string>(request.Payload),
        Actions = request.Actions.Select(a => new NotificationAction(a.Id, a.Title)).ToList()
    };
}

#if !ANDROID && !IOS
sealed class PlatformLocalNotifications : ILocalNotificationPlatform
{
    public static ILocalNotificationPlatform Create() => new PlatformLocalNotifications();
    public Task EnsureChannelAsync(NotificationChannelRequest channel, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task ShowAsync(NotificationRequest request, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task CancelPlatformAsync(int id, CancellationToken cancellationToken) => Task.CompletedTask;
}
#endif
