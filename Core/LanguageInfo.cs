namespace MyTranslate.Core;

/// <summary>
/// 语言枚举 — 支持中英互译为主，可扩展其他语言
/// </summary>
public enum Language
{
    Auto,       // 自动识别
    Chinese,    // 中文（简体）
    English,    // 英语
    Japanese,   // 日语
    Korean,     // 韩语
    French,     // 法语
    German,     // 德语
    Spanish,    // 西班牙语
    Russian,    // 俄语
}

/// <summary>
/// 语言信息模型 — 封装语言的显示名称和各翻译器 API 代码
/// </summary>
public class LanguageInfo
{
    /// <summary>语言枚举值</summary>
    public Language Language { get; }

    /// <summary>界面显示名称</summary>
    public string DisplayName { get; }

    /// <summary>腾讯翻译 API 代码</summary>
    public string TencentCode { get; }

    /// <summary>百度翻译 API 代码（预留）</summary>
    public string BaiduCode { get; }

    /// <summary>阿里翻译 API 代码（预留）</summary>
    public string AlibabaCode { get; }

    public LanguageInfo(Language language, string displayName,
        string tencentCode, string baiduCode = "", string alibabaCode = "")
    {
        Language = language;
        DisplayName = displayName;
        TencentCode = tencentCode;
        BaiduCode = baiduCode;
        AlibabaCode = alibabaCode;
    }

    /// <summary>
    /// 预定义语言列表 — 各翻译器的语言代码对照表
    /// </summary>
    public static readonly LanguageInfo[] SupportedLanguages =
    [
        new(Language.Auto,     "自动识别", "auto", "auto", "auto"),
        new(Language.Chinese,  "中文",     "zh",   "zh",   "zh"),
        new(Language.English,  "英语",     "en",   "en",   "en"),
        new(Language.Japanese, "日语",     "ja",   "jp",   "ja"),
        new(Language.Korean,   "韩语",     "ko",   "kor",  "ko"),
        new(Language.French,   "法语",     "fr",   "fra",  "fr"),
        new(Language.German,   "德语",     "de",   "de",   "de"),
        new(Language.Spanish,  "西班牙语", "es",   "spa",  "es"),
        new(Language.Russian,  "俄语",     "ru",   "ru",   "ru"),
    ];

    /// <summary>
    /// 根据枚举值获取语言信息
    /// </summary>
    public static LanguageInfo? GetByLanguage(Language language)
        => SupportedLanguages.FirstOrDefault(l => l.Language == language);

    /// <summary>
    /// 获取所有语言显示名称（用于填充 ComboBox）
    /// </summary>
    public static string[] GetDisplayNames()
        => SupportedLanguages.Select(l => l.DisplayName).ToArray();
}
