using System.Collections.ObjectModel;
using Avalonia.Threading;
using AovoCL.Core;
using AovoCL.Desktop.Services;

namespace AovoCL.Desktop.ViewModels;

/// <summary>在线清单条目，带下载进度。</summary>
public sealed class RemoteVersionItem : ObservableObject
{
    private bool _installed;
    private bool _downloading;
    private double _percent;
    private string _statusText = "";

    public RemoteVersionItem(VersionEntry entry, bool installed)
    {
        Entry = entry;
        _installed = installed;
    }

    public VersionEntry Entry { get; }

    public string Id => Entry.Id;

    public string TypeLabel => Entry.Type switch
    {
        "release" => "正式版",
        "snapshot" => "快照",
        "old_beta" => "Beta",
        "old_alpha" => "Alpha",
        _ => Entry.Type
    };

    public string Subtitle => $"{TypeLabel} · {Entry.ReleaseTime.ToLocalTime():yyyy-MM-dd}";

    public bool IsInstalled
    {
        get => _installed;
        set => SetProperty(ref _installed, value);
    }

    public bool IsDownloading
    {
        get => _downloading;
        set => SetProperty(ref _downloading, value);
    }

    public double Percent
    {
        get => _percent;
        set => SetProperty(ref _percent, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }
}

public enum InstallStageViewState
{
    Waiting,
    InProgress,
    Completed,
    Cancelled,
    Failed
}

/// <summary>安装总览中的单个阶段行。</summary>
public sealed class InstallStageItem : ObservableObject
{
    private double _percent;
    private InstallStageViewState _state = InstallStageViewState.Waiting;

    public InstallStageItem(InstallStage stage, string name, string activeLabel)
    {
        Stage = stage;
        Name = name;
        ActiveLabel = activeLabel;
    }

    public InstallStage Stage { get; }
    public string Name { get; }
    public string ActiveLabel { get; }
    public bool IsDownloadStage => Stage is InstallStage.GameFiles or InstallStage.Assets;

    public double Percent
    {
        get => _percent;
        private set
        {
            if (!SetProperty(ref _percent, value)) return;
            OnPropertyChanged(nameof(StatusText));
        }
    }

    public InstallStageViewState State
    {
        get => _state;
        private set
        {
            if (!SetProperty(ref _state, value)) return;
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(IsWaiting));
            OnPropertyChanged(nameof(IsActive));
            OnPropertyChanged(nameof(IsCompleted));
            OnPropertyChanged(nameof(IsCancelled));
            OnPropertyChanged(nameof(IsFailed));
        }
    }

    public string StatusText => State switch
    {
        InstallStageViewState.Waiting => "等待",
        InstallStageViewState.InProgress when IsDownloadStage => $"{Percent:0}%",
        InstallStageViewState.InProgress => "进行中",
        InstallStageViewState.Completed => "完成",
        InstallStageViewState.Cancelled => "已取消",
        _ => "失败"
    };

    public bool IsWaiting => State == InstallStageViewState.Waiting;
    public bool IsActive => State == InstallStageViewState.InProgress;
    public bool IsCompleted => State == InstallStageViewState.Completed;
    public bool IsCancelled => State == InstallStageViewState.Cancelled;
    public bool IsFailed => State == InstallStageViewState.Failed;

    public void Start()
    {
        Percent = 0;
        State = InstallStageViewState.InProgress;
    }

    public void Update(double percent) => Percent = Math.Clamp(percent, 0, 100);

    public void Complete()
    {
        Percent = 100;
        State = InstallStageViewState.Completed;
    }

    public void Cancel()
    {
        if (IsActive) State = InstallStageViewState.Cancelled;
    }

    public void Fail()
    {
        if (IsActive) State = InstallStageViewState.Failed;
    }
}

public enum InstallTaskState
{
    Running,
    Completed,
    Cancelled,
    Failed
}

/// <summary>当前安装任务；总进度按四个阶段等权汇总。</summary>
public sealed class InstallTaskViewModel : ObservableObject
{
    private double _percent;
    private string _statusText;
    private string _detailText = "";
    private string _speedText = "—";
    private string _remainingText = "—";
    private string _currentFile = "";
    private InstallTaskState _state = InstallTaskState.Running;
    private int _activeStageIndex;

