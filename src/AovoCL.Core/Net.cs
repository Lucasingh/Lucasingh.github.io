using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace AovoCL.Core;

/// <summary>下载源：官方 or BMCLAPI 镜像，失败时自动回退官方。</summary>
public sealed class MirrorOptions
{
    public bool UseMirror { get; set; } = true;

    private const string Bmcl = "https://bmclapi2.bangbang93.com";

    private const string MojangMeta = "https://piston-meta.mojang.com";

    public string ManifestUrl => UseMirror
        ? Bmcl + "/mc/game/version_manifest_v2.json"
        : MojangMeta + "/mc/game/version_manifest_v2.json";

    private static readonly (string From, string To)[] Rules =
    {
        ("https://piston-meta.mojang.com", Bmcl),
        ("https://launchermeta.mojang.com", Bmcl),
        ("https://launcher.mojang.com", Bmcl),
        ("https://resources.download.minecraft.net", Bmcl + "/assets"),
        ("https://libraries.minecraft.net", Bmcl + "/maven")
    };

    // 反向映射：镜像返回重定向或不可用时，还原成官方地址重试
    private static readonly (string Mirror, string Official)[] ReverseRules =
    {
        (Bmcl + "/assets", "https://resources.download.minecraft.net"),
        (Bmcl + "/maven", "https://libraries.minecraft.net"),
        (Bmcl, MojangMeta)
    };

    public string Map(string url)
    {
        if (!UseMirror) return url;
        foreach (var (from, to) in Rules)
        {
            if (url.StartsWith(from, StringComparison.OrdinalIgnoreCase))
                return to + url[from.Length..];
        }
        return url;
    }

    /// <summary>把镜像地址还原为官方地址；不是镜像地址时原样返回。</summary>
    public string Unmap(string url)
    {
        if (!UseMirror) return url;
        foreach (var (mirror, official) in ReverseRules)
        {
            if (url.StartsWith(mirror, StringComparison.OrdinalIgnoreCase))
                return official + url[mirror.Length..];
        }
        return url;
    }
}

/// <summary>下载器调优参数。可随时修改，下一次下载批次生效。</summary>
public sealed class DownloadOptions
{
    public const int DefaultMaxConcurrency = 16;
    public const int DefaultSliceConcurrency = 4;
    public const int DefaultSliceThresholdMb = 4;

    /// <summary>全局并发连接上限（单文件与分片共享同一个预算）。</summary>
    public int MaxConcurrency { get; set; } = DefaultMaxConcurrency;

    /// <summary>单文件内部分片并发数上限。</summary>
    public int SliceConcurrency { get; set; } = DefaultSliceConcurrency;

    /// <summary>超过该大小的文件尝试 Range 分片下载。</summary>
    public long SliceThreshold { get; set; } = DefaultSliceThresholdMb * 1024L * 1024L;

    /// <summary>单个分片的最小字节数，避免小文件切出过多碎片。</summary>
    public long MinSliceSize { get; set; } = 1024L * 1024L;

    /// <summary>是否启用大文件分片；关闭后全部走单连接。</summary>
    public bool EnableSliced { get; set; } = true;
}

public sealed record DownloadTask(string Url, string TargetPath, string? Sha1 = null, long Size = 0, string Label = "");

/// <summary>下载进度快照（可变对象，避免高频分配）。</summary>
public sealed class TransferProgress
{
    private long _downloaded;
    private long _total;
    private int _filesDone;
    private int _filesTotal;
    private string _current = "";
    private double _speed;

    private readonly Queue<(long Tick, long Bytes)> _speedSamples = new();
    private long _lastSampleTick;

    public long Downloaded => Interlocked.Read(ref _downloaded);
    public long Total => Interlocked.Read(ref _total);
    public int FilesDone => Volatile.Read(ref _filesDone);
    public int FilesTotal => Volatile.Read(ref _filesTotal);
    public string Current => Volatile.Read(ref _current);

    /// <summary>最近约 2 秒滑动窗口的平均速度（字节/秒）。</summary>
    public double SpeedBytesPerSecond => Volatile.Read(ref _speed);

    public double Percent => Total > 0
        ? Math.Min(100d, Downloaded * 100d / Total)
        : FilesTotal > 0 ? FilesDone * 100d / FilesTotal : 0d;

    public void AddTotal(long bytes) => Interlocked.Add(ref _total, bytes);

    public void AddDownloaded(long bytes)
    {
        Interlocked.Add(ref _downloaded, bytes);
        SampleSpeed();
    }

