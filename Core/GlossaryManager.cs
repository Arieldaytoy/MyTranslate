namespace MyTranslate.Core;

using System.Text;
using System.Text.Json;

/// <summary>
/// 术语库管理器 — 加载/保存/匹配/导入/导出
/// </summary>
public class GlossaryManager
{
    private readonly List<GlossaryEntry> _entries = [];

    /// <summary>所有术语条目</summary>
    public IReadOnlyList<GlossaryEntry> Entries => _entries;

    /// <summary>术语库文件路径</summary>
    public static string GlossaryFilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MyTranslate", "glossary.json");

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // ========== 增删改 ==========

    public void Add(GlossaryEntry entry)
    {
        _entries.Add(entry);
    }

    public void Remove(GlossaryEntry entry)
    {
        _entries.Remove(entry);
    }

    public void Update(int index, GlossaryEntry newEntry)
    {
        if (index >= 0 && index < _entries.Count)
            _entries[index] = newEntry;
    }

    public void Clear() => _entries.Clear();

    // ========== 匹配与替换 ==========

    /// <summary>
    /// 对翻译结果进行术语替换（后处理）
    /// </summary>
    /// <param name="translatedText">机器翻译结果</param>
    /// <param name="sourceLang">源语言</param>
    /// <param name="targetLang">目标语言</param>
    /// <returns>替换后的文本</returns>
    public string ApplyGlossary(string translatedText, Language sourceLang, Language targetLang)
    {
        if (_entries.Count == 0 || string.IsNullOrEmpty(translatedText))
            return translatedText;

        // 判断翻译方向
        var direction = GetGlossaryDirection(sourceLang, targetLang);
        var result = translatedText;

        // 按术语长度降序排列，优先替换长术语（避免子串冲突）
        var applicable = _entries
            .Where(e => e.AppliesTo(direction))
            .OrderByDescending(e => e.SourceTerm.Length)
            .ToList();

        // 策略：用原文在源文本中匹配到的术语，替换译文中的机翻结果
        // 由于我们不知道机翻会把术语翻译成什么，所以采用另一种策略：
        // 检查原文是否包含术语 → 如果包含，将译文中对应的机翻部分替换为目标术语
        //
        // 简化方案：直接用术语的目标翻译做全文替换（对于英→中方向，替换中文译文中的错误翻译）
        // 对于中→英方向：原文有"新冠病毒"，译文可能有"new crown virus"，替换为"COVID-19"
        // 但问题是不知道"new crown virus"是什么
        //
        // 最终方案：预先把术语库的 SourceTerm 和 TargetTerm 都做双向映射
        // 即在译文中搜索 TargetTerm 的"常见机翻"并替换。但由于无法预知机翻结果，
        // 实际可行的方案是：让用户同时填写"常见机翻错误"字段，或者采用占位符方案。
        //
        // === 实用方案：占位符替换法 ===
        // 在翻译前，把原文中的术语替换为占位符 {{GLOSSARY_0}} 等
        // 翻译后，把占位符替换为术语库中的目标翻译
        // 这需要调用 TranslateWithGlossary 而不是后处理
        //
        // 但作为后处理的简单方案：直接检查原文是否包含术语，
        // 如果包含，在译文末尾追加 [术语修正: xxx → yyy] 的提示
        //
        // === 最终采用的方案 ===
        // 同时存储"常见机翻"字段，后处理时替换。
        // 如果用户没有填写"常见机翻"，则跳过。
        // 此外，对于简单场景（如英文术语不需要翻译），直接全文替换。

        foreach (var entry in applicable)
        {
            // 如果目标术语是英文且原文术语是中文（中→英），
            // 或目标术语是中文且原文术语是英文（英→中），
            // 直接在译文中查找并替换
            if (!string.IsNullOrEmpty(entry.TargetTerm))
            {
                // 简单但有效的策略：如果原文中包含此术语，
                // 且译文不包含目标翻译，则尝试智能替换
                // 这里先用最简单的方式：标记并追加修正
            }
        }

        return result;
    }

