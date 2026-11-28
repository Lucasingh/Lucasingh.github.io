using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Avalonia.Threading;
using AovoCL.Core;
using AovoCL.Core.VersionManagement;
using AovoCL.Desktop.Services;

namespace AovoCL.Desktop.ViewModels;

/// <summary>启动页：版本选择（新版自动分类/收藏/隔离/多文件夹）+ 离线启动 + 进度与日志。</summary>
public sealed class HomeViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private readonly StatusBarViewModel _status;
    private ExtendedVersion? _selectedVersion;
    private string _filter = "";
    private CategoryFilter _categoryFilter = CategoryFilter.All;
    private string _statusText = "选择版本后即可启动";
    private double _progressPercent;
    private bool _isBusy;
    private bool _isLoading;
    private RunningGame? _running;
    private CancellationTokenSource? _cts;
    private string _loadedFolderId = "";

    public HomeViewModel(AppSettings settings, StatusBarViewModel status)
    {
        _settings = settings;
        _status = status;

        // 分类筛选下拉
        CategoryFilterOptions = new ObservableCollection<string> { "全部", "收藏", "模组", "原版类", "其他" };

        RefreshCommand = new AsyncCommand(() => RefreshAsync(true));
        StartCommand = new AsyncCommand(ToggleLaunchAsync, () => SelectedVersion != null);
        ToggleLogCommand = new RelayCommand(() => Log.IsVisible = !Log.IsVisible);
        ClearLogCommand = new RelayCommand(Log.Clear);

        // 文件夹与版本卡片操作
        AddFolderCommand = new AsyncCommand(AddFolderAsync);
        RemoveFolderCommand = new AsyncParamCommand(p => RemoveFolderAsync(p as GameFolder));
        ToggleFavoriteCommand = new AsyncParamCommand(p => ToggleFavoriteAsync(p as ExtendedVersion));
        ToggleIsolationCommand = new AsyncParamCommand(p => ToggleIsolationAsync(p as ExtendedVersion));
        DeleteVersionCommand = new AsyncParamCommand(p => DeleteVersionAsync(p as ExtendedVersion));
        OpenVersionFolderCommand = new AsyncParamCommand(p => OpenVersionFolderAsync(p as ExtendedVersion));
        LaunchVersionCommand = new AsyncParamCommand(p => LaunchSpecificAsync(p as ExtendedVersion));

        _settings.PropertyChanged += OnSettingsChanged;

        // 活动文件夹变化 → 重新扫描
        _settings.VersionHub.Folders.ActiveFolderChanged += (_, folder) =>
        {
            _ = RefreshAsync(false);
        };
    }

    // ===== 公共属性 =====

    public AppSettings Settings => _settings;
    public LogBuffer Log { get; } = new();

    /// <summary>左侧文件夹选择：所有绑定的游戏文件夹。</summary>
    public ObservableCollection<GameFolder> GameFolders { get; } = new();

    /// <summary>当前活动文件夹（ComboBox 双向绑定）。</summary>
    public GameFolder? ActiveFolder
    {
        get => _activeFolder;
        set
        {
            if (ReferenceEquals(_activeFolder, value)) return;
            _activeFolder = value;
            if (value != null) _settings.VersionHub.Folders.SetActive(value.Id);
            OnPropertyChanged();
        }
    }
    private GameFolder? _activeFolder;

    /// <summary>分类筛选选项（全部 / 收藏 / 模组 / 原版类 / 其他）。</summary>
    public CategoryFilter CategoryFilterValue
    {
        get => _categoryFilter;
        set
        {
            if (SetProperty(ref _categoryFilter, value))
            {
                ApplyFilter();
                OnPropertyChanged(nameof(CategoryFilterLabel));
            }
        }
    }

    public string CategoryFilterLabel => CategoryFilterValue switch
    {
        AovoCL.Core.VersionManagement.CategoryFilter.All => "全部",
        AovoCL.Core.VersionManagement.CategoryFilter.Favorite => "收藏",
        AovoCL.Core.VersionManagement.CategoryFilter.Modded => "模组",
        AovoCL.Core.VersionManagement.CategoryFilter.OriginalLike => "原版类",
        AovoCL.Core.VersionManagement.CategoryFilter.Other => "其他",
        _ => "全部"
    };

    public ObservableCollection<ExtendedVersion> Versions { get; } = new();

    public AsyncCommand RefreshCommand { get; }
    public AsyncCommand StartCommand { get; }
    public RelayCommand ToggleLogCommand { get; }
    public RelayCommand ClearLogCommand { get; }

    // ===== 文件夹与版本卡片操作 =====
    public AsyncCommand AddFolderCommand { get; }
    public AsyncParamCommand RemoveFolderCommand { get; }
    public AsyncParamCommand ToggleFavoriteCommand { get; }
    public AsyncParamCommand ToggleIsolationCommand { get; }
    public AsyncParamCommand DeleteVersionCommand { get; }
    public AsyncParamCommand OpenVersionFolderCommand { get; }
    public AsyncParamCommand LaunchVersionCommand { get; }

    /// <summary>由 MainWindow 注入：弹出文件夹选择器。</summary>
    public Func<Task<string?>>? PickFolderAsync { get; set; }

    /// <summary>由 MainWindow 注入：二次确认弹窗。</summary>
    public Func<string, string, Task<bool>>? ConfirmAsync { get; set; }

    // ===== 分类筛选下拉 =====
    public ObservableCollection<string> CategoryFilterOptions { get; }

    public int CategoryFilterSelectedIndex
    {
        get => (int)_categoryFilter;
        set
        {
            if (value < 0 || value > 4) return;
            _categoryFilter = (CategoryFilter)value;
            ApplyFilter();
            OnPropertyChanged();
        }
    }

    public string Filter
    {
        get => _filter;
        set
        {
            if (!SetProperty(ref _filter, value)) return;
            ApplyFilter();
        }
    }

    public ExtendedVersion? SelectedVersion
    {
        get => _selectedVersion;
        set
        {
            if (ReferenceEquals(_selectedVersion, value)) return;
            _selectedVersion = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CurrentVersionId));
            OnPropertyChanged(nameof(CurrentVersionSubtitle));
            OnPropertyChanged(nameof(CurrentStatusBadge));
            OnPropertyChanged(nameof(CurrentIsolationBadge));
            OnPropertyChanged(nameof(HasCurrentStatusBadge));
            OnPropertyChanged(nameof(HasCurrentIsolationBadge));
            StartCommand.RaiseCanExecuteChanged();
        }
    }

    public string CurrentVersionId => SelectedVersion?.Name ?? "未选择版本";

    public string CurrentVersionSubtitle
    {
        get
        {
            if (SelectedVersion == null) return "还没有已安装的版本";
            var loader = LoaderLabel(SelectedVersion.Loader);
            var type = TypeLabel(SelectedVersion.Type);
            var modCount = SelectedVersion.ModCount > 0 ? $" · {SelectedVersion.ModCount} 模组" : "";
            return $"{type} · {loader}{modCount}";
        }
    }

    public string CurrentStatusBadge => SelectedVersion?.Status switch
    {
        VersionStatus.Error => "错误",
        VersionStatus.MissingAssets => "缺失资源",
        VersionStatus.MissingLibraries => "缺失依赖",
        _ => ""
    };

    public string CurrentIsolationBadge =>
        SelectedVersion?.IsIsolatedResolved == true
            ? "已隔离"
            : "";

    public bool HasCurrentStatusBadge => !string.IsNullOrEmpty(CurrentStatusBadge);
    public bool HasCurrentIsolationBadge => !string.IsNullOrEmpty(CurrentIsolationBadge);

    public bool IsVersionListEmpty => Versions.Count == 0 && !IsBusy;

    public bool ActiveFolderInvalid =>
        _settings.VersionHub.ActiveFolder?.IsValidDirectory() != true;

    public string EmptyTitle => ActiveFolderInvalid
        ? "这个文件夹里没有 versions 目录"
        : "这个文件夹里还没有版本";

    public string EmptyDetail => ActiveFolderInvalid
        ? "点右上角「添加文件夹」，选择你真正的 Minecraft 游戏目录（含 versions 文件夹）。"
        : "去「下载」页安装一个版本，或切换到其他游戏文件夹。";

    public bool IsLoading => _isLoading;

    public string JavaSummary => string.IsNullOrWhiteSpace(_settings.JavaPath)
        ? "自动检测 / 自动下载"
        : Path.GetFileName(_settings.JavaPath);

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public double ProgressPercent
    {
        get => _progressPercent;
        private set => SetProperty(ref _progressPercent, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public bool IsRunning => _running is { HasExited: false };

    public string StartButtonText => IsRunning ? "结束游戏" : "启动游戏";

    // ===== 进入/离开 =====

    /// <summary>进入启动页时调用：自动扫描活动文件夹版本列表。</summary>
    public void OnActivated()
    {
        RefreshFolderList();
        ActiveFolder = _settings.VersionHub.ActiveFolder;

        // 扫描（如果文件夹没变过则跳过）
        if (!string.Equals(_loadedFolderId, _settings.VersionHub.ActiveFolder?.Id, StringComparison.OrdinalIgnoreCase))
            _ = RefreshAsync(false);
    }

    private void RefreshFolderList()
    {
        GameFolders.Clear();
        foreach (var f in _settings.VersionHub.Folders.Folders)
            GameFolders.Add(f);
        OnPropertyChanged(nameof(ActiveFolder));
        OnPropertyChanged(nameof(ActiveFolderInvalid));
        OnPropertyChanged(nameof(EmptyTitle));
        OnPropertyChanged(nameof(EmptyDetail));
    }

    // ===== 文件夹管理 =====

    private async Task AddFolderAsync()
    {
        if (PickFolderAsync == null) return;
        var path = await PickFolderAsync();
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            var folder = _settings.VersionHub.Folders.Add(path);
            RefreshFolderList();
            _settings.VersionHub.Folders.SetActive(folder.Id);
            Log.Append($"已添加游戏文件夹：{folder.Name}（{folder.FolderPath}）");
            await RefreshAsync(true);
        }
        catch (Exception ex)
        {
            StatusText = "添加文件夹失败";
            _status.SetError("添加文件夹失败：" + ex.Message);
            Log.Append("添加文件夹失败：" + ex.Message);
        }
    }

    private async Task RemoveFolderAsync(GameFolder? folder)
    {
        if (folder == null) return;
        if (ConfirmAsync != null)
        {
            var ok = await ConfirmAsync("移除游戏文件夹",
                $"确定要移除「{folder.Name}」吗？\n只会从启动器列表中移除，不会删除磁盘上的文件。");
            if (!ok) return;
        }

        try
        {
            if (_settings.VersionHub.Folders.Remove(folder.Id))
            {
                RefreshFolderList();
                ActiveFolder = _settings.VersionHub.ActiveFolder;
                await RefreshAsync(true);
            }
        }
        catch (Exception ex)
        {
            StatusText = "移除失败";
            _status.SetError("移除文件夹失败：" + ex.Message);
        }
    }

    // ===== 扫描 =====

    public async Task RefreshAsync(bool force)
    {
        var hub = _settings.VersionHub;
        var folder = hub.ActiveFolder;
        if (folder == null)
        {
            StatusText = "没有活动的游戏文件夹";
            _status.SetError("没有活动的游戏文件夹");
            return;
        }

        IsBusy = true;
        _isLoading = true;
        OnPropertyChanged(nameof(IsLoading));
        StatusText = "扫描版本…";
        _status.SetInfo("扫描版本…");

        try
        {
            var raw = await hub.ScanActiveAsync();

            _loadedFolderId = folder.Id;
            ApplyFilterTo(raw);
            SelectedVersion ??= Versions.FirstOrDefault();

            StatusText = $"共 {raw.Count} 个版本";
            _status.SetInfo($"共 {raw.Count} 个版本");
            Log.Append($"扫描完成：活动文件夹 {folder.Name}，共 {raw.Count} 个版本");
        }
        catch (Exception ex)
        {
            StatusText = "扫描失败";
            _status.SetError("扫描失败：" + ex.Message);
            Log.Append("扫描版本失败：" + ex.Message);
        }
        finally
        {
            IsBusy = false;
            _isLoading = false;
            OnPropertyChanged(nameof(IsLoading));
        }
    }

    /// <summary>兼容 DownloadViewModel.LibraryChanged 回调：下载完成后刷新已安装列表。</summary>
    public void RefreshInstalledFlags()
    {
        _ = RefreshAsync(false);
    }

    private void ApplyFilterTo(IEnumerable<ExtendedVersion> raw)
    {
        var classifier = _settings.VersionHub.Classifier;
        var globalIsolation = _settings.GlobalIsolationDefault;

        // 先给所有版本批量算好 IsIsolatedResolved
        foreach (var v in raw) v.IsIsolatedResolved = v.ResolveIsolation(globalIsolation);

        IEnumerable<ExtendedVersion> list = raw;

        // 分类筛选
        list = _categoryFilter switch
        {
            AovoCL.Core.VersionManagement.CategoryFilter.All => list,
            AovoCL.Core.VersionManagement.CategoryFilter.Favorite => list.Where(v => v.IsFavorite),
            AovoCL.Core.VersionManagement.CategoryFilter.Modded => list.Where(v => v.Category == VersionCategory.Modded),
            AovoCL.Core.VersionManagement.CategoryFilter.OriginalLike => list.Where(v => v.Category == VersionCategory.OriginalLike),
            AovoCL.Core.VersionManagement.CategoryFilter.Other => list.Where(v => v.Category == VersionCategory.Other),
            _ => list
        };

        // 搜索关键词
        var kw = _filter.Trim();
        if (!string.IsNullOrEmpty(kw))
            list = classifier.Search(list, kw);

        Versions.Clear();
        foreach (var v in list) Versions.Add(v);
        OnPropertyChanged(nameof(IsVersionListEmpty));
        OnPropertyChanged(nameof(ActiveFolderInvalid));
        OnPropertyChanged(nameof(EmptyTitle));
        OnPropertyChanged(nameof(EmptyDetail));

        // 选中状态维护
        if (SelectedVersion != null && !Versions.Contains(SelectedVersion))
            SelectedVersion = Versions.FirstOrDefault();
        else if (SelectedVersion == null)
            SelectedVersion = Versions.FirstOrDefault();
    }

    private void ApplyFilter()
    {
        // 没有缓存 → 等下一次 RefreshAsync
        if (!Versions.Any() && string.IsNullOrEmpty(_loadedFolderId)) return;
        // 已经有 raw 缓存但我们没存 → 简化处理：重新扫描一次（非 force，内部有缓存）
        _ = RefreshAsync(false);
    }

    // ===== 收藏 / 隔离切换 =====

    public async Task ToggleFavoriteAsync(ExtendedVersion? v)
    {
        if (v == null) return;
        await _settings.VersionHub.ToggleFavoriteAsync(v.Id);
        await RefreshAsync(false);
    }

    public async Task ToggleIsolationAsync(ExtendedVersion? v)
    {
        if (v == null) return;
        await _settings.VersionHub.ToggleIsolationAsync(v.Id);
        await RefreshAsync(false);
    }

    // ===== 删除 / 打开目录 / 启动指定版本 =====

    private async Task DeleteVersionAsync(ExtendedVersion? v)
    {
        if (v == null) return;

        if (ConfirmAsync != null)
        {
            var ok = await ConfirmAsync("删除版本",
                $"确定要删除版本「{v.Name}」吗？\n只删除该版本目录，共享的 libraries / assets 不受影响。此操作不可撤销。");
            if (!ok) return;
        }

        try
        {
            var folder = _settings.VersionHub.ActiveFolder;
            if (folder == null) return;

            var dir = Path.Combine(folder.VersionsDir, v.Id);
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);

            Log.Append($"已删除版本：{v.Name}");
            await RefreshAsync(true);
        }
        catch (Exception ex)
        {
            StatusText = "删除失败";
            _status.SetError("删除版本失败：" + ex.Message);
            Log.Append($"删除版本失败（可能正在运行）：{ex.Message}");
        }
    }

    private Task OpenVersionFolderAsync(ExtendedVersion? v)
    {
        var folder = _settings.VersionHub.ActiveFolder;
        if (v == null || folder == null) return Task.CompletedTask;

        var dir = Path.Combine(folder.VersionsDir, v.Id);
        try
        {
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            if (OperatingSystem.IsWindows())
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
            else if (OperatingSystem.IsMacOS())
                Process.Start("open", $"\"{dir}\"");
            else
                Process.Start("xdg-open", $"\"{dir}\"");
        }
        catch (Exception ex)
        {
            Log.Append("打开版本目录失败：" + ex.Message);
        }
        return Task.CompletedTask;
    }

    private async Task LaunchSpecificAsync(ExtendedVersion? v)
    {
        if (v == null) return;
        SelectedVersion = v;
        if (!IsRunning) await ToggleLaunchAsync();
    }

    // ===== 启动 =====

    private async Task ToggleLaunchAsync()
    {
        if (IsRunning)
        {
            _running?.Stop();
            Log.Append("正在结束游戏进程…");
            return;
        }

        if (SelectedVersion == null) return;

        _cts = new CancellationTokenSource();
        IsBusy = true;
        ProgressPercent = 0;
        Log.Append($"===== 启动 {SelectedVersion.Id} =====");
        _status.SetProgress(0, $"启动 {SelectedVersion.Id}");

        try
        {
            var launcher = _settings.Context.Service;
            var folder = _settings.VersionHub.ActiveFolder
                ?? throw new InvalidOperationException("没有活动的游戏文件夹");

            // 如果版本启用了隔离，确保隔离目录结构已创建
            var globalIsolation = _settings.GlobalIsolationDefault;
            if (SelectedVersion.ResolveIsolation(globalIsolation))
                VersionIsolation.EnsureIsolatedStructure(folder, SelectedVersion, globalIsolation);

            // 隔离模式下，把 gameDir 指向隔离目录
            var gameDirectory = SelectedVersion.ResolveIsolation(globalIsolation)
                ? VersionIsolation.GetIsolatedRoot(folder, SelectedVersion)
                : folder.FolderPath;

            var request = new LaunchRequest
            {
                VersionId = SelectedVersion.Id,
                VersionUrl = "",
                PlayerName = string.IsNullOrWhiteSpace(_settings.Username) ? "Steve" : _settings.Username,
                GameDirectory = gameDirectory,
                MemoryMb = _settings.MemoryMb,
                JavaPath = string.IsNullOrWhiteSpace(_settings.JavaPath) ? null : _settings.JavaPath
            };

            var running = await launcher.LaunchAsync(
                request,
                line => Log.Append(line),
                (stage, percent) => Dispatcher.UIThread.Post(() =>
                {
                    StatusText = stage;
                    ProgressPercent = percent;
                    _status.SetProgress(percent, stage);
                }),
                _cts.Token);

            _running = running;
            Log.Append($"游戏已启动（PID {running.ProcessId}）");
            StatusText = "游戏运行中";
            _status.SetSuccess($"游戏运行中（PID {running.ProcessId}）");
            RaiseRunningChanged();

            running.Exited += code => Dispatcher.UIThread.Post(() =>
            {
                Log.Append($"游戏已退出，退出代码 {code}");
                _running = null;
                StatusText = "已退出";
                ProgressPercent = 0;
                _status.SetIdle($"游戏已退出，退出代码 {code}");
                RaiseRunningChanged();
            });
        }
        catch (OperationCanceledException)
        {
            StatusText = "已取消";
            ProgressPercent = 0;
            _status.SetIdle("启动已取消");
            Log.Append("启动已取消");
        }
        catch (Exception ex)
        {
            StatusText = "启动失败";
            _status.SetError("启动失败：" + ex.Message);
            Log.Append("启动失败：" + ex.Message);
        }
        finally
        {
            IsBusy = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    // ===== 辅助 =====

    private void RaiseRunningChanged()
    {
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(StartButtonText));
    }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(AppSettings.JavaPath):
                OnPropertyChanged(nameof(JavaSummary));
                break;
            case nameof(AppSettings.GlobalIsolationDefault):
                _ = RefreshAsync(false);
                break;
        }
    }

    public void Shutdown() => _running?.Stop();

    // ===== 展示文本 =====

    public static string TypeLabel(VersionType t) => t switch
    {
        VersionType.Release => "正式版",
        VersionType.Snapshot => "快照",
        VersionType.OldBeta => "Beta",
        VersionType.OldAlpha => "Alpha",
        VersionType.AprilFools => "愚人节",
        _ => "未知"
    };

    public static string LoaderLabel(LoaderType l) => l switch
    {
        LoaderType.Vanilla => "原版",
        LoaderType.Forge => "Forge",
        LoaderType.Fabric => "Fabric",
        LoaderType.NeoForge => "NeoForge",
        LoaderType.Quilt => "Quilt",
        LoaderType.OptiFine => "OptiFine",
        LoaderType.LiteLoader => "LiteLoader",
        _ => "无"
    };
}
