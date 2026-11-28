using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AovoCL.Core.VersionManagement;

/// <summary>
/// 多游戏文件夹管理器：负责文件夹列表 CRUD、活动文件夹切换、持久化。
/// 持久化文件：{app_data}/AovoCL/folders.json
/// </summary>
public sealed class GameFolderManager
{
    private readonly string _persistDir;
    private readonly string _foldersJsonPath;
    private readonly string _versionsPerFolderDir;
    private readonly ConcurrentDictionary<string, GameFolder> _folders = new();

    public const string DefaultFolderId = "folder_default";

    public GameFolderManager()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrEmpty(appData))
            appData = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _persistDir = Path.Combine(appData, "AovoCL");
        _foldersJsonPath = Path.Combine(_persistDir, "folders.json");
        _versionsPerFolderDir = Path.Combine(_persistDir, "versions");
    }

    public GameFolderManager(string persistDir)
    {
        _persistDir = persistDir;
        _foldersJsonPath = Path.Combine(persistDir, "folders.json");
        _versionsPerFolderDir = Path.Combine(persistDir, "versions");
    }

    public IReadOnlyList<GameFolder> Folders => _folders.Values.OrderByDescending(f => f.IsDefault).ThenBy(f => f.Name).ToList();

    public GameFolder? ActiveFolder { get; private set; }

    /// <summary>全局隔离默认值（用户偏好，持久化）。</summary>
    public bool GlobalIsolationDefault { get; private set; }

    public event EventHandler<GameFolder?>? ActiveFolderChanged;
    public event EventHandler? FoldersChanged;

    // ===================== 初始化 =====================

    /// <summary>加载持久化数据；若无则自动探测默认 .minecraft 文件夹。</summary>
    public void Initialize()
    {
        Directory.CreateDirectory(_persistDir);
        Directory.CreateDirectory(_versionsPerFolderDir);

        if (File.Exists(_foldersJsonPath))
        {
            try
            {
                var json = File.ReadAllText(_foldersJsonPath);
                var root = JsonSerializer.Deserialize<FoldersPersist>(json);
                if (root != null)
                {
                    GlobalIsolationDefault = root.GlobalIsolationDefault;
                    foreach (var f in root.Folders ?? [])
                        _folders.TryAdd(f.Id, f);
                    if (!string.IsNullOrEmpty(root.ActiveFolderId) && _folders.TryGetValue(root.ActiveFolderId, out var active))
                        ActiveFolder = active;
                }
            }
            catch
            {
                // 持久化损坏，回退到自动探测
            }
        }

        // 无任何文件夹 → 自动添加默认 .minecraft
        if (_folders.IsEmpty)
        {
            var defaultPath = GameFolder.DetectDefaultPath();
            var folder = new GameFolder(DefaultFolderId, defaultPath, "默认文件夹", isDefault: true);
            _folders.TryAdd(folder.Id, folder);
            ActiveFolder = folder;
        }

        // 如果没有活动文件夹，选第一个
        if (ActiveFolder == null && !_folders.IsEmpty)
            ActiveFolder = Folders[0];
    }

    // ===================== CRUD =====================

    public GameFolder Add(string path, string? displayName = null)
    {
        path = (path ?? "").Trim();
        if (string.IsNullOrEmpty(path))
            throw new ArgumentException("路径不能为空");
        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException($"路径不存在：{path}");

        // 去重（同路径）
        var existing = _folders.Values.FirstOrDefault(f =>
            string.Equals(f.FolderPath, path, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
            throw new InvalidOperationException($"该路径已绑定：{existing.Name}");

        var id = $"folder_{Guid.NewGuid().ToString("N")[..8]}";
        var name = displayName ?? System.IO.Path.GetFileName(path.TrimEnd(System.IO.Path.DirectorySeparatorChar));
        if (string.IsNullOrEmpty(name)) name = path;

        var folder = new GameFolder(id, path, name, isDefault: false);
        _folders.TryAdd(id, folder);
        FoldersChanged?.Invoke(this, EventArgs.Empty);
        Save();
        return folder;
    }

    public bool Remove(string folderId)
    {
        if (!_folders.TryGetValue(folderId, out var folder)) return false;
        if (folder.IsDefault)
            throw new InvalidOperationException("系统自动添加的默认文件夹不可删除（可先添加其他文件夹并切换活动）");

        if (!_folders.TryRemove(folderId, out _)) return false;

        // 如果删的是活动文件夹，切换到第一个
        if (ActiveFolder?.Id == folderId)
        {
            ActiveFolder = _folders.IsEmpty ? null : Folders[0];
            ActiveFolderChanged?.Invoke(this, ActiveFolder);
        }

        // 清理该文件夹下的持久化版本数据
        var vjson = Path.Combine(_versionsPerFolderDir, $"{folderId}.json");
        if (File.Exists(vjson)) { try { File.Delete(vjson); } catch { } }

        FoldersChanged?.Invoke(this, EventArgs.Empty);
        Save();
        return true;
    }

    public bool Rename(string folderId, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName)) return false;
        if (!_folders.TryGetValue(folderId, out var folder)) return false;
        if (folder.Name == newName) return false;
        folder.Name = newName.Trim();
        FoldersChanged?.Invoke(this, EventArgs.Empty);
        Save();
        return true;
    }

    public bool SetActive(string folderId)
    {
        if (!_folders.TryGetValue(folderId, out var folder)) return false;
        if (ActiveFolder?.Id == folderId) return false;
        ActiveFolder = folder;
        ActiveFolderChanged?.Invoke(this, folder);
        Save();
        return true;
    }

    public bool SetGlobalIsolation(bool enabled)
    {
        if (GlobalIsolationDefault == enabled) return false;
        GlobalIsolationDefault = enabled;
        Save();
        return true;
    }

    // ===================== 持久化 =====================

    private void Save()
    {
        try
        {
            var root = new FoldersPersist
            {
                ActiveFolderId = ActiveFolder?.Id,
                GlobalIsolationDefault = GlobalIsolationDefault,
                Folders = _folders.Values.ToList()
            };
            var json = JsonSerializer.Serialize(root, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            Directory.CreateDirectory(_persistDir);
            File.WriteAllText(_foldersJsonPath, json);
        }
        catch
        {
            // 持久化失败不影响内存态
        }
    }

    // ===================== 内部类型 =====================

    private sealed class FoldersPersist
    {
        [JsonPropertyName("active_folder_id")]
        public string? ActiveFolderId { get; set; }

        [JsonPropertyName("global_isolation_default")]
        public bool GlobalIsolationDefault { get; set; }

        [JsonPropertyName("folders")]
        public List<GameFolder>? Folders { get; set; }
    }
}
