using System.IO.Compression;
using System.Text.Json;

namespace AovoCL.Core;

public sealed class AssetIndexFile
{
    public Dictionary<string, AssetObject>? Objects { get; set; }
    public bool Virtual { get; set; }
    public bool MapToResources { get; set; }
}

public sealed class AssetObject
{
    public string Hash { get; set; } = "";
    public long Size { get; set; }
}

public enum InstallStage
{
    Resolving,
    GameFiles,
    Assets,
    Finalizing
}

public enum InstallStageStatus
{
    InProgress,
    Completed
}

/// <summary>某一安装阶段的不可变进度快照；传输数据仅属于当前下载批次。</summary>
public sealed record InstallProgress(
    InstallStage Stage,
    InstallStageStatus Status,
    double StagePercent,
    int FilesDone,
    int FilesTotal,
    string CurrentFile,
    double SpeedBytesPerSecond)
{
    public static InstallProgress ForStage(InstallStage stage, InstallStageStatus status) =>
        new(stage, status, status == InstallStageStatus.Completed ? 100 : 0, 0, 0, "", 0);

    public static InstallProgress ForTransfer(InstallStage stage, TransferProgress transfer) =>
        new(stage, InstallStageStatus.InProgress, transfer.Percent, transfer.FilesDone,
            transfer.FilesTotal, transfer.Current, transfer.SpeedBytesPerSecond);
}

/// <summary>安装器：补齐客户端、依赖库、资源文件，并释放老版本 natives。</summary>
public sealed class GameInstaller
{
    private readonly GamePaths _paths;
    private readonly FileDownloader _downloader;
    private readonly VersionService _versions;

    public GameInstaller(GamePaths paths, FileDownloader downloader, VersionService versions)
    {
        _paths = paths;
        _downloader = downloader;
        _versions = versions;
    }

