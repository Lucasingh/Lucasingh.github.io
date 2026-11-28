using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AovoCL.Desktop.Services;

namespace AovoCL.Desktop.ViewModels;

/// <summary>主窗口外壳：侧栏导航、顶栏、内容区、状态栏与共享设置。</summary>
public sealed class MainWindowViewModel : ObservableObject
{
    private const double ExpandedNavWidth = 210;
    private const double CollapsedNavWidth = 64;
    private const double NarrowThreshold = 940;

    private NavigationItem? _selectedNav;
    private bool _isNarrow;

    public MainWindowViewModel()
    {
        Settings = new AppSettings();
        Status = new StatusBarViewModel();
        Home = new HomeViewModel(Settings, Status);
        Download = new DownloadViewModel(Settings, Status);
        SettingsPage = new SettingsViewModel(Settings, Status);
        About = new AboutViewModel(Settings);
        Logs = new LogsViewModel(Home.Log);

        Download.ConfirmAsync = (title, message) =>
            ConfirmAsync?.Invoke(title, message) ?? Task.FromResult(true);
        Download.LibraryChanged = Home.RefreshInstalledFlags;
        SettingsPage.PickFolderAsync = () =>
            PickFolderAsync?.Invoke() ?? Task.FromResult<string?>(null);
        SettingsPage.RootChangedAsync = ReloadAfterRootChangedAsync;

        Home.ConfirmAsync = (title, message) =>
            ConfirmAsync?.Invoke(title, message) ?? Task.FromResult(true);
        Home.PickFolderAsync = () =>
            PickFolderAsync?.Invoke() ?? Task.FromResult<string?>(null);

        NavItems = new ObservableCollection<NavigationItem>
        {
            new("启动", "选择版本并启动游戏", Icon("IconLaunch"), Home),
            new("下载", "在线下载与本地版本管理", Icon("IconDownload"), Download),
            new("设置", "账号、Java、内存与下载源", Icon("IconSettings"), SettingsPage),
            new("日志", "游戏运行输出与错误信息", Icon("IconLog"), Logs),
            new("关于", "AovoCL 与启动核心说明", Icon("IconInfo"), About)
        };

        _selectedNav = NavItems[0];
        ToggleNavCommand = new RelayCommand(ToggleNav);
        GoHomeCommand = new RelayCommand(() => SelectedNav = NavItems[0]);
        GoSettingsCommand = new RelayCommand(() => SelectedNav = NavItems[2]);

        // 首次进入启动页（构造函数不会经过 SelectedNav setter，需手动激活一次）
        Home.OnActivated();
    }

    public AppSettings Settings { get; }
    public ThemeManager Theme => Settings.Theme;
    public StatusBarViewModel Status { get; }
    public HomeViewModel Home { get; }
    public DownloadViewModel Download { get; }
    public SettingsViewModel SettingsPage { get; }
    public LogsViewModel Logs { get; }
    public AboutViewModel About { get; }

    public ObservableCollection<NavigationItem> NavItems { get; }

    public RelayCommand ToggleNavCommand { get; }
    public RelayCommand GoHomeCommand { get; }
    public RelayCommand GoSettingsCommand { get; }

    /// <summary>由窗口注入的确认框与目录选择器。</summary>
    public Func<string, string, Task<bool>>? ConfirmAsync { get; set; }
    public Func<Task<string?>>? PickFolderAsync { get; set; }

    public NavigationItem? SelectedNav
    {
        get => _selectedNav;
        set
        {
            if (value == null || ReferenceEquals(_selectedNav, value)) return;

            if (_selectedNav?.Page is DownloadViewModel)
                Download.CancelActive();

            _selectedNav = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CurrentPage));
            OnPropertyChanged(nameof(PageTitle));
            OnPropertyChanged(nameof(PageSubtitle));

            switch (value.Page)
            {
                case HomeViewModel home:
                    home.OnActivated();
                    break;
                case DownloadViewModel download:
                    download.OnActivated();
                    break;
            }
        }
    }

    public object? CurrentPage => _selectedNav?.Page;

    public string PageTitle => _selectedNav?.Title ?? "AovoCL";

    public string PageSubtitle => _selectedNav?.Subtitle ?? "";

    public bool IsNavCollapsed { get; private set; }

    public bool IsNavExpanded => !IsNavCollapsed;

    public bool IsNavToggleVisible => !_isNarrow;

    public double NavWidth => IsNavCollapsed ? CollapsedNavWidth : ExpandedNavWidth;

    public string NavToggleText => IsNavCollapsed ? "展开导航" : "收起导航";

    /// <summary>窗口宽度变化时自动折叠为图标栏，避免窄窗口破版。</summary>
    public void SetWindowWidth(double width)
    {
        var narrow = width < NarrowThreshold;
        if (narrow == _isNarrow) return;

        _isNarrow = narrow;
        OnPropertyChanged(nameof(IsNavToggleVisible));

        if (narrow) ApplyNavCollapsed(true);
    }

    private void ToggleNav() => ApplyNavCollapsed(!IsNavCollapsed);

    private void ApplyNavCollapsed(bool collapsed)
    {
        if (IsNavCollapsed == collapsed) return;
        IsNavCollapsed = collapsed;
        OnPropertyChanged(nameof(IsNavCollapsed));
        OnPropertyChanged(nameof(IsNavExpanded));
        OnPropertyChanged(nameof(NavWidth));
        OnPropertyChanged(nameof(NavToggleText));
    }

    public async Task InitializeAsync() => await Home.RefreshAsync(false);

    private async Task ReloadAfterRootChangedAsync()
    {
        await Task.WhenAll(Home.RefreshAsync(false), Download.RefreshAsync(false));
    }

    public void Shutdown()
    {
        Download.CancelActive();
        Home.Shutdown();
    }

    private static Geometry Icon(string key) =>
        Application.Current?.FindResource(key) as Geometry
        ?? StreamGeometry.Parse("M0,0 H1 V1 Z");
}