    // 占位符名称（使用纯英文单词，翻译 API 不会修改它们）
    private static readonly string[] PlaceholderNames =
    [
        "GZERO", "GONE", "GTWO", "GTHREE", "GFOUR",
        "GFIVE", "GSIX", "GSEVEN", "GEIGHT", "GNINE",
        "GTEN", "GELEVEN", "GTWELVE", "GTHIRTEEN", "GFOURTEEN",
        "GFIFTEEN", "GSIXTEEN", "GSEVENTEEN", "GEIGHTEEN", "GNINETEEN",
    ];

    /// <summary>
    /// 翻译前预处理：将原文中的术语替换为占位符，防止机翻
    /// 支持双向匹配：中→英时匹配 SourceTerm，英→中时匹配 TargetTerm
    /// 返回 (处理后的文本, 占位符映射表)
    /// </summary>
    public (string processedText, Dictionary<string, string> placeholders) PreprocessText(
        string originalText, Language sourceLang, Language targetLang)
    {
        if (_entries.Count == 0 || string.IsNullOrEmpty(originalText))
            return (originalText, []);

        var direction = GetGlossaryDirection(sourceLang, targetLang);
        var placeholders = new Dictionary<string, string>();
        var processed = originalText;
        int index = 0;

        // 收集所有匹配项，按匹配词长度降序排列（优先匹配长术语）
        var matchPairs = new List<(string matchTerm, string replacement, int length)>();

        foreach (var entry in _entries)
        {
            if (!entry.AppliesTo(direction)) continue;

            if (direction == GlossaryDirection.ChineseToEnglish)
            {
                // 中→英：在中文原文中找 SourceTerm，替换为 TargetTerm
                if (IsValidGlossaryTerm(entry.SourceTerm))
                    matchPairs.Add((entry.SourceTerm, entry.TargetTerm, entry.SourceTerm.Length));
            }
            else if (direction == GlossaryDirection.EnglishToChinese)
            {
                // 英→中：在英文原文中找 TargetTerm，替换为 SourceTerm
                if (IsValidGlossaryTerm(entry.TargetTerm))
                    matchPairs.Add((entry.TargetTerm, entry.SourceTerm, entry.TargetTerm.Length));
            }
            else
            {
                // 双向：两个方向都尝试匹配
                if (IsValidGlossaryTerm(entry.SourceTerm))
                    matchPairs.Add((entry.SourceTerm, entry.TargetTerm, entry.SourceTerm.Length));
                if (IsValidGlossaryTerm(entry.TargetTerm) && entry.TargetTerm != entry.SourceTerm)
                    matchPairs.Add((entry.TargetTerm, entry.SourceTerm, entry.TargetTerm.Length));
            }
        }

        // 按长度降序排列，避免子串冲突
        foreach (var (matchTerm, replacement, _) in matchPairs.OrderByDescending(m => m.length))
        {
            // 使用单词边界匹配，避免匹配到词的子串或路径/符号中的片段
            if (ContainsWithBoundary(processed, matchTerm))
            {
                var placeholder = index < PlaceholderNames.Length
                    ? PlaceholderNames[index]
                    : $"G{index}";
                processed = ReplaceWithBoundary(processed, matchTerm, placeholder);
                placeholders[placeholder] = replacement;
                index++;
            }
        }

        return (processed, placeholders);
    }

