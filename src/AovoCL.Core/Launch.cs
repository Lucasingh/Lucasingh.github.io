using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace AovoCL.Core;

/// <summary>离线账号：UUID 与 Java 的 UUID.nameUUIDFromBytes 保持一致（MD5 版本 3）。</summary>
public static class OfflineAuth
{
    public static OfflineProfile Create(string name)
    {
        var player = string.IsNullOrWhiteSpace(name) ? "Player" : name.Trim();
        return new OfflineProfile
        {
            Name = player,
            Uuid = UuidV3("OfflinePlayer:" + player),
            AccessToken = Guid.NewGuid().ToString("N")
        };
    }

    private static string UuidV3(string input)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(input));
        hash[6] = (byte)((hash[6] & 0x0F) | 0x30);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);

        var hex = Convert.ToHexString(hash).ToLowerInvariant();
        return $"{hex[..8]}-{hex[8..12]}-{hex[12..16]}-{hex[16..20]}-{hex[20..]}";
    }
}

public sealed class LaunchRequest
{
    public string VersionId { get; set; } = "";
    public string VersionUrl { get; set; } = "";
    public string PlayerName { get; set; } = "Player";
    public string GameDirectory { get; set; } = "";
    public double MemoryMb { get; set; } = 4096;
    public string? JavaPath { get; set; }
    public string JavaArguments { get; set; } = "";
}

public sealed class LaunchPlan
{
    public string JavaPath { get; set; } = "";
    public string MainClass { get; set; } = "";
    public string WorkingDirectory { get; set; } = "";
    public List<string> JvmArguments { get; set; } = new();
    public List<string> GameArguments { get; set; } = new();

    public ProcessStartInfo ToStartInfo()
    {
        var info = new ProcessStartInfo(JavaPath)
        {
            WorkingDirectory = WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true
        };

        foreach (var arg in JvmArguments) info.ArgumentList.Add(arg);
        info.ArgumentList.Add(MainClass);
        foreach (var arg in GameArguments) info.ArgumentList.Add(arg);
        return info;
    }

    public string CommandLine => string.Join(" ", new[] { JavaPath }
        .Concat(JvmArguments)
        .Concat(new[] { MainClass })
        .Concat(GameArguments)
        .Select(Quote));

    private static string Quote(string value) =>
        value.Contains(' ') || value.Contains('\t') ? $"\"{value}\"" : value;
}

/// <summary>按 PCL2 的方式拼装启动参数：先 jvm 再主类再游戏参数，占位符统一替换。</summary>
public sealed class LaunchPlanBuilder
{
    private static readonly Regex Placeholder = new(@"\$\{([^}]+)\}", RegexOptions.Compiled);

    private readonly GamePaths _paths;

    public LaunchPlanBuilder(GamePaths paths) => _paths = paths;

    public LaunchPlan Build(GameVersion version, LaunchRequest request, OfflineProfile profile, JavaRuntime java)
    {
        var gameDir = string.IsNullOrWhiteSpace(request.GameDirectory) ? _paths.Root : request.GameDirectory;
        Directory.CreateDirectory(gameDir);

        var classpath = BuildClasspath(version);
        var natives = _paths.NativesDir(version.Id);
        Directory.CreateDirectory(natives);

        var memory = Math.Max(512, (int)request.MemoryMb);
        var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["auth_player_name"] = profile.Name,
            ["version_name"] = version.Id,
            ["game_directory"] = gameDir,
            ["assets_root"] = _paths.AssetsDir,
            ["game_assets"] = Directory.Exists(Path.Combine(_paths.AssetsDir, "virtual", version.AssetIndex?.Id ?? ""))
                ? Path.Combine(_paths.AssetsDir, "virtual", version.AssetIndex?.Id ?? "")
                : _paths.AssetsDir,
            ["assets_index_name"] = version.AssetIndex?.Id ?? version.Assets ?? "legacy",
            ["auth_uuid"] = profile.Uuid,
            ["auth_access_token"] = profile.AccessToken,
            ["auth_session"] = profile.AccessToken,
            ["auth_xuid"] = "0",
            ["clientid"] = "aovocl",
            ["user_type"] = "legacy",
            ["version_type"] = version.Type,
            ["natives_directory"] = natives,
            ["launcher_name"] = "AovoCL",
            ["launcher_version"] = "0.2.0",
            ["classpath"] = classpath,
            ["user_properties"] = "{}"
        };

        var features = new Dictionary<string, bool>
        {
            ["is_demo_user"] = false,
            ["has_custom_resolution"] = false,
            ["has_quick_plays_support"] = false,
            ["is_quick_play_singleplayer"] = false,
            ["is_quick_play_multiplayer"] = false,
            ["is_quick_play_realms"] = false
        };

        var jvm = new List<string>
        {
            $"-Xmx{memory}M",
            $"-Xms{Math.Max(512, memory / 2)}M",
            "-XX:+UseG1GC",
            "-Dlog4j2.formatMsgNoLookups=true",
            $"-Djava.library.path={natives}",
            "-Dminecraft.launcher.brand=AovoCL",
            "-Dminecraft.launcher.brand.version=0.2.0"
        };

        if (!string.IsNullOrWhiteSpace(request.JavaArguments))
            jvm.AddRange(SplitArguments(request.JavaArguments));

        if (version.JvmArguments.Count > 0)
        {
            foreach (var entry in version.JvmArguments)
            {
                if (!RuleEvaluator.IsAllowed(entry.Rules, features)) continue;
                jvm.AddRange(entry.Values.Select(v => Substitute(v, replacements)));
            }
        }
        else
        {
            jvm.Add("-cp");
            jvm.Add(classpath);
        }

        var game = new List<string>();
        if (version.GameArguments.Count > 0)
        {
            foreach (var entry in version.GameArguments)
            {
                if (!RuleEvaluator.IsAllowed(entry.Rules, features)) continue;
                game.AddRange(entry.Values.Select(v => Substitute(v, replacements)));
            }
        }
        else if (!string.IsNullOrEmpty(version.LegacyArguments))
        {
            game.AddRange(SplitArguments(version.LegacyArguments).Select(v => Substitute(v, replacements)));
        }

        if (version.Logging?.File != null && !string.IsNullOrEmpty(version.Logging.Argument))
        {
            var logPath = Path.Combine(_paths.LogConfigsDir, version.Logging.File.Id ?? "client.xml");
            jvm.Add(Substitute(version.Logging.Argument.Replace("${path}", logPath), replacements));
        }

        return new LaunchPlan
        {
            JavaPath = java.Path,
            MainClass = version.MainClass,
            WorkingDirectory = gameDir,
            JvmArguments = jvm,
            GameArguments = game
        };
    }

    private string BuildClasspath(GameVersion version)
    {
        var separator = Path.PathSeparator.ToString();
        var entries = new List<string>();

        foreach (var library in version.Libraries)
        {
            if (library.IsNative) continue;
            if (!RuleEvaluator.IsAllowed(library.Rules)) continue;

            var path = Path.Combine(_paths.LibrariesDir,
                library.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path)) entries.Add(path);
        }

        var jarId = string.IsNullOrEmpty(version.ClientJarVersionId) ? version.Id : version.ClientJarVersionId;
        var clientJar = _paths.VersionJar(jarId);
        if (File.Exists(clientJar)) entries.Add(clientJar);

        return string.Join(separator, entries);
    }

    private static string Substitute(string value, Dictionary<string, string> map) =>
        Placeholder.Replace(value, m => map.TryGetValue(m.Groups[1].Value, out var replacement) ? replacement : "");

    private static IEnumerable<string> SplitArguments(string raw) =>
        raw.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
