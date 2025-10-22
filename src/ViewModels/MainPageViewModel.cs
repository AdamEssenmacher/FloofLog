using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FloofLog.Models;
using FloofLog.Services;

using Microsoft.Maui.Controls;

namespace FloofLog.ViewModels;

public sealed partial class MainPageViewModel : ObservableObject
{
    private const int RecentActivityLimit = 20;
    private readonly IPetLogService _petLogService;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private int _totalPets;

    [ObservableProperty]
    private int _activitiesLoggedToday;

    [ObservableProperty]
    private int _pendingReminders;

    [ObservableProperty]
    private string? _newActivityName;

    [ObservableProperty]
    private string? _newReminderTitle;

    [ObservableProperty]
    private string _lastFeedingSummary = "No feedings logged yet.";

    [ObservableProperty]
    private string _nextWalkSummary = "No walks scheduled.";

    public MainPageViewModel(IPetLogService petLogService)
    {
        _petLogService = petLogService;

        RecentActivities = new ObservableCollection<PetActivity>();
        UpcomingReminders = new ObservableCollection<PetReminder>();

        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsBusy);
        AddActivityCommand = new AsyncRelayCommand(AddActivityAsync, CanExecuteAddActivity);
        LogFeedingCommand = new AsyncRelayCommand(LogFeedingAsync, CanExecuteLogFeeding);
        ScheduleReminderCommand = new AsyncRelayCommand(ScheduleReminderAsync, CanExecuteScheduleReminder);
        EditActivityCommand = new AsyncRelayCommand<PetActivity>(EditActivityAsync, CanModifyActivity);
        DeleteActivityCommand = new AsyncRelayCommand<PetActivity>(DeleteActivityAsync, CanModifyActivity);
        EditReminderCommand = new AsyncRelayCommand<PetReminder>(EditReminderAsync, CanModifyReminder);
        DeleteReminderCommand = new AsyncRelayCommand<PetReminder>(DeleteReminderAsync, CanModifyReminder);