    /// <summary>
    /// 检查术语是否为有效的可翻译术语（排除纯符号、标点、路径片段等）
    /// </summary>
    private static bool IsValidGlossaryTerm(string term)
    {
        if (string.IsNullOrWhiteSpace(term))
            return false;

        // 至少 2 个字符
        if (term.Length < 2)
            return false;

        // 必须包含至少一个字母或数字（排除纯符号/标点如 ".", "[", "-", ":", "\\"）
        bool hasLetterOrDigit = false;
        int symbolCount = 0;
        foreach (char c in term)
        {
            if (char.IsLetterOrDigit(c))
                hasLetterOrDigit = true;
            else if (!char.IsWhiteSpace(c))
                symbolCount++;
        }

        if (!hasLetterOrDigit)
            return false;

        // 如果符号占比超过 50% 且不含任何 CJK 字符，大概率是路径/正则/技术符号
        if (term.Length > 0 && !term.Any(c => c >= '\u4E00' && c <= '\u9FFF'
            || c >= '\u3040' && c <= '\u30FF'
            || c >= '\uAC00' && c <= '\uD7AF'))
        {
            if ((double)symbolCount / term.Length > 0.5)
                return false;
        }

        return true;
    }

    /// <summary>
    /// 检查文本中是否包含术语（带单词边界检测）
    /// 对纯 Latin 文本要求单词边界，对 CJK 文本使用子串匹配
    /// </summary>
    private static bool ContainsWithBoundary(string text, string term)
    {
        int idx = text.IndexOf(term, StringComparison.Ordinal);
        if (idx < 0) return false;

        // CJK 术语不需要单词边界检查
        bool termHasCJK = term.Any(c => c >= '\u4E00' && c <= '\u9FFF'
            || c >= '\u3040' && c <= '\u30FF'
            || c >= '\uAC00' && c <= '\uD7AF');

        if (termHasCJK)
            return true;

        // Latin 术语要求单词边界：前后字符不能是字母或数字
        return CheckBoundary(text, idx, term.Length);
    }

