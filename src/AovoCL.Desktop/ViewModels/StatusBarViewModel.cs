using Avalonia.Threading;

namespace AovoCL.Desktop.ViewModels;

/// <summary>底部状态栏：当前操作、详情、速度与总体进度。</summary>
public sealed class StatusBarViewModel : ObservableObject
{
    private string _text = "就绪";
    private string _detail = "";
    private string _speed = "";
    private double _progress;
    private bool _isProgressVisible;

    public string Text
    {
        get => _text;
        private set => SetProperty(ref _text, value);
    }

    public string Detail
    {
        get => _detail;
        private set => SetProperty(ref _detail, value);
    }

    public string Speed
    {
        get => _speed;
        private set
        {
            if (!SetProperty(ref _speed, value)) return;
            OnPropertyChanged(nameof(HasSpeed));
        }
    }

    public bool HasSpeed => _speed.Length > 0;

    public double Progress
    {
        get => _progress;
        private set
        {
            if (!SetProperty(ref _progress, value)) return;
            OnPropertyChanged(nameof(ProgressText));
        }
    }

    public string ProgressText => $"{_progress:0}%";

    public bool IsProgressVisible
    {
        get => _isProgressVisible;
        private set => SetProperty(ref _isProgressVisible, value);
    }

    public void SetInfo(string text, string? detail = null)
    {
        if (detail != null) OnUi(() => { Text = text; Detail = detail; });
        else OnUi(() => Text = text);
    }

    public void SetProgress(double percent, string? detail = null)
    {
        OnUi(() =>
        {
            Progress = Math.Clamp(percent, 0, 100);
            IsProgressVisible = true;
            if (detail != null) Detail = detail;
        });
    }

    public void SetSpeed(string text) => OnUi(() => Speed = text);

    /// <summary>任务结束（取消 / 进程退出）后回到空闲态，收起进度与详情。</summary>
    public void SetIdle(string text) => OnUi(() =>
    {
        Text = text;
        Detail = "";
        Speed = "";
        Progress = 0;
        IsProgressVisible = false;
    });

    public void SetSuccess(string text) => OnUi(() =>
    {
        Text = text;
        Detail = "";
        Speed = "";
        Progress = 100;
        IsProgressVisible = false;
    });

    public void SetError(string text) => OnUi(() =>
    {
        Text = text;
        Detail = "";
        Speed = "";
        IsProgressVisible = false;
    });

    private static void OnUi(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess()) action();
        else Dispatcher.UIThread.Post(action);
    }
}
