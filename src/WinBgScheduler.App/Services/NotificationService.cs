using Microsoft.Toolkit.Uwp.Notifications;
using WinBgScheduler.Core.Models;

namespace WinBgScheduler.App.Services;

/// <summary>Windows toast notifications. Activation arrives on a background thread.</summary>
public sealed class NotificationService(ImageRenderService renderer)
{
    public event Action<ToastArguments>? Activated;

    public void Initialize() =>
        ToastNotificationManagerCompat.OnActivated += e => Activated?.Invoke(ToastArguments.Parse(e.Argument));

    public void ShowUpcoming(Schedule schedule, Occurrence occurrence, string? imagePath, DateTime now)
    {
        var minutes = (int)Math.Ceiling((occurrence.Start - now).TotalMinutes);
        var when = minutes <= 0 ? "agora" : minutes == 1 ? "em 1 minuto" : $"em {minutes} minutos";
        var builder = new ToastContentBuilder()
            .AddArgument("action", "open")
            .AddText($"O papel de parede muda {when}")
            .AddText($"{schedule.Name} · {occurrence.Start:HH:mm}–{occurrence.End:HH:mm}");

        if (imagePath is not null)
        {
            try
            {
                builder.AddHeroImage(new Uri(renderer.Thumbnail(imagePath)));
            }
            catch (Exception)
            {
                // A missing preview shouldn't block the notification.
            }
        }

        builder
            .AddButton(new ToastButton()
                .SetContent("Pular desta vez")
                .AddArgument("action", "skip")
                .AddArgument("schedule", schedule.Id.ToString())
                .AddArgument("start", occurrence.Start.Ticks.ToString()))
            .AddButton(new ToastButton().SetContent("Abrir").AddArgument("action", "open"))
            .Show();
    }

    public void ShowInfo(string title, string text) =>
        new ToastContentBuilder().AddArgument("action", "open").AddText(title).AddText(text).Show();
}
