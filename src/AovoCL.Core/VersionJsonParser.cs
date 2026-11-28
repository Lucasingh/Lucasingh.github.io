using System.Text.Json;

namespace AovoCL.Core;

/// <summary>把版本 JSON 解析成 GameVersion。手写解析是为了兼容 string / object 混合的 arguments。</summary>
public static class VersionJsonParser
{
    public static GameVersion Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var version = new GameVersion
        {
            Id = Str(root, "id") ?? "",
            Type = Str(root, "type") ?? "release",
            MainClass = Str(root, "mainClass") ?? "",
            InheritsFrom = Str(root, "inheritsFrom"),
            Assets = Str(root, "assets"),
            LegacyArguments = Str(root, "minecraftArguments")
        };

        if (root.TryGetProperty("assetIndex", out var index))
        {
            version.AssetIndex = new AssetIndexRef
            {
                Id = Str(index, "id") ?? "",
                Url = Str(index, "url") ?? "",
                Sha1 = Str(index, "sha1"),
                Size = Num(index, "size"),
                TotalSize = Num(index, "totalSize")
            };
        }

        if (root.TryGetProperty("downloads", out var downloads) && downloads.TryGetProperty("client", out var client))
            version.Client = ParseDownload(client);

        if (root.TryGetProperty("javaVersion", out var java) && java.TryGetProperty("majorVersion", out var major))
            version.JavaVersionMajor = major.GetInt32();

        if (root.TryGetProperty("libraries", out var libraries))
        {
            foreach (var item in libraries.EnumerateArray())
                version.Libraries.Add(ParseLibrary(item));
        }

        if (root.TryGetProperty("arguments", out var arguments))
        {
            if (arguments.TryGetProperty("jvm", out var jvm))
                version.JvmArguments.AddRange(ParseArguments(jvm));
            if (arguments.TryGetProperty("game", out var game))
                version.GameArguments.AddRange(ParseArguments(game));
        }

        if (root.TryGetProperty("logging", out var logging) && logging.TryGetProperty("client", out var logClient))
        {
            version.Logging = new LoggingConfig
            {
                Argument = Str(logClient, "argument") ?? "",
                File = logClient.TryGetProperty("file", out var logFile) ? ParseDownload(logFile) : null
            };
        }

        return version;
    }

    private static List<ArgumentEntry> ParseArguments(JsonElement array)
    {
        var result = new List<ArgumentEntry>();
        foreach (var element in array.EnumerateArray())
        {
            var entry = new ArgumentEntry();
            if (element.ValueKind == JsonValueKind.String)
            {
                entry.Values.Add(element.GetString() ?? "");
            }
            else if (element.ValueKind == JsonValueKind.Object)
            {
                if (element.TryGetProperty("rules", out var rules))
                    entry.Rules.AddRange(ParseRules(rules));

                if (element.TryGetProperty("value", out var value))
                {
                    if (value.ValueKind == JsonValueKind.String)
                        entry.Values.Add(value.GetString() ?? "");
                    else if (value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var v in value.EnumerateArray())
                            if (v.ValueKind == JsonValueKind.String) entry.Values.Add(v.GetString()!);
                    }
                }
            }

            if (entry.Values.Count > 0 || entry.Rules.Count > 0)
                result.Add(entry);
        }
        return result;
    }

    public static List<GameRule> ParseRules(JsonElement rules)
    {
        var list = new List<GameRule>();
        if (rules.ValueKind != JsonValueKind.Array) return list;

        foreach (var rule in rules.EnumerateArray())
        {
            var r = new GameRule { Action = Str(rule, "action") ?? "allow" };

            if (rule.TryGetProperty("os", out var os))
            {
                r.OsName = Str(os, "name");
                r.OsArch = Str(os, "arch");
                r.OsVersion = Str(os, "version");
            }

            if (rule.TryGetProperty("features", out var features))
            {
                r.Features = new Dictionary<string, bool>();
                foreach (var f in features.EnumerateObject())
                    r.Features[f.Name] = f.Value.ValueKind == JsonValueKind.True;
            }

            list.Add(r);
        }
        return list;
    }

    private static Library ParseLibrary(JsonElement element)
    {
        var library = new Library
        {
            Name = Str(element, "name") ?? "",
            Rules = element.TryGetProperty("rules", out var rules) ? ParseRules(rules) : new List<GameRule>()
        };

        if (element.TryGetProperty("extract", out var extract) && extract.TryGetProperty("exclude", out var exclude))
        {
            foreach (var e in exclude.EnumerateArray())
                if (e.ValueKind == JsonValueKind.String) library.ExtractExclude.Add(e.GetString()!);
        }

        // 老版本：natives 是一个 { windows: "natives-windows", ... } 的字典
        string? nativesClassifier = null;
        if (element.TryGetProperty("natives", out var natives) &&
            natives.TryGetProperty(RuleEvaluator.OsName, out var classifier))
        {
            nativesClassifier = classifier.GetString()?.Replace("${arch}", RuleEvaluator.OsArch == "x86" ? "32" : "64");
            library.IsNative = true;
        }

        var assigned = false;
        if (element.TryGetProperty("downloads", out var downloads))
        {
            if (nativesClassifier != null &&
                downloads.TryGetProperty("classifiers", out var classifiers) &&
                classifiers.TryGetProperty(nativesClassifier, out var nativeDownload))
            {
                Apply(library, nativeDownload);
                assigned = true;
            }
            else if (downloads.TryGetProperty("artifact", out var artifact))
            {
                Apply(library, artifact);
                assigned = true;
            }
        }

        if (!assigned)
        {
            var (path, url) = MavenPath.FromName(library.Name, nativesClassifier);
            library.RelativePath = path;
            library.Url = url;
        }

        return library;
    }

    private static void Apply(Library library, JsonElement download)
    {
        library.Url = Str(download, "url") ?? library.Url;
        library.Sha1 = Str(download, "sha1");

        var path = Str(download, "path");
        if (!string.IsNullOrEmpty(path))
            library.RelativePath = path;

        library.Size = Num(download, "size");

        if (string.IsNullOrEmpty(library.RelativePath))
        {
            var (derived, _) = MavenPath.FromName(library.Name);
            library.RelativePath = derived;
        }
    }

    private static DownloadInfo ParseDownload(JsonElement element) => new()
    {
        Url = Str(element, "url") ?? "",
        Sha1 = Str(element, "sha1"),
        Size = Num(element, "size"),
        Path = Str(element, "path")
    };

    private static string? Str(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static long Num(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt64()
            : 0;
}
