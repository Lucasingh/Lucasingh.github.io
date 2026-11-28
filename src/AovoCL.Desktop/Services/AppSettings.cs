using AovoCL.Core;
using AovoCL.Core.VersionManagement;
using AovoCL.Desktop.ViewModels;

namespace AovoCL.Desktop.Services;

/// <summary>UI 侧的全局设置，启动页 / 下载页 / 设置页共享同一份实例。</summary>
public sealed class AppSettings : ObservableObject
{
    public const double MinMemoryMb = 1024;
    public const double MaxMemoryMb = 16384;

    private string _gameRoot;
    private string _username = "Steve";
    private string _javaPath = "";
    private double _memoryMb = 4096;
    private bool _useMirror = true;
    private double _downloadConcurrency = DownloadOptions.DefaultMaxConcurrency;
    private bool _useSlicedDownload = true;

    public AppSettings()
    {
        // ===== 版本管理子系统（多文件夹 / 自动分类 / 隔离 / 收藏） =====
        VersionHub = new VersionHub();

        // 全局隔离默认值
        GlobalIsolationDefault = VersionHub.Folders.GlobalIsolationDefault;

        // LauncherContext 跟随活动文件夹
        var activePath = VersionHub.ActiveFolder?.FolderPath
            ?? GameFolder.DetectDefaultPath();
        _gameRoot = activePath;
        Context = new LauncherContext(_gameRoot, _useMirror);
        Context.SetDownloadOptions((int)_downloadConcurrency, _useSlicedDownload);
        Theme = new ThemeManager();

        // 活动文件夹变化 → 重建 LauncherContext 根目录
        VersionHub.Folders.ActiveFolderChanged += (_, folder) =>
        {
            if (folder == null) return;
            GameRoot = folder.FolderPath;
        };
    }

    // ===== 版本管理总门面 =====
    public VersionHub VersionHub { get; }

    public bool GlobalIsolationDefault
    {
        get => _globalIsolationDefault;
        set
        {
            if (!SetProperty(ref _globalIsolationDefault, value)) return;
            VersionHub.Folders.SetGlobalIsolation(value);
        }
    }
    private bool _globalIsolationDefault;

    public LauncherContext Context { get; }

    public ThemeManager Theme { get; }

    public string GameRoot
    {
        get => _gameRoot;
        set
        {
            var text = (value ?? "").Trim();
            if (text.Length == 0 || text == _gameRoot) return;
            _gameRoot = text;
            Context.SetRoot(text);
            OnPropertyChanged();
        }
    }

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value ?? "");
    }

    public string JavaPath
    {
        get => _javaPath;
        set => SetProperty(ref _javaPath, value ?? "");
    }

    public double MemoryMb
    {
        get => _memoryMb;
        set
        {
            var clamped = Math.Clamp(Math.Round(value / 512) * 512, MinMemoryMb, MaxMemoryMb);
            if (!SetProperty(ref _memoryMb, clamped)) return;
            OnPropertyChanged(nameof(MemoryText));
        }
    }

    public string MemoryText => $"{(int)_memoryMb} MB";

    public bool UseMirror
    {
        get => _useMirror;
        set
        {
            if (!SetProperty(ref _useMirror, value)) return;
            Context.SetMirror(value);
        }
    }

    /// <summary>全局下载连接上限（单文件与分片共享）。</summary>
    public double DownloadConcurrency
    {
        get => _downloadConcurrency;
        set
        {
            var clamped = Math.Clamp(Math.Round(value / 4) * 4, 4, 32);
            if (!SetProperty(ref _downloadConcurrency, clamped)) return;
            OnPropertyChanged(nameof(DownloadConcurrencyText));
            Context.SetDownloadOptions((int)clamped, _useSlicedDownload);
        }
    }

    public string DownloadConcurrencyText => $"{(int)_downloadConcurrency} 个连接";

    /// <summary>是否对 ≥4 MB 的文件启用 Range 分片。</summary>
    public bool UseSlicedDownload
    {
        get => _useSlicedDownload;
        set
        {
            if (!SetProperty(ref _useSlicedDownload, value)) return;
            Context.SetDownloadOptions((int)_downloadConcurrency, value);
        }
    }
}
