using System;
using System.Globalization;

using FloofLog.Models;

using Microsoft.Maui.Controls;

namespace FloofLog.Converters;

public sealed class ActivityIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not PetActivity activity)
        {
            return null;
        }

        var name = activity.DisplayName?.Trim() ?? string.Empty;
        if (name.Length == 0 && !string.IsNullOrWhiteSpace(activity.Notes))
        {
            name = activity.Notes;
        }

        if (ContainsKeyword(name, "feed"))
        {
            return "🍽️";
        }

        if (ContainsKeyword(name, "walk") || ContainsKeyword(name, "stroll"))
        {
            return "🚶";
        }

        if (ContainsKeyword(name, "med"))
        {
            return "💊";
        }

        return "🐾";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();

    private static bool ContainsKeyword(string text, string keyword) => text.Contains(keyword, StringComparison.OrdinalIgnoreCase);
}
