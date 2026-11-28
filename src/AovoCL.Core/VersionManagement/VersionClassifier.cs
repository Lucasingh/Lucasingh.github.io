using System.Text.RegularExpressions;

namespace AovoCL.Core.VersionManagement;

/// <summary>
/// 版本自动分类 + 排序。
/// 分类优先级：收藏 → 模组 → 原版类 → 其他
/// 同类别内按语义化版本号降序，无法解析则按安装时间降序。
/// </summary>
public sealed class VersionClassifier
{
    private static readonly Regex SemverRegex = new(
        @"^(\d+)\.(\d+)(?:\.(\d+))?(?:[-_][A-Za-z0-9.]+)?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>为每个版本自动分配 Category（根据收藏/加载器/状态综合判定）。</summary>
    public void AssignCategory(IEnumerable<ExtendedVersion> versions)
    {
        foreach (var v in versions)
        {
            v.Category = ComputeCategory(v);
        }
    }

    private static VersionCategory ComputeCategory(ExtendedVersion v)
    {
        // 1. 收藏优先级最高
        if (v.IsFavorite) return VersionCategory.Favorite;

        // 2. 模组类：有加载器 或 有 mods
        if (v.Loader != LoaderType.Vanilla && v.Loader != LoaderType.None)
            return VersionCategory.Modded;
        if (v.ModCount > 0)
            return VersionCategory.Modded;

        // 3. 原版类：release + vanilla（或仅 OptiFine）
        if (v.Type == VersionType.Release &&
            (v.Loader == LoaderType.Vanilla || v.Loader == LoaderType.OptiFine))
            return VersionCategory.OriginalLike;

        // 4. 错误状态也算其他
        if (v.Status != VersionStatus.Normal)
            return VersionCategory.Other;

        // 5. 其余（snapshot / old_* / april_fools / unknown）都归其他
        return VersionCategory.Other;
    }

    /// <summary>按 Category 优先级 + 语义化版本号排序。
    /// Category 枚举值 Favorite=0 → Modded=1 → OriginalLike=2 → Other=3，升序即正确优先级。</summary>
    public IReadOnlyList<ExtendedVersion> Sort(IEnumerable<ExtendedVersion> versions)
    {
        return versions
            .OrderBy(v => (int)v.Category)
            .ThenByDescending(v => ParseSemverRank(v.Id))
            .ThenByDescending(v => v.InstalledAt)
            .ToList();
    }

    /// <summary>
    /// 尝试把版本 id 解析成语义化版本三元组用于排序。
    /// 例如 "1.20.1-forge-47.3.0" → (1,20,1)
    /// 返回 (0,0,0) 表示无法解析。
    /// </summary>
    private static (int Major, int Minor, int Patch) ParseSemverRank(string id)
    {
        if (string.IsNullOrEmpty(id)) return (0, 0, 0);
        var match = SemverRegex.Match(id);
        if (!match.Success) return (0, 0, 0);

        int.TryParse(match.Groups[1].Value, out var major);
        int.TryParse(match.Groups[2].Value, out var minor);
        int.TryParse(match.Groups[3].Value, out var patch);
        return (major, minor, patch);
    }

    /// <summary>
    /// 过滤版本列表到指定 CategoryFilter。
    /// </summary>
    public IEnumerable<ExtendedVersion> Filter(IEnumerable<ExtendedVersion> versions, CategoryFilter filter)
    {
        return filter switch
        {
            CategoryFilter.All => versions,
            CategoryFilter.Favorite => versions.Where(v => v.Category == VersionCategory.Favorite),
            CategoryFilter.Modded => versions.Where(v => v.Category == VersionCategory.Modded),
            CategoryFilter.OriginalLike => versions.Where(v => v.Category == VersionCategory.OriginalLike),
            CategoryFilter.Other => versions.Where(v => v.Category == VersionCategory.Other),
            _ => versions
        };
    }

    /// <summary>
    /// 按搜索关键词过滤（id + displayName + 类型 + 加载器名）。
    /// </summary>
    public IEnumerable<ExtendedVersion> Search(IEnumerable<ExtendedVersion> versions, string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return versions;
        var kw = keyword.Trim().ToLowerInvariant();
        return versions.Where(v =>
            v.Id.ToLowerInvariant().Contains(kw) ||
            (v.DisplayName?.ToLowerInvariant().Contains(kw) == true) ||
            (v.Name?.ToLowerInvariant().Contains(kw) == true) ||
            v.Type.ToString().ToLowerInvariant().Contains(kw) ||
            v.Loader.ToString().ToLowerInvariant().Contains(kw));
    }
}
