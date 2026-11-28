using System.IO;
using System.Text.Json.Serialization;

namespace AovoCL.Core.VersionManagement;

/// <summary>一个独立的 Minecraft 安装位置（游戏文件夹）。</summary>
public sealed class GameFolder
{
    public GameFolder() { }

    public GameFolder(string id, string path, string name, bool isDefault = false)
    {
        Id = id;
        FolderPath = path;
        Name = name;
        IsDefault = isDefault;
    }

    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>磁盘绝对路径。</summary>
    [JsonPropertyName("path")]
    public string FolderPath { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("is_default")]
    public bool IsDefault { get; set; }

    [JsonIgnore]
    public string VersionsDir => System.IO.Path.Combine(FolderPath, "versions");

    [JsonIgnore]
    public string LibrariesDir => System.IO.Path.Combine(FolderPath, "libraries");

    [JsonIgnore]
    public string AssetsDir => System.IO.Path.Combine(FolderPath, "assets");

    [JsonIgnore]
    public string ModsDir => System.IO.Path.Combine(FolderPath, "mods");

    public bool IsValidDirectory() =>
        !string.IsNullOrWhiteSpace(FolderPath) &&
        Directory.Exists(FolderPath) &&
        Directory.Exists(VersionsDir);

    /// <summary>默认 .minecraft 文件夹路径探测。</summary>
    public static string DetectDefaultPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrEmpty(appData))
            return System.IO.Path.Combine(appData, "minecraft");
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrEmpty(home)
            ? System.IO.Path.Combine(".", ".minecraft")
            : System.IO.Path.Combine(home, ".minecraft");
    }
}