    public void SetFilesTotal(int count) => Volatile.Write(ref _filesTotal, count);
    public void SetCurrent(string value) => Volatile.Write(ref _current, value);
    public void FileDone() => Interlocked.Increment(ref _filesDone);

    /// <summary>每批下载开始时重置统计，避免不同阶段累加导致进度越界。</summary>
    public void Reset()
    {
        Interlocked.Exchange(ref _downloaded, 0);
        Interlocked.Exchange(ref _total, 0);
        Interlocked.Exchange(ref _filesDone, 0);
        Interlocked.Exchange(ref _filesTotal, 0);
        Volatile.Write(ref _current, "");
        Volatile.Write(ref _speed, 0);
        Interlocked.Exchange(ref _lastSampleTick, 0);
        lock (_speedSamples) _speedSamples.Clear();
    }

    /// <summary>250ms 采样一次，取 2 秒窗口内的字节差 / 时间差。</summary>
    private void SampleSpeed()
    {
        var now = Stopwatch.GetTimestamp();
        var previous = Interlocked.Read(ref _lastSampleTick);
        var minGap = Stopwatch.Frequency / 4;
        if (now - previous < minGap) return;
        if (Interlocked.CompareExchange(ref _lastSampleTick, now, previous) != previous) return;

        var total = Downloaded;
        lock (_speedSamples)
        {
            _speedSamples.Enqueue((now, total));
            var minTick = now - Stopwatch.Frequency * 2;
            while (_speedSamples.Count > 1 && _speedSamples.Peek().Tick < minTick)
                _speedSamples.Dequeue();

            var first = _speedSamples.Peek();
            var seconds = (now - first.Tick) / (double)Stopwatch.Frequency;
            if (seconds > 0.3)
                Volatile.Write(ref _speed, (total - first.Bytes) / seconds);
        }
    }
}

/// <summary>
/// 并发下载器：多文件并发 + 大文件 Range 分片，共享全局连接预算。
/// 已存在且 SHA1 匹配的文件直接跳过；先写临时文件，校验通过后原子替换。
/// </summary>
public sealed class FileDownloader
{
    /// <summary>全应用共用一个 HTTP 客户端，避免每实例一套连接池。</summary>
    private static readonly Lazy<HttpClient> SharedHttp = new(CreateHttpClient);

    private const int BufferSize = 81920;
    private const int MaxAttempts = 3;
    private const int HeaderTimeoutMs = 30_000;
    private const long SingleNoDataMs = 15_000;
    private const long SingleMinBytesPerSecond = 16 * 1024;
    private const long SliceNoDataMs = 8_000;
    private const long SliceMinBytesPerSecond = 32 * 1024;

    private readonly HttpClient _http;
    private readonly MirrorOptions _mirror;

    public FileDownloader(MirrorOptions mirror, int concurrency = DownloadOptions.DefaultMaxConcurrency)
    {
        _mirror = mirror;
        Options = new DownloadOptions { MaxConcurrency = Math.Clamp(concurrency, 1, 64) };
        _http = SharedHttp.Value;
    }

    /// <summary>下载调优参数（全局并发、分片开关等），可在运行期修改。</summary>
    public DownloadOptions Options { get; }

    /// <summary>兼容旧调用：等同于 <see cref="DownloadOptions.MaxConcurrency"/>。</summary>
    public int Concurrency
    {
        get => Options.MaxConcurrency;
        set => Options.MaxConcurrency = Math.Clamp(value, 1, 64);
    }

