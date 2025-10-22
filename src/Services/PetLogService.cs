using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using FloofLog.Models;

using Microsoft.Maui.Storage;

namespace FloofLog.Services;

public sealed class PetLogService : IPetLogService
{
    private const string DataFileName = "petlog.json";

    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private readonly ObservableCollection<Pet> _pets = new();
    private readonly ObservableCollection<PetActivity> _activities = new();
    private readonly ObservableCollection<PetReminder> _reminders = new();
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly string _dataFilePath;

    public PetLogService()
    {
        _serializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        _dataFilePath = Path.Combine(FileSystem.AppDataDirectory, DataFileName);
        Directory.CreateDirectory(FileSystem.AppDataDirectory);

        Pets = new ReadOnlyObservableCollection<Pet>(_pets);
        Activities = new ReadOnlyObservableCollection<PetActivity>(_activities);
        Reminders = new ReadOnlyObservableCollection<PetReminder>(_reminders);

        try
        {
            LoadAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load pet log data: {ex}");
        }
    }

    public ReadOnlyObservableCollection<Pet> Pets { get; }

    public ReadOnlyObservableCollection<PetActivity> Activities { get; }

    public ReadOnlyObservableCollection<PetReminder> Reminders { get; }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_dataFilePath))
            {
                return;
            }

            await using var stream = new FileStream(
                _dataFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true);

            var data = await JsonSerializer.DeserializeAsync<PetLogSnapshot>(
                stream,
                _serializerOptions,
                cancellationToken);

            if (data is null)
            {
                return;
            }

            UpdateCollection(_pets, data.Pets);
            UpdateCollection(_activities, data.Activities);
            UpdateCollection(_reminders, data.Reminders);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            await SaveSnapshotAsync(cancellationToken);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task<Pet> CreatePetAsync(Pet pet, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pet);

        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            if (pet.Id == Guid.Empty)
            {
                pet.Id = Guid.NewGuid();
            }

            var now = DateTimeOffset.UtcNow;
            pet.CreatedAt = now;
            pet.UpdatedAt = now;

            _pets.Add(pet);

            await SaveSnapshotAsync(cancellationToken);

            return pet;
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public Task<Pet?> GetPetAsync(Guid petId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var pet = _pets.FirstOrDefault(p => p.Id == petId);
        return Task.FromResult(pet);
    }

    public async Task UpdatePetAsync(Pet pet, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pet);

        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            var existing = _pets.FirstOrDefault(p => p.Id == pet.Id)
                ?? throw new KeyNotFoundException($"No pet found with identifier {pet.Id}.");

            existing.DisplayName = pet.DisplayName;
            existing.Notes = pet.Notes;
            existing.ArchivedAt = pet.ArchivedAt;
            existing.UpdatedAt = DateTimeOffset.UtcNow;

            await SaveSnapshotAsync(cancellationToken);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task<bool> DeletePetAsync(Guid petId, CancellationToken cancellationToken = default)
    {
        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            var pet = _pets.FirstOrDefault(p => p.Id == petId);
            if (pet is null)
            {
                return false;
            }

            var relatedActivities = _activities.Where(a => a.PetId == petId).ToList();
            foreach (var activity in relatedActivities)
            {
                _activities.Remove(activity);
            }

            var relatedReminders = _reminders.Where(r => r.PetId == petId).ToList();
            foreach (var reminder in relatedReminders)
            {
                _reminders.Remove(reminder);
            }

            var removed = _pets.Remove(pet);

            if (removed)
            {
                await SaveSnapshotAsync(cancellationToken);
            }

            return removed;
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task<PetActivity> CreateActivityAsync(PetActivity activity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(activity);

        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            if (activity.Id == Guid.Empty)
            {
                activity.Id = Guid.NewGuid();
            }

            if (!_pets.Any(p => p.Id == activity.PetId))
            {
                throw new KeyNotFoundException($"No pet found with identifier {activity.PetId}.");
            }

            var now = DateTimeOffset.UtcNow;
            activity.CreatedAt = now;
            activity.UpdatedAt = now;

            _activities.Add(activity);

            await SaveSnapshotAsync(cancellationToken);

            return activity;
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public Task<PetActivity?> GetActivityAsync(Guid activityId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var activity = _activities.FirstOrDefault(a => a.Id == activityId);
        return Task.FromResult(activity);
    }

    public async Task UpdateActivityAsync(PetActivity activity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(activity);

        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            var existing = _activities.FirstOrDefault(a => a.Id == activity.Id)
                ?? throw new KeyNotFoundException($"No activity found with identifier {activity.Id}.");

            if (!_pets.Any(p => p.Id == activity.PetId))
            {
                throw new KeyNotFoundException($"No pet found with identifier {activity.PetId}.");
            }

            existing.PetId = activity.PetId;
            existing.DisplayName = activity.DisplayName;
            existing.Notes = activity.Notes;
            existing.OccurredAt = activity.OccurredAt;
            existing.Recurrence = activity.Recurrence;
            existing.UpdatedAt = DateTimeOffset.UtcNow;

            await SaveSnapshotAsync(cancellationToken);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task<bool> DeleteActivityAsync(Guid activityId, CancellationToken cancellationToken = default)
    {
        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            var activity = _activities.FirstOrDefault(a => a.Id == activityId);
            if (activity is null)
            {
                return false;
            }

            var removed = _activities.Remove(activity);
            if (removed)
            {
                await SaveSnapshotAsync(cancellationToken);
            }

            return removed;
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task<PetReminder> CreateReminderAsync(PetReminder reminder, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reminder);

        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            if (reminder.Id == Guid.Empty)
            {
                reminder.Id = Guid.NewGuid();
            }

            if (!_pets.Any(p => p.Id == reminder.PetId))
            {
                throw new KeyNotFoundException($"No pet found with identifier {reminder.PetId}.");
            }

            var now = DateTimeOffset.UtcNow;
            reminder.CreatedAt = now;
            reminder.UpdatedAt = now;

            _reminders.Add(reminder);

            await SaveSnapshotAsync(cancellationToken);

            return reminder;
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public Task<PetReminder?> GetReminderAsync(Guid reminderId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var reminder = _reminders.FirstOrDefault(r => r.Id == reminderId);
        return Task.FromResult(reminder);
    }

    public async Task UpdateReminderAsync(PetReminder reminder, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reminder);

        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            var existing = _reminders.FirstOrDefault(r => r.Id == reminder.Id)
                ?? throw new KeyNotFoundException($"No reminder found with identifier {reminder.Id}.");

            if (!_pets.Any(p => p.Id == reminder.PetId))
            {
                throw new KeyNotFoundException($"No pet found with identifier {reminder.PetId}.");
            }

            existing.PetId = reminder.PetId;
            existing.DisplayName = reminder.DisplayName;
            existing.Notes = reminder.Notes;
            existing.RemindAt = reminder.RemindAt;
            existing.Recurrence = reminder.Recurrence;
            existing.UpdatedAt = DateTimeOffset.UtcNow;

            await SaveSnapshotAsync(cancellationToken);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task<bool> DeleteReminderAsync(Guid reminderId, CancellationToken cancellationToken = default)
    {
        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            var reminder = _reminders.FirstOrDefault(r => r.Id == reminderId);
            if (reminder is null)
            {
                return false;
            }

            var removed = _reminders.Remove(reminder);
            if (removed)
            {
                await SaveSnapshotAsync(cancellationToken);
            }

            return removed;
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private void UpdateCollection<T>(ObservableCollection<T> target, IEnumerable<T>? source)
    {
        target.Clear();
        foreach (var item in source ?? Enumerable.Empty<T>())
        {
            target.Add(item);
        }
    }

    private async Task SaveSnapshotAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_dataFilePath)!);

        var snapshot = new PetLogSnapshot
        {
            Pets = _pets.Select(ClonePet).ToList(),
            Activities = _activities.Select(CloneActivity).ToList(),
            Reminders = _reminders.Select(CloneReminder).ToList()
        };

        await using var stream = new FileStream(
            _dataFilePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            useAsync: true);

        await JsonSerializer.SerializeAsync(stream, snapshot, _serializerOptions, cancellationToken);
    }

    private static Pet ClonePet(Pet pet)
    {
        return new Pet
        {
            Id = pet.Id,
            DisplayName = pet.DisplayName,
            Notes = pet.Notes,
            CreatedAt = pet.CreatedAt,
            UpdatedAt = pet.UpdatedAt,
            ArchivedAt = pet.ArchivedAt
        };
    }

    private static PetActivity CloneActivity(PetActivity activity)
    {
        return new PetActivity
        {
            Id = activity.Id,
            PetId = activity.PetId,
            DisplayName = activity.DisplayName,
            Notes = activity.Notes,
            OccurredAt = activity.OccurredAt,
            CreatedAt = activity.CreatedAt,
            UpdatedAt = activity.UpdatedAt,
            Recurrence = CloneRecurrence(activity.Recurrence)
        };
    }

    private static PetReminder CloneReminder(PetReminder reminder)
    {
        return new PetReminder
        {
            Id = reminder.Id,
            PetId = reminder.PetId,
            DisplayName = reminder.DisplayName,
            Notes = reminder.Notes,
            CreatedAt = reminder.CreatedAt,
            UpdatedAt = reminder.UpdatedAt,
            RemindAt = reminder.RemindAt,
            Recurrence = CloneRecurrence(reminder.Recurrence)
        };
    }

    private static RecurrenceInfo? CloneRecurrence(RecurrenceInfo? recurrence)
    {
        if (recurrence is null)
        {
            return null;
        }

        return new RecurrenceInfo
        {
            Frequency = recurrence.Frequency,
            Interval = recurrence.Interval,
            NextOccurrence = recurrence.NextOccurrence,
            EndDate = recurrence.EndDate
        };
    }

    private sealed class PetLogSnapshot
    {
        public IList<Pet> Pets { get; set; } = new List<Pet>();
        public IList<PetActivity> Activities { get; set; } = new List<PetActivity>();
        public IList<PetReminder> Reminders { get; set; } = new List<PetReminder>();
    }
}
