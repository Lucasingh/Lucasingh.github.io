using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace AovoCL.Core;

/// <summary>PCL2 同款思路：规则不满足即跳过，最后一条命中规则决定结果。</summary>
public static class RuleEvaluator
{
    public static string OsName =>
        OperatingSystem.IsWindows() ? "windows" :
        OperatingSystem.IsMacOS() ? "osx" : "linux";

    public static string OsArch => RuntimeInformation.ProcessArchitecture switch
    {
        Architecture.X86 => "x86",
        Architecture.Arm64 => "arm64",
        _ => "x64"
    };

    public static bool IsAllowed(IReadOnlyList<GameRule> rules, IReadOnlyDictionary<string, bool>? features = null)
    {
        if (rules.Count == 0) return true;

        var allowed = false;
        foreach (var rule in rules)
        {
            if (Matches(rule, features))
                allowed = string.Equals(rule.Action, "allow", StringComparison.OrdinalIgnoreCase);
        }
        return allowed;
    }

    private static bool Matches(GameRule rule, IReadOnlyDictionary<string, bool>? features)
    {
        if (rule.OsName != null && !string.Equals(rule.OsName, OsName, StringComparison.OrdinalIgnoreCase))
            return false;

        if (rule.OsArch != null && !string.Equals(rule.OsArch, OsArch, StringComparison.OrdinalIgnoreCase))
            return false;

        if (rule.OsVersion != null)
        {
            if (!Regex.IsMatch(Environment.OSVersion.Version.ToString(), rule.OsVersion))
                return false;
        }

        if (rule.Features is { Count: > 0 })
        {
            foreach (var (key, expected) in rule.Features)
            {
                var actual = features != null && features.TryGetValue(key, out var v) && v;
                if (actual != expected) return false;
            }
        }

        return true;
    }
}

/// <summary>Maven 坐标 → 仓库相对路径。</summary>
public static class MavenPath
{
    public static (string Path, string Url) FromName(string name, string? classifierOverride = null)
    {
        var extension = "jar";
        var at = name.IndexOf('@');
        if (at >= 0)
        {
            extension = name[(at + 1)..];
            name = name[..at];
        }

        var parts = name.Split(':');
        var group = parts.Length > 0 ? parts[0] : "";
        var artifact = parts.Length > 1 ? parts[1] : "";
        var version = parts.Length > 2 ? parts[2] : "";
        var classifier = classifierOverride ?? (parts.Length > 3 ? parts[3] : null);

        var file = $"{artifact}-{version}";
        if (!string.IsNullOrEmpty(classifier)) file += "-" + classifier;
        file += "." + extension;

        var path = $"{group.Replace('.', '/')}/{artifact}/{version}/{file}";
        return (path, "https://libraries.minecraft.net/" + path);
    }
}
