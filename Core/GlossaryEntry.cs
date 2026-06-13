namespace MyTranslate.Core;

/// <summary>
/// 术语条目
/// </summary>
public class GlossaryEntry
{
    /// <summary>原文术语</summary>
    public string SourceTerm { get; set; } = "";

    /// <summary>期望的翻译</summary>
    public string TargetTerm { get; set; } = "";

    /// <summary>翻译方向</summary>
    public GlossaryDirection Direction { get; set; } = GlossaryDirection.Both;

    /// <summary>备注（可选）</summary>
    public string? Note { get; set; }

    /// <summary>
    /// 检查该条目是否在指定翻译方向下生效
    /// </summary>
    public bool AppliesTo(GlossaryDirection queryDirection)
        => Direction == GlossaryDirection.Both || Direction == queryDirection;
}

/// <summary>
/// 术语翻译方向
/// </summary>
public enum GlossaryDirection
{
    /// <summary>中→英</summary>
    ChineseToEnglish,

    /// <summary>英→中</summary>
    EnglishToChinese,

    /// <summary>双向</summary>
    Both,
}
