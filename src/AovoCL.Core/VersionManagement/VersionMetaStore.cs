using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AovoCL.Core.VersionManagement;

/// <summary>
/// 每个游戏文件夹下版本元数据的持久化存储（收藏、隔离覆盖、显示名、UI 筛选状态）。
/// 持久化路径：{persist_dir}/versions/{folderId}.json
/// </summary>
public sealed class VersionMetaStore
{
    private readonly string _versionsDir;
    private readonly ConcurrentDictionary<string, FolderMeta> _cache = new();

    public VersionMetaStore(string persistDir)
    {
        _versionsDir = Path.Combine(persistDir, "versions");
        Directory.CreateDirectory(_versionsDir);
    }

    /// <summary>从持久化加载某文件夹的元数据。</summary>
    public FolderMeta Load(string folderId)
    {
        if (_cache.TryGetValue(folderId, out var cached)) return cached;

        var path = Path.Combine(_versionsDir, $"{folderId}.json");
        FolderMeta meta;
        if (File.Exists(path))
        {
            try
            {
                var json = File.ReadAllText(path);
                meta = JsonSerializer.Deserialize<FolderMeta>(json) ?? new FolderMeta();
            }
            catch
            {
                meta = new FolderMeta();
            }
        }
        else
        {
            meta = new FolderMeta();
        }
        _cache.TryAdd(folderId, meta);
        return meta;
    }

    /// <summary>把元数据保存到磁盘（立即写）。</summary>
    public void Save(string folderId)
    {
        if (!_cache.TryGetValue(folderId, out var meta)) return;
        try
        {
            var path = Path.Combine(_versionsDir, $"{folderId}.json");
            var json = JsonSerializer.Serialize(meta, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            File.WriteAllText(path, json);
        }
        catch { }
    }

    /// <summary>把扫描结果中的版本与持久化元数据合并（用户偏好覆盖扫描默认值）。</summary>
    public void ApplyTo(string folderId, IEnumerable<ExtendedVersion> versions)
    {
        var meta = Load(folderId);
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var v in versions)
        {
            known.Add(v.Id);
            if (meta.Versions.TryGetValue(v.Id, out var vm))
            {
                v.IsFavorite = vm.IsFavorite;
                v.IsIsolated = vm.IsIsolated;
                v.IsIsolationOverridden = vm.IsIsolationOverridden;
                v.DisplayName = vm.DisplayName ?? "";
                if (!string.IsNullOrEmpty(v.DisplayName)) v.Name = v.DisplayName;
            }
        }

        // 清理不再存在的版本的持久化条目
        var staleKeys = meta.Versions.Keys.Where(k => !known.Contains(k)).ToList();
        foreach (var k in staleKeys)
            meta.Versions.Remove(k);
    }

    public void SetFavorite(string folderId, string versionId, bool isFavorite)
    {
        var meta = Load(folderId);
        EnsureVersionEntry(meta, versionId);
        meta.Versions[versionId].IsFavorite = isFavorite;
        Save(folderId);
    }

    public void SetIsolation(string folderId, string versionId, bool isIsolated, bool overridden)
    {
        var meta = Load(folderId);
        EnsureVersionEntry(meta, versionId);
        meta.Versions[versionId].IsIsolated = isIsolated;
        meta.Versions[versionId].IsIsolationOverridden = overridden;
        Save(folderId);
    }

    public void SetDisplayName(string folderId, string versionId, string displayName)
    {
        var meta = Load(folderId);
        EnsureVersionEntry(meta, versionId);
        meta.Versions[versionId].DisplayName = displayName;
        Save(folderId);
    }

    public void SetFilter(string folderId, CategoryFilter filter, string searchText, double scrollOffset)
    {
        var meta = Load(folderId);
        meta.FilterCategory = filter;
        meta.SearchText = searchText;
        meta.ScrollOffset = scrollOffset;
        Save(folderId);
    }

    // ===================== 内部 =====================

    private static void EnsureVersionEntry(FolderMeta meta, string versionId)
    {
        if (!meta.Versions.ContainsKey(versionId))
            meta.Versions[versionId] = new VersionMetaEntry();
    }

    public sealed class FolderMeta
    {
        [JsonPropertyName("filter_category")]
        public CategoryFilter FilterCategory { get; set; } = CategoryFilter.All;

        [JsonPropertyName("search_text")]
        public string SearchText { get; set; } = "";

        [JsonPropertyName("scroll_offset")]
        public double ScrollOffset { get; set; }

        [JsonPropertyName("versions")]
        public Dictionary<string, VersionMetaEntry> Versions { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class VersionMetaEntry
    {
        [JsonPropertyName("is_favorite")]
        public bool IsFavorite { get; set; }

        [JsonPropertyName("is_isolated")]
        public bool IsIsolated { get; set; }

        [JsonPropertyName("is_isolation_overridden")]
        public bool IsIsolationOverridden { get; set; }

        [JsonPropertyName("display_name")]
        public string? DisplayName { get; set; }
    }
}
