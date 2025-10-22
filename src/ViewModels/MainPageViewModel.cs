using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FloofLog.Models;
using FloofLog.Services;

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

    public MainPageViewModel(IPetLogService petLogService)
    {
        _petLogService = petLogService;

        RecentActivities = new ObservableCollection<PetActivity>();
        UpcomingReminders = new ObservableCollection<PetReminder>();

        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsBusy);
        AddActivityCommand = new AsyncRelayCommand(AddActivityAsync, CanExecuteAddActivity);
        LogFeedingCommand = new AsyncRelayCommand(LogFeedingAsync, CanExecuteLogFeeding);
        ScheduleReminderCommand = new AsyncRelayCommand(ScheduleReminderAsync, CanExecuteScheduleReminder);

        _ = RefreshAsync();
    }

    public ObservableCollection<PetActivity> RecentActivities { get; }

    public ObservableCollection<PetReminder> UpcomingReminders { get; }

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand AddActivityCommand { get; }

    public IAsyncRelayCommand LogFeedingCommand { get; }

    public IAsyncRelayCommand ScheduleReminderCommand { get; }

    private bool CanExecuteAddActivity() => !IsBusy && !string.IsNullOrWhiteSpace(NewActivityName);

    private bool CanExecuteLogFeeding() => !IsBusy && _petLogService.Pets.Any();

    private bool CanExecuteScheduleReminder() => !IsBusy && !string.IsNullOrWhiteSpace(NewReminderTitle);

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
        if (!CanExecuteAddActivity())
        {
            return;
        }

        var pet = _petLogService.Pets.FirstOrDefault();
        if (pet is null)
        {
            StatusMessage = "Add a pet before logging activities.";
            return;
        }

        try
        {
            IsBusy = true;
            var activity = new PetActivity
            {
                PetId = pet.Id,
                DisplayName = NewActivityName!.Trim(),
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
        if (!CanExecuteScheduleReminder())
        {
            return;
        }

        var pet = _petLogService.Pets.FirstOrDefault();
        if (pet is null)
        {
            StatusMessage = "Add a pet before scheduling reminders.";
            return;
        }

        try
        {
            IsBusy = true;
            var reminder = new PetReminder
            {
                PetId = pet.Id,
                DisplayName = NewReminderTitle!.Trim(),
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
    }

    partial void OnIsBusyChanged(bool value) => NotifyCommandStates();

    partial void OnNewActivityNameChanged(string? value) => AddActivityCommand.NotifyCanExecuteChanged();

    partial void OnNewReminderTitleChanged(string? value) => ScheduleReminderCommand.NotifyCanExecuteChanged();

    private void NotifyCommandStates()
    {
        RefreshCommand.NotifyCanExecuteChanged();
        AddActivityCommand.NotifyCanExecuteChanged();
        LogFeedingCommand.NotifyCanExecuteChanged();
        ScheduleReminderCommand.NotifyCanExecuteChanged();
    }
}
