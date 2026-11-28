namespace AovoCL.Core;

/// <summary>版本清单（version_manifest_v2.json）。</summary>
public sealed class VersionManifest
{
    public LatestVersions? Latest { get; set; }
    public List<VersionEntry> Versions { get; set; } = new();
}

public sealed class LatestVersions
{
    public string? Release { get; set; }
    public string? Snapshot { get; set; }
}

/// <summary>清单里的一条版本记录。</summary>
public sealed class VersionEntry
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string Url { get; set; } = "";
    public string? Sha1 { get; set; }
    public long Size { get; set; }
    public DateTime ReleaseTime { get; set; }
}

/// <summary>单个文件下载描述。</summary>
public sealed class DownloadInfo
{
    public string? Id { get; set; }
    public string Url { get; set; } = "";
    public string? Sha1 { get; set; }
    public long Size { get; set; }
    public string? Path { get; set; }
}

public sealed class AssetIndexRef
{
    public string Id { get; set; } = "";
    public string Url { get; set; } = "";
    public string? Sha1 { get; set; }
    public long Size { get; set; }
    public long TotalSize { get; set; }
}

/// <summary>一条规则，对应 JSON 中 rules 数组的元素。</summary>
public sealed class GameRule
{
    public string Action { get; set; } = "allow";
    public string? OsName { get; set; }
    public string? OsArch { get; set; }
    public string? OsVersion { get; set; }
    public Dictionary<string, bool>? Features { get; set; }
}

/// <summary>参数项：一条字符串，或带 rules 的对象。</summary>
public sealed class ArgumentEntry
{
    public List<GameRule> Rules { get; set; } = new();
    public List<string> Values { get; set; } = new();
}

/// <summary>依赖库。</summary>
public sealed class Library
{
    public string Name { get; set; } = "";
    /// <summary>相对 libraries 目录的路径，如 org/lwjgl/lwjgl/3.3.1/lwjgl-3.3.1.jar。</summary>
    public string RelativePath { get; set; } = "";
    public string Url { get; set; } = "";
    public string? Sha1 { get; set; }
    public long Size { get; set; }
    public List<GameRule> Rules { get; set; } = new();
    /// <summary>是否为需要解压的本地库（老版本 LWJGL 2.x）。</summary>
    public bool IsNative { get; set; }
    /// <summary>解压时排除的目录，如 META-INF/。</summary>
    public List<string> ExtractExclude { get; set; } = new();

    public string FileName => System.IO.Path.GetFileName(RelativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
}

public sealed class LoggingConfig
{
    public string Argument { get; set; } = "";
    public DownloadInfo? File { get; set; }
}

/// <summary>合并 inheritsFrom 之后的最终版本描述。</summary>
public sealed class GameVersion
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "release";
    public string MainClass { get; set; } = "";
    public string? InheritsFrom { get; set; }
    public string? Assets { get; set; } = "legacy";
    public AssetIndexRef? AssetIndex { get; set; }
    public DownloadInfo? Client { get; set; }
    /// <summary>客户端 jar 归属的版本号（启用 inheritsFrom 时可能指向父版本）。</summary>
    public string ClientJarVersionId { get; set; } = "";
    public int JavaVersionMajor { get; set; }
    /// <summary>1.12 及以前的 minecraftArguments 字符串。</summary>
    public string? LegacyArguments { get; set; }
    public List<Library> Libraries { get; set; } = new();
    public List<ArgumentEntry> JvmArguments { get; set; } = new();
    public List<ArgumentEntry> GameArguments { get; set; } = new();
    public LoggingConfig? Logging { get; set; }
}

/// <summary>离线账号。</summary>
public sealed class OfflineProfile
{
    public string Name { get; set; } = "Player";
    public string Uuid { get; set; } = "";
    public string AccessToken { get; set; } = "0";
}
