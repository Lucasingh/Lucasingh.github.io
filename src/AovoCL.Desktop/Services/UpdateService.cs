using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace AovoCL.Desktop.Services;

public sealed record UpdateInfo(
    string VersionText,
    Version Version,
    string Notes,
    string DownloadUrl,
    string PageUrl);

/// <summary>整体下载进度：百分比、瞬时速度（字节/秒）与已下载 / 总字节数。</summary>
public sealed record DownloadProgress(double Percent, double BytesPerSecond, long Received = 0, long Total = 0);

/// <summary>单个下载线程的进度：序号从 0 开始，Count 为本次使用的线程总数。</summary>
public sealed record SegmentProgress(
    int Index, int Count, double Percent, double BytesPerSecond, bool Completed);

/// <summary>启动时向更新源查询新版本；当前使用 GitHub Releases，可通过环境变量替换为其他源。</summary>
public sealed class UpdateService
{
    public const string Repository = "Lucasingh/Lucasingh.github.io";

    /// <summary>GitHub 下载加速前缀：直接拼在原始地址前面即可访问同一资产。</summary>
    private static readonly string[] Mirrors =
    {
        "https://ghproxy.net/",
        "https://gh-proxy.com/",
        "https://ghfast.top/"
    };

    /// <summary>单文件分段下载的最大并发线程数。</summary>
    public const int MaxConnections = 32;

    private const int BufferSize = 81920;

    private static readonly HttpClient Http = CreateClient();
    private static readonly HttpClient DownloadHttp = CreateDownloadClient();

    /// <summary>当前程序版本，取自项目文件里的 Version。</summary>
    public static Version CurrentVersion =>
        typeof(UpdateService).Assembly.GetName().Version ?? new Version(0, 0);

