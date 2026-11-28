using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using AovoCL.Desktop.Services;
using AovoCL.Desktop.ViewModels;

namespace AovoCL.Desktop.Views;

/// <summary>独立更新窗口：展示整体进度、下载速度与每个下载线程的进度。</summary>
public partial class UpdateWindow : Window
{
    private readonly UpdateInfo _info;
    private readonly UpdateViewModel _viewModel;
    private readonly TaskCompletionSource<string?> _result = new();
    private CancellationTokenSource? _cts;

    public UpdateWindow()
        : this(new UpdateInfo("0.0.0", new Version(0, 0), "", "", ""))
    {
    }

    public UpdateWindow(UpdateInfo info)
    {
        InitializeComponent();

        _info = info;
        _viewModel = new UpdateViewModel(info.VersionText);
        DataContext = _viewModel;

        Opened += async (_, _) => await StartAsync();
        Closing += (_, _) =>
        {
            _cts?.Cancel();
            _result.TrySetResult(null);
        };
    }

    /// <summary>以模态方式显示并开始下载；返回安装包路径，取消或失败返回 null。</summary>
    public Task<string?> DownloadAsync(Window owner)
    {
        _ = ShowDialog(owner);
        return _result.Task;
    }

    private async Task StartAsync()
    {
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        _viewModel.Reset();

        try
        {
            var path = await new UpdateService().DownloadAsync(
                _info,
                _viewModel.ApplyProgress,
                _viewModel.ApplySegment,
                UpdateService.MaxConnections,
                _cts.Token);

            _viewModel.Complete();
            _result.TrySetResult(path);

            await Task.Delay(600);
            if (IsVisible) Close();
        }
        catch (OperationCanceledException)
        {
            if (IsVisible) Close();
        }
        catch (Exception ex)
        {
            _viewModel.Fail(ex.Message);
        }
    }

    private void OnRetryClick(object? sender, RoutedEventArgs e) => _ = StartAsync();

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close();

    private void OnMinimizeClick(object? sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    /// <summary>无边框窗口：按住标题栏拖动。</summary>
    private void OnTitleBarPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        BeginMoveDrag(e);
    }
}
