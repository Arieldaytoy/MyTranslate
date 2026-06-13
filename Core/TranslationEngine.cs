namespace MyTranslate.Core;

/// <summary>
/// 翻译调度器 — 统一管理翻译引擎，提供持久化历史缓存、术语库后处理能力
/// </summary>
public class TranslationEngine
{
    private readonly Dictionary<string, ITranslator> _translators = [];
    private ITranslator? _currentTranslator;

    // 内存缓存（会话内快速查找）
    private readonly Dictionary<string, TranslationResult> _memoryCache = [];
    private const int MaxMemoryCacheSize = 1000;

    /// <summary>当前使用的翻译器</summary>
    public ITranslator? CurrentTranslator => _currentTranslator;

    /// <summary>所有已注册的翻译器</summary>
    public IReadOnlyDictionary<string, ITranslator> Translators => _translators;

    /// <summary>术语库管理器</summary>
    public GlossaryManager Glossary { get; } = new();

    /// <summary>翻译历史管理器（持久化）</summary>
    public TranslationHistoryManager History { get; } = new();

    /// <summary>注册一个翻译器</summary>
    public void RegisterTranslator(ITranslator translator)
    {
        _translators[translator.Id] = translator;
    }

    /// <summary>根据 ID 切换当前翻译器</summary>
    public bool SetCurrentTranslator(string translatorId)
    {
        if (_translators.TryGetValue(translatorId, out var translator))
        {
            _currentTranslator = translator;
            return true;
        }
        return false;
    }

    /// <summary>执行翻译（文本规范化 → 同语言检测 → 历史缓存 → 内存缓存 → 术语库预处理 → API → 后处理）</summary>
    public async Task<TranslationResult> TranslateAsync(string text, Language source, Language target)
    {
        if (_currentTranslator == null)
            return TranslationResult.Fail(text, "未选择翻译器", source, target, "None");

        if (string.IsNullOrWhiteSpace(text))
            return TranslationResult.Fail(text, "文本为空", source, target, _currentTranslator.Name);

        // 规范化文本：去除首尾空白、合并连续空格（修复划词翻译缓存未命中问题）
        text = NormalizeText(text);

        // ===== 第0层：同语言检测 =====
        if (LanguageDetector.IsSameLanguage(text, source, target))
        {
            return TranslationResult.Ok(
                text, text, source, target, "(同语言)",
                TimeSpan.Zero, isCached: true, translationSource: TranslationSource.SameLang);
        }

        // ===== 第1层：查持久化历史缓存 =====
        var historyEntry = History.Lookup(text, source, target);
        if (historyEntry != null)
        {
            return TranslationResult.Ok(
                text, historyEntry.TranslatedText,
                source, target, historyEntry.TranslatorName,
                TimeSpan.Zero, isCached: true, translationSource: TranslationSource.Cache);
        }

        // ===== 第2层：查内存缓存 =====
        var memoryKey = BuildMemoryKey(text, source, target);
        if (_memoryCache.TryGetValue(memoryKey, out var memCached))
        {
            memCached.IsCached = true;
            memCached.Source = TranslationSource.Cache;
            return memCached;
        }

        // ===== 技术模式保护（路径、URL、扩展名等） =====
        var (protectedText, protectMap) = TextProtector.Protect(text);

        // ===== 术语库预处理 =====
        var (processedText, placeholders) = Glossary.PreprocessText(protectedText, source, target);

        var stripped = processedText;
        foreach (var ph in placeholders.Keys)
            stripped = stripped.Replace(ph, "");

        TranslationResult result;

        if (string.IsNullOrWhiteSpace(stripped) && placeholders.Count > 0)
        {
            // 全部是术语，不调 API
            var directTranslation = processedText;
            foreach (var (ph, term) in placeholders)
                directTranslation = directTranslation.Replace(ph, term);

            // 还原技术模式保护
            directTranslation = TextProtector.Restore(directTranslation, protectMap);

            result = TranslationResult.Ok(text, directTranslation.Trim(),
                source, target, _currentTranslator.Name, TimeSpan.Zero,
                translationSource: TranslationSource.Glossary);
        }
        else
        {
            // 调翻译器（带失败重试）
            result = await TranslateWithRetryAsync(processedText, source, target);
            result.Source = TranslationSource.API;

            // 术语库后处理
            if (result.Success && placeholders.Count > 0)
            {
                result.TranslatedText = Glossary.PostprocessText(result.TranslatedText, placeholders);
            }

            // 还原技术模式保护（路径、URL、扩展名等）
            if (result.Success && protectMap.Count > 0)
            {
                result.TranslatedText = TextProtector.Restore(result.TranslatedText, protectMap);
            }
        }

        // ===== 写缓存 =====
        if (result.Success)
        {
            if (_memoryCache.Count >= MaxMemoryCacheSize)
                _memoryCache.Clear();
            _memoryCache[memoryKey] = result;

            var detectedSourceLang = source;
            var detectedTargetLang = target;

            if (detectedSourceLang == Language.Auto)
            {
                var detected = LanguageDetector.Detect(text);
                if (detected != Language.Auto)
                    detectedSourceLang = detected;
            }

            if (detectedTargetLang == Language.Auto)
            {
                var detected = LanguageDetector.Detect(result.TranslatedText);
                if (detected != Language.Auto)
                    detectedTargetLang = detected;
            }

            History.Save(text, result.TranslatedText,
                source, detectedSourceLang, target == Language.Auto ? detectedTargetLang : target,
                _currentTranslator.Name);

            // 每次翻译后立即保存到文件，确保关闭程序时不丢失缓存
            History.SaveToFile();
        }

        return result;
    }