    public InstallTaskViewModel(string versionId)
    {
        VersionId = versionId;
        Stages = new[]
        {
            new InstallStageItem(InstallStage.Resolving, "解析版本与 JSON", "解析版本与 JSON…"),
            new InstallStageItem(InstallStage.GameFiles, "下载游戏文件", "下载游戏文件…"),
            new InstallStageItem(InstallStage.Assets, "下载资源文件", "下载资源文件…"),
            new InstallStageItem(InstallStage.Finalizing, "安装收尾", "安装收尾…")
        };
        Stages[0].Start();
        _statusText = Stages[0].ActiveLabel;
    }

    public string VersionId { get; }
    public string Title => $"{VersionId} 安装";
    public IReadOnlyList<InstallStageItem> Stages { get; }

    public double Percent
    {
        get => _percent;
        private set
        {
            if (!SetProperty(ref _percent, value)) return;
            OnPropertyChanged(nameof(PercentText));
        }
    }

    public string PercentText => $"{Percent:0.00}%";

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string DetailText
    {
        get => _detailText;
        private set
        {
            if (!SetProperty(ref _detailText, value)) return;
            OnPropertyChanged(nameof(HasDetail));
        }
    }

    public bool HasDetail => DetailText.Length > 0;

    public string SpeedText
    {
        get => _speedText;
        private set => SetProperty(ref _speedText, value);
    }

    public string RemainingText
    {
        get => _remainingText;
        private set => SetProperty(ref _remainingText, value);
    }

    public string CurrentFile
    {
        get => _currentFile;
        private set
        {
            if (!SetProperty(ref _currentFile, value)) return;
            OnPropertyChanged(nameof(HasCurrentFile));
        }
    }

    public bool HasCurrentFile => CurrentFile.Length > 0;

    public InstallTaskState State
    {
        get => _state;
        private set
        {
            if (!SetProperty(ref _state, value)) return;
            OnPropertyChanged(nameof(IsRunning));
            OnPropertyChanged(nameof(IsCompleted));
            OnPropertyChanged(nameof(IsCancelled));
            OnPropertyChanged(nameof(IsFailed));
        }
    }

    public bool IsRunning => State == InstallTaskState.Running;
    public bool IsCompleted => State == InstallTaskState.Completed;
    public bool IsCancelled => State == InstallTaskState.Cancelled;
    public bool IsFailed => State == InstallTaskState.Failed;

    public void Apply(InstallProgress report)
    {
        if (!IsRunning) return;

        var stageIndex = (int)report.Stage;
        if (stageIndex < _activeStageIndex) return;

        var stage = Stages[stageIndex];
        if (report.Status == InstallStageStatus.InProgress)
        {
            if (stage.IsCompleted) return;
            if (!stage.IsActive)
            {
                stage.Start();
                _activeStageIndex = stageIndex;
                StatusText = stage.ActiveLabel;
                ClearTransfer();
            }

            stage.Update(report.StagePercent);
            UpdateOverall(stageIndex, report.StagePercent);

            if (stage.IsDownloadStage)
            {
                SpeedText = report.SpeedBytesPerSecond > 0 ? FormatSpeed(report.SpeedBytesPerSecond) : "—";
                RemainingText = report.FilesTotal > 0
                    ? Math.Max(0, report.FilesTotal - report.FilesDone).ToString()
                    : "—";
                CurrentFile = report.CurrentFile;
            }
        }
        else
        {
            stage.Complete();
            _activeStageIndex = stageIndex;
            UpdateOverall(stageIndex, 100);
            StatusText = $"{stage.Name}完成";
            ClearTransfer();
        }
    }

    public void MarkCompleted()
    {
        foreach (var stage in Stages) stage.Complete();
        Percent = 100;
        StatusText = "安装完成";
        DetailText = "";
        State = InstallTaskState.Completed;
        ClearTransfer();
    }

    public void MarkCancelled()
    {
        State = InstallTaskState.Cancelled;
        StatusText = "安装已取消";
        DetailText = "";
        foreach (var stage in Stages) stage.Cancel();
        ClearTransfer();
    }

