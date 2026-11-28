using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using AovoCL.Desktop.Services;
using AovoCL.Desktop.ViewModels;
using AovoCL.Desktop.Views.Pages;

namespace AovoCL.Desktop.Views;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly Dictionary<object, Control> _pages = new();
    private bool _checkingUpdate;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainWindowViewModel
        {
            ConfirmAsync = (title, message) => Prompt.ConfirmAsync(this, title, message),
            PickFolderAsync = PickFolderAsync
        };
        DataContext = _viewModel;
        _viewModel.SettingsPage.CheckUpdatesAsync = () => CheckForUpdatesAsync(true);

        // 启动时预加载全部页面，切换导航只做显示，避免首次切换现建控件导致动画掉帧
        _pages[_viewModel.Home] = new HomeView { DataContext = _viewModel.Home };
        _pages[_viewModel.Download] = new DownloadView { DataContext = _viewModel.Download };
        _pages[_viewModel.SettingsPage] = new SettingsView { DataContext = _viewModel.SettingsPage };
        _pages[_viewModel.Logs] = new LogsView { DataContext = _viewModel.Logs };
        _pages[_viewModel.About] = new AboutView { DataContext = _viewModel.About };

        if (_viewModel.CurrentPage is { } firstPage && _pages.TryGetValue(firstPage, out var firstView))
            PageHost.Content = firstView;

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        Opened += async (_, _) =>
        {
            await _viewModel.InitializeAsync();
            await CheckForUpdatesAsync();
        };
        Closing += (_, _) => _viewModel.Shutdown();
        SizeChanged += (_, _) => _viewModel.SetWindowWidth(Bounds.Width);
        PropertyChanged += (_, e) =>
        {
            if (e.Property == WindowStateProperty) UpdateMaximizedMargin();
        };
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainWindowViewModel.CurrentPage)) return;
        if (_viewModel.CurrentPage is { } page && _pages.TryGetValue(page, out var view))
            PageHost.Content = view;
    }

    /// <summary>检查更新：启动时静默检查，设置页手动检查会反馈结果；有新版先征求用户同意再下载安装。</summary>
    private async Task CheckForUpdatesAsync(bool manual = false)
    {
        if (_checkingUpdate) return;
        _checkingUpdate = true;

        try
        {
            UpdateInfo? info;
            try
            {
                info = await new UpdateService().CheckAsync();
            }
            catch (Exception ex)
            {
                if (manual) _viewModel.Status.SetError("检查更新失败：" + ex.Message);
                return;
            }

            if (info == null)
            {
                if (manual) _viewModel.Status.SetSuccess($"已是最新版本 AovoCL {UpdateService.CurrentVersion.ToString(3)}");
                return;
            }

            if (!await Prompt.UpdateAsync(this, info, UpdateService.CurrentVersion)) return;

            // 独立的更新窗口展示下载进度、速度与各线程状态，不在右下角后台下载
            var path = await new UpdateWindow(info).DownloadAsync(this);
            if (path == null) return;

            _viewModel.Status.SetSuccess("更新包已下载，正在安装");
            UpdateService.RunInstaller(path);
            Close();
        }
        catch (Exception ex)
        {
            _viewModel.Status.SetError("更新失败：" + ex.Message);
        }
        finally
        {
            _checkingUpdate = false;
        }
    }

    /// <summary>无边框窗口：标题栏拖动移动，双击切换最大化。</summary>
    private void OnTitleBarPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }

        BeginMoveDrag(e);
    }

    private void OnMinimizeClick(object? sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void OnToggleMaximizeClick(object? sender, RoutedEventArgs e) => ToggleMaximize();

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void ToggleMaximize() =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    /// <summary>无边框最大化时窗口边界会延伸到屏幕外，留出系统缩放边距避免内容被裁切。</summary>
    private void UpdateMaximizedMargin()
    {
        var margin = WindowState == WindowState.Maximized && OperatingSystem.IsWindows() ? 7 : 0;
        RootGrid.Margin = new Thickness(margin);
    }

    /// <summary>目录选择是纯 UI 交互，交给窗口处理（跨平台 StorageProvider）。</summary>
    private async Task<string?> PickFolderAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择游戏目录",
            AllowMultiple = false
        });

        return folders.Count == 0 ? null : folders[0].Path.LocalPath;
    }
}
