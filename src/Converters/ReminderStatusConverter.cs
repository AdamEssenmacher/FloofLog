using System;
using System.Globalization;

using FloofLog.Models;

using Microsoft.Maui.Controls;

namespace FloofLog.Converters;

public sealed class ReminderStatusConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not PetReminder reminder)
        {
            return null;
        }

        if (reminder.RemindAt is null)
        {
            return "Ready when you are";
        }

        var now = DateTimeOffset.Now;
        if (reminder.RemindAt <= now)
        {
            var overdue = now - reminder.RemindAt.Value;
            return overdue.TotalMinutes < 1
                ? "Due now"
                : $"Overdue by {FormatDuration(overdue)}";
        }

        var untilDue = reminder.RemindAt.Value - now;
        return untilDue.TotalMinutes < 1
            ? "Due now"
            : $"Due in {FormatDuration(untilDue)}";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();

    private static string FormatDuration(TimeSpan span)
    {
        if (span.TotalMinutes < 1)
        {
            return "moments";
        }

        if (span.TotalHours < 1)
        {
            var minutes = Math.Max(1, (int)Math.Round(span.TotalMinutes));
            return minutes == 1 ? "1 minute" : $"{minutes} minutes";
        }

        if (span.TotalDays < 1)
        {
            var hours = Math.Max(1, (int)Math.Round(span.TotalHours));
            return hours == 1 ? "1 hour" : $"{hours} hours";
        }

        var days = Math.Max(1, (int)Math.Round(span.TotalDays));
        return days == 1 ? "1 day" : $"{days} days";
    }
}
