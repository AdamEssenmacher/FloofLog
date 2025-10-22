using System;

using FloofLog.Models;

using Microsoft.Maui.Controls;

namespace FloofLog.Selectors;

public sealed class ReminderTemplateSelector : DataTemplateSelector
{
    public DataTemplate? UpcomingTemplate { get; set; }

    public DataTemplate? OverdueTemplate { get; set; }

    protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
    {
        if (item is not PetReminder reminder)
        {
            throw new ArgumentException($"Expected {nameof(PetReminder)} but received {item?.GetType()}.", nameof(item));
        }

        if (reminder.RemindAt is null)
        {
            return UpcomingTemplate ?? OverdueTemplate ?? new DataTemplate(() => new ContentView());
        }

        return reminder.RemindAt <= DateTimeOffset.Now
            ? OverdueTemplate ?? UpcomingTemplate ?? new DataTemplate(() => new ContentView())
            : UpcomingTemplate ?? OverdueTemplate ?? new DataTemplate(() => new ContentView());
    }
}