    private static HttpClient CreateHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.All,
            ConnectTimeout = TimeSpan.FromSeconds(20),
            MaxConnectionsPerServer = 32,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1),
            EnableMultipleHttp2Connections = true
        };

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(10),
            DefaultRequestVersion = HttpVersion.Version20,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("AovoCL/0.2");
        return client;
    }

    public async Task<string> GetStringAsync(string url, CancellationToken ct = default)
    {
        var primary = _mirror.Map(url);
        var text = await TryGetStringAsync(primary, ct);
        if (text != null) return text;

        // 镜像可能重定向到不可达节点（如 BMCLAPI 的签名下载域名），此时回退官方源
        var fallback = _mirror.Unmap(primary);
        if (!string.Equals(fallback, primary, StringComparison.OrdinalIgnoreCase))
        {
            text = await TryGetStringAsync(fallback, ct);
            if (text != null) return text;
        }

        throw new IOException($"请求失败：{url}");
    }

    private async Task<string?> TryGetStringAsync(string url, CancellationToken ct)
    {
        try
        {
            using var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return null;
        }
    }

    public async Task<T?> GetJsonAsync<T>(string url, CancellationToken ct = default) where T : class
    {
        var text = await GetStringAsync(url, ct);
        return JsonSerializer.Deserialize<T>(text, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    public async Task DownloadAllAsync(IReadOnlyList<DownloadTask> tasks, TransferProgress progress,
        Action<TransferProgress>? onProgress, CancellationToken ct = default)
    {
        progress.Reset();
        progress.SetFilesTotal(tasks.Count);

        // 先算出总量，跳过已就绪的文件
        var pending = new List<DownloadTask>(tasks.Count);
        foreach (var task in tasks)
        {
            if (task.Size > 0) progress.AddTotal(task.Size);

            if (File.Exists(task.TargetPath) && (task.Sha1 == null || await Sha1MatchesAsync(task.TargetPath, task.Sha1)))
            {
                progress.AddDownloaded(task.Size);
                progress.FileDone();
                continue;
            }
            pending.Add(task);
        }

        onProgress?.Invoke(progress);
        if (pending.Count == 0) return;

        // 全局连接预算：单文件占 1 个，分片每条连接占 1 个，杜绝并发爆炸
        using var limiter = new SemaphoreSlim(Math.Clamp(Options.MaxConcurrency, 1, 64));

        var reportSync = new object();
        var lastReport = Stopwatch.StartNew();
        var sync = new object();
        var failures = new List<string>();

        void Report(bool force = false)
        {
            lock (reportSync)
            {
                if (!force && lastReport.ElapsedMilliseconds < 120) return;
                lastReport.Restart();
                onProgress?.Invoke(progress);
            }
        }

        var workers = pending.Select(async task =>
        {
            try
            {
                progress.SetCurrent(task.Label.Length > 0 ? task.Label : Path.GetFileName(task.TargetPath));
                await DownloadOneAsync(task, progress, limiter, () => Report(), ct);
                progress.FileDone();
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                lock (sync) failures.Add($"{task.Label} ({task.Url})：{ex.Message}");
                throw;
            }
            finally
            {
                Report();
            }
        });

        try
        {
            await Task.WhenAll(workers);
        }
        catch
        {
            ct.ThrowIfCancellationRequested();
            throw new IOException("有文件下载失败：" + Environment.NewLine + string.Join(Environment.NewLine, failures.Take(5)));
        }
        finally
        {
            Report(true);
        }
    }

    private async Task DownloadOneAsync(DownloadTask task, TransferProgress progress, SemaphoreSlim limiter,
        Action report, CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(task.TargetPath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        // 独立临时文件名，避免多开时互相抢占
        var temp = $"{task.TargetPath}.{Guid.NewGuid():N}.part";
        Exception? lastError = null;

        try
        {
            for (var attempt = 0; attempt < MaxAttempts; attempt++)
            {
                // 前两次走镜像，最后一次回退到官方地址
                var url = attempt < 2 ? _mirror.Map(task.Url) : task.Url;
                try
                {
                    var sliced = false;
                    if (Options.EnableSliced && task.Size >= Options.SliceThreshold && SliceCount(task.Size) > 1)
                        sliced = await TryDownloadSlicedAsync(url, task, temp, progress, limiter, report, ct);

                    if (!sliced)
                        await DownloadSingleAsync(url, task, temp, progress, limiter, report, ct);
                    else
                        await VerifyAndCommitAsync(temp, task, ct);

                    return;
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    lastError = ex;
                    TryDelete(temp);
                    await Task.Delay(300 * (attempt + 1), ct);
                }
            }
        }
        finally
        {
            TryDelete(temp);
        }

        throw new IOException(lastError?.Message ?? "下载失败");
    }

    /// <summary>单连接下载：边下边算 SHA1，完成后校验并原子替换。</summary>
    private async Task DownloadSingleAsync(string url, DownloadTask task, string temp, TransferProgress progress,
        SemaphoreSlim limiter, Action report, CancellationToken ct)
    {
        await limiter.WaitAsync(ct);
        long written = 0;
        try
        {
            // 响应头超时：CDN 偶发“连接建立但永不响应”，不能等 HttpClient 的整体超时
            HttpResponseMessage response;
            using (var headerCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                headerCts.CancelAfter(HeaderTimeoutMs);
                response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, headerCts.Token);
            }

            using var responseHolder = response;
            response.EnsureSuccessStatusCode();

            // 停滞保护：长时间没有数据或持续低速时掐断连接，交给上层重试
            using var watchdog = new StallWatchdog(ct, SingleNoDataMs, SingleMinBytesPerSecond);

            await using (var source = await response.Content.ReadAsStreamAsync(ct))
            await using (var target = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, true))
            using (var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA1))
            {
                var buffer = new byte[BufferSize];
                int read;
                while ((read = await source.ReadAsync(buffer, watchdog.Token)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, read), ct);
                    hasher.AppendData(buffer, 0, read);
                    written += read;
                    progress.AddDownloaded(read);
                    watchdog.AddBytes(read);
                    report();
                }

                if (task.Sha1 != null)
                {
                    var actual = Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant();
                    if (!actual.Equals(task.Sha1, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException($"SHA1 校验失败：{actual} != {task.Sha1}");
                }
            }

            File.Move(temp, task.TargetPath, true);
        }
        catch
        {
            // 本次尝试写入的字节会被丢弃，进度相应回退，重试时重新计入
            if (written > 0) progress.AddDownloaded(-written);
            throw;
        }
        finally
        {
            limiter.Release();
        }
    }

    /// <summary>
    /// 尝试分片下载。返回 false 表示该源不支持 Range，调用方应回退单连接。
    /// 分片写入同一个临时文件的对应偏移（RandomAccess），避免合并大文件带来的额外 IO。
    /// </summary>
    private async Task<bool> TryDownloadSlicedAsync(string url, DownloadTask task, string temp,
        TransferProgress progress, SemaphoreSlim limiter, Action report, CancellationToken ct)
    {
        var (supported, length) = await ProbeRangeAsync(url, ct);
        if (!supported || length <= 0) return false;

        // 服务器声明的长度与清单不一致时按单连接处理，减少异常路径
        if (task.Size > 0 && length != task.Size) return false;

        var workers = SliceCount(length);
        if (workers <= 1) return false;

        // 分块队列：块大小 ≥ MinSliceSize，且最多切 128 块，避免超长尾块拖住整个文件
        var chunkSize = Math.Max(Options.MinSliceSize, (length + 127) / 128);

        await using var file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.ReadWrite,
            BufferSize, FileOptions.Asynchronous);
        file.SetLength(length);

        var handle = file.SafeFileHandle;
        var nextChunk = -1;

        var running = new List<Task>(workers);
        try
        {
            for (var i = 0; i < workers; i++)
                running.Add(SliceWorkerAsync(url, task, handle, length, chunkSize,
                    () => Interlocked.Increment(ref nextChunk), limiter, progress, report, ct));

            await Task.WhenAll(running);
            return true;
        }
        catch
        {
            try { file.SetLength(0); } catch { /* 忽略清理失败 */ }
            throw;
        }
    }

    /// <summary>分片工作线程：从共享队列领取字节区间，慢连接只影响它当前那一块。</summary>
    private async Task SliceWorkerAsync(string url, DownloadTask task, SafeFileHandle handle, long length, long chunkSize,
        Func<long> claim, SemaphoreSlim limiter, TransferProgress progress, Action report, CancellationToken ct)
    {
        while (true)
        {
            var index = claim();
            var start = index * chunkSize;
            if (start >= length) return;

            var end = Math.Min(length - 1, start + chunkSize - 1);
            await DownloadRangeAsync(url, task, handle, start, end, limiter, progress, report, ct);
        }
    }

    /// <summary>下载一个字节区间：重试时从已写入位置继续，避免重复下载。</summary>
    private async Task DownloadRangeAsync(string url, DownloadTask task, SafeFileHandle handle, long start, long end,
        SemaphoreSlim limiter, TransferProgress progress, Action report, CancellationToken ct)
    {
        var offset = start;
        Exception? lastError = null;

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            if (offset > end) return;

            // 前两次走当前源并带看门狗，最后一次回退官方源且不设吞吐阈值
            var useWatchdog = attempt < MaxAttempts - 1;
            var sliceUrl = useWatchdog ? url : task.Url;
            try
            {
                await limiter.WaitAsync(ct);
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, sliceUrl);
                    request.Headers.Range = new RangeHeaderValue(offset, end);

                    HttpResponseMessage response;
                    using (var headerCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                    {
                        headerCts.CancelAfter(HeaderTimeoutMs);
                        response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, headerCts.Token);
                    }

                    using var responseHolder = response;
                    if (response.StatusCode != HttpStatusCode.PartialContent)
                        throw new IOException($"分片响应异常：{(int)response.StatusCode}");

                    await using var source = await response.Content.ReadAsStreamAsync(ct);
                    using var watchdog = useWatchdog
                        ? new StallWatchdog(ct, SliceNoDataMs, SliceMinBytesPerSecond)
                        : null;
                    var token = watchdog?.Token ?? ct;

                    var buffer = new byte[BufferSize];
                    int read;
                    while ((read = await source.ReadAsync(buffer, token)) > 0)
                    {
                        if (offset + read > end + 1)
                            throw new InvalidDataException("分片数据超出请求范围");

                        await RandomAccess.WriteAsync(handle, buffer.AsMemory(0, read), offset, ct);
                        offset += read;
                        progress.AddDownloaded(read);
                        watchdog?.AddBytes(read);
                        report();
                    }

                    if (offset > end) return;
                    throw new IOException($"分片数据不完整：剩余 {end - offset + 1} 字节");
                }
                finally
                {
                    limiter.Release();
                }
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                lastError = ex;
                await Task.Delay(200 * (attempt + 1), ct);
            }
        }

        throw new IOException($"分片 {start}-{end} 下载失败：{lastError?.Message}");
    }

    /// <summary>探测是否支持 Range：返回 206 且能解析出完整长度才启用分片。</summary>
    private async Task<(bool Supported, long Length)> ProbeRangeAsync(string url, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Range = new RangeHeaderValue(0, 0);

            using var probeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            probeCts.CancelAfter(HeaderTimeoutMs);
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, probeCts.Token);
            if (response.StatusCode != HttpStatusCode.PartialContent) return (false, 0);

            var acceptRanges = response.Headers.AcceptRanges;
            if (acceptRanges.Count > 0 &&
                !acceptRanges.Any(v => v.Equals("bytes", StringComparison.OrdinalIgnoreCase)))
                return (false, 0);

            var range = response.Content.Headers.ContentRange;
            return range?.Length is > 0 ? (true, range.Length.Value) : (false, 0);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return (false, 0);
        }
    }

    private int SliceCount(long size)
    {
        var configured = Math.Clamp(Options.SliceConcurrency, 2, 16);
        var minSize = Math.Max(1, Options.MinSliceSize);
        var bySize = (int)Math.Max(1, size / minSize);
        return Math.Min(configured, bySize);
    }

    private static async Task VerifyAndCommitAsync(string temp, DownloadTask task, CancellationToken ct)
    {
        if (task.Sha1 != null)
        {
            await using var stream = File.OpenRead(temp);
            var hash = await SHA1.HashDataAsync(stream, ct);
            var actual = Convert.ToHexString(hash).ToLowerInvariant();
            if (!actual.Equals(task.Sha1, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"SHA1 校验失败：{actual} != {task.Sha1}");
        }

        File.Move(temp, task.TargetPath, true);
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* 忽略清理失败 */ }
    }

    /// <summary>停滞看门狗：太久没有字节或平均吞吐过低时取消当前请求，交给重试逻辑。</summary>
    private sealed class StallWatchdog : IDisposable
    {
        private readonly CancellationTokenSource _cts;
        private readonly Timer _timer;
        private readonly long _noDataMs;
        private readonly long _minBytesPerSecond;
        private long _startTick = Environment.TickCount64;
        private long _lastDataTick = Environment.TickCount64;
        private long _bytes;

        public StallWatchdog(CancellationToken ct, long noDataMs, long minBytesPerSecond)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _noDataMs = noDataMs;
            _minBytesPerSecond = minBytesPerSecond;
            _timer = new Timer(Check, null, 1000, 1000);
        }

        public CancellationToken Token => _cts.Token;

        public void AddBytes(int count)
        {
            Interlocked.Add(ref _bytes, count);
            Volatile.Write(ref _lastDataTick, Environment.TickCount64);
        }

        private void Check(object? state)
        {
            try
            {
                var now = Environment.TickCount64;
                if (now - Volatile.Read(ref _lastDataTick) > _noDataMs)
                {
                    _cts.Cancel();
                    return;
                }

                var elapsed = now - Volatile.Read(ref _startTick);
                if (elapsed < 8000) return;
                if (Interlocked.Read(ref _bytes) * 1000 / elapsed < _minBytesPerSecond)
                    _cts.Cancel();
            }
            catch
            {
                // 已释放，忽略
            }
        }

        public void Dispose()
        {
            _timer.Dispose();
            _cts.Dispose();
        }
    }

    private static async Task<bool> Sha1MatchesAsync(string path, string expected)
    {
        try
        {
            await using var stream = File.OpenRead(path);
            var hash = await SHA1.HashDataAsync(stream);
            return Convert.ToHexString(hash).Equals(expected, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
