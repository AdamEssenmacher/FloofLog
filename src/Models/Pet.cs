using System;

namespace FloofLog.Models;

public sealed class Pet : ObservableModel
{
    private Guid _id = Guid.NewGuid();
    private string _displayName = string.Empty;
    private string? _notes;
    private DateTimeOffset _createdAt = DateTimeOffset.UtcNow;
    private DateTimeOffset? _updatedAt;
    private DateTimeOffset? _archivedAt;

    public Guid Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    public string? Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    public DateTimeOffset CreatedAt
    {
        get => _createdAt;
        set => SetProperty(ref _createdAt, value);
    }

    public DateTimeOffset? UpdatedAt
    {
        get => _updatedAt;
        set => SetProperty(ref _updatedAt, value);
    }

    public DateTimeOffset? ArchivedAt
    {
        get => _archivedAt;
        set => SetProperty(ref _archivedAt, value);
    }
}
