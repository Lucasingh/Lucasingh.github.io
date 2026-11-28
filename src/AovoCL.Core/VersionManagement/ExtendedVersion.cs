using System.Text.Json.Serialization;

namespace AovoCL.Core.VersionManagement;

/// <summary>版本扫描后生成的完整版本信息（面向 UI 消费）。</summary>
public sealed class ExtendedVersion
{
    // ===== 基础字段 =====
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("folder_id")]
    public string FolderId { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonIgnore]
    public string VersionJsonPath { get; set; } = "";

    [JsonIgnore]
    public string JarPath { get; set; } = "";

    // ===== 类型字段 =====
    [JsonPropertyName("type")]
    public VersionType Type { get; set; } = VersionType.Unknown;

    [JsonPropertyName("loader")]
    public LoaderType Loader { get; set; } = LoaderType.None;

    [JsonPropertyName("inherits_from")]
    public string? InheritsFrom { get; set; }

    [JsonIgnore]
    public IReadOnlyList<ExtendedVersion>? InheritanceChain { get; set; }

    // ===== 状态字段 =====
    [JsonPropertyName("status")]
    public VersionStatus Status { get; set; } = VersionStatus.Normal;

    [JsonIgnore]
    public string? ErrorMessage { get; set; }

    // ===== 用户偏好字段（持久化） =====
    [JsonPropertyName("is_isolated")]
    public bool IsIsolated { get; set; }

    [JsonPropertyName("is_favorite")]
    public bool IsFavorite { get; set; }

    [JsonPropertyName("is_isolation_overridden")]
    public bool IsIsolationOverridden { get; set; }

    // ===== 自动分类字段 =====
    [JsonPropertyName("category")]
    public VersionCategory Category { get; set; } = VersionCategory.Other;

    // ===== 辅助字段 =====
    [JsonIgnore]
    public DateTime InstalledAt { get; set; } = DateTime.MinValue;

    [JsonIgnore]
    public long SizeBytes { get; set; }

    [JsonPropertyName("mod_count")]
    public int ModCount { get; set; }

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = "";

    /// <summary>最终隔离状态（考虑全局开关 + 本地覆盖）。</summary>
    public bool ResolveIsolation(bool globalIsolationDefault) =>
        IsIsolationOverridden ? IsIsolated : globalIsolationDefault;

    // ===== UI 辅助属性（纯计算，不持久化） =====

    /// <summary>卡片副标题文本：类型 · 加载器 · 模组数。</summary>
    public string Subtitle
    {
        get
        {
            var type = Type switch
            {
                VersionType.Release => "正式版",
                VersionType.Snapshot => "快照",
                VersionType.OldBeta => "Beta",
                VersionType.OldAlpha => "Alpha",
                VersionType.AprilFools => "愚人节",
                _ => "未知"
            };
            var loader = Loader switch
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
            var mods = ModCount > 0 ? $" · {ModCount} 模组" : "";
            return $"{type} · {loader}{mods}";
        }
    }

    /// <summary>卡片徽章的加载器缩写（2–3 个字母）。</summary>
    public string LoaderAbbr => Loader switch
    {
        LoaderType.Vanilla => "MC",
        LoaderType.Forge => "FG",
        LoaderType.Fabric => "FB",
        LoaderType.NeoForge => "NF",
        LoaderType.Quilt => "QL",
        LoaderType.OptiFine => "OF",
        LoaderType.LiteLoader => "LL",
        _ => "?"
    };

    /// <summary>卡片里显示的状态标签（仅 error / missing 时非空）。</summary>
    public string StatusLabel => Status switch
    {
        VersionStatus.Error => "错误",
        VersionStatus.MissingAssets => "缺资源",
        VersionStatus.MissingLibraries => "缺依赖",
        _ => ""
    };

    /// <summary>是否需要显示错误状态徽章。</summary>
    public bool IsErrorStatus => Status != VersionStatus.Normal;

    /// <summary>版本目录大小的可读文本。</summary>
    public string SizeText
    {
        get
        {
            if (SizeBytes <= 0) return "";
            double mb = SizeBytes / 1024.0 / 1024.0;
            return mb >= 1024 ? $"{mb / 1024:0.0} GB" : $"{mb:0} MB";
        }
    }

    /// <summary>
    /// 运行时计算好的隔离最终状态（HomeViewModel 在给 UI 前批量赋值）。
    /// 不参与 JSON 持久化。
    /// </summary>
    [JsonIgnore]
    public bool IsIsolatedResolved { get; set; }
}
