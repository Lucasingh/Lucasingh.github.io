using System.Text.Json;

namespace AovoCL.Core;

public sealed record ResolvedVersion(GameVersion Version, string RawJson);

/// <summary>负责版本清单 / 版本 JSON 的获取、缓存与 inheritsFrom 继承合并。</summary>
public sealed class VersionService
{
    private readonly GamePaths _paths;
    private readonly FileDownloader _downloader;
    private readonly MirrorOptions _mirror;
    private VersionManifest? _manifest;
    private DateTime _manifestTime;

    public VersionService(GamePaths paths, FileDownloader downloader, MirrorOptions mirror)
    {
        _paths = paths;
        _downloader = downloader;
        _mirror = mirror;
    }

    public async Task<VersionManifest> GetManifestAsync(bool forceRefresh = false, CancellationToken ct = default)
    {
        var cacheFile = Path.Combine(_paths.CacheDir, "version_manifest_v2.json");

        if (!forceRefresh && _manifest != null && (DateTime.Now - _manifestTime).TotalMinutes < 30)
            return _manifest;

        if (!forceRefresh && File.Exists(cacheFile) && (DateTime.Now - File.GetLastWriteTime(cacheFile)).TotalMinutes < 30)
        {
            try
            {
                var cached = JsonSerializer.Deserialize<VersionManifest>(
                    await File.ReadAllTextAsync(cacheFile, ct),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (cached is { Versions.Count: > 0 })
                {
                    _manifest = cached;
                    _manifestTime = DateTime.Now;
                    return cached;
                }
            }
            catch
            {
                // 缓存损坏则重新下载
            }
        }

        var text = await _downloader.GetStringAsync(_mirror.ManifestUrl, ct);
        var manifest = JsonSerializer.Deserialize<VersionManifest>(text,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("版本清单解析失败");

        Directory.CreateDirectory(_paths.CacheDir);
        await File.WriteAllTextAsync(cacheFile, text, ct);

        _manifest = manifest;
        _manifestTime = DateTime.Now;
        return manifest;
    }

    /// <summary>读取版本 JSON：优先本地缓存，其次网络。</summary>
    public async Task<string> GetVersionJsonAsync(string id, string url, CancellationToken ct = default)
    {
        // 已安装版本（含重命名后的自定义名称）以本地 json 为准，避免离线或改名后无法解析
        var local = _paths.VersionJson(id);
        if (File.Exists(local))
        {
            var installed = await File.ReadAllTextAsync(local, ct);
            if (installed.Contains("\"id\"")) return installed;
        }

        var cacheFile = Path.Combine(_paths.CacheDir, "versions", id + ".json");
        if (File.Exists(cacheFile))
        {
            var cached = await File.ReadAllTextAsync(cacheFile, ct);
            if (cached.Contains("\"id\"")) return cached;
        }

        var text = await _downloader.GetStringAsync(url, ct);
        Directory.CreateDirectory(Path.GetDirectoryName(cacheFile)!);
        await File.WriteAllTextAsync(cacheFile, text, ct);
        return text;
    }

    /// <summary>解析版本，并展开 inheritsFrom（Forge/整合包依赖该机制）。</summary>
    public async Task<ResolvedVersion> ResolveAsync(string id, string url, CancellationToken ct = default)
    {
        var raw = await GetVersionJsonAsync(id, url, ct);
        var version = VersionJsonParser.Parse(raw);
        if (string.IsNullOrEmpty(version.ClientJarVersionId)) version.ClientJarVersionId = version.Id;

        if (string.IsNullOrEmpty(version.InheritsFrom))
            return new ResolvedVersion(version, raw);

        var manifest = await GetManifestAsync(false, ct);
        var parentEntry = manifest.Versions.FirstOrDefault(v => v.Id == version.InheritsFrom)
            ?? throw new InvalidDataException($"父版本 {version.InheritsFrom} 不存在于版本清单中");

        var parent = await ResolveAsync(parentEntry.Id, parentEntry.Url, ct);
        return new ResolvedVersion(Merge(parent.Version, version), raw);
    }

    private static GameVersion Merge(GameVersion parent, GameVersion child)
    {
        var merged = new GameVersion
        {
            Id = child.Id,
            Type = child.Type,
            MainClass = string.IsNullOrEmpty(child.MainClass) ? parent.MainClass : child.MainClass,
            InheritsFrom = child.InheritsFrom,
            Assets = child.Assets ?? parent.Assets,
            AssetIndex = child.AssetIndex ?? parent.AssetIndex,
            Client = child.Client ?? parent.Client,
            ClientJarVersionId = child.Client != null ? child.Id : parent.ClientJarVersionId,
            JavaVersionMajor = Math.Max(parent.JavaVersionMajor, child.JavaVersionMajor),
            LegacyArguments = child.LegacyArguments ?? parent.LegacyArguments,
            Logging = child.Logging ?? parent.Logging
        };

        var libraries = new Dictionary<string, Library>(StringComparer.OrdinalIgnoreCase);
        foreach (var lib in parent.Libraries) libraries[lib.Name] = lib;
        foreach (var lib in child.Libraries) libraries[lib.Name] = lib;
        merged.Libraries.AddRange(libraries.Values);

        merged.JvmArguments.AddRange(parent.JvmArguments);
        merged.JvmArguments.AddRange(child.JvmArguments);
        merged.GameArguments.AddRange(parent.GameArguments);
        merged.GameArguments.AddRange(child.GameArguments);
        return merged;
    }
}
