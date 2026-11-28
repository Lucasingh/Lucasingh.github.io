using System.Collections.Concurrent;

namespace AovoCL.Core.VersionManagement;

/// <summary>
/// 版本管理总门面：对 UI 层暴露一个统一入口。
/// 持有 GameFolderManager、VersionScanner、VersionClassifier、VersionMetaStore。
/// </summary>
public sealed class VersionHub
{
    private readonly GameFolderManager _folderMgr;
    private readonly VersionScanner _scanner = new();
    private readonly VersionClassifier _classifier = new();
    private readonly ConcurrentDictionary<string, List<ExtendedVersion>> _scanCache = new();
    private readonly VersionMetaStore _metaStore;

    public VersionHub()
    {
        _folderMgr = new GameFolderManager();
        _folderMgr.Initialize();
        _metaStore = new VersionMetaStore(GetPersistDir());
        _folderMgr.ActiveFolderChanged += (_, _) => _scanCache.Clear();
        _folderMgr.FoldersChanged += (_, _) => _scanCache.Clear();
    }

    public VersionHub(GameFolderManager folderMgr, VersionMetaStore metaStore)
    {
        _folderMgr = folderMgr;
        _metaStore = metaStore;
    }

    // ===================== 对外属性 =====================

    public GameFolderManager Folders => _folderMgr;
    public VersionMetaStore MetaStore => _metaStore;
    public VersionClassifier Classifier => _classifier;

    public GameFolder? ActiveFolder => _folderMgr.ActiveFolder;

    // ===================== 扫描入口 =====================

    /// <summary>
    /// 扫描活动文件夹（或指定文件夹）的所有版本，应用用户偏好，返回已分类排序的列表。
    /// </summary>
    public async Task<IReadOnlyList<ExtendedVersion>> ScanActiveAsync(CancellationToken ct = default)
    {
        var folder = _folderMgr.ActiveFolder
            ?? throw new InvalidOperationException("没有活动的游戏文件夹");
        return await ScanAsync(folder, ct);
    }

    public async Task<IReadOnlyList<ExtendedVersion>> ScanAsync(GameFolder folder, CancellationToken ct = default)
    {
        var raw = await _scanner.ScanAsync(folder, ct);

        // 合并用户持久化偏好
        _metaStore.ApplyTo(folder.Id, raw);

        // 自动分类
        _classifier.AssignCategory(raw);

        // 排序
        var sorted = _classifier.Sort(raw);

        // 缓存
        _scanCache[folder.Id] = new List<ExtendedVersion>(sorted);

        return sorted;
    }

    /// <summary>清缓存并重新扫描。</summary>
    public void InvalidateCache(string folderId) => _scanCache.TryRemove(folderId, out _);

    // ===================== 便捷操作 =====================

    public async Task<bool> ToggleFavoriteAsync(string versionId)
    {
        var folder = ActiveFolder ?? throw new InvalidOperationException("没有活动文件夹");
        var cache = _scanCache.TryGetValue(folder.Id, out var list) ? list : (await ScanAsync(folder)).ToList();
        var v = cache.FirstOrDefault(x => string.Equals(x.Id, versionId, StringComparison.OrdinalIgnoreCase));
        if (v == null) return false;
        v.IsFavorite = !v.IsFavorite;
        _metaStore.SetFavorite(folder.Id, versionId, v.IsFavorite);
        _classifier.AssignCategory(cache); // 重分类
        return true;
    }

    public async Task<bool> ToggleIsolationAsync(string versionId, bool? overridden = null)
    {
        var folder = ActiveFolder ?? throw new InvalidOperationException("没有活动文件夹");
        var cache = _scanCache.TryGetValue(folder.Id, out var list) ? list : (await ScanAsync(folder)).ToList();
        var v = cache.FirstOrDefault(x => string.Equals(x.Id, versionId, StringComparison.OrdinalIgnoreCase));
        if (v == null) return false;

        var willOverride = overridden ?? !v.IsIsolationOverridden;
        v.IsIsolationOverridden = willOverride;
        if (willOverride) v.IsIsolated = !v.IsIsolated;

        _metaStore.SetIsolation(folder.Id, versionId, v.IsIsolated, v.IsIsolationOverridden);
        return true;
    }

    // ===================== 持久化 =====================

    private static string GetPersistDir()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrEmpty(appData))
            appData = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(appData, "AovoCL");
    }
}
