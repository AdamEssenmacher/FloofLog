using System;

namespace FloofLog.Models;

public enum RecurrenceFrequency
{
    None = 0,
    Daily,
    Weekly,
    Monthly,
    Yearly
}

public sealed class RecurrenceInfo : ObservableModel
{
    private RecurrenceFrequency _frequency;
    private int _interval = 1;
    private DateTimeOffset? _nextOccurrence;
    private DateTimeOffset? _endDate;

    public RecurrenceFrequency Frequency
    {
        get => _frequency;
        set => SetProperty(ref _frequency, value);
    }

    public int Interval
    {
        get => _interval;
        set => SetProperty(ref _interval, value < 1 ? 1 : value);
    }

    public DateTimeOffset? NextOccurrence
    {
        get => _nextOccurrence;
        set => SetProperty(ref _nextOccurrence, value);
    }

    public DateTimeOffset? EndDate
    {
        get => _endDate;
        set => SetProperty(ref _endDate, value);
    }
}