    /// <summary>规范化文本：去除首尾空白、合并连续空格，保留换行符</summary>
    private static string NormalizeText(string text)
    {
        text = text.Trim();

        var sb = new System.Text.StringBuilder(text.Length);
        bool lastWasSpace = false;
        bool lastWasNewline = false;

        foreach (char c in text)
        {
            if (c == '\r')
            {
                // 跳过 \r，只处理 \n
                continue;
            }
            else if (c == '\n')
            {
                if (!lastWasNewline)
                {
                    sb.Append('\n');
                    lastWasNewline = true;
                    lastWasSpace = false;
                }
            }
            else if (char.IsWhiteSpace(c))
            {
                if (!lastWasSpace && !lastWasNewline)
                {
                    sb.Append(' ');
                    lastWasSpace = true;
                }
            }
            else
            {
                sb.Append(c);
                lastWasSpace = false;
                lastWasNewline = false;
            }
        }

        return sb.ToString();
    }

    /// <summary>带重试的翻译调用（首次失败自动重试一次）</summary>
    private async Task<TranslationResult> TranslateWithRetryAsync(
        string text, Language source, Language target)
    {
        try
        {
            var result = await _currentTranslator!.TranslateAsync(text, source, target);

            if (!result.Success && result.ErrorMessage != null
                && !result.ErrorMessage.Contains("文本为空")
                && !result.ErrorMessage.Contains("未配置"))
            {
                await Task.Delay(200);
                result = await _currentTranslator.TranslateAsync(text, source, target);
            }

            return result;
        }
        catch
        {
            await Task.Delay(300);
            return await _currentTranslator!.TranslateAsync(text, source, target);
        }
    }

    /// <summary>清除内存缓存（不清除持久化历史）</summary>
    public void ClearCache() => _memoryCache.Clear();

    /// <summary>强制调用 API 翻译（跳过所有缓存层：持久化缓存、内存缓存）</summary>
    public async Task<TranslationResult> ForceTranslateAsync(string text, Language source, Language target)
    {
        if (_currentTranslator == null)
            return TranslationResult.Fail(text, "未选择翻译器", source, target, "None");

        if (string.IsNullOrWhiteSpace(text))
            return TranslationResult.Fail(text, "文本为空", source, target, _currentTranslator.Name);

        text = NormalizeText(text);

        if (LanguageDetector.IsSameLanguage(text, source, target))
            return TranslationResult.Ok(text, text, source, target, "(同语言)",
                TimeSpan.Zero, isCached: true, translationSource: TranslationSource.SameLang);

        // 技术模式保护
        var (protectedText, protectMap) = TextProtector.Protect(text);

        // 术语库预处理
        var (processedText, placeholders) = Glossary.PreprocessText(protectedText, source, target);

        var stripped = processedText;
        foreach (var ph in placeholders.Keys)
            stripped = stripped.Replace(ph, "");

        TranslationResult result;

        if (string.IsNullOrWhiteSpace(stripped) && placeholders.Count > 0)
        {
            var directTranslation = processedText;
            foreach (var (ph, term) in placeholders)
                directTranslation = directTranslation.Replace(ph, term);
            directTranslation = TextProtector.Restore(directTranslation, protectMap);
            result = TranslationResult.Ok(text, directTranslation.Trim(),
                source, target, _currentTranslator.Name, TimeSpan.Zero,
                translationSource: TranslationSource.Glossary);
        }
        else
        {
            result = await TranslateWithRetryAsync(processedText, source, target);
            result.Source = TranslationSource.API;

            if (result.Success && placeholders.Count > 0)
                result.TranslatedText = Glossary.PostprocessText(result.TranslatedText, placeholders);

            if (result.Success && protectMap.Count > 0)
                result.TranslatedText = TextProtector.Restore(result.TranslatedText, protectMap);
        }

        // 写缓存（覆盖旧条目）
        if (result.Success)
        {
            var memoryKey = BuildMemoryKey(text, source, target);
            if (_memoryCache.Count >= MaxMemoryCacheSize) _memoryCache.Clear();
            _memoryCache[memoryKey] = result;

            var detectedSourceLang = source;
            var detectedTargetLang = target;
            if (detectedSourceLang == Language.Auto)
            {
                var detected = LanguageDetector.Detect(text);
                if (detected != Language.Auto) detectedSourceLang = detected;
            }
            if (detectedTargetLang == Language.Auto)
            {
                var detected = LanguageDetector.Detect(result.TranslatedText);
                if (detected != Language.Auto) detectedTargetLang = detected;
            }

            History.Save(text, result.TranslatedText,
                source, detectedSourceLang, target == Language.Auto ? detectedTargetLang : target,
                _currentTranslator.Name);
            History.SaveToFile();
        }

        return result;
    }

    /// <summary>清除所有缓存（包括持久化历史）</summary>
    public void ClearAllCaches()
    {
        _memoryCache.Clear();
        History.Clear();
    }

    /// <summary>手动保存历史到文件</summary>
    public void SaveHistory() => History.SaveToFile();

    /// <summary>获取已注册翻译器的显示名称列表</summary>
    public string[] GetTranslatorNames()
        => _translators.Values.Select(t => t.Name).ToArray();

    private static string BuildMemoryKey(string text, Language source, Language target)
        => $"{text}|{source}|{target}";
}
