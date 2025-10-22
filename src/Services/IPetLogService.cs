using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

using FloofLog.Models;

namespace FloofLog.Services;

public interface IPetLogService
{
    ReadOnlyObservableCollection<Pet> Pets { get; }
    ReadOnlyObservableCollection<PetActivity> Activities { get; }
    ReadOnlyObservableCollection<PetReminder> Reminders { get; }

    Task LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);

    Task<Pet> CreatePetAsync(Pet pet, CancellationToken cancellationToken = default);
    Task<Pet?> GetPetAsync(Guid petId, CancellationToken cancellationToken = default);
    Task UpdatePetAsync(Pet pet, CancellationToken cancellationToken = default);
    Task<bool> DeletePetAsync(Guid petId, CancellationToken cancellationToken = default);

    Task<PetActivity> CreateActivityAsync(PetActivity activity, CancellationToken cancellationToken = default);
    Task<PetActivity?> GetActivityAsync(Guid activityId, CancellationToken cancellationToken = default);
    Task UpdateActivityAsync(PetActivity activity, CancellationToken cancellationToken = default);
    Task<bool> DeleteActivityAsync(Guid activityId, CancellationToken cancellationToken = default);

    Task<PetReminder> CreateReminderAsync(PetReminder reminder, CancellationToken cancellationToken = default);
    Task<PetReminder?> GetReminderAsync(Guid reminderId, CancellationToken cancellationToken = default);
    Task UpdateReminderAsync(PetReminder reminder, CancellationToken cancellationToken = default);
    Task<bool> DeleteReminderAsync(Guid reminderId, CancellationToken cancellationToken = default);
}