    public void MarkFailed(string message)
    {
        State = InstallTaskState.Failed;
        StatusText = "安装失败";
        DetailText = message;
        foreach (var stage in Stages) stage.Fail();
        ClearTransfer();
    }

    private void UpdateOverall(int stageIndex, double stagePercent)
    {
        var overall = (stageIndex + Math.Clamp(stagePercent, 0, 100) / 100d) * 25d;
        Percent = Math.Max(Percent, overall);
    }

    private void ClearTransfer()
    {
        SpeedText = "—";
        RemainingText = "—";
        CurrentFile = "";
    }

    internal static string FormatSpeed(double bytesPerSecond) =>
        bytesPerSecond switch
        {
            >= 1L << 20 => $"{bytesPerSecond / (1L << 20):0.0} MB/s",
            >= 1L << 10 => $"{bytesPerSecond / (1L << 10):0} KB/s",
            _ => $"{bytesPerSecond:0} B/s"
        };
}

/// <summary>本地已安装版本，可改名用于重命名。</summary>
public sealed class LocalVersionItem : ObservableObject
{
    private string _editName;
    private bool _isBusy;
    private bool _isRenaming;

    public LocalVersionItem(InstalledVersion version)
    {
        Id = version.Id;
        DirectoryPath = version.DirectoryPath;
        _editName = version.Id;
        TypeLabel = version.Type switch
        {
            "release" => "正式版",
            "snapshot" => "快照",
            "old_beta" => "Beta",
            "old_alpha" => "Alpha",
            _ => "整合 / 模组"
        };
        SizeText = FormatSize(version.SizeBytes);
        ModifiedText = version.Modified.ToString("yyyy-MM-dd HH:mm");
    }

    public string Id { get; }
    public string DirectoryPath { get; }
    public string TypeLabel { get; }
    public string SizeText { get; }
    public string ModifiedText { get; }

    public string Summary => $"{TypeLabel} · {SizeText} · {ModifiedText}";

    public string EditName
    {
        get => _editName;
        set => SetProperty(ref _editName, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    /// <summary>为 true 时显示改名输入框，默认收起。</summary>
    public bool IsRenaming
    {
        get => _isRenaming;
        set
        {
            if (!SetProperty(ref _isRenaming, value)) return;
            OnPropertyChanged(nameof(RenameButtonText));
        }
    }

    public string RenameButtonText => IsRenaming ? "确定" : "重命名";

    public AsyncParamCommand? RenameCommand { get; init; }
    public AsyncParamCommand? DeleteCommand { get; init; }
    public AsyncParamCommand? OpenFolderCommand { get; init; }

    public void ResetEditName() => EditName = Id;

    private static string FormatSize(long bytes) => bytes switch
    {
        >= 1L << 30 => $"{bytes / (double)(1L << 30):0.0} GB",
        >= 1L << 20 => $"{bytes / (double)(1L << 20):0.0} MB",
        _ => $"{bytes / 1024d:0} KB"
    };
}

/// <summary>下载页资源分类：原版游戏可用，其余为后续 Forge / 模组等资源预留。</summary>
public sealed class ResourceKindItem
{
    public ResourceKindItem(string name, bool isAvailable)
    {
        Name = name;
        IsAvailable = isAvailable;
    }

    public string Name { get; }
    public bool IsAvailable { get; }
}

/// <summary>安装加载器占位项，后续接入 Forge / Fabric 等实际安装。</summary>
public sealed class LoaderOptionItem
{
    public LoaderOptionItem(string name, string status)
    {
        Name = name;
        Status = status;
    }

    public string Name { get; }
    public string Status { get; }
}

/// <summary>下载页：在线版本下载 + 本地版本管理（重命名 / 删除 / 打开目录）。</summary>
public sealed class DownloadViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private readonly StatusBarViewModel _status;
    private readonly List<RemoteVersionItem> _allRemote = new();

    private CancellationTokenSource? _cts;
    private InstallTaskViewModel? _currentTask;
    private RemoteVersionItem? _selectedVersion;
    private string _loadedRoot = "";
    private string _search = "";
    private int _selectedResourceIndex;

    public DownloadViewModel(AppSettings settings, StatusBarViewModel status)
    {
        _settings = settings;
        _status = status;

        ResourceKinds = new List<ResourceKindItem>
        {
            new("原版游戏", true),
            new("Mod", false),
            new("整合包", false),
            new("数据包", false),
            new("资源包", false),
            new("光影包", false)
        };

        LoaderOptions = new List<LoaderOptionItem>
        {
            new("Forge", "即将支持"),
            new("NeoForge", "即将支持"),
            new("Fabric", "即将支持"),
            new("OptiFine", "即将支持")
        };

        CategoryFilter.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(VersionCategoryFilter.SelectedIndex)) ApplyFilter();
        };

