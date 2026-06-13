namespace MyTranslate.Core;

/// <summary>
/// 翻译器统一接口 — 所有翻译引擎都需要实现此接口
/// </summary>
public interface ITranslator
{
    /// <summary>翻译器显示名称（如"腾讯翻译"）</summary>
    string Name { get; }

    /// <summary>翻译器唯一标识（如"tencent"）</summary>
    string Id { get; }

    /// <summary>
    /// 异步翻译文本
    /// </summary>
    /// <param name="text">待翻译文本</param>
    /// <param name="source">源语言</param>
    /// <param name="target">目标语言</param>
    /// <returns>翻译结果</returns>
    Task<TranslationResult> TranslateAsync(string text, Language source, Language target);

    /// <summary>
    /// 检查翻译器是否已正确配置（API 密钥等）
    /// </summary>
    bool IsConfigured();
}