        _ = RefreshAsync();
    }

    public ObservableCollection<PetActivity> RecentActivities { get; }

    public ObservableCollection<PetReminder> UpcomingReminders { get; }

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand AddActivityCommand { get; }

    public IAsyncRelayCommand LogFeedingCommand { get; }

    public IAsyncRelayCommand ScheduleReminderCommand { get; }

    public IAsyncRelayCommand<PetActivity> EditActivityCommand { get; }

    public IAsyncRelayCommand<PetActivity> DeleteActivityCommand { get; }

    public IAsyncRelayCommand<PetReminder> EditReminderCommand { get; }

    public IAsyncRelayCommand<PetReminder> DeleteReminderCommand { get; }

    private bool CanExecuteAddActivity() => !IsBusy;

    private bool CanExecuteLogFeeding() => !IsBusy && _petLogService.Pets.Any();

    private bool CanExecuteScheduleReminder() => !IsBusy;

    private bool CanModifyActivity(PetActivity? activity) => !IsBusy && activity is not null;

    private bool CanModifyReminder(PetReminder? reminder) => !IsBusy && reminder is not null;

    private async Task RefreshAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            await _petLogService.LoadAsync();

            SyncActivities();
            SyncReminders();
            UpdateMetrics();

            StatusMessage = $"Last refreshed at {DateTimeOffset.Now:t}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to refresh: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            NotifyCommandStates();
        }
    }

    private async Task AddActivityAsync()
    {
        var pet = _petLogService.Pets.FirstOrDefault();
        if (pet is null)
        {
            StatusMessage = "Add a pet before logging activities.";
            return;
        }

        var description = await GetActivityDescriptionAsync(NewActivityName, forcePrompt: false);
        if (string.IsNullOrWhiteSpace(description))
        {
            StatusMessage = "Activity entry cancelled.";
            return;
        }

        try
        {
            IsBusy = true;
            var activity = new PetActivity
            {
                PetId = pet.Id,
                DisplayName = description,
                OccurredAt = DateTimeOffset.Now
            };

            var created = await _petLogService.CreateActivityAsync(activity);
            InsertRecentActivity(created);
            UpdateMetrics();

            NewActivityName = string.Empty;
            StatusMessage = $"Added activity '{created.DisplayName}'.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to add activity: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            NotifyCommandStates();
        }
    }

    private async Task LogFeedingAsync()
    {
        if (!CanExecuteLogFeeding())
        {
            return;
        }

        var pet = _petLogService.Pets.First();

        try
        {
            IsBusy = true;
            var activity = new PetActivity
            {
                PetId = pet.Id,
                DisplayName = $"Feeding for {pet.DisplayName}",
                Notes = "Auto-logged feeding",
                OccurredAt = DateTimeOffset.Now
            };

            var created = await _petLogService.CreateActivityAsync(activity);
            InsertRecentActivity(created);
            UpdateMetrics();

            StatusMessage = $"Logged feeding for {pet.DisplayName}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to log feeding: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            NotifyCommandStates();
        }
    }

    private async Task ScheduleReminderAsync()
    {
        var pet = _petLogService.Pets.FirstOrDefault();
        if (pet is null)
        {
            StatusMessage = "Add a pet before scheduling reminders.";
            return;
        }

        var title = await GetReminderTitleAsync(NewReminderTitle, forcePrompt: false);
        if (string.IsNullOrWhiteSpace(title))
        {
            StatusMessage = "Reminder scheduling cancelled.";
            return;
        }

        try
        {
            IsBusy = true;
            var reminder = new PetReminder
            {
                PetId = pet.Id,
                DisplayName = title,
                RemindAt = DateTimeOffset.Now.AddHours(1)
            };

            var created = await _petLogService.CreateReminderAsync(reminder);
            InsertUpcomingReminder(created);
            UpdateMetrics();

            NewReminderTitle = string.Empty;
            StatusMessage = $"Scheduled reminder '{created.DisplayName}'.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to schedule reminder: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            NotifyCommandStates();
        }
    }

    private async Task EditActivityAsync(PetActivity? activity)
    {
        if (!CanModifyActivity(activity))
        {
            return;
        }

        var updatedName = await GetActivityDescriptionAsync(activity!.DisplayName, forcePrompt: true);
        if (string.IsNullOrWhiteSpace(updatedName) || string.Equals(updatedName, activity.DisplayName, StringComparison.Ordinal))
        {
            StatusMessage = "Activity unchanged.";
            return;
        }

        try
        {
            IsBusy = true;
            activity.DisplayName = updatedName;
            await _petLogService.UpdateActivityAsync(activity);
            SyncActivities();
            UpdateMetrics();
            StatusMessage = $"Updated activity to '{activity.DisplayName}'.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to update activity: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            NotifyCommandStates();
        }
    }

    private async Task DeleteActivityAsync(PetActivity? activity)
    {
        if (!CanModifyActivity(activity))
        {
            return;
        }

        try
        {
            IsBusy = true;
            var removed = await _petLogService.DeleteActivityAsync(activity!.Id);
            if (removed)
            {
                SyncActivities();
                UpdateMetrics();
                StatusMessage = $"Deleted activity '{activity.DisplayName}'.";
            }
            else
            {
                StatusMessage = "Unable to delete activity.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to delete activity: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            NotifyCommandStates();
        }
    }

    private async Task EditReminderAsync(PetReminder? reminder)
    {
        if (!CanModifyReminder(reminder))
        {
            return;
        }

        var updatedTitle = await GetReminderTitleAsync(reminder!.DisplayName, forcePrompt: true);
        if (string.IsNullOrWhiteSpace(updatedTitle) || string.Equals(updatedTitle, reminder.DisplayName, StringComparison.Ordinal))
        {
            StatusMessage = "Reminder unchanged.";
            return;
        }

        try
        {
            IsBusy = true;
            reminder.DisplayName = updatedTitle;
            await _petLogService.UpdateReminderAsync(reminder);
            SyncReminders();
            UpdateMetrics();
            StatusMessage = $"Updated reminder to '{reminder.DisplayName}'.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to update reminder: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            NotifyCommandStates();
        }
    }

    private async Task DeleteReminderAsync(PetReminder? reminder)
    {
        if (!CanModifyReminder(reminder))
        {
            return;
        }

        try
        {
            IsBusy = true;
            var removed = await _petLogService.DeleteReminderAsync(reminder!.Id);
            if (removed)
            {
                SyncReminders();
                UpdateMetrics();
                StatusMessage = $"Deleted reminder '{reminder.DisplayName}'.";
            }
            else
            {
                StatusMessage = "Unable to delete reminder.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to delete reminder: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            NotifyCommandStates();
        }
    }

    private static async Task<string?> GetActivityDescriptionAsync(string? initialValue, bool forcePrompt)
    {
        if (!forcePrompt && !string.IsNullOrWhiteSpace(initialValue))
        {
            return initialValue.Trim();
        }

        if (GetActivePage() is not Page page)
        {
            return null;
        }

        return await page.DisplayPromptAsync(
            "Log Activity",
            "Describe the activity",
            "Save",
            "Cancel",
            initialValue: initialValue,
            placeholder: "e.g., Evening walk",
            maxLength: 100,
            keyboard: Keyboard.Text);
    }

    private static async Task<string?> GetReminderTitleAsync(string? initialValue, bool forcePrompt)
    {
        if (!forcePrompt && !string.IsNullOrWhiteSpace(initialValue))
        {
            return initialValue.Trim();
        }

        if (GetActivePage() is not Page page)
        {
            return null;
        }

        return await page.DisplayPromptAsync(
            "Schedule Reminder",
            "What should we remind you about?",
            "Save",
            "Cancel",
            initialValue: initialValue,
            placeholder: "e.g., Morning walk",
            maxLength: 100,
            keyboard: Keyboard.Text);
    }

    private void SyncActivities()
    {
        var activities = _petLogService.Activities
            .OrderByDescending(a => a.OccurredAt)
            .Take(RecentActivityLimit)
            .ToList();

        ReplaceCollection(RecentActivities, activities);
    }

    private void SyncReminders()
    {
        var reminders = _petLogService.Reminders
            .OrderBy(r => r.RemindAt ?? DateTimeOffset.MaxValue)
            .ToList();

        ReplaceCollection(UpcomingReminders, reminders);
    }

    private void InsertRecentActivity(PetActivity activity)
    {
        RecentActivities.Add(activity);
        var ordered = RecentActivities
            .OrderByDescending(a => a.OccurredAt)
            .Take(RecentActivityLimit)
            .ToList();

        ReplaceCollection(RecentActivities, ordered);
    }

    private void InsertUpcomingReminder(PetReminder reminder)
    {
        UpcomingReminders.Add(reminder);
        var ordered = UpcomingReminders
            .OrderBy(r => r.RemindAt ?? DateTimeOffset.MaxValue)
            .ToList();

        ReplaceCollection(UpcomingReminders, ordered);
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> collection, IReadOnlyList<T> items)
    {
        collection.Clear();
        for (var i = 0; i < items.Count; i++)
        {
            collection.Add(items[i]);
        }
    }

    private void UpdateMetrics()
    {
        TotalPets = _petLogService.Pets.Count;
        ActivitiesLoggedToday = _petLogService.Activities.Count(a => a.OccurredAt.Date == DateTimeOffset.Now.Date);
        PendingReminders = UpcomingReminders.Count(r => r.RemindAt is null || r.RemindAt >= DateTimeOffset.Now);
        LastFeedingSummary = BuildLastFeedingSummary();
        NextWalkSummary = BuildNextWalkSummary();
    }

    partial void OnIsBusyChanged(bool value) => NotifyCommandStates();

    private void NotifyCommandStates()
    {
        RefreshCommand.NotifyCanExecuteChanged();
        AddActivityCommand.NotifyCanExecuteChanged();
        LogFeedingCommand.NotifyCanExecuteChanged();
        ScheduleReminderCommand.NotifyCanExecuteChanged();
        EditActivityCommand.NotifyCanExecuteChanged();
        DeleteActivityCommand.NotifyCanExecuteChanged();
        EditReminderCommand.NotifyCanExecuteChanged();
        DeleteReminderCommand.NotifyCanExecuteChanged();
    }

    private string BuildLastFeedingSummary()
    {
        var lastFeeding = _petLogService.Activities
            .Where(IsFeedingActivity)
            .OrderByDescending(a => a.OccurredAt)
            .FirstOrDefault();

        if (lastFeeding is null)
        {
            return "No feedings logged yet.";
        }

        var petName = GetPetName(lastFeeding.PetId);
        return string.Format(CultureInfo.CurrentCulture, "{0} was fed {1}.", petName, FormatRelativePast(lastFeeding.OccurredAt));
    }

    private string BuildNextWalkSummary()
    {
        var nextWalk = _petLogService.Reminders
            .Where(IsWalkReminder)
            .OrderBy(r => r.RemindAt ?? DateTimeOffset.MaxValue)
            .FirstOrDefault();

        if (nextWalk is null)
        {
            return "No walks scheduled.";
        }

        var petName = GetPetName(nextWalk.PetId);
        if (nextWalk.RemindAt is null)
        {
            return string.Format(CultureInfo.CurrentCulture, "Walk {0} when you're ready.", petName);
        }

        var descriptor = nextWalk.RemindAt <= DateTimeOffset.Now
            ? $"was due {FormatRelativePast(nextWalk.RemindAt.Value)}"
            : $"scheduled {FormatRelativeFuture(nextWalk.RemindAt.Value)}";

        return string.Format(CultureInfo.CurrentCulture, "Next walk for {0} is {1}.", petName, descriptor);
    }

    private static bool IsFeedingActivity(PetActivity activity) =>
        activity.DisplayName.Contains("feed", StringComparison.OrdinalIgnoreCase) ||
        (!string.IsNullOrWhiteSpace(activity.Notes) && activity.Notes.Contains("feed", StringComparison.OrdinalIgnoreCase));

    private static bool IsWalkReminder(PetReminder reminder)
    {
        if (!string.IsNullOrWhiteSpace(reminder.DisplayName) && reminder.DisplayName.Contains("walk", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(reminder.Notes) && reminder.Notes.Contains("walk", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private string GetPetName(Guid petId)
    {
        var pet = _petLogService.Pets.FirstOrDefault(p => p.Id == petId);
        return pet?.DisplayName ?? "your pet";
    }

    private static string FormatRelativePast(DateTimeOffset timestamp)
    {
        var span = DateTimeOffset.Now - timestamp;
        if (span.TotalMinutes < 1)
        {
            return "just now";
        }

        if (span.TotalHours < 1)
        {
            var minutes = Math.Max(1, (int)Math.Round(span.TotalMinutes));
            return minutes == 1 ? "1 minute ago" : string.Format(CultureInfo.CurrentCulture, "{0} minutes ago", minutes);
        }

        if (span.TotalDays < 1)
        {
            var hours = Math.Max(1, (int)Math.Round(span.TotalHours));
            return hours == 1 ? "1 hour ago" : string.Format(CultureInfo.CurrentCulture, "{0} hours ago", hours);
        }

        var days = Math.Max(1, (int)Math.Round(span.TotalDays));
        return days == 1 ? "1 day ago" : string.Format(CultureInfo.CurrentCulture, "{0} days ago", days);
    }

    private static string FormatRelativeFuture(DateTimeOffset timestamp)
    {
        var span = timestamp - DateTimeOffset.Now;
        if (span.TotalMinutes < 1)
        {
            return "momentarily";
        }

        if (span.TotalHours < 1)
        {
            var minutes = Math.Max(1, (int)Math.Round(span.TotalMinutes));
            return minutes == 1 ? "in 1 minute" : string.Format(CultureInfo.CurrentCulture, "in {0} minutes", minutes);
        }

        if (span.TotalDays < 1)
        {
            var hours = Math.Max(1, (int)Math.Round(span.TotalHours));
            return hours == 1 ? "in 1 hour" : string.Format(CultureInfo.CurrentCulture, "in {0} hours", hours);
        }

        var days = Math.Max(1, (int)Math.Round(span.TotalDays));
        return days == 1 ? "tomorrow" : string.Format(CultureInfo.CurrentCulture, "in {0} days", days);
    }

    private static Page? GetActivePage()
    {
        if (Application.Current?.Windows.FirstOrDefault()?.Page is Page page)
        {
            return page;
        }

#pragma warning disable CS0618
        return Application.Current?.MainPage;
#pragma warning restore CS0618
    }
}
