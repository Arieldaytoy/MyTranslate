namespace MyTranslate.Core;

/// <summary>翻译来源</summary>
public enum TranslationSource
{
    /// <summary>调用翻译 API</summary>
    API,
    /// <summary>持久化缓存命中</summary>
    Cache,
    /// <summary>纯术语匹配（未调 API）</summary>
    Glossary,
    /// <summary>同语言跳过</summary>
    SameLang,
}

/// <summary>
/// 翻译结果模型
/// </summary>
public class TranslationResult
{
    /// <summary>翻译后的文本</summary>
    public string TranslatedText { get; set; } = string.Empty;

    /// <summary>原文</summary>
    public string OriginalText { get; set; } = string.Empty;

    /// <summary>源语言</summary>
    public Language SourceLanguage { get; set; }

    /// <summary>目标语言</summary>
    public Language TargetLanguage { get; set; }

    /// <summary>使用的翻译器名称</summary>
    public string TranslatorName { get; set; } = string.Empty;

    /// <summary>翻译耗时</summary>
    public TimeSpan Duration { get; set; }

    /// <summary>翻译完成的时间戳</summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>是否来自缓存</summary>
    public bool IsCached { get; set; }

    /// <summary>翻译来源</summary>
    public TranslationSource Source { get; set; } = TranslationSource.API;

    /// <summary>翻译来源的显示文本</summary>
    public string SourceDisplay => Source switch
    {
        TranslationSource.API => "API",
        TranslationSource.Cache => "缓存",
        TranslationSource.Glossary => "术语库",
        TranslationSource.SameLang => "同语言",
        _ => "未知",
    };

    /// <summary>是否翻译成功</summary>
    public bool Success { get; set; }

    /// <summary>错误信息（失败时有值）</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>输入来源标签（如 [按钮]、[悬停]、[划词]）</summary>
    public string InputTag { get; set; } = "";

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static TranslationResult Ok(string original, string translated,
        Language source, Language target, string translator, TimeSpan duration,
        bool isCached = false, TranslationSource translationSource = TranslationSource.API)
    {
        return new TranslationResult
        {
            OriginalText = original,
            TranslatedText = translated,
            SourceLanguage = source,
            TargetLanguage = target,
            TranslatorName = translator,
            Duration = duration,
            IsCached = isCached,
            Source = translationSource,
            Success = true,
        };
    }

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static TranslationResult Fail(string original, string error,
        Language source, Language target, string translator)
    {
        return new TranslationResult
        {
            OriginalText = original,
            SourceLanguage = source,
            TargetLanguage = target,
            TranslatorName = translator,
            Success = false,
            ErrorMessage = error,
        };
    }
}
