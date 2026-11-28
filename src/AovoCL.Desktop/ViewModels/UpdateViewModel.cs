using System.Collections.ObjectModel;
using Avalonia.Threading;
using AovoCL.Desktop.Services;

namespace AovoCL.Desktop.ViewModels;

/// <summary>更新下载窗口的数据：整体进度、速度与各下载线程的独立进度。</summary>
public sealed class UpdateViewModel : ObservableObject
{
    private readonly Dictionary<int, UpdateSegmentRow> _rows = new();

    private double _percent;
    private string _speedText = "—";
    private string _sizeText = "准备中";
    private string _etaText = "";
    private string _statusText = "正在选择最快的下载源…";
    private string _threadsText = "0 / 0";
    private bool _isDownloading = true;
    private bool _hasFailed;

    public UpdateViewModel(string versionText)
    {
        VersionText = versionText;
    }

    public string VersionText { get; }

    public string Heading => $"正在更新 AovoCL {VersionText}";

    public ObservableCollection<UpdateSegmentRow> Segments { get; } = new();

    public double Percent
    {
        get => _percent;
        private set
        {
            if (!SetProperty(ref _percent, value)) return;
            OnPropertyChanged(nameof(PercentText));
        }
    }

    public string PercentText => $"{_percent:0.#}%";

    public string SpeedText
    {
        get => _speedText;
        private set => SetProperty(ref _speedText, value);
    }

    public string SizeText
    {
        get => _sizeText;
        private set => SetProperty(ref _sizeText, value);
    }

    public string EtaText
    {
        get => _etaText;
        private set => SetProperty(ref _etaText, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string ThreadsText
    {
        get => _threadsText;
        private set => SetProperty(ref _threadsText, value);
    }

    public bool IsDownloading
    {
        get => _isDownloading;
        private set
        {
            if (!SetProperty(ref _isDownloading, value)) return;
            OnPropertyChanged(nameof(IsRetryVisible));
        }
    }

    public bool HasFailed
    {
        get => _hasFailed;
        private set
        {
            if (!SetProperty(ref _hasFailed, value)) return;
            OnPropertyChanged(nameof(IsRetryVisible));
            OnPropertyChanged(nameof(IsStatusAccent));
        }
    }

    public bool IsRetryVisible => _hasFailed && !_isDownloading;

    public bool IsStatusAccent => !_hasFailed;

    /// <summary>开始新一轮下载前清空上一次的线程与错误状态。</summary>
    public void Reset()
    {
        OnUi(() =>
        {
            _rows.Clear();
            Segments.Clear();
            HasFailed = false;
            IsDownloading = true;
            Percent = 0;
            SpeedText = "—";
            SizeText = "准备中";
            EtaText = "";
            ThreadsText = "0 / 0";
            StatusText = "正在选择最快的下载源…";
        });
    }

    public void ApplyProgress(DownloadProgress progress)
    {
        OnUi(() =>
        {
            Percent = progress.Percent;
            SpeedText = progress.BytesPerSecond > 0
                ? UpdateService.FormatSpeed(progress.BytesPerSecond)
                : "—";
            StatusText = $"正在下载 AovoCL {VersionText}…";

            if (progress.Total <= 0) return;

            SizeText = $"{FormatSize(progress.Received)} / {FormatSize(progress.Total)}";
            var remaining = progress.Total - progress.Received;
            EtaText = progress.BytesPerSecond > 1
                ? "剩余 " + FormatDuration(remaining / progress.BytesPerSecond)
                : "";
        });
    }

    public void ApplySegment(SegmentProgress segment)
    {
        OnUi(() =>
        {
            if (!_rows.TryGetValue(segment.Index, out var row))
            {
                row = new UpdateSegmentRow(segment.Index);
                _rows[segment.Index] = row;
                Segments.Add(row);
            }

            row.Apply(segment);
            var done = Segments.Count(item => item.IsDone);
            ThreadsText = $"{done} / {Segments.Count}";
        });
    }

    public void Complete()
    {
        OnUi(() =>
        {
            Percent = 100;
            SpeedText = "";
            EtaText = "";
            IsDownloading = false;
            StatusText = "下载完成，正在启动安装程序…";
            foreach (var row in Segments) row.MarkDone();
        });
    }

    public void Fail(string message)
    {
        OnUi(() =>
        {
            IsDownloading = false;
            HasFailed = true;
            SpeedText = "";
            EtaText = "";
            StatusText = "下载失败：" + message;
        });
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        >= 1024L * 1024 * 1024 => $"{bytes / 1024d / 1024d / 1024d:0.00} GB",
        >= 1024 * 1024 => $"{bytes / 1024d / 1024d:0.0} MB",
        >= 1024 => $"{bytes / 1024d:0} KB",
        _ => $"{bytes} B"
    };

    private static string FormatDuration(double seconds)
    {
        if (seconds < 1) return "1 秒内";
        if (seconds < 60) return $"{seconds:0} 秒";
        if (seconds < 3600) return $"{(int)(seconds / 60)} 分 {seconds % 60:0} 秒";
        return $"{(int)(seconds / 3600)} 小时 {(int)(seconds % 3600 / 60)} 分";
    }

    private static void OnUi(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess()) action();
        else Dispatcher.UIThread.Post(action);
    }
}

/// <summary>单个下载线程的进度行。</summary>
public sealed class UpdateSegmentRow : ObservableObject
{
    private double _percent;
    private string _speedText = "";
    private bool _isDone;

    public UpdateSegmentRow(int index)
    {
        Label = $"{index + 1:00}";
    }

    public string Label { get; }

    public double Percent
    {
        get => _percent;
        private set
        {
            if (!SetProperty(ref _percent, value)) return;
            OnPropertyChanged(nameof(PercentText));
        }
    }

    public string PercentText => $"{_percent:0}%";

    public string SpeedText
    {
        get => _speedText;
        private set => SetProperty(ref _speedText, value);
    }

    public bool IsDone
    {
        get => _isDone;
        private set => SetProperty(ref _isDone, value);
    }

    public string StateText => _isDone ? "完成" : _speedText;

    public void Apply(SegmentProgress segment)
    {
        Percent = segment.Percent;
        SpeedText = segment.Completed || segment.BytesPerSecond <= 0
            ? ""
            : UpdateService.FormatSpeed(segment.BytesPerSecond);
        IsDone = segment.Completed;
        OnPropertyChanged(nameof(StateText));
    }

    public void MarkDone()
    {
        Percent = 100;
        SpeedText = "";
        IsDone = true;
        OnPropertyChanged(nameof(StateText));
    }
}
