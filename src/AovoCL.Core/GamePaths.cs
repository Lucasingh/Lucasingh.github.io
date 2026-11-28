namespace AovoCL.Core;

/// <summary>游戏目录布局，与官方/PCL2 一致，便于互相导入。</summary>
public sealed class GamePaths
{
    public GamePaths(string root) => Root = root;

    public string Root { get; }

    public string VersionsDir => Path.Combine(Root, "versions");
    public string LibrariesDir => Path.Combine(Root, "libraries");
    public string AssetsDir => Path.Combine(Root, "assets");
    public string IndexesDir => Path.Combine(AssetsDir, "indexes");
    public string ObjectsDir => Path.Combine(AssetsDir, "objects");
    public string LogConfigsDir => Path.Combine(AssetsDir, "log_configs");
    public string RuntimeDir => Path.Combine(Root, "runtime");
    public string CacheDir => Path.Combine(Root, "cache");

    public string VersionDir(string id) => Path.Combine(VersionsDir, id);
    public string VersionJson(string id) => Path.Combine(VersionDir(id), id + ".json");
    public string VersionJar(string id) => Path.Combine(VersionDir(id), id + ".jar");
    public string NativesDir(string id) => Path.Combine(VersionDir(id), "natives");

    public static string DefaultRoot
    {
        get
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrEmpty(appData)) appData = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(appData, "AovoCL");
        }
    }

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(VersionsDir);
        Directory.CreateDirectory(LibrariesDir);
        Directory.CreateDirectory(ObjectsDir);
        Directory.CreateDirectory(IndexesDir);
        Directory.CreateDirectory(LogConfigsDir);
        Directory.CreateDirectory(CacheDir);
    }
}
