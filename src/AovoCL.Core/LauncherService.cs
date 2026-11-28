using System.Diagnostics;

namespace AovoCL.Core;

public sealed class RunningGame
{
    private readonly Process _process;

    internal RunningGame(Process process, string versionId)
    {
        _process = process;
        VersionId = versionId;
    }

    public string VersionId { get; }
    public int ProcessId => _process.Id;
    public bool HasExited => _process.HasExited;
    public int ExitCode => _process.ExitCode;

    public event Action<int>? Exited;

    internal void Attach() => _process.Exited += (_, _) =>
    {
        try { Exited?.Invoke(_process.ExitCode); } catch { /* 忽略 */ }
    };

    public Task WaitForExitAsync() => _process.WaitForExitAsync();

    public void Stop()
    {
        try
        {
            if (!_process.HasExited) _process.Kill(true);
        }
        catch
        {
            // 进程可能已退出
        }
    }
}

/// <summary>对外门面：版本列表 → 安装 → Java → 启动，UI 与 CLI 共用。</summary>
public sealed class LauncherService
{
    public LauncherService(string root, bool useMirror = true, DownloadOptions? downloadOptions = null)
    {
        Paths = new GamePaths(root);
        Mirror = new MirrorOptions { UseMirror = useMirror };
        Downloader = new FileDownloader(Mirror);
        if (downloadOptions != null)
        {
            Downloader.Options.MaxConcurrency = downloadOptions.MaxConcurrency;
            Downloader.Options.SliceConcurrency = downloadOptions.SliceConcurrency;
            Downloader.Options.SliceThreshold = downloadOptions.SliceThreshold;
            Downloader.Options.MinSliceSize = downloadOptions.MinSliceSize;
            Downloader.Options.EnableSliced = downloadOptions.EnableSliced;
        }
        Versions = new VersionService(Paths, Downloader, Mirror);
        Installer = new GameInstaller(Paths, Downloader, Versions);
        JavaManager = new JavaRuntimeManager(Paths);
        LocalVersions = new VersionManager(Paths);
    }

    public GamePaths Paths { get; }
    public MirrorOptions Mirror { get; }
    public FileDownloader Downloader { get; }
    public VersionService Versions { get; }
    public GameInstaller Installer { get; }
    public JavaRuntimeManager JavaManager { get; }
    public VersionManager LocalVersions { get; }

    public async Task<IReadOnlyList<VersionEntry>> GetVersionsAsync(bool refresh, CancellationToken ct = default)
    {
        var manifest = await Versions.GetManifestAsync(refresh, ct);
        return manifest.Versions;
    }

    public bool IsInstalled(string versionId) =>
        File.Exists(Paths.VersionJson(versionId)) && File.Exists(Paths.VersionJar(versionId));

    public async Task<IReadOnlyList<JavaRuntime>> DetectJavaAsync(CancellationToken ct = default) =>
        await JavaLocator.DetectAsync(Paths.Root, ct);

    public async Task<RunningGame> LaunchAsync(LaunchRequest request, Action<string> log,
        Action<string, double> progress, CancellationToken ct = default)
    {
        Paths.EnsureDirectories();

        progress("检查游戏文件", 0);
        var transfer = new TransferProgress();
        var version = await Installer.InstallAsync(
            request.VersionId,
            request.VersionUrl,
            transfer,
            p => progress($"下载文件 {p.FilesDone}/{p.FilesTotal}", p.Percent),
            log,
            ct);

        var requiredJava = version.JavaVersionMajor > 0 ? version.JavaVersionMajor : 8;

        progress("准备 Java 环境", 0);
        JavaRuntime java;
        if (!string.IsNullOrWhiteSpace(request.JavaPath) && File.Exists(request.JavaPath))
        {
            var custom = await JavaLocator.ProbeAsync(request.JavaPath, ct);
            if (custom == null) throw new InvalidOperationException($"无法执行指定 Java：{request.JavaPath}");
            if (custom.MajorVersion < requiredJava)
                log($"警告：该版本建议 Java {requiredJava}+，当前为 Java {custom.MajorVersion}");
            java = custom;
        }
        else
        {
            java = await JavaManager.EnsureAsync(requiredJava, log, ct);
        }
        log($"使用 Java {java.VersionText}（{java.Path}）");

        progress("生成启动参数", 0);
        var profile = OfflineAuth.Create(request.PlayerName);
        var plan = new LaunchPlanBuilder(Paths).Build(version, request, profile, java);

        log($"游戏目录：{plan.WorkingDirectory}");
        log("启动命令：" + plan.CommandLine);

        var process = new Process { StartInfo = plan.ToStartInfo(), EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) => { if (e.Data != null) log(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) log(e.Data); };

        if (!process.Start())
            throw new InvalidOperationException("无法启动 Java 进程");

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var running = new RunningGame(process, version.Id);
        running.Attach();
        progress("已启动", 100);
        return running;
    }
}
