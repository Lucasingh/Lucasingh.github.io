using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
using AovoCL.Desktop.ViewModels;

namespace AovoCL.Desktop.Views.Pages;

public partial class LogsView : UserControl
{
    private LogsViewModel? _vm;

    public LogsView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach();
    }

    private void Attach()
    {
        if (_vm != null) _vm.Log.PropertyChanged -= OnLogChanged;
        _vm = DataContext as LogsViewModel;
        if (_vm != null) _vm.Log.PropertyChanged += OnLogChanged;
    }

    private void OnLogChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(LogBuffer.Text)) return;
        if (_vm?.AutoScroll != true) return;
        Dispatcher.UIThread.Post(() => LogScroll.ScrollToEnd(), DispatcherPriority.Background);
    }
}