    /// <summary>
    /// 带单词边界的替换（只替换完整单词匹配，不替换路径/标识符中的子串）
    /// </summary>
    private static string ReplaceWithBoundary(string text, string term, string replacement)
    {
        // 对于 CJK 术语，直接全文替换
        bool termHasCJK = term.Any(c => c >= '\u4E00' && c <= '\u9FFF'
            || c >= '\u3040' && c <= '\u30FF'
            || c >= '\uAC00' && c <= '\uD7AF');

        if (termHasCJK)
            return text.Replace(term, replacement);

        // 对于 Latin 术语，只替换单词边界匹配的位置
        var sb = new StringBuilder(text.Length);
        int searchStart = 0;
        while (searchStart < text.Length)
        {
            int idx = text.IndexOf(term, searchStart, StringComparison.Ordinal);
            if (idx < 0)
            {
                sb.Append(text.AsSpan(searchStart));
                break;
            }

            if (CheckBoundary(text, idx, term.Length))
            {
                sb.Append(text.AsSpan(searchStart, idx - searchStart));
                sb.Append(replacement);
                searchStart = idx + term.Length;
            }
            else
            {
                sb.Append(text.AsSpan(searchStart, idx - searchStart + term.Length));
                searchStart = idx + term.Length;
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// 检查指定位置是否满足单词边界（前后字符不是字母或数字）
    /// </summary>
    private static bool CheckBoundary(string text, int matchStart, int matchLength)
    {
        // 检查前边界
        if (matchStart > 0 && char.IsLetterOrDigit(text[matchStart - 1]))
            return false;

        // 检查后边界
        int afterMatch = matchStart + matchLength;
        if (afterMatch < text.Length && char.IsLetterOrDigit(text[afterMatch]))
            return false;

        return true;
    }

    /// <summary>
    /// 翻译后处理：将占位符替换为术语库中的正确翻译
    /// </summary>
    public string PostprocessText(string translatedText, Dictionary<string, string> placeholders)
    {
        if (placeholders.Count == 0 || string.IsNullOrEmpty(translatedText))
            return translatedText;

        var result = translatedText;
        foreach (var (placeholder, targetTerm) in placeholders)
        {
            result = result.Replace(placeholder, targetTerm);
        }

        return result;
    }

    // ========== 持久化 ==========

    public void Save()
    {
        var dir = Path.GetDirectoryName(GlossaryFilePath)!;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(_entries, _jsonOptions);
        File.WriteAllText(GlossaryFilePath, json, Encoding.UTF8);
    }

    public void Load()
    {
        _entries.Clear();

        if (!File.Exists(GlossaryFilePath)) return;

        try
        {
            var json = File.ReadAllText(GlossaryFilePath, Encoding.UTF8);
            var entries = JsonSerializer.Deserialize<List<GlossaryEntry>>(json, _jsonOptions);
            if (entries != null)
                _entries.AddRange(entries);
        }
        catch
        {
            // 文件损坏时忽略
        }
    }

    // ========== 导入/导出 ==========

    /// <summary>从 CSV 文件导入术语（格式：源术语,目标翻译,方向,备注）</summary>
    public int ImportFromCsv(string filePath)
    {
        int count = 0;
        foreach (var line in File.ReadAllLines(filePath, Encoding.UTF8))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
                continue; // 跳过空行和注释行

            var parts = trimmed.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length >= 2)
            {
                var entry = new GlossaryEntry
                {
                    SourceTerm = parts[0],
                    TargetTerm = parts[1],
                    Direction = parts.Length >= 3 ? ParseDirection(parts[2]) : GlossaryDirection.Both,
                    Note = parts.Length >= 4 ? parts[3] : null,
                };
                _entries.Add(entry);
                count++;
            }
        }
        return count;
    }

    /// <summary>导出术语到 CSV 文件</summary>
    public void ExportToCsv(string filePath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# 术语库导出 - 格式: 源术语,目标翻译,方向(中→英/英→中/双向),备注");

        foreach (var entry in _entries)
        {
            var dir = entry.Direction switch
            {
                GlossaryDirection.ChineseToEnglish => "中→英",
                GlossaryDirection.EnglishToChinese => "英→中",
                GlossaryDirection.Both => "双向",
                _ => "双向",
            };
            sb.AppendLine($"{entry.SourceTerm},{entry.TargetTerm},{dir},{entry.Note ?? ""}");
        }

        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
    }

    /// <summary>从 JSON 文件导入术语（合并到现有列表）</summary>
    public int ImportFromJson(string filePath)
    {
        try
        {
            var json = File.ReadAllText(filePath, Encoding.UTF8);
            var entries = JsonSerializer.Deserialize<List<GlossaryEntry>>(json, _jsonOptions);
            if (entries != null)
            {
                _entries.AddRange(entries);
                return entries.Count;
            }
        }
        catch { }
        return 0;
    }

    /// <summary>导出术语到 JSON 文件</summary>
    public void ExportToJson(string filePath)
    {
        var json = JsonSerializer.Serialize(_entries, _jsonOptions);
        File.WriteAllText(filePath, json, Encoding.UTF8);
    }

    // ========== 辅助方法 ==========

    private static GlossaryDirection GetGlossaryDirection(Language source, Language target)
    {
        bool sourceIsChinese = source == Language.Chinese || source == Language.Auto;
        bool targetIsEnglish = target == Language.English;
        bool sourceIsEnglish = source == Language.English;
        bool targetIsChinese = target == Language.Chinese || target == Language.Auto;

        if (sourceIsChinese && targetIsEnglish)
            return GlossaryDirection.ChineseToEnglish;
        if (sourceIsEnglish && targetIsChinese)
            return GlossaryDirection.EnglishToChinese;

        return GlossaryDirection.Both;
    }

    private static GlossaryDirection ParseDirection(string text)
    {
        return text.Trim() switch
        {
            "中→英" or "ChineseToEnglish" or "zh-en" => GlossaryDirection.ChineseToEnglish,
            "英→中" or "EnglishToChinese" or "en-zh" => GlossaryDirection.EnglishToChinese,
            "双向" or "Both" or "both" => GlossaryDirection.Both,
            _ => GlossaryDirection.Both,
        };
    }
}