    /// <summary>查询最新版本；没有更新或缺少安装包时返回 null。</summary>
    public async Task<UpdateInfo?> CheckAsync(CancellationToken ct = default)
    {
        var json = await Http.GetStringAsync(FeedUrl, ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var tag = root.TryGetProperty("tag_name", out var tagElement) ? tagElement.GetString() ?? "" : "";
        if (!TryParseVersion(tag, out var version)) return null;
        if (version <= CurrentVersion) return null;

        var url = FindInstallerUrl(root);
        if (url == null) return null;

        var notes = root.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "";
        var page = root.TryGetProperty("html_url", out var html) ? html.GetString() ?? "" : "";

        return new UpdateInfo(tag.TrimStart('v', 'V'), version, notes.Trim(), url, page);
    }

    /// <summary>下载新版本安装包：先并发测速选出最快源，再下载；失败自动换下一个源。</summary>
    public async Task<string> DownloadAsync(
        UpdateInfo info,
        Action<DownloadProgress>? onProgress,
        Action<SegmentProgress>? onSegment = null,
        int maxConnections = MaxConnections,
        CancellationToken ct = default)
    {
        var path = Path.Combine(Path.GetTempPath(), $"AovoCL-Setup-{info.VersionText}.exe");
        onProgress?.Invoke(new DownloadProgress(0, 0));

        var candidates = await RankSourcesAsync(info.DownloadUrl, ct);

        Exception? lastError = null;
        foreach (var url in candidates)
        {
            try
            {
                return await DownloadFromAsync(url, path, onProgress, onSegment, maxConnections, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }

        throw lastError ?? new IOException("没有可用的下载源");
    }

    /// <summary>用安装包静默覆盖安装：等待本进程退出后由安装器接管文件。</summary>
    public static void RunInstaller(string path)
    {
        var start = new ProcessStartInfo(path, "/SILENT /SUPPRESSMSGBOXES /CLOSEAPPLICATIONS /NORESTART")
        {
            UseShellExecute = true
        };
        Process.Start(start);
    }

    public static string FormatSpeed(double bytesPerSecond) => bytesPerSecond switch
    {
        >= 1024 * 1024 => $"{bytesPerSecond / 1024d / 1024d:0.0} MB/s",
        >= 1024 => $"{bytesPerSecond / 1024d:0} KB/s",
        > 0 => $"{bytesPerSecond:0} B/s",
        _ => ""
    };

    private static async Task<string> DownloadFromAsync(
        string url, string path, Action<DownloadProgress>? onProgress,
        Action<SegmentProgress>? onSegment, int maxConnections, CancellationToken ct)
    {
        // 支持 Range 时用多线程分段下载；否则退回单连接流式下载
        long total;
        try
        {
            total = await ProbeLengthAsync(url, ct);
        }
        catch
        {
            total = -1;
        }

        if (total > 0)
            return await DownloadParallelAsync(url, total, path, onProgress, onSegment, maxConnections, ct);

        return await DownloadStreamAsync(url, path, onProgress, onSegment, ct);
    }

    /// <summary>用 Range 0-0 探测文件总长度；返回 -1 表示该源不支持分段。</summary>
    private static async Task<long> ProbeLengthAsync(string url, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Range = new RangeHeaderValue(0, 0);

        using var response = await DownloadHttp.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (response.StatusCode != HttpStatusCode.PartialContent) return -1;

        return response.Content.Headers.ContentRange?.Length ?? -1;
    }

    /// <summary>多线程分段下载：每个线程负责一段连续区间，按偏移写入同一文件。</summary>
    private static async Task<string> DownloadParallelAsync(
        string url, long total, string path, Action<DownloadProgress>? onProgress,
        Action<SegmentProgress>? onSegment, int maxConnections, CancellationToken ct)
    {
        // 每段至少约 512 KB，线程数上限由调用方决定（默认 32）
        var connections = (int)Math.Clamp(total / (512 * 1024), 1, Math.Clamp(maxConnections, 1, MaxConnections));
        var chunk = (total + connections - 1) / connections;

        await using var target = new FileStream(
            path, FileMode.Create, FileAccess.Write, FileShare.Read, BufferSize, FileOptions.Asynchronous);
        target.SetLength(total);

        long received = 0;
        long lastBytes = 0;
        long lastTick = 0;
        var meter = Stopwatch.StartNew();
        var gate = new object();

        void Report(int delta)
        {
            lock (gate)
            {
                received += delta;

                var elapsed = meter.ElapsedMilliseconds;
                if (lastTick == 0)
                {
                    lastTick = elapsed;
                    return;
                }

                if (elapsed - lastTick < 400) return;

                var speed = (received - lastBytes) / Math.Max(0.001, (elapsed - lastTick) / 1000d);
                lastBytes = received;
                lastTick = elapsed;
                onProgress?.Invoke(new DownloadProgress(received * 100d / total, speed, received, total));
            }
        }

        // 先把所有线程登记到界面，未开始的显示 0%
        for (var i = 0; i < connections; i++)
            onSegment?.Invoke(new SegmentProgress(i, connections, 0, 0, false));

        var tasks = new List<Task>(connections);
        for (var i = 0; i < connections; i++)
        {
            var index = i;
            tasks.Add(Task.Run(async () =>
            {
                var start = index * chunk;
                var end = Math.Min(total - 1, start + chunk - 1);
                if (start > end) return;

                var segmentLength = end - start + 1;
                var buffer = new byte[BufferSize];
                var position = start;
                var rateLimitRetries = 0;
                var stallRetries = 0;
                long segmentReceived = 0;
                long segmentLast = 0;
                var segmentMeter = Stopwatch.StartNew();

                // 分段请求可能被 CDN 限流（429）或提前断开，从断点续传重试即可，不必整包重下
                while (position <= end)
                {
                    var before = position;

                    using var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Range = new RangeHeaderValue(position, end);

                    using var response = await DownloadHttp.SendAsync(
                        request, HttpCompletionOption.ResponseHeadersRead, ct);

                    if (response.StatusCode == HttpStatusCode.PartialContent)
                    {
                        rateLimitRetries = 0;
                        await using var stream = await response.Content.ReadAsStreamAsync(ct);
                        int read;
                        while ((read = await stream.ReadAsync(buffer, ct)) > 0)
                        {
                            await RandomAccess.WriteAsync(
                                target.SafeFileHandle, buffer.AsMemory(0, read), position, ct);
                            position += read;
                            segmentReceived += read;
                            stallRetries = 0;
                            Report(read);

                            if (segmentMeter.ElapsedMilliseconds >= 500)
                            {
                                var speed = (segmentReceived - segmentLast) / segmentMeter.Elapsed.TotalSeconds;
                                segmentLast = segmentReceived;
                                segmentMeter.Restart();
                                onSegment?.Invoke(new SegmentProgress(
                                    index, connections, segmentReceived * 100d / segmentLength, speed, false));
                            }
                        }
                    }
                    else if (IsRetryable(response.StatusCode) && rateLimitRetries < 8)
                    {
                        rateLimitRetries++;
                        var delay = response.Headers.RetryAfter?.Delta
                            ?? TimeSpan.FromMilliseconds(400 * Math.Pow(2, rateLimitRetries));
                        if (delay > TimeSpan.FromSeconds(15)) delay = TimeSpan.FromSeconds(15);
                        await Task.Delay(delay, ct);
                    }
                    else
                    {
                        response.EnsureSuccessStatusCode();
                        throw new InvalidOperationException("下载源不支持分段下载");
                    }

                    // 服务器提前关闭连接：稍作等待后从断点继续，连续多次毫无进展才放弃
                    if (position == before)
                    {
                        if (++stallRetries > 8) throw new IOException("下载源传输中断");
                        await Task.Delay(TimeSpan.FromMilliseconds(200 * stallRetries), ct);
                    }
                }

                onSegment?.Invoke(new SegmentProgress(index, connections, 100, 0, true));
            }, ct));
        }

        await Task.WhenAll(tasks);
        onProgress?.Invoke(new DownloadProgress(100, 0, total, total));
        return path;
    }

    private static bool IsRetryable(HttpStatusCode status) =>
        status == HttpStatusCode.TooManyRequests || (int)status >= 500;

    /// <summary>单连接流式下载（源不支持 Range 或分段失败时的兜底）。</summary>
    private static async Task<string> DownloadStreamAsync(
        string url, string path, Action<DownloadProgress>? onProgress,
        Action<SegmentProgress>? onSegment, CancellationToken ct)
    {
        onSegment?.Invoke(new SegmentProgress(0, 1, 0, 0, false));

        using var response = await DownloadHttp.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? -1;
        await using var source = await response.Content.ReadAsStreamAsync(ct);
        await using var target = File.Create(path);

        var buffer = new byte[BufferSize];
        long received = 0;
        long lastBytes = 0;
        var meter = Stopwatch.StartNew();
        int read;

        while ((read = await source.ReadAsync(buffer, ct)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read), ct);
            received += read;

            if (meter.ElapsedMilliseconds >= 400)
            {
                var speed = (received - lastBytes) / meter.Elapsed.TotalSeconds;
                lastBytes = received;
                meter.Restart();
                if (total > 0)
                {
                    var percent = received * 100d / total;
                    onProgress?.Invoke(new DownloadProgress(percent, speed, received, total));
                    onSegment?.Invoke(new SegmentProgress(0, 1, percent, speed, false));
                }
            }
        }

        if (total > 0)
        {
            onProgress?.Invoke(new DownloadProgress(100, 0, total, total));
            onSegment?.Invoke(new SegmentProgress(0, 1, 100, 0, true));
        }

        return path;
    }

    /// <summary>并发探测各下载源前 192 KB 的速度，按快慢排序；探测失败的直接剔除。</summary>
    private static async Task<List<string>> RankSourcesAsync(string directUrl, CancellationToken ct)
    {
        var candidates = new List<string> { directUrl };
        foreach (var mirror in Mirrors)
            candidates.Add($"{mirror}{directUrl}");

        var probes = candidates.Select(async url =>
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            try
            {
                return (Url: url, Speed: await MeasureAsync(url, timeout.Token));
            }
            catch
            {
                return (Url: url, Speed: 0d);
            }
        });

        var results = await Task.WhenAll(probes);
        var ranked = results.Where(r => r.Speed > 0).OrderByDescending(r => r.Speed).Select(r => r.Url).ToList();

        // 全部探测失败时仍按原顺序尝试，交给下载阶段报错
        return ranked.Count > 0 ? ranked : candidates;
    }

    private static async Task<double> MeasureAsync(string url, CancellationToken ct)
    {
        var meter = Stopwatch.StartNew();
        using var response = await DownloadHttp.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var buffer = new byte[64 * 1024];
        var total = 0;
        while (total < 192 * 1024)
        {
            var read = await stream.ReadAsync(buffer, ct);
            if (read == 0) break;
            total += read;
        }

        meter.Stop();
        if (total == 0) throw new IOException("下载源返回空内容");
        return total / Math.Max(0.01, meter.Elapsed.TotalSeconds);
    }

    private static string FeedUrl =>
        Environment.GetEnvironmentVariable("AOVOCL_UPDATE_FEED")
        ?? $"https://api.github.com/repos/{Repository}/releases/latest";

    private static string? FindInstallerUrl(JsonElement root)
    {
        if (!root.TryGetProperty("assets", out var assets)) return null;

        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var nameElement) ? nameElement.GetString() ?? "" : "";
            if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
            if (!name.Contains("Setup", StringComparison.OrdinalIgnoreCase)) continue;
            if (asset.TryGetProperty("browser_download_url", out var url)) return url.GetString();
        }

        return null;
    }

    private static bool TryParseVersion(string tag, out Version version)
    {
        var text = tag.Trim().TrimStart('v', 'V');
        return Version.TryParse(text, out version!);
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("AovoCL-Updater");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    /// <summary>下载专用客户端：放宽超时、允许更多并发连接，并保持原始字节不错解压。</summary>
    private static HttpClient CreateDownloadClient()
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.None,
            MaxConnectionsPerServer = 64,
            ConnectTimeout = TimeSpan.FromSeconds(10)
        };

        var client = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("AovoCL-Updater");
        return client;
    }
}
