using System.Windows.Input;

namespace AovoCL.Desktop.ViewModels;

/// <summary>同步命令，用于纯 UI 操作（导航、切换主题等）。</summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public void Execute(object? parameter)
    {
        if (CanExecute(parameter)) _execute();
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

/// <summary>轻量异步命令，避免为 MVP 引入完整 MVVM 框架。</summary>
public sealed class AsyncCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private bool _running;

    public AsyncCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => !_running && (_canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;

        _running = true;
        RaiseCanExecuteChanged();
        try
        {
            await _execute();
        }
        finally
        {
            _running = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

/// <summary>带参数的异步命令，用于列表项上的下载 / 重命名 / 删除按钮。</summary>
public sealed class AsyncParamCommand : ICommand
{
    private readonly Func<object?, Task> _execute;
    private readonly Func<object?, bool>? _canExecute;
    private bool _running;

    public AsyncParamCommand(Func<object?, Task> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => !_running && (_canExecute?.Invoke(parameter) ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;

        _running = true;
        RaiseCanExecuteChanged();
        try
        {
            await _execute(parameter);
        }
        finally
        {
            _running = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
