using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
using AovoCL.Desktop.ViewModels;

namespace AovoCL.Desktop.Views.Pages;

public partial class HomeView : UserControl
{
    private ScrollViewer? _logScroll;
    private HomeViewModel? _viewModel;

    public HomeView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => Bind();
        DataContextChanged += (_, _) => Bind();
    }

    /// <summary>日志更新后自动滚动到底部（纯 UI 行为）。</summary>
    private void Bind()
    {
        if (_viewModel != null) _viewModel.Log.PropertyChanged -= OnLogPropertyChanged;

        _viewModel = DataContext as HomeViewModel;
        if (_viewModel != null) _viewModel.Log.PropertyChanged += OnLogPropertyChanged;

        _logScroll = this.FindControl<ScrollViewer>("LogScroll");
    }

    private void OnLogPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(LogBuffer.Text)) return;
        Dispatcher.UIThread.Post(() => _logScroll?.ScrollToEnd());
    }
}
