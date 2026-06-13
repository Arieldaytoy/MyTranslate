namespace MyTranslate.Core;

/// <summary>
/// 简单的语言检测器 — 基于 Unicode 字符范围统计
/// </summary>
public static class LanguageDetector
{
    /// <summary>
    /// 检测文本最可能的语言
    /// </summary>
    public static Language Detect(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Language.Auto;

        int chinese = 0;    // CJK 统一汉字
        int hiragana = 0;   // 日语平假名
        int katakana = 0;   // 日语片假名
        int hangul = 0;     // 韩语
        int latin = 0;      // 拉丁字母（英/法/德/西等）
        int cyrillic = 0;   // 西里尔字母（俄语）

        foreach (char c in text)
        {
            if (c >= '\u4E00' && c <= '\u9FFF')       chinese++;   // CJK 统一汉字
            else if (c >= '\u3040' && c <= '\u309F')   hiragana++;  // 平假名
            else if (c >= '\u30A0' && c <= '\u30FF')   katakana++;  // 片假名
            else if (c >= '\uAC00' && c <= '\uD7AF')   hangul++;    // 韩文音节
            else if (c >= '\u1100' && c <= '\u11FF')   hangul++;    // 韩文字母
            else if (c >= '\u3130' && c <= '\u318F')   hangul++;    // 韩文兼容字母
            else if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')) latin++;
            else if (c >= '\u0400' && c <= '\u04FF')   cyrillic++;  // 西里尔字母
        }

        // 日语特征：有假名
        if (hiragana + katakana > 0)
            return Language.Japanese;

        // 韩语特征：有韩文
        if (hangul > 0)
            return Language.Korean;

        // 俄语特征：有西里尔字母
        if (cyrillic > 0)
            return Language.Russian;

        // 中文 vs 英文
        // 有 CJK 字符时，拉丁字母需要至少 2 倍于中文字符才判定为英语
        // 避免 UI 文本如 "关闭翻译 (Ctrl+Y)" 因拉丁字母略多而被误判为英语
        if (chinese > 0)
        {
            if (latin > 0 && latin > chinese * 2)
                return Language.English;
            return Language.Chinese;
        }

        if (latin > 0)
            return Language.English;

        // 纯数字/符号等无法判断
        return Language.Auto;
    }

    /// <summary>
    /// 检查源语言和目标语言是否实质相同（考虑 Auto 检测）
    /// </summary>
    public static bool IsSameLanguage(string text, Language source, Language target)
    {
        if (source == target && source != Language.Auto)
            return true;

        if (source == Language.Auto)
        {
            var detected = Detect(text);
            if (detected == target)
                return true;
        }

        if (target == Language.Auto)
        {
            var detected = Detect(text);
            if (detected == source)
                return true;
        }

        return false;
    }
}
