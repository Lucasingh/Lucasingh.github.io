using Avalonia.Media;

namespace AovoCL.Desktop.ViewModels;

/// <summary>左侧导航项：图标 + 标题 + 对应页面 ViewModel。</summary>
public sealed class NavigationItem
{
    public NavigationItem(string title, string subtitle, Geometry icon, ObservableObject page)
    {
        Title = title;
        Subtitle = subtitle;
        Icon = icon;
        Page = page;
    }

    public string Title { get; }

    public string Subtitle { get; }

    public Geometry Icon { get; }

    public ObservableObject Page { get; }
}
