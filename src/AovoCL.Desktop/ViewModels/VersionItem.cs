using AovoCL.Core;

namespace AovoCL.Desktop.ViewModels;

/// <summary>在线清单中的一个版本，供启动页选择。</summary>
public sealed class VersionItem : ObservableObject
{
    private bool _installed;

    public VersionItem(VersionEntry entry, bool installed)
    {
        Entry = entry;
        _installed = installed;
    }

    public VersionEntry Entry { get; }

    public string Id => Entry.Id;

    public string TypeLabel => Entry.Type switch
    {
        "release" => "正式版",
        "snapshot" => "快照",
        "old_beta" => "Beta",
        "old_alpha" => "Alpha",
        _ => Entry.Type
    };

    public string Subtitle => $"{TypeLabel} · {Entry.ReleaseTime.ToLocalTime():yyyy-MM-dd}";

    public bool IsInstalled
    {
        get => _installed;
        set => SetProperty(ref _installed, value);
    }
}
