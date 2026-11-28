using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace AovoCL.Core;

public sealed record JavaRuntime(string Path, int MajorVersion, string VersionText);

/// <summary>Java 探测：环境变量、PATH、常见安装目录，逐个执行 java -version 判定大版本。</summary>
public static class JavaLocator
{
    public static string JavaExecutable => OperatingSystem.IsWindows() ? "java.exe" : "java";

    public static async Task<IReadOnlyList<JavaRuntime>> DetectAsync(string gameRoot, CancellationToken ct = default)
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var exe = JavaExecutable;

        void TryAdd(string? path)
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) candidates.Add(path);
        }

        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrWhiteSpace(javaHome)) TryAdd(Path.Combine(javaHome, "bin", exe));

        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            if (!string.IsNullOrWhiteSpace(dir)) TryAdd(Path.Combine(dir.Trim(), exe));

        foreach (var root in GetSearchRoots(gameRoot))
        {
            if (!Directory.Exists(root)) continue;
            try
            {
                foreach (var dir in Directory.EnumerateDirectories(root))
                {
                    TryAdd(Path.Combine(dir, "bin", exe));
                    TryAdd(Path.Combine(dir, "Contents", "Home", "bin", exe));
                    foreach (var sub in Directory.EnumerateDirectories(dir))
                    {
                        TryAdd(Path.Combine(sub, "bin", exe));
                        TryAdd(Path.Combine(sub, "Contents", "Home", "bin", exe));
                    }
                }
            }
            catch
            {
                // 权限不足时跳过
            }
        }

        // 启动器自己下载的 JRE 目录层级更深，单独递归扫描
        var runtimeRoot = Path.Combine(gameRoot, "runtime");
        if (Directory.Exists(runtimeRoot))
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(runtimeRoot, exe, SearchOption.AllDirectories))
                    TryAdd(file);
            }
            catch
            {
                // 忽略权限问题
            }
        }

        var results = new List<JavaRuntime>();
        foreach (var path in candidates)
        {
            ct.ThrowIfCancellationRequested();
            var runtime = await ProbeAsync(path, ct);
            if (runtime != null) results.Add(runtime);
        }

        return results.OrderBy(r => r.MajorVersion).ToList();
    }

    private static IEnumerable<string> GetSearchRoots(string gameRoot)
    {
        yield return Path.Combine(gameRoot, "runtime");

        if (OperatingSystem.IsWindows())
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            foreach (var baseDir in new[] { programFiles, programFilesX86 })
            {
                yield return Path.Combine(baseDir, "Java");
                yield return Path.Combine(baseDir, "Eclipse Adoptium");
                yield return Path.Combine(baseDir, "Microsoft");
                yield return Path.Combine(baseDir, "Zulu");
                yield return Path.Combine(baseDir, "Amazon Corretto");
                yield return Path.Combine(baseDir, "BellSoft");
                yield return Path.Combine(baseDir, "Semeru");
                yield return Path.Combine(baseDir, "GraalVM");
                yield return Path.Combine(baseDir, "AdoptOpenJDK");
            }

            yield return Path.Combine(localAppData, "Programs", "Eclipse Adoptium");
            yield return Path.Combine(userProfile, ".jdks");
        }
        else if (OperatingSystem.IsMacOS())
        {
            yield return "/Library/Java/JavaVirtualMachines";
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Java", "JavaVirtualMachines");
        }
        else
        {
            yield return "/usr/lib/jvm";
            yield return "/usr/java";
            yield return "/opt";
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".sdkman", "candidates", "java");
        }
    }

    public static async Task<JavaRuntime?> ProbeAsync(string javaPath, CancellationToken ct = default)
    {
        try
        {
            var info = new ProcessStartInfo(javaPath)
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            info.ArgumentList.Add("-version");

            using var process = Process.Start(info);
            if (process == null) return null;

            var text = await process.StandardError.ReadToEndAsync(ct) + await process.StandardOutput.ReadToEndAsync(ct);

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await process.WaitForExitAsync(timeout.Token);

            var match = Regex.Match(text, "version \"([^\"]+)\"");
            if (!match.Success) return null;

            var versionText = match.Groups[1].Value;
            return new JavaRuntime(javaPath, ParseMajor(versionText), versionText);
        }
        catch
        {
            return null;
        }
    }

    public static int ParseMajor(string versionText)
    {
        if (versionText.StartsWith("1."))
        {
            var parts = versionText.Split('.');
            return parts.Length > 1 && int.TryParse(parts[1], out var legacy) ? legacy : 8;
        }

        var major = versionText.Split('.', '-', '_', '+')[0];
        return int.TryParse(major, out var value) ? value : 0;
    }
}

/// <summary>没有可用 Java 时，从 Adoptium 拉一个 JRE 到 gameRoot/runtime。</summary>
public sealed class JavaRuntimeManager
{
    private readonly GamePaths _paths;
    private readonly HttpClient _http = new();

