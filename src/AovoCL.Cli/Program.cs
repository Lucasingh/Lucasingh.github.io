using AovoCL.Core;

// 命令行入口：便于在无界面环境下验证下载与启动链路
var command = args.Length > 0 ? args[0] : "help";
var options = ParseOptions(args.Skip(1).ToArray());

var root = options.GetValueOrDefault("dir") ?? Path.Combine(GamePaths.DefaultRoot, "minecraft");
var mirror = !options.ContainsKey("no-mirror");
var launcher = new LauncherService(root, mirror);

Log($"游戏目录：{root}");
Log(mirror ? "下载源：BMCLAPI 镜像" : "下载源：官方");

switch (command)
{
    case "versions":
    {
        var versions = await launcher.GetVersionsAsync(options.ContainsKey("refresh"));
        foreach (var version in versions.Take(15))
            Log($"{version.Id,-24} {version.Type,-10} {version.ReleaseTime:yyyy-MM-dd}");
        Log($"共 {versions.Count} 个版本");
        break;
    }

    case "install":
    {
        var id = RequireVersionId(options);
        var entry = await FindAsync(launcher, id);
        var progress = new TransferProgress();
        var version = await launcher.Installer.InstallAsync(entry.Id, entry.Url, progress,
            p => Console.Write($"\r{p.Percent,6:F1}%  {p.FilesDone}/{p.FilesTotal}  {Trim(p.Current)}          "),
            Log);
        Console.WriteLine();
        Log($"安装完成：{version.Id}（需要 Java {version.JavaVersionMajor}）");
        break;
    }

    case "java":
    {
        var runtimes = await launcher.DetectJavaAsync();
        if (runtimes.Count == 0) Log("未检测到 Java");
        foreach (var runtime in runtimes)
            Log($"Java {runtime.MajorVersion}  {runtime.VersionText}  {runtime.Path}");
        break;
    }

    case "launch":
    {
        var id = RequireVersionId(options);
        var entry = await FindAsync(launcher, id);
        var request = new LaunchRequest
        {
            VersionId = entry.Id,
            VersionUrl = entry.Url,
            PlayerName = options.GetValueOrDefault("name") ?? "Steve",
            GameDirectory = root,
            MemoryMb = double.TryParse(options.GetValueOrDefault("memory"), out var memory) ? memory : 4096,
            JavaPath = options.GetValueOrDefault("java")
        };

        var game = await launcher.LaunchAsync(request, Log,
            (stage, percent) => Console.Write($"\r{stage} {percent:F1}%          "));

        Log($"游戏已启动，PID {game.ProcessId}；日志会持续输出，输入 exit 结束进程");

        if (options.ContainsKey("wait"))
        {
            await game.WaitForExitAsync();
            Log($"游戏进程已退出，代码 {game.ExitCode}");
            break;
        }

        _ = Task.Run(async () =>
        {
            while (!game.HasExited) await Task.Delay(500);
            Log($"游戏进程已退出，代码 {game.ExitCode}");
            Environment.Exit(0);
        });

        while (Console.ReadLine() is { } line)
        {
            if (line.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                game.Stop();
                break;
            }
        }
        break;
    }

    default:
        Log("""
            用法：
              dotnet run --project src/AovoCL.Cli -- versions [--refresh] [--dir <游戏目录>]
              dotnet run --project src/AovoCL.Cli -- java
              dotnet run --project src/AovoCL.Cli -- install <版本号>
              dotnet run --project src/AovoCL.Cli -- launch <版本号> [--name 玩家名] [--memory 4096] [--java java路径]
            可选参数：--dir <目录>  --no-mirror
            """);
        break;
}

return;

static string RequireVersionId(Dictionary<string, string> options) =>
    options.GetValueOrDefault("id") ?? throw new ArgumentException("缺少版本号，例如：install 1.20.1");

static async Task<VersionEntry> FindAsync(LauncherService launcher, string id)
{
    var versions = await launcher.GetVersionsAsync(false);
    return versions.FirstOrDefault(v => v.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
           ?? throw new ArgumentException($"版本清单中不存在 {id}");
}

static Dictionary<string, string> ParseOptions(string[] args)
{
    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i < args.Length; i++)
    {
        var arg = args[i];
        if (arg.StartsWith("--"))
        {
            var key = arg[2..];
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                result[key] = args[++i];
            else
                result[key] = "true";
        }
        else if (!result.ContainsKey("id"))
        {
            result["id"] = arg;
        }
    }
    return result;
}

static string Trim(string value) => value.Length <= 40 ? value : value[..40];

static void Log(string message) => Console.WriteLine(message);
