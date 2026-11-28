using AovoCL.Core;
using AovoCL.Desktop.Services;

namespace AovoCL.Desktop.ViewModels;

/// <summary>设置页：账号、游戏目录、Java、内存、下载源、外观。</summary>
public sealed class SettingsViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private readonly StatusBarViewModel _status;
    private string _javaStatus = "未检测。留空时启动会自动探测，必要时下载 JRE。";

    public SettingsViewModel(AppSettings settings, StatusBarViewModel status)
    {
        _settings = settings;
        _status = status;

        BrowseFolderCommand = new AsyncCommand(BrowseFolderAsync);
        DetectJavaCommand = new AsyncCommand(DetectJavaAsync);
        CheckUpdateCommand = new AsyncCommand(CheckUpdateAsync);
    }

    public AppSettings Settings => _settings;

    public ThemeManager Theme => _settings.Theme;

    public AsyncCommand BrowseFolderCommand { get; }
    public AsyncCommand DetectJavaCommand { get; }
    public AsyncCommand CheckUpdateCommand { get; }

    public string CurrentVersionText => $"当前版本 {UpdateService.CurrentVersion.ToString(3)}";

    /// <summary>手动检查更新（由主窗口注入，弹窗与下载流程复用启动检查）。</summary>
    public Func<Task>? CheckUpdatesAsync { get; set; }

    private Task CheckUpdateAsync() => CheckUpdatesAsync?.Invoke() ?? Task.CompletedTask;

    public IReadOnlyList<string> ThemeOptions { get; } = new[] { "浅色", "深色", "跟随系统" };

    /// <summary>打开目录选择器（由窗口注入，保证 View 不写业务逻辑）。</summary>
    public Func<Task<string?>>? PickFolderAsync { get; set; }

    /// <summary>游戏目录变化后通知主窗口刷新各页面（由主窗口注入）。</summary>
    public Func<Task>? RootChangedAsync { get; set; }

    public string JavaStatus
    {
        get => _javaStatus;
        private set => SetProperty(ref _javaStatus, value);
    }

    private async Task BrowseFolderAsync()
    {
        if (PickFolderAsync == null) return;

        var path = await PickFolderAsync();
        if (string.IsNullOrWhiteSpace(path)) return;

        _settings.GameRoot = path;
        _status.SetInfo($"游戏目录已切换为 {path}");

        if (RootChangedAsync != null) await RootChangedAsync();
    }

    private async Task DetectJavaAsync()
    {
        JavaStatus = "正在扫描本机 Java…";
        _status.SetInfo("正在扫描本机 Java…");

        try
        {
            var list = await _settings.Context.Service.DetectJavaAsync();
            if (list.Count == 0)
            {
                JavaStatus = "未找到本机 Java，启动时将自动下载 JRE。";
                _status.SetInfo("未找到 Java，启动时将自动下载 JRE");
                return;
            }

            var best = list.LastOrDefault(j => j.MajorVersion >= 17) ?? list.Last();
            _settings.JavaPath = best.Path;
            JavaStatus = $"已选择 Java {best.VersionText}（共发现 {list.Count} 个）";
            _status.SetSuccess($"已选择 Java {best.MajorVersion}");
        }
        catch (Exception ex)
        {
            JavaStatus = "Java 检测失败：" + ex.Message;
            _status.SetError("Java 检测失败：" + ex.Message);
        }
    }
}
