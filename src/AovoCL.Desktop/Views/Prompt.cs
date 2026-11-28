using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using AovoCL.Desktop.Services;

namespace AovoCL.Desktop.Views;

/// <summary>轻量确认对话框，配色走主题资源，不引入第三方对话框库。</summary>
public static class Prompt
{
    public static async Task<bool> ConfirmAsync(Window owner, string title, string message)
    {
        var result = new TaskCompletionSource<bool>();
        var variant = owner.ActualThemeVariant;

        var ok = new Button
        {
            Content = "确定",
            MinWidth = 88,
            IsDefault = true,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Classes = { "primary" }
        };

        var cancel = new Button
        {
            Content = "取消",
            MinWidth = 88,
            IsCancel = true,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Classes = { "ghost" }
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { cancel, ok }
        };

        var dialog = new Window
        {
            Title = title,
            Width = 420,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            RequestedThemeVariant = variant,
            Background = ThemeBrush("SidebarBrush", variant),
            Content = new Border
            {
                Padding = new Thickness(20),
                Child = new StackPanel
                {
                    Spacing = 16,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = message,
                            TextWrapping = TextWrapping.Wrap,
                            FontSize = 13
                        },
                        buttons
                    }
                }
            }
        };

        ok.Click += (_, _) =>
        {
            result.TrySetResult(true);
            dialog.Close();
        };
        cancel.Click += (_, _) =>
        {
            result.TrySetResult(false);
            dialog.Close();
        };
        dialog.Closed += (_, _) => result.TrySetResult(false);

        await dialog.ShowDialog(owner);
        return await result.Task;
    }

    private static IBrush ThemeBrush(string key, ThemeVariant variant)
    {
        if (Application.Current is { } app && app.TryFindResource(key, variant, out var value) && value is IBrush brush)
            return brush;

        return Brushes.Transparent;
    }

    /// <summary>更新说明是 Markdown 文本，这里只去掉标题标记，保留原有换行与列表。</summary>
    private static string FormatNotes(string notes)
    {
        if (string.IsNullOrWhiteSpace(notes)) return "本次更新没有填写说明。";

        var lines = notes.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].TrimStart();
            if (trimmed.StartsWith('#')) lines[i] = trimmed.TrimStart('#', ' ');
        }

        return string.Join('\n', lines).Trim();
    }

    /// <summary>发现新版本时展示更新内容，返回是否立即更新。</summary>
    public static async Task<bool> UpdateAsync(Window owner, UpdateInfo info, Version current)
    {
        var result = new TaskCompletionSource<bool>();
        var variant = owner.ActualThemeVariant;

        var notes = new TextBlock
        {
            Text = FormatNotes(info.Notes),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12.5,
            LineHeight = 19
        };

        var notesBox = new Border
        {
            Background = ThemeBrush("ControlBrush", variant),
            BorderBrush = ThemeBrush("CardBorderBrush", variant),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14),
            MinHeight = 90,
            Child = new ScrollViewer
            {
                MaxHeight = 260,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                Content = notes
            }
        };

        var later = new Button
        {
            Content = "稍后",
            MinWidth = 88,
            IsCancel = true,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Classes = { "ghost" }
        };

        var now = new Button
        {
            Content = "立即更新",
            MinWidth = 96,
            IsDefault = true,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Classes = { "primary" }
        };

        var dialog = new Window
        {
            Title = "发现新版本",
            Width = 520,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            RequestedThemeVariant = variant,
            Background = ThemeBrush("SidebarBrush", variant),
            Content = new Border
            {
                Padding = new Thickness(20),
                Child = new StackPanel
                {
                    Spacing = 14,
                    Children =
                    {
                        new StackPanel
                        {
                            Spacing = 4,
                            Children =
                            {
                                new TextBlock
                                {
                                    Text = $"发现新版本 {info.VersionText}",
                                    FontSize = 17,
                                    FontWeight = FontWeight.SemiBold
                                },
                                new TextBlock
                                {
                                    Text = $"当前版本 {current.ToString(3)} · 更新内容如下",
                                    FontSize = 11.5,
                                    Foreground = ThemeBrush("TextMutedBrush", variant)
                                }
                            }
                        },
                        notesBox,
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 8,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Children = { later, now }
                        }
                    }
                }
            }
        };

        now.Click += (_, _) =>
        {
            result.TrySetResult(true);
            dialog.Close();
        };
        later.Click += (_, _) =>
        {
            result.TrySetResult(false);
            dialog.Close();
        };
        dialog.Closed += (_, _) => result.TrySetResult(false);

        await dialog.ShowDialog(owner);
        return await result.Task;
    }
}
