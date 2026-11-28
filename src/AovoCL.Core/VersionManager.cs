using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AovoCL.Core;

public sealed record InstalledVersion(string Id, string Type, string DirectoryPath, long SizeBytes, DateTime Modified);

/// <summary>本地版本管理：扫描 / 重命名 / 删除 / 打开目录。</summary>
public sealed class VersionManager
{
    private readonly GamePaths _paths;

    public VersionManager(GamePaths paths) => _paths = paths;

    public IReadOnlyList<InstalledVersion> Scan()
    {
        if (!Directory.Exists(_paths.VersionsDir)) return Array.Empty<InstalledVersion>();

        var list = new List<InstalledVersion>();
        foreach (var dir in Directory.EnumerateDirectories(_paths.VersionsDir))
        {
            var id = Path.GetFileName(dir);
            var json = Path.Combine(dir, id + ".json");
            if (!File.Exists(json)) continue;

            list.Add(new InstalledVersion(id, ReadType(json), dir, MeasureSize(dir), Directory.GetLastWriteTime(dir)));
        }

        return list.OrderByDescending(v => v.Modified).ToList();
    }

    public void Delete(string id)
    {
        var dir = _paths.VersionDir(id);
        if (!Directory.Exists(dir)) throw new DirectoryNotFoundException($"版本 {id} 不存在");
        Directory.Delete(dir, true);
    }

    /// <summary>重命名：目录、json、jar 同步改名，并改写 json 内的 id 字段。</summary>
    public string Rename(string oldId, string newId)
    {
        newId = newId.Trim();
        if (string.IsNullOrWhiteSpace(newId))
            throw new ArgumentException("版本名不能为空");
        if (newId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || newId.Contains('\\') || newId.Contains('/'))
            throw new ArgumentException("版本名包含非法字符");
        if (newId.Length > 64)
            throw new ArgumentException("版本名过长");

        var source = _paths.VersionDir(oldId);
        var target = _paths.VersionDir(newId);
        if (!Directory.Exists(source))
            throw new DirectoryNotFoundException($"版本 {oldId} 不存在");
        if (Directory.Exists(target))
            throw new InvalidOperationException($"版本 {newId} 已存在");

        Directory.Move(source, target);

        try
        {
            var oldJson = Path.Combine(target, oldId + ".json");
            var newJson = Path.Combine(target, newId + ".json");
            if (File.Exists(oldJson)) File.Move(oldJson, newJson);

            if (File.Exists(newJson) && JsonNode.Parse(File.ReadAllText(newJson)) is JsonObject node)
            {
                node["id"] = newId;
                File.WriteAllText(newJson, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            }

            var oldJar = Path.Combine(target, oldId + ".jar");
            if (File.Exists(oldJar)) File.Move(oldJar, Path.Combine(target, newId + ".jar"));
        }
        catch
        {
            // 改名中途失败时尽量回滚目录
            try { if (Directory.Exists(target) && !Directory.Exists(source)) Directory.Move(target, source); }
            catch { /* 忽略 */ }
            throw;
        }

        return newId;
    }

    public void OpenFolder(string id)
    {
        var path = _paths.VersionDir(id);
        if (!Directory.Exists(path)) throw new DirectoryNotFoundException($"版本 {id} 不存在");

        if (OperatingSystem.IsWindows())
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        else if (OperatingSystem.IsMacOS())
            Process.Start(new ProcessStartInfo("open") { ArgumentList = { path } });
        else
            Process.Start(new ProcessStartInfo("xdg-open") { ArgumentList = { path } });
    }

    public static bool IsValidName(string name) =>
        !string.IsNullOrWhiteSpace(name) &&
        name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
        !name.Contains('/') && !name.Contains('\\');

    private static string ReadType(string jsonPath)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(jsonPath));
            return document.RootElement.TryGetProperty("type", out var type) ? type.GetString() ?? "release" : "release";
        }
        catch
        {
            return "release";
        }
    }

    private static long MeasureSize(string dir)
    {
        try
        {
            return Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Sum(f =>
            {
                try { return new FileInfo(f).Length; } catch { return 0L; }
            });
        }
        catch
        {
            return 0;
        }
    }
}
