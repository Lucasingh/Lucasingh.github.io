namespace AovoCL.Core.VersionManagement;

/// <summary>
/// 版本隔离路径计算 + 启动参数注入。
/// 隔离模式：该版本使用独立的 mods/config/saves/resourcepacks 目录。
/// 非隔离：使用游戏根目录下的共享目录。
/// </summary>
public static class VersionIsolation
{
    private const string IsolatedSubdirs = "mods|config|saves|resourcepacks|logs|screenshots|schematics";

    /// <summary>
    /// 该版本的隔离目录根路径（即 versions/{id}/）。
    /// </summary>
    public static string GetIsolatedRoot(GameFolder folder, ExtendedVersion version)
        => Path.Combine(folder.VersionsDir, version.Id);

    /// <summary>
    /// 该版本的 mods 目录（隔离 or 共享）。
    /// </summary>
    public static string GetModsDir(GameFolder folder, ExtendedVersion version, bool globalDefault)
        => version.ResolveIsolation(globalDefault)
            ? Path.Combine(GetIsolatedRoot(folder, version), "mods")
            : Path.Combine(folder.FolderPath, "mods");

    public static string GetConfigDir(GameFolder folder, ExtendedVersion version, bool globalDefault)
        => version.ResolveIsolation(globalDefault)
            ? Path.Combine(GetIsolatedRoot(folder, version), "config")
            : Path.Combine(folder.FolderPath, "config");

    public static string GetSavesDir(GameFolder folder, ExtendedVersion version, bool globalDefault)
        => version.ResolveIsolation(globalDefault)
            ? Path.Combine(GetIsolatedRoot(folder, version), "saves")
            : Path.Combine(folder.FolderPath, "saves");

    public static string GetResourcepacksDir(GameFolder folder, ExtendedVersion version, bool globalDefault)
        => version.ResolveIsolation(globalDefault)
            ? Path.Combine(GetIsolatedRoot(folder, version), "resourcepacks")
            : Path.Combine(folder.FolderPath, "resourcepacks");

    /// <summary>
    /// 确保隔离目录结构已创建（仅当隔离启用时调用）。
    /// </summary>
    public static void EnsureIsolatedStructure(GameFolder folder, ExtendedVersion version, bool globalDefault)
    {
        if (!version.ResolveIsolation(globalDefault)) return;
        var root = GetIsolatedRoot(folder, version);
        foreach (var sub in IsolatedSubdirs.Split('|'))
            Directory.CreateDirectory(Path.Combine(root, sub));
    }

    /// <summary>
    /// 在启动参数中注入 -Dminecraft.gameDir。
    /// 返回新的 JVM 参数（隔离时追加 -Dminecraft.gameDir=versions/{id}）。
    /// </summary>
    public static string InjectLaunchArgs(GameFolder folder, ExtendedVersion version, bool globalDefault, string originalJvmArgs)
    {
        if (!version.ResolveIsolation(globalDefault)) return originalJvmArgs;

        // 相对 versions/{id}/ 的相对路径，相对于游戏根目录
        var relativeGameDir = $"versions/{version.Id}";
        var injected = $"-Dminecraft.gameDir={relativeGameDir}";

        if (string.IsNullOrWhiteSpace(originalJvmArgs))
            return injected;

        // 去重：如果已有 gameDir 则替换
        var args = originalJvmArgs;
        var gameDirMarker = "-Dminecraft.gameDir=";
        var idx = args.IndexOf(gameDirMarker, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            // 找到参数结尾（空格或字符串结尾）
            var end = args.IndexOf(' ', idx);
            if (end < 0) end = args.Length;
            args = args[..idx] + injected + args[end..];
        }
        else
        {
            args = args.TrimEnd() + " " + injected;
        }

        return args;
    }
}
