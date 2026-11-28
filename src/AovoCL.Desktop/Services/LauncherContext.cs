using AovoCL.Core;

namespace AovoCL.Desktop.Services;

/// <summary>持有 LauncherService；游戏目录变化时重建，镜像与下载参数原地生效。</summary>
public sealed class LauncherContext
{
    private LauncherService _service;
    private int _maxConcurrency = DownloadOptions.DefaultMaxConcurrency;
    private bool _sliced = true;

    public LauncherContext(string root, bool useMirror)
    {
        Root = root;
        _service = new LauncherService(root, useMirror);
    }

    public string Root { get; private set; }

    public LauncherService Service => _service;

    public void SetRoot(string root)
    {
        if (string.Equals(root, Root, StringComparison.OrdinalIgnoreCase)) return;
        Root = root;
        _service = new LauncherService(root, _service.Mirror.UseMirror);
        ApplyDownloadOptions();
    }

    public void SetMirror(bool useMirror) => _service.Mirror.UseMirror = useMirror;

    public void SetDownloadOptions(int maxConcurrency, bool enableSliced)
    {
        _maxConcurrency = Math.Clamp(maxConcurrency, 1, 64);
        _sliced = enableSliced;
        ApplyDownloadOptions();
    }

    private void ApplyDownloadOptions()
    {
        _service.Downloader.Options.MaxConcurrency = _maxConcurrency;
        _service.Downloader.Options.EnableSliced = _sliced;
    }
}