    public async Task<GameVersion> InstallAsync(string id, string url, TransferProgress progress,
        Action<TransferProgress>? onProgress, Action<string> log, CancellationToken ct = default,
        Action<InstallProgress>? onInstallProgress = null)
    {
        void ReportStage(InstallStage stage, InstallStageStatus status) =>
            onInstallProgress?.Invoke(InstallProgress.ForStage(stage, status));

        void ReportTransfer(InstallStage stage, TransferProgress transfer)
        {
            onProgress?.Invoke(transfer);
            onInstallProgress?.Invoke(InstallProgress.ForTransfer(stage, transfer));
        }

        _paths.EnsureDirectories();
        ReportStage(InstallStage.Resolving, InstallStageStatus.InProgress);
        log($"解析版本 {id} …");

        var resolved = await _versions.ResolveAsync(id, url, ct);
        var version = resolved.Version;

        Directory.CreateDirectory(_paths.VersionDir(id));
        await File.WriteAllTextAsync(_paths.VersionJson(id), resolved.RawJson, ct);

        if (version.Client == null)
            throw new InvalidDataException("该版本没有客户端下载信息");

        ReportStage(InstallStage.Resolving, InstallStageStatus.Completed);
        ReportStage(InstallStage.GameFiles, InstallStageStatus.InProgress);

        var tasks = new List<DownloadTask>
        {
            new(version.Client.Url, _paths.VersionJar(version.ClientJarVersionId),
                version.Client.Sha1, version.Client.Size, "客户端主文件")
        };

        var nativeLibraries = new List<Library>();
        foreach (var library in version.Libraries)
        {
            if (!RuleEvaluator.IsAllowed(library.Rules)) continue;

            var target = Path.Combine(_paths.LibrariesDir,
                library.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            tasks.Add(new DownloadTask(library.Url, target, library.Sha1, library.Size, library.FileName));

            if (library.IsNative) nativeLibraries.Add(library);
        }

        if (version.Logging?.File != null && !string.IsNullOrEmpty(version.Logging.File.Url))
        {
            var logFile = version.Logging.File;
            var path = Path.Combine(_paths.LogConfigsDir, logFile.Id ?? "client.xml");
            tasks.Add(new DownloadTask(logFile.Url, path, logFile.Sha1, logFile.Size, "日志配置"));
        }

        string? indexPath = null;
        if (version.AssetIndex != null && !string.IsNullOrEmpty(version.AssetIndex.Url))
        {
            indexPath = Path.Combine(_paths.IndexesDir, version.AssetIndex.Id + ".json");
            tasks.Add(new DownloadTask(version.AssetIndex.Url, indexPath,
                version.AssetIndex.Sha1, version.AssetIndex.Size, "资源索引"));
        }

        log($"开始下载游戏文件，共 {tasks.Count} 项 …");
        await _downloader.DownloadAllAsync(tasks, progress,
            transfer => ReportTransfer(InstallStage.GameFiles, transfer), ct);
        ReportStage(InstallStage.GameFiles, InstallStageStatus.Completed);
        ReportStage(InstallStage.Assets, InstallStageStatus.InProgress);

        // 资源文件（objects）按索引批量补齐
        AssetIndexFile? index = null;
        if (indexPath != null && File.Exists(indexPath))
        {
            index = JsonSerializer.Deserialize<AssetIndexFile>(
                await File.ReadAllTextAsync(indexPath, ct),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (index?.Objects is { Count: > 0 })
            {
                var assetTasks = new List<DownloadTask>(index.Objects.Count);
                foreach (var (name, obj) in index.Objects)
                {
                    var sub = obj.Hash[..2];
                    var target = Path.Combine(_paths.ObjectsDir, sub, obj.Hash);
                    var assetUrl = $"https://resources.download.minecraft.net/{sub}/{obj.Hash}";
                    assetTasks.Add(new DownloadTask(assetUrl, target, obj.Hash, obj.Size, name));
                }

                log($"开始下载资源文件，共 {assetTasks.Count} 项 …");
                await _downloader.DownloadAllAsync(assetTasks, progress,
                    transfer => ReportTransfer(InstallStage.Assets, transfer), ct);
            }
        }

        ReportStage(InstallStage.Assets, InstallStageStatus.Completed);
        ReportStage(InstallStage.Finalizing, InstallStageStatus.InProgress);

        if (index?.Objects is { Count: > 0 } && (index.Virtual || index.MapToResources))
        {
            var virtualDir = Path.Combine(_paths.AssetsDir, "virtual", version.AssetIndex!.Id);
            foreach (var (name, obj) in index.Objects)
            {
                var source = Path.Combine(_paths.ObjectsDir, obj.Hash[..2], obj.Hash);
                var target = Path.Combine(virtualDir, name.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                if (File.Exists(source)) File.Copy(source, target, true);
            }
            log("已完成 legacy 资源的虚拟目录映射");
        }

        ExtractNatives(id, nativeLibraries, log);
        log($"版本 {id} 准备完成");
        ReportStage(InstallStage.Finalizing, InstallStageStatus.Completed);
        return version;
    }

    /// <summary>老版本 LWJGL 需要把 natives 里的 dll/so 解压到独立目录。</summary>
    private void ExtractNatives(string id, IEnumerable<Library> libraries, Action<string> log)
    {
        var nativesDir = _paths.NativesDir(id);
        Directory.CreateDirectory(nativesDir);

        foreach (var library in libraries)
        {
            var jar = Path.Combine(_paths.LibrariesDir,
                library.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(jar)) continue;

            try
            {
                using var archive = ZipFile.OpenRead(jar);
                foreach (var entry in archive.Entries)
                {
                    if (entry.FullName.EndsWith('/') || entry.FullName.StartsWith("META-INF/", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (library.ExtractExclude.Any(x => entry.FullName.StartsWith(x, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    var target = Path.Combine(nativesDir, Path.GetFileName(entry.FullName));
                    entry.ExtractToFile(target, true);
                }
                log($"已释放 natives：{library.FileName}");
            }
            catch (Exception ex)
            {
                log($"natives 解压失败 {library.FileName}：{ex.Message}");
            }
        }
    }
}
