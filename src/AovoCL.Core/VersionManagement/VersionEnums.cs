namespace AovoCL.Core.VersionManagement;

/// <summary>Minecraft 官方版本类型（来自 version_manifest_v2.json 的 type 字段）。</summary>
public enum VersionType
{
    Release,          // release
    Snapshot,         // snapshot
    OldBeta,          // old_beta
    OldAlpha,         // old_alpha
    AprilFools,       // 愚人节版本（根据 id 特征判断）
    Unknown           // 无法识别
}

/// <summary>加载器 / 模组加载平台。</summary>
public enum LoaderType
{
    Vanilla,          // 无加载器
    Forge,            // net.minecraftforge
    Fabric,           // net.fabricmc
    NeoForge,         // net.neoforged
    Quilt,            // org.quiltmc
    OptiFine,         // 仅含 OptiFine
    LiteLoader,       // LiteLoader
    None              // 未知
}

/// <summary>版本健康状态。</summary>
public enum VersionStatus
{
    Normal,           // 正常
    Error,            // 致命错误：json 损坏 / 继承循环 / 父版本缺失
    MissingAssets,    // AssetIndex 中 objects 缺失
    MissingLibraries  // libraries 数组声明的库缺失
}

/// <summary>自动分类枚举（内部值为排序优先级）。</summary>
public enum VersionCategory
{
    Favorite = 0,
    Modded = 1,
    OriginalLike = 2,
    Other = 3
}

/// <summary>UI 分类筛选器选项。</summary>
public enum CategoryFilter
{
    All,
    Favorite,
    Modded,
    OriginalLike,
    Other
}
