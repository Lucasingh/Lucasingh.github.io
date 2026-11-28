using System.Text.Json;
using System.Text.Json.Nodes;

namespace AovoCL.Core.VersionManagement;

/// <summary>
/// 版本扫描器：扫描游戏文件夹下 versions/ 目录，识别合法版本、解析 JSON、
/// 检测加载器类型、构建继承链、标记健康状态。
/// </summary>
public sealed class VersionScanner
{
    public VersionScanner() { }

    // ===================== 主入口 =====================

    public Task<IReadOnlyList<ExtendedVersion>> ScanAsync(GameFolder folder, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var result = ScanSync(folder, ct);
            return (IReadOnlyList<ExtendedVersion>)result;
        }, ct);
    }

    private List<ExtendedVersion> ScanSync(GameFolder folder, CancellationToken ct)
    {
        var result = new List<ExtendedVersion>();
        if (!folder.IsValidDirectory()) return result;

        var versionDirs = new List<DirectoryInfo>();
        try
        {
            versionDirs = new DirectoryInfo(folder.VersionsDir)
                .GetDirectories()
                .OrderByDescending(d => d.LastWriteTimeUtc)
                .ToList();
        }
        catch
        {
            return result;
        }

        // 第一遍：扫描所有目录，构造 ExtendedVersion（不带继承链）
        var rawMap = new Dictionary<string, ExtendedVersion>(StringComparer.OrdinalIgnoreCase);
        foreach (var dir in versionDirs)
        {
            ct.ThrowIfCancellationRequested();
            var v = ScanSingleVersion(folder, dir);
            if (v != null) rawMap[v.Id] = v;
        }

        // 第二遍：解析继承链（循环引用检测 + 父版本缺失检测）
        foreach (var v in rawMap.Values)
        {
            ct.ThrowIfCancellationRequested();
            ResolveInheritance(v, rawMap, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        // 第三遍：计算每个版本的大小 + mods 数量
        foreach (var v in rawMap.Values)
        {
            ct.ThrowIfCancellationRequested();
            ComputeSizeAndMods(folder, v);
        }

        return rawMap.Values.ToList();
    }

    // ===================== 单版本扫描 =====================

    private ExtendedVersion? ScanSingleVersion(GameFolder folder, DirectoryInfo dir)
    {
        var id = dir.Name;
        var jsonPath = Path.Combine(dir.FullName, id + ".json");
        var jarPath = Path.Combine(dir.FullName, id + ".jar");

        var version = new ExtendedVersion
        {
            Id = id,
            FolderId = folder.Id,
            Name = id,
            VersionJsonPath = jsonPath,
            JarPath = jarPath,
            InstalledAt = dir.CreationTimeUtc
        };

        // 检测 JSON
        if (!File.Exists(jsonPath))
        {
            version.Status = VersionStatus.Error;
            version.ErrorMessage = $"缺少 {id}.json";
            return version;
        }

        JsonObject? jsonObj;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
            jsonObj = JsonNode.Parse(doc.RootElement.GetRawText())?.AsObject();
        }
        catch (Exception ex)
        {
            version.Status = VersionStatus.Error;
            version.ErrorMessage = $"JSON 解析失败：{ex.Message}";
            return version;
        }

        if (jsonObj == null)
        {
            version.Status = VersionStatus.Error;
            version.ErrorMessage = "JSON 根节点不是对象";
            return version;
        }

        // 提取基础字段
        if (jsonObj.TryGetPropertyValue("id", out var idNode) && idNode?.GetValue<string>() is var parsedId && !string.IsNullOrEmpty(parsedId))
            version.Id = parsedId;

        if (jsonObj.TryGetPropertyValue("type", out var typeNode) && typeNode?.GetValue<string>() is var typeStr)
            version.Type = ParseVersionType(typeStr ?? id);
        else
            version.Type = ParseVersionType(id);

        if (jsonObj.TryGetPropertyValue("inheritsFrom", out var inhNode))
            version.InheritsFrom = inhNode?.GetValue<string>();

        // 检测 jar 是否存在
        if (!File.Exists(jarPath))
        {
            // 有些 loader 版本没有独立 jar（直接继承原版 jar），不算错误
            if (version.Type == VersionType.Release || version.Type == VersionType.Snapshot)
            {
                version.Status = VersionStatus.Error;
                version.ErrorMessage = $"缺少 {id}.jar";
            }
        }

        // 检测加载器类型（综合 libraries + 关键词）
        version.Loader = DetectLoaderType(jsonObj, id, version.InheritsFrom);

        // 检测 libraries 完整性
        CheckLibrariesIntegrity(folder, jsonObj, version);

        // 检测 assets 完整性（仅当有 assetIndex 时）
        CheckAssetsIntegrity(folder, jsonObj, version);

        return version;
    }

    // ===================== 版本类型解析 =====================

    private static VersionType ParseVersionType(string typeOrId)
    {
        if (string.IsNullOrWhiteSpace(typeOrId)) return VersionType.Unknown;
        var lower = typeOrId.ToLowerInvariant();

        // 先直接匹配 type 字段值
        switch (lower)
        {
            case "release": return VersionType.Release;
            case "snapshot": return VersionType.Snapshot;
            case "old_beta": return VersionType.OldBeta;
            case "old_alpha": return VersionType.OldAlpha;
            case "april_fools": return VersionType.AprilFools;
        }

        // 再根据 id 特征推断
        if (lower.Contains("april") || lower.Contains("fool")) return VersionType.AprilFools;
        if (lower.StartsWith("old_beta_")) return VersionType.OldBeta;
        if (lower.StartsWith("old_alpha_")) return VersionType.OldAlpha;
        if (char.IsDigit(lower[0]) && lower.Contains('.') && lower.Contains('-') == false)
        {
            // 1.20.1 这种标准 release
            return VersionType.Release;
        }
        if (lower.Contains("-pre") || lower.Contains("pre-")) return VersionType.Snapshot;
        if (char.IsDigit(lower[0]) && lower.Contains('w')) return VersionType.Snapshot; // 24w15a
        if (lower.Contains("snapshot")) return VersionType.Snapshot;

        return VersionType.Unknown;
    }

    // ===================== 加载器类型识别 =====================

    /// <summary>
    /// 优先级：libraries 依赖特征 > 目录名/版本 id 关键词。
    /// </summary>
    private static LoaderType DetectLoaderType(JsonObject json, string versionId, string? inheritsFrom)
    {
        // 1. 从 libraries 数组检测
        if (json.TryGetPropertyValue("libraries", out var libsNode) && libsNode is JsonArray libsArr)
        {
            foreach (var lib in libsArr)
            {
                var name = lib?["name"]?.GetValue<string>();
                if (string.IsNullOrEmpty(name)) continue;

                if (name.StartsWith("net.minecraftforge", StringComparison.OrdinalIgnoreCase)) return LoaderType.Forge;
                if (name.StartsWith("net.neoforged", StringComparison.OrdinalIgnoreCase)) return LoaderType.NeoForge;
                if (name.StartsWith("net.fabricmc", StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith("org.fabricmc", StringComparison.OrdinalIgnoreCase)) return LoaderType.Fabric;
                if (name.StartsWith("org.quiltmc", StringComparison.OrdinalIgnoreCase)) return LoaderType.Quilt;
                if (name.StartsWith("optifine", StringComparison.OrdinalIgnoreCase)) return LoaderType.OptiFine;
                if (name.StartsWith("com.mumfrey.liteloader", StringComparison.OrdinalIgnoreCase)) return LoaderType.LiteLoader;
            }
        }

        // 2. 从目录名 / version id 关键词推断
        var id = versionId.ToLowerInvariant();
        if (id.Contains("forge") && !id.Contains("fabric")) return LoaderType.Forge;
        if (id.Contains("neoforge") || id.Contains("neo-forge")) return LoaderType.NeoForge;
        if (id.Contains("fabric")) return LoaderType.Fabric;
        if (id.Contains("quilt")) return LoaderType.Quilt;
        if (id.Contains("optifine") || id.Contains("opti-fine")) return LoaderType.OptiFine;
        if (id.Contains("liteloader")) return LoaderType.LiteLoader;

        // 3. 无继承链 → Vanilla；有继承链但没特征 → 可能是自制版本（仍算 Vanilla，后续由用户覆盖）
        return LoaderType.Vanilla;
    }

    // ===================== 继承链构建 =====================

    private void ResolveInheritance(
        ExtendedVersion v,
        Dictionary<string, ExtendedVersion> all,
        HashSet<string> visiting)
    {
        // 已处理过
        if (v.InheritanceChain != null) return;

        // 循环引用检测
        if (visiting.Contains(v.Id))
        {
            v.InheritanceChain = Array.Empty<ExtendedVersion>();
            v.Status = VersionStatus.Error;
            v.ErrorMessage = "继承链存在循环引用";
            return;
        }

        // 无继承 → 链就是自己
        if (string.IsNullOrEmpty(v.InheritsFrom))
        {
            v.InheritanceChain = new[] { v };
            return;
        }

        // 父版本不存在
        if (!all.TryGetValue(v.InheritsFrom, out var parent))
        {
            v.InheritanceChain = new[] { v };
            v.Status = VersionStatus.Error;
            v.ErrorMessage = $"父版本 {v.InheritsFrom} 不存在";
            return;
        }

        // 递归解析父
        visiting.Add(v.Id);
        ResolveInheritance(parent, all, visiting);
        visiting.Remove(v.Id);

        // 当前版本链 = 自己 + 父链
        var chain = new List<ExtendedVersion>(parent.InheritanceChain!) { v };
        v.InheritanceChain = chain;
    }

    // ===================== 完整性检测 =====================

    private static void CheckLibrariesIntegrity(GameFolder folder, JsonObject json, ExtendedVersion v)
    {
        if (json.TryGetPropertyValue("libraries", out var libsNode) && libsNode is JsonArray libsArr)
        {
            int missing = 0;
            foreach (var lib in libsArr)
            {
                var name = lib?["name"]?.GetValue<string>();
                if (string.IsNullOrEmpty(name)) continue;
                var ruleAction = lib?["rules"]?.AsArray()?
                    .OfType<JsonObject>().FirstOrDefault()?["action"]?.GetValue<string>();
                if (ruleAction == "disallow") continue;

                // Maven 坐标 → 相对路径：group/artifact/version/artifact-version.jar
                var parts = name.Split(':');
                if (parts.Length < 3) continue;
                var relative = Path.Combine(
                    parts[0].Replace('.', '/'),
                    parts[1],
                    parts[2],
                    $"{parts[1]}-{parts[2]}.jar");
                var full = Path.Combine(folder.LibrariesDir, relative);
                if (!File.Exists(full)) missing++;
            }
            if (missing > 0)
            {
                v.Status = v.Status == VersionStatus.Normal
                    ? VersionStatus.MissingLibraries
                    : v.Status;
                v.ErrorMessage = v.Status == VersionStatus.Normal
                    ? $"缺失 {missing} 个依赖库"
                    : v.ErrorMessage + $"；另缺 {missing} 个依赖库";
            }
        }
    }

    private static void CheckAssetsIntegrity(GameFolder folder, JsonObject json, ExtendedVersion v)
    {
        if (json.TryGetPropertyValue("assetIndex", out var aiNode) && aiNode is JsonObject aiObj)
        {
            var url = aiObj.TryGetPropertyValue("url", out var urlNode) ? urlNode?.GetValue<string>() : null;
            // 只检测 assetIndex 文件是否下载了
            var aiId = aiObj.TryGetPropertyValue("id", out var idNode) ? idNode?.GetValue<string>() : "";
            var indexesDir = Path.Combine(folder.AssetsDir, "indexes");
            var localIndex = Path.Combine(indexesDir, aiId + ".json");
            if (!string.IsNullOrEmpty(url) && !File.Exists(localIndex))
            {
                v.Status = v.Status == VersionStatus.Normal
                    ? VersionStatus.MissingAssets
                    : v.Status;
                v.ErrorMessage = v.Status == VersionStatus.Normal
                    ? "缺失 assetIndex"
                    : v.ErrorMessage + "；另缺失 assetIndex";
            }
        }
    }

    private static void ComputeSizeAndMods(GameFolder folder, ExtendedVersion v)
    {
        try
        {
            var versionDir = new DirectoryInfo(Path.Combine(folder.VersionsDir, v.Id));
            if (versionDir.Exists)
                v.SizeBytes = versionDir.EnumerateFiles("*", SearchOption.AllDirectories)
                    .Sum(f => f.Length);
        }
        catch
        {
            v.SizeBytes = 0;
        }

        // mods 数量：扫描两种可能路径，取数量多的那个（无需知道全局隔离状态）
        try
        {
            var isolatedMods = Path.Combine(folder.VersionsDir, v.Id, "mods");
            var sharedMods = Path.Combine(folder.FolderPath, "mods");
            int isolatedCount = Directory.Exists(isolatedMods)
                ? Directory.EnumerateFiles(isolatedMods, "*.jar", SearchOption.TopDirectoryOnly).Count()
                : 0;
            int sharedCount = Directory.Exists(sharedMods)
                ? Directory.EnumerateFiles(sharedMods, "*.jar", SearchOption.TopDirectoryOnly).Count()
                : 0;
            v.ModCount = Math.Max(isolatedCount, sharedCount);
        }
        catch { v.ModCount = 0; }
    }
}
