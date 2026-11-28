using System.Runtime.InteropServices;
using AovoCL.Desktop.Services;

namespace AovoCL.Desktop.ViewModels;

/// <summary>关于页信息，数据全部来自运行时，不写死版本号。</summary>
public sealed class AboutViewModel : ObservableObject
{
    public AboutViewModel(AppSettings settings)
    {
        Theme = settings.Theme;
        Version = typeof(AboutViewModel).Assembly.GetName().Version?.ToString(3) ?? "0.2.0";
        Runtime = RuntimeInformation.FrameworkDescription;
        Platform = RuntimeInformation.OSDescription;
    }

    public ThemeManager Theme { get; }

    public string Version { get; }

    public string Runtime { get; }

    public string Platform { get; }

    public IReadOnlyList<string> Features { get; } = new[]
    {
        "版本清单：BMCLAPI 镜像 + 官方源回退，本地缓存 30 分钟",
        "版本解析：新旧 arguments 格式、inheritsFrom 合并、规则判定",
        "下载安装：多线程并发、SHA1 校验、镜像失败自动回退",
        "资源补齐：assets objects、legacy 虚拟目录、老版本 natives 解压",
        "Java：本机探测，缺失时从 Adoptium 镜像自动下载 JRE",
        "启动：占位符替换、classpath 组装、离线 UUID、日志实时回流",
        "版本管理：在线下载、重命名、删除、打开版本目录"
    };
}
