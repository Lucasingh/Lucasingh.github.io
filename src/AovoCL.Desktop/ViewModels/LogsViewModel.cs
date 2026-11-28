namespace AovoCL.Desktop.ViewModels;

/// <summary>运行日志页：复用 HomeViewModel 的日志缓冲，提供清空与自动滚动。</summary>
public sealed class LogsViewModel : ObservableObject
{
    private bool _autoScroll = true;

    public LogsViewModel(LogBuffer log)
    {
        Log = log;
        ClearCommand = new RelayCommand(log.Clear);
    }

    public LogBuffer Log { get; }

    public RelayCommand ClearCommand { get; }

    public bool AutoScroll
    {
        get => _autoScroll;
        set => SetProperty(ref _autoScroll, value);
    }
}
