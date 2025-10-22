using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FloofLog.Models;
using FloofLog.Services;

using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace FloofLog.ViewModels;

public sealed partial class ManagePetsViewModel : ObservableObject
{
    private readonly IPetLogService _petLogService;

    public ManagePetsViewModel(IPetLogService petLogService)
    {
        _petLogService = petLogService;

        Pets = new ObservableCollection<Pet>();

        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsBusy);
        AddPetCommand = new AsyncRelayCommand(AddPetAsync, () => !IsBusy);
        EditPetCommand = new AsyncRelayCommand<Pet>(EditPetAsync, CanModifyPet);
        DeletePetCommand = new AsyncRelayCommand<Pet>(DeletePetAsync, CanModifyPet);

        SubscribeToPetChanges();
        SyncPets();
    }

    public ObservableCollection<Pet> Pets { get; }

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand AddPetCommand { get; }

    public IAsyncRelayCommand<Pet> EditPetCommand { get; }

    public IAsyncRelayCommand<Pet> DeletePetCommand { get; }

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private int _totalPets;

    partial void OnIsBusyChanged(bool value) => NotifyCommandStates();

    private void SubscribeToPetChanges()
    {
        if (_petLogService.Pets is INotifyCollectionChanged observable)
        {
            observable.CollectionChanged += (_, _) => MainThread.BeginInvokeOnMainThread(SyncPets);
        }
    }

    private bool CanModifyPet(Pet? pet) => !IsBusy && pet is not null;

    private void NotifyCommandStates()
    {
        RefreshCommand.NotifyCanExecuteChanged();
        AddPetCommand.NotifyCanExecuteChanged();
        EditPetCommand.NotifyCanExecuteChanged();
        DeletePetCommand.NotifyCanExecuteChanged();
    }

    private void SyncPets()
    {
        var orderedPets = _petLogService.Pets
            .OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(p => p.CreatedAt)
            .ToList();

        ReplaceCollection(Pets, orderedPets);
        TotalPets = Pets.Count;
    }

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
            SyncPets();
            StatusMessage = $"Last refreshed at {DateTimeOffset.Now:t}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to refresh pets: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            NotifyCommandStates();
        }
    }

    private async Task AddPetAsync()
    {
        var name = await RequestPetNameAsync(null, forcePrompt: true);
        if (string.IsNullOrWhiteSpace(name))
        {
            StatusMessage = "Pet creation cancelled.";
            return;
        }

        var notes = await RequestPetNotesAsync(null);

        try
        {
            IsBusy = true;
            var pet = new Pet
            {
                DisplayName = name,
                Notes = notes
            };

            var created = await _petLogService.CreatePetAsync(pet);
            SyncPets();
            StatusMessage = $"Added {created.DisplayName}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to add pet: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            NotifyCommandStates();
        }
    }

    private async Task EditPetAsync(Pet? pet)
    {
        if (!CanModifyPet(pet))
        {
            return;
        }

        var updatedName = await RequestPetNameAsync(pet!.DisplayName, forcePrompt: true);
        if (string.IsNullOrWhiteSpace(updatedName))
        {
            StatusMessage = "Pet unchanged.";
            return;
        }

        var updatedNotes = await RequestPetNotesAsync(pet.Notes);

        if (string.Equals(updatedName, pet.DisplayName, StringComparison.Ordinal) &&
            string.Equals(updatedNotes ?? string.Empty, pet.Notes ?? string.Empty, StringComparison.Ordinal))
        {
            StatusMessage = "Pet unchanged.";
            return;
        }

        try
        {
            IsBusy = true;
            pet.DisplayName = updatedName;
            pet.Notes = updatedNotes;
            await _petLogService.UpdatePetAsync(pet);
            SyncPets();
            StatusMessage = $"Updated {pet.DisplayName}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to update pet: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            NotifyCommandStates();
        }
    }

    private async Task DeletePetAsync(Pet? pet)
    {
        if (!CanModifyPet(pet))
        {
            return;
        }

        if (!await ConfirmDeleteAsync(pet!))
        {
            StatusMessage = "Pet deletion cancelled.";
            return;
        }

        try
        {
            IsBusy = true;
            var removed = await _petLogService.DeletePetAsync(pet!.Id);
            if (removed)
            {
                SyncPets();
                StatusMessage = $"Deleted {pet.DisplayName}.";
            }
            else
            {
                StatusMessage = "Unable to delete pet.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to delete pet: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            NotifyCommandStates();
        }
    }

    private static async Task<string?> RequestPetNameAsync(string? initialValue, bool forcePrompt)
    {
        if (!forcePrompt && !string.IsNullOrWhiteSpace(initialValue))
        {
            return initialValue.Trim();
        }

        if (GetActivePage() is not Page page)
        {
            return initialValue;
        }

        var result = await page.DisplayPromptAsync(
            "Pet name",
            "What is your pet called?",
            "Save",
            "Cancel",
            initialValue: initialValue,
            placeholder: "e.g., Luna",
            maxLength: 50,
            keyboard: Keyboard.Text);

        return string.IsNullOrWhiteSpace(result) ? null : result.Trim();
    }

    private static async Task<string?> RequestPetNotesAsync(string? initialValue)
    {
        if (GetActivePage() is not Page page)
        {
            return initialValue;
        }

        var result = await page.DisplayPromptAsync(
            "Pet notes",
            "Add notes or special care instructions (optional).",
            "Save",
            "Skip",
            initialValue: initialValue,
            placeholder: "Loves squeaky toys",
            maxLength: 200,
            keyboard: Keyboard.Text);

        if (result is null)
        {
            return initialValue;
        }

        result = result.Trim();
        return string.IsNullOrEmpty(result) ? null : result;
    }

    private static async Task<bool> ConfirmDeleteAsync(Pet pet)
    {
        if (GetActivePage() is not Page page)
        {
            return false;
        }

        return await page.DisplayAlert(
            "Delete pet",
            $"Remove {pet.DisplayName} and their history?",
            "Delete",
            "Cancel");
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> collection, IReadOnlyList<T> items)
    {
        collection.Clear();
        for (var i = 0; i < items.Count; i++)
        {
            collection.Add(items[i]);
        }
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