    public JavaRuntimeManager(GamePaths paths)
    {
        _paths = paths;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("AovoCL/0.2");
        _http.Timeout = TimeSpan.FromMinutes(10);
    }

    public async Task<JavaRuntime> EnsureAsync(int requiredMajor, Action<string> log, CancellationToken ct = default)
    {
        var installed = await JavaLocator.DetectAsync(_paths.Root, ct);
        var compatible = installed
            .Where(r => requiredMajor <= 8 ? r.MajorVersion == 8 : r.MajorVersion >= requiredMajor)
            .OrderBy(r => Math.Abs(r.MajorVersion - requiredMajor))
            .ThenBy(r => r.MajorVersion)
            .FirstOrDefault();

        if (compatible != null) return compatible;

        var major = requiredMajor <= 8 ? 8 : requiredMajor;
        log($"未找到 Java {major}+，准备自动下载 JRE {major} …");
        return await DownloadAsync(major, log, ct);
    }

    public async Task<JavaRuntime> DownloadAsync(int major, Action<string> log, CancellationToken ct = default)
    {
        var os = OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsMacOS() ? "mac" : "linux";
        var arch = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture
            == System.Runtime.InteropServices.Architecture.Arm64 ? "aarch64" : "x64";

        var targetDir = Path.Combine(_paths.RuntimeDir, $"jre-{major}");
        Directory.CreateDirectory(targetDir);

        // 国内优先走清华镜像，失败再回退 Adoptium 官方
        var candidates = new List<string>();
        var tuna = await ResolveTunaUrlAsync(major, os, arch, ct);
        if (tuna != null) candidates.Add(tuna);
        candidates.Add($"https://api.adoptium.net/v3/binary/latest/{major}/ga/{os}/{arch}/jre/hotspot/normal/eclipse");

        Exception? lastError = null;
        foreach (var url in candidates)
        {
            try
            {
                log($"下载 JRE {major}（{os}-{arch}）：{url}");
                return await DownloadAndInstallAsync(url, targetDir, log, ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                lastError = ex;
                log($"该源下载失败：{ex.Message}");
            }
        }

        throw new IOException("JRE 下载失败：" + (lastError?.Message ?? "未知错误"));
    }

    private async Task<string?> ResolveTunaUrlAsync(int major, string os, string arch, CancellationToken ct)
    {
        try
        {
            var baseUrl = $"https://mirrors.tuna.tsinghua.edu.cn/Adoptium/{major}/jre/{arch}/{os}/";
            var html = await _http.GetStringAsync(baseUrl, ct);
            var matches = Regex.Matches(html, @"(OpenJDK\d+U-jre_[^""'<>/\s]+\.(?:zip|tar\.gz))");
            return matches.Count == 0 ? null : baseUrl + matches[^1].Groups[1].Value;
        }
        catch
        {
            return null;
        }
    }

    private async Task<JavaRuntime> DownloadAndInstallAsync(string url, string targetDir,
        Action<string> log, CancellationToken ct)
    {
        var isZip = url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
        var tempFile = Path.Combine(targetDir, isZip ? "jre.zip" : "jre.tar.gz");
        try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { /* 忽略 */ }

        using (var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            response.EnsureSuccessStatusCode();
            await using var source = await response.Content.ReadAsStreamAsync(ct);
            await using var target = File.Create(tempFile);

            var buffer = new byte[81920];
            long total = 0;
            var loggedMb = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, ct)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), ct);
                total += read;

                var mb = (int)(total / (1024 * 1024));
                if (mb >= loggedMb + 10)
                {
                    loggedMb = mb;
                    log($"已下载 JRE：{mb} MB");
                }
            }
        }

        if (new FileInfo(tempFile).Length < 10 * 1024 * 1024)
            throw new InvalidDataException("下载内容异常（文件过小）");

        log("解压 JRE …");
        var extractDir = Path.Combine(targetDir, "jre");
        if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
        Directory.CreateDirectory(extractDir);

        if (isZip)
            ZipFile.ExtractToDirectory(tempFile, extractDir);
        else
        {
            await using var file = File.OpenRead(tempFile);
            await using var gzip = new GZipStream(file, CompressionMode.Decompress);
            TarFile.ExtractToDirectory(gzip, extractDir, true);
        }

        try { File.Delete(tempFile); } catch { /* 忽略 */ }

        var javaPath = Directory.EnumerateFiles(extractDir, JavaLocator.JavaExecutable, SearchOption.AllDirectories).FirstOrDefault()
            ?? throw new FileNotFoundException("JRE 解压后未找到 java 可执行文件");

        if (!OperatingSystem.IsWindows())
        {
            try { File.SetUnixFileMode(javaPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); }
            catch { /* 忽略 */ }
        }

        var runtime = await JavaLocator.ProbeAsync(javaPath, ct)
            ?? throw new InvalidOperationException("下载的 JRE 无法执行");

        log($"JRE {runtime.VersionText} 就绪：{javaPath}");
        return runtime;
    }
}
