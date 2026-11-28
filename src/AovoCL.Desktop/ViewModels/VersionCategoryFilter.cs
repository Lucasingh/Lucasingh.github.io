namespace AovoCL.Desktop.ViewModels;

/// <summary>版本分类菜单项，标签后跟实时数量。</summary>
public sealed class VersionCategoryItem : ObservableObject
{
    private int _count;

    public VersionCategoryItem(string label, string? type)
    {
        Label = label;
        Type = type;
    }

    public string Label { get; }

    /// <summary>对应版本清单中的 type；null 表示「全部」。</summary>
    public string? Type { get; }

    public int Count
    {
        get => _count;
        set
        {
            if (SetProperty(ref _count, value)) OnPropertyChanged(nameof(Display));
        }
    }

    public string Display => Count > 0 ? $"{Label} {Count}" : Label;
}

/// <summary>版本清单的分类筛选：正式版 / 快照 / Beta / Alpha。</summary>
public sealed class VersionCategoryFilter : ObservableObject
{
    private int _selectedIndex;

    public VersionCategoryFilter()
    {
        Options = new List<VersionCategoryItem>
        {
            new("全部", null),
            new("正式版", "release"),
            new("快照", "snapshot"),
            new("Beta", "old_beta"),
            new("Alpha", "old_alpha")
        };
    }

    public IReadOnlyList<VersionCategoryItem> Options { get; }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (value < 0 || value >= Options.Count) return;
            if (SetProperty(ref _selectedIndex, value)) OnPropertyChanged(nameof(CurrentLabel));
        }
    }

    public string? CurrentType =>
        _selectedIndex > 0 && _selectedIndex < Options.Count ? Options[_selectedIndex].Type : null;

    public string CurrentLabel =>
        _selectedIndex >= 0 && _selectedIndex < Options.Count ? Options[_selectedIndex].Label : "全部";

    public bool Accepts(string? versionType) =>
        CurrentType is null || string.Equals(versionType, CurrentType, StringComparison.Ordinal);

    /// <summary>按传入的 type 列表刷新每个分类的数量。</summary>
    public void RefreshCounts(IEnumerable<string?> types)
    {
        var counts = new int[Options.Count];
        foreach (var type in types)
        {
            counts[0]++;
            for (var i = 1; i < Options.Count; i++)
            {
                if (type == Options[i].Type) counts[i]++;
            }
        }

        for (var i = 0; i < Options.Count; i++)
            Options[i].Count = counts[i];
    }
}
