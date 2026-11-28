using Avalonia;
using Avalonia.Styling;
using AovoCL.Desktop.ViewModels;

namespace AovoCL.Desktop.Services;

public enum ThemeMode
{
    System,
    Light,
    Dark
}

/// <summary>主题切换：浅色 / 深色 / 跟随系统，通过 ThemeVariant 实现。</summary>
public sealed class ThemeManager : ObservableObject
{
    private ThemeMode _mode = ThemeMode.System;

    public ThemeManager()
    {
        CycleCommand = new RelayCommand(Cycle);
    }

    public ThemeMode Mode
    {
        get => _mode;
        set
        {
            if (!SetProperty(ref _mode, value)) return;
            Apply();
            OnPropertyChanged(nameof(ModeLabel));
            OnPropertyChanged(nameof(SelectedIndex));
            OnPropertyChanged(nameof(IsLight));
        }
    }

    public string ModeLabel => _mode switch
    {
        ThemeMode.Light => "浅色",
        ThemeMode.Dark => "深色",
        _ => "跟随系统"
    };

    /// <summary>设置页分段控件绑定：0 = 浅色，1 = 深色，2 = 跟随系统。</summary>
    public int SelectedIndex
    {
        get => _mode switch { ThemeMode.Light => 0, ThemeMode.Dark => 1, _ => 2 };
        set
        {
            if (value < 0 || value > 2) return;
            Mode = value switch
            {
                0 => ThemeMode.Light,
                1 => ThemeMode.Dark,
                _ => ThemeMode.System
            };
        }
    }

    public bool IsLight => _mode == ThemeMode.Light;

    public RelayCommand CycleCommand { get; }

    public void Cycle() => Mode = _mode switch
    {
        ThemeMode.System => ThemeMode.Light,
        ThemeMode.Light => ThemeMode.Dark,
        _ => ThemeMode.System
    };

    private void Apply()
    {
        if (Application.Current == null) return;
        Application.Current.RequestedThemeVariant = _mode switch
        {
            ThemeMode.Light => ThemeVariant.Light,
            ThemeMode.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
    }
}