        RefreshCommand = new AsyncCommand(() => RefreshAsync(true));
        RefreshLocalCommand = new AsyncCommand(() => { LoadLocal(); return Task.CompletedTask; });
        StartDownloadCommand = new AsyncCommand(
            async () => await DownloadAsync(SelectedVersion),
            () => SelectedVersion != null && _cts == null);
        CancelDownloadCommand = new AsyncCommand(() =>
        {
            CancelActive();
            return Task.CompletedTask;
        });
        RenameCommand = new AsyncParamCommand(p => RenameAsync(p as LocalVersionItem));
        DeleteCommand = new AsyncParamCommand(p => DeleteAsync(p as LocalVersionItem));
        OpenFolderCommand = new AsyncParamCommand(p =>
        {
            OpenFolder(p as LocalVersionItem);
            return Task.CompletedTask;
        });
    }

    /// <summary>由主窗口注入，删除前弹出确认框。</summary>
    public Func<string, string, Task<bool>>? ConfirmAsync { get; set; }

    /// <summary>本地版本发生变化（下载 / 重命名 / 删除）后通知启动页刷新安装标记。</summary>
    public Action? LibraryChanged { get; set; }

    /// <summary>在线版本分类菜单（正式版 / 快照 / Beta / Alpha）。</summary>
    public VersionCategoryFilter CategoryFilter { get; } = new();

    /// <summary>资源类型：原版游戏可下载，社区资源为后续功能预留。</summary>
    public IReadOnlyList<ResourceKindItem> ResourceKinds { get; }

    /// <summary>安装加载器占位列表（Forge / NeoForge / Fabric / OptiFine）。</summary>
    public IReadOnlyList<LoaderOptionItem> LoaderOptions { get; }

    public ObservableCollection<RemoteVersionItem> RemoteVersions { get; } = new();
    public ObservableCollection<LocalVersionItem> LocalVersions { get; } = new();

    public AsyncCommand RefreshCommand { get; }
    public AsyncCommand RefreshLocalCommand { get; }
    public AsyncCommand StartDownloadCommand { get; }
    public AsyncCommand CancelDownloadCommand { get; }
    public AsyncParamCommand RenameCommand { get; }
    public AsyncParamCommand DeleteCommand { get; }
    public AsyncParamCommand OpenFolderCommand { get; }

    public int SelectedResourceIndex
    {
        get => _selectedResourceIndex;
        set
        {
            if (value < 0 || value >= ResourceKinds.Count) return;
            if (!SetProperty(ref _selectedResourceIndex, value)) return;
            OnPropertyChanged(nameof(IsVanillaSelected));
            OnPropertyChanged(nameof(IsComingSoonSelected));
            OnPropertyChanged(nameof(ComingSoonTitle));
        }
    }

    public bool IsVanillaSelected => SelectedResourceIndex == 0;

    public bool IsComingSoonSelected => SelectedResourceIndex > 0;

    public string ComingSoonTitle => $"{ResourceKinds[SelectedResourceIndex].Name}即将支持";

    /// <summary>当前选择的在线版本，由「开始下载」统一安装。</summary>
    public RemoteVersionItem? SelectedVersion
    {
        get => _selectedVersion;
        set
        {
            if (!SetProperty(ref _selectedVersion, value)) return;
            OnPropertyChanged(nameof(SelectedVersionText));
            OnPropertyChanged(nameof(SelectedFolderText));
            OnPropertyChanged(nameof(HasSelectedVersion));
            StartDownloadCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>是否已选定版本，用于决定「已选 / 加载器 / 开始下载」区域是否展示。</summary>
    public bool HasSelectedVersion => SelectedVersion != null;

    public string SelectedVersionText => SelectedVersion?.Id ?? "未选择版本";

    public string SelectedFolderText
    {
        get
        {
            var root = _settings.Context.Root;
            var name = System.IO.Path.GetFileName(
                root.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar));
            return string.IsNullOrEmpty(name) ? root : name;
        }
    }

    public string Search
    {
        get => _search;
        set
        {
            if (!SetProperty(ref _search, value)) return;
            ApplyFilter();
        }
    }

    public string RemoteFilterText =>
        $"{CategoryFilter.CurrentLabel} · 显示 {RemoteVersions.Count} / {_allRemote.Count} 个版本";

    public string LocalCountText => $"共 {LocalVersions.Count} 个本地版本";

    public InstallTaskViewModel? CurrentTask
    {
        get => _currentTask;
        private set
        {
            if (!SetProperty(ref _currentTask, value)) return;
            OnPropertyChanged(nameof(HasTask));
        }
    }

    public bool HasTask => CurrentTask != null;

    public void OnActivated()
    {
        if (!string.Equals(_loadedRoot, _settings.Context.Root, StringComparison.OrdinalIgnoreCase) || _allRemote.Count == 0)
            _ = RefreshAsync(false);
        else
            LoadLocal();
    }

    public async Task RefreshAsync(bool refreshManifest)
    {
        _status.SetInfo("正在获取版本清单…");

        try
        {
            var entries = await _settings.Context.Service.GetVersionsAsync(refreshManifest);
            var launcher = _settings.Context.Service;

            var keepId = SelectedVersion?.Id;
            _allRemote.Clear();
            foreach (var entry in entries)
                _allRemote.Add(new RemoteVersionItem(entry, launcher.IsInstalled(entry.Id)));

            _loadedRoot = _settings.Context.Root;
            UpdateCategoryLabels();
            ApplyFilter();
            SelectedVersion = keepId == null
                ? null
                : RemoteVersions.FirstOrDefault(item => string.Equals(item.Id, keepId, StringComparison.OrdinalIgnoreCase));
            LoadLocal();
            _status.SetSuccess($"可下载 {entries.Count} 个版本，本地 {LocalVersions.Count} 个");
        }
        catch (Exception ex)
        {
            _status.SetError("获取版本清单失败：" + ex.Message);
        }
    }

    /// <summary>按清单 type 刷新分类数量，例如「正式版 612」。</summary>
    private void UpdateCategoryLabels() =>
        CategoryFilter.RefreshCounts(_allRemote.Select(item => item.Entry.Type));

    private void ApplyFilter()
    {
        var keyword = _search.Trim();

        RemoteVersions.Clear();
        foreach (var item in _allRemote)
        {
            if (!CategoryFilter.Accepts(item.Entry.Type)) continue;
            if (keyword.Length > 0 && !item.Id.Contains(keyword, StringComparison.OrdinalIgnoreCase)) continue;
            RemoteVersions.Add(item);
        }

        OnPropertyChanged(nameof(RemoteFilterText));

        if (SelectedVersion != null && !RemoteVersions.Contains(SelectedVersion))
            SelectedVersion = null;
    }

    private void LoadLocal()
    {
        var launcher = _settings.Context.Service;
        LocalVersions.Clear();
        foreach (var version in launcher.LocalVersions.Scan())
            LocalVersions.Add(new LocalVersionItem(version)
            {
                RenameCommand = RenameCommand,
                DeleteCommand = DeleteCommand,
                OpenFolderCommand = OpenFolderCommand
            });

        var installed = LocalVersions.Select(v => v.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var item in _allRemote)
            item.IsInstalled = installed.Contains(item.Id);

        OnPropertyChanged(nameof(LocalCountText));
    }

    private async Task DownloadAsync(RemoteVersionItem? item)
    {
        if (item == null || item.IsDownloading || _cts != null) return;

        _cts = new CancellationTokenSource();
        StartDownloadCommand.RaiseCanExecuteChanged();
        var task = new InstallTaskViewModel(item.Id);
        CurrentTask = task;
        item.IsDownloading = true;
        item.Percent = 0;
        item.StatusText = "准备中…";
        _status.SetProgress(0, $"准备下载 {item.Id}");

        var transfer = new TransferProgress();

        try
        {
            var launcher = _settings.Context.Service;
            await launcher.Installer.InstallAsync(
                item.Id,
                item.Entry.Url,
                transfer,
                onProgress: null,
                line => _status.SetInfo(line),
                _cts.Token,
                onInstallProgress: report => Dispatcher.UIThread.Post(() =>
                {
                    if (!ReferenceEquals(CurrentTask, task) || !task.IsRunning) return;

                    task.Apply(report);
                    item.Percent = task.Percent;
                    item.StatusText = $"{task.StatusText} · {task.Percent:0}%";
                    _status.SetProgress(task.Percent, $"{item.Id} · {task.StatusText}");
                    _status.SetSpeed(report.SpeedBytesPerSecond > 0
                        ? $"↓ {InstallTaskViewModel.FormatSpeed(report.SpeedBytesPerSecond)}"
                        : "");
                }));

            task.MarkCompleted();
            item.IsInstalled = true;
            item.StatusText = "安装完成";
            _status.SetSuccess($"{item.Id} 安装完成");
            LoadLocal();
            LibraryChanged?.Invoke();
        }
        catch (OperationCanceledException)
        {
            task.MarkCancelled();
            item.StatusText = "已取消";
            _status.SetIdle($"{item.Id} 安装已取消");
        }
        catch (Exception ex)
        {
            task.MarkFailed(ex.Message);
            item.StatusText = "失败：" + ex.Message;
            _status.SetError($"{item.Id} 安装失败：{ex.Message}");
        }
        finally
        {
            item.IsDownloading = false;
            _cts?.Dispose();
            _cts = null;
            StartDownloadCommand.RaiseCanExecuteChanged();
        }
    }

    private Task RenameAsync(LocalVersionItem? item)
    {
        if (item == null) return Task.CompletedTask;

        // 首次点击只展开输入框，再次点击才真正改名
        if (!item.IsRenaming)
        {
            item.ResetEditName();
            item.IsRenaming = true;
            return Task.CompletedTask;
        }

        var newName = item.EditName.Trim();
        if (string.Equals(newName, item.Id, StringComparison.Ordinal))
        {
            item.IsRenaming = false;
            _status.SetInfo("版本名未变化");
            return Task.CompletedTask;
        }

        if (!VersionManager.IsValidName(newName))
        {
            _status.SetError("版本名非法：不能为空，且不能包含 \\ / : * ? \" < > |");
            item.ResetEditName();
            return Task.CompletedTask;
        }

        try
        {
            _settings.Context.Service.LocalVersions.Rename(item.Id, newName);
            _status.SetSuccess($"已将 {item.Id} 重命名为 {newName}");
            LoadLocal();
            LibraryChanged?.Invoke();
        }
        catch (Exception ex)
        {
            _status.SetError("重命名失败：" + ex.Message);
            item.ResetEditName();
        }

        return Task.CompletedTask;
    }

    private async Task DeleteAsync(LocalVersionItem? item)
    {
        if (item == null) return;

        var confirmed = ConfirmAsync == null ||
                        await ConfirmAsync("删除版本", $"确定要删除版本 {item.Id} 吗？\n版本目录及其中的文件会被永久删除。");
        if (!confirmed) return;

        try
        {
            _settings.Context.Service.LocalVersions.Delete(item.Id);
            _status.SetSuccess($"已删除版本 {item.Id}");
            LoadLocal();
            LibraryChanged?.Invoke();
        }
        catch (Exception ex)
        {
            _status.SetError("删除失败：" + ex.Message);
        }
    }

    private void OpenFolder(LocalVersionItem? item)
    {
        if (item == null) return;
        try
        {
            _settings.Context.Service.LocalVersions.OpenFolder(item.Id);
            _status.SetInfo($"已打开 {item.DirectoryPath}");
        }
        catch (Exception ex)
        {
            _status.SetError("打开目录失败：" + ex.Message);
        }
    }

    /// <summary>取消当前下载（离开下载页或关闭窗口时调用）。</summary>
    public void CancelActive()
    {
        try { _cts?.Cancel(); } catch { /* 已释放 */ }
    }

}
