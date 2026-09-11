using Plugin.Maui.LocalNotifications;

namespace Plugin.Maui.LocalNotifications.Sample;

public partial class MainPage : ContentPage
{
    readonly Label log = new();

    public MainPage()
    {
        InitializeComponent();
        LocalNotifications.Current.NotificationTapped += (_, e) =>
            MainThread.BeginInvokeOnMainThread(() => log.Text = $"Tapped {e.Id} action={e.ActionId}");
        Root.Children.Add(new Button { Text = "Immediate", Command = new Command(async () => await Schedule(NotificationSchedule.Immediate(), 2001)) });
        Root.Children.Add(new Button { Text = "In 15 seconds", Command = new Command(async () => await Schedule(NotificationSchedule.At(DateTimeOffset.Now.AddSeconds(15)), 2002)) });
        Root.Children.Add(new Button { Text = "Every 60 seconds", Command = new Command(async () => await Schedule(NotificationSchedule.Every(TimeSpan.FromSeconds(60)), 2003)) });
        Root.Children.Add(new Button { Text = "Pending", Command = new Command(async () => log.Text = string.Join(", ", (await LocalNotifications.Current.GetPendingAsync()).Select(p => p.Id))) });
        Root.Children.Add(new Button { Text = "Cancel all", Command = new Command(async () => await LocalNotifications.Current.CancelAllAsync()) });
        Root.Children.Add(log);
    }

    async Task Schedule(NotificationSchedule schedule, int id)
    {
        await LocalNotifications.Current.EnsureChannelAsync(new NotificationChannelRequest { Id = "sync", Name = "Sync", Importance = NotificationImportance.Default });
        await LocalNotifications.Current.ScheduleAsync(new NotificationRequest
        {
            Id = id,
            Title = "LocalNotifications sample",
            Body = $"id={id} {schedule.Kind}",
            ChannelId = "sync",
            Schedule = schedule,
            Payload = new Dictionary<string, string> { ["route"] = "//uploads" },
            Actions = { new NotificationAction("open", "Open"), new NotificationAction("later", "Later") }
        });
        log.Text = $"scheduled {id}";
    }
}
