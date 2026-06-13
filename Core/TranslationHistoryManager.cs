namespace MyTranslate.Core;

using System.Text;
using System.Text.Json;

/// <summary>
/// 翻译历史缓存管理器 — 支持多向查找（正向/反向/链式），持久化到本地
/// </summary>
public class TranslationHistoryManager
{
    private readonly Dictionary<string, HistoryEntry> _cache = [];

    // 反向索引：translatedText → 所有包含该译文的缓存 key 列表
    private readonly Dictionary<string, List<string>> _reverseIndex = [];

    /// <summary>历史缓存文件路径</summary>
    public static string HistoryFilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MyTranslate", "translation_history.json");

    // 加载后是否需要自动保存（清理了错误条目时）
    private bool _needsSaveAfterLoad;

    /// <summary>最大缓存条数</summary>
    public int MaxEntries { get; set; } = 10000;

    /// <summary>当前缓存条数</summary>
    public int Count => _cache.Count;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // ========== 多向查找 ==========

    /// <summary>
    /// 多向查找翻译结果（正向 → 反向 → 单跳链式）
    /// 先检测输入文本的实际语言，再用具体语言进行匹配
    /// </summary>
    public HistoryEntry? Lookup(string originalText, Language source, Language target)
    {
        if (string.IsNullOrEmpty(originalText))
            return null;

        // Step 0: 如果源语言是 Auto，先检测实际语言
        var detectedSource = source;
        if (source == Language.Auto)
        {
            var detected = LanguageDetector.Detect(originalText);
            if (detected != Language.Auto)
                detectedSource = detected;
        }

        // 1. 正向查找
        var direct = FindDirect(originalText, detectedSource, target);
        if (direct != null)
            return direct;

        // 2. 反向查找（输入文本是某条记录的译文）
        var reverse = FindReverse(originalText, detectedSource, target);
        if (reverse != null)
            return reverse;

        // 3. 单跳链式查找：A→B→C
        var chain = FindChain(originalText, detectedSource, target);
        if (chain != null)
            return chain;

        return null;
    }

    /// <summary>正向查找（具体语言直接比较）</summary>
    private HistoryEntry? FindDirect(string text, Language source, Language target)
    {
        foreach (var entry in _cache.Values)
        {
            if (entry.OriginalText.Equals(text, StringComparison.OrdinalIgnoreCase)
                && entry.SourceLanguage == source
                && entry.TargetLanguage == target)
            {
                return entry;
            }
        }
        return null;
    }

    /// <summary>反向查找：输入文本是某条记录的译文，返回该记录的原文（支持多义词）</summary>
    private HistoryEntry? FindReverse(string text, Language source, Language target)
    {
        // 检查 text 是否匹配某条记录的译文（含多义词分号分隔的情况）
        var lookupText = text.ToLowerInvariant();

        // 1. 精确反向索引匹配
        if (_reverseIndex.TryGetValue(lookupText, out var reverseKeys))
        {
            foreach (var key in reverseKeys)
            {
                if (_cache.TryGetValue(key, out var entry)
                    && entry.TargetLanguage == source
                    && entry.SourceLanguage == target)
                {
                    return new HistoryEntry
                    {
                        OriginalText = text,
                        TranslatedText = entry.OriginalText,
                        SourceLanguage = source,
                        TargetLanguage = target,
                        TranslatorName = entry.TranslatorName,
                        Timestamp = entry.Timestamp,
                    };
                }
            }
        }

        // 2. 遍历查找：text 可能匹配多义词中的某一个（如缓存 "我→I;my;me"，查 "my"）
        foreach (var entry in _cache.Values)
        {
            if (entry.TargetLanguage != source || entry.SourceLanguage != target)
                continue;

            var translations = entry.TranslatedText.Split(';', StringSplitOptions.TrimEntries);
            foreach (var t in translations)
            {
                if (t.Equals(text, StringComparison.OrdinalIgnoreCase))
                {
                    return new HistoryEntry
                    {
                        OriginalText = text,
                        TranslatedText = entry.OriginalText,
                        SourceLanguage = source,
                        TargetLanguage = target,
                        TranslatorName = entry.TranslatorName,
                        Timestamp = entry.Timestamp,
                    };
                }
            }
        }

        return null;
    }

    /// <summary>单跳链式查找（A→B→C，具体语言直接比较）</summary>
    private HistoryEntry? FindChain(string text, Language source, Language target)
    {
        // 3a. 从正向匹配出发的中继
        foreach (var entry in _cache.Values)
        {
            if (entry.OriginalText.Equals(text, StringComparison.OrdinalIgnoreCase)
                && entry.SourceLanguage == source)
            {
                var midText = entry.TranslatedText;
                var midLang = entry.TargetLanguage;

                // 中间语言与目标语言相同时跳过（避免无意义的 A→B→B）
                if (midLang == target) continue;

                var chainResult = FindDirect(midText, midLang, target);
                if (chainResult != null)
                {
                    return new HistoryEntry
                    {
                        OriginalText = text,
                        TranslatedText = chainResult.TranslatedText,
                        SourceLanguage = source,
                        TargetLanguage = target,
                        TranslatorName = $"链式({entry.TranslatorName}→{chainResult.TranslatorName})",
                        Timestamp = chainResult.Timestamp,
                    };
                }
            }
        }

        // 3b. 从反向匹配出发的中继
        var lookupText = text.ToLowerInvariant();
        if (_reverseIndex.TryGetValue(lookupText, out var revKeys))
        {
            foreach (var key in revKeys)
            {
                if (_cache.TryGetValue(key, out var revEntry)
                    && revEntry.TargetLanguage == source)
                {
                    var midText = revEntry.OriginalText;
                    var midLang = revEntry.SourceLanguage;

                    if (midLang == target) continue;

                    var chainResult = FindDirect(midText, midLang, target);
                    if (chainResult != null)
                    {
                        return new HistoryEntry
                        {
                            OriginalText = text,
                            TranslatedText = chainResult.TranslatedText,
                            SourceLanguage = source,
                            TargetLanguage = target,
                            TranslatorName = $"链式({revEntry.TranslatorName}→{chainResult.TranslatorName})",
                            Timestamp = chainResult.Timestamp,
                        };
                    }
                }
            }
        }

        return null;
    }

    // ========== 写入 ==========

    /// <summary>
    /// 保存一条翻译结果到缓存（同时更新反向索引）
    /// 如果同一 key 已存在且译文不同，追加为多义词（分号分隔）
    /// </summary>
    public void Save(string originalText, string translatedText,
        Language selectedSource, Language detectedSource, Language target, string translatorName)
    {
        var key = BuildKey(originalText, detectedSource, target);

        if (_cache.TryGetValue(key, out var existing))
        {
            // 已存在 → 追加译文（多义词支持）
            var translations = existing.TranslatedText
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            if (!translations.Contains(translatedText, StringComparer.OrdinalIgnoreCase))
            {
                translations.Add(translatedText);
                existing.TranslatedText = string.Join(";", translations);
                existing.Timestamp = DateTime.Now;
                RebuildReverseIndex();
            }
        }
        else
        {
            _cache[key] = new HistoryEntry
            {
                OriginalText = originalText,
                TranslatedText = translatedText,
                SelectedSourceLanguage = selectedSource,
                SourceLanguage = detectedSource,
                TargetLanguage = target,
                TranslatorName = translatorName,
                Timestamp = DateTime.Now,
            };

            // 更新反向索引
            var revKey = translatedText.ToLowerInvariant();
            if (!_reverseIndex.ContainsKey(revKey))
                _reverseIndex[revKey] = [];
            if (!_reverseIndex[revKey].Contains(key))
                _reverseIndex[revKey].Add(key);
        }

        // 超出上限时清理旧条目
        if (_cache.Count > MaxEntries)
            CleanupOldEntries();
    }

    // ========== 清除 ==========

    public void Clear()
    {
        _cache.Clear();
        _reverseIndex.Clear();
        SaveToFile();
    }

    /// <summary>删除一条缓存条目</summary>
    public void RemoveEntry(string originalText, Language source, Language target)
    {
        var key = BuildKey(originalText, source, target);
        if (_cache.Remove(key))
            RebuildReverseIndex();
    }

    /// <summary>更新一条缓存条目的译文（支持分号分隔的多义词）</summary>
    public void UpdateTranslation(string originalText, Language source, Language target, string newTranslation)
    {
        var key = BuildKey(originalText, source, target);
        if (_cache.TryGetValue(key, out var entry))
        {
            entry.TranslatedText = newTranslation;
            entry.Timestamp = DateTime.Now;
            RebuildReverseIndex();
        }
    }

    /// <summary>追加译文到已有条目（多义词用分号分隔）</summary>
    public void AppendTranslation(string originalText, Language source, Language target, string additionalTranslation)
    {
        var key = BuildKey(originalText, source, target);
        if (_cache.TryGetValue(key, out var entry))
        {
            var existing = entry.TranslatedText;
            var translations = existing.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            if (!translations.Contains(additionalTranslation, StringComparer.OrdinalIgnoreCase))
            {
                translations.Add(additionalTranslation);
                entry.TranslatedText = string.Join(";", translations);
                entry.Timestamp = DateTime.Now;
                RebuildReverseIndex();
            }
        }
    }

    // ========== 导出导入 ==========

    /// <summary>导出为 CSV（兼容 Excel/WPS，UTF-8 BOM）</summary>
    public void ExportToCsv(string filePath)
    {
        var sb = new StringBuilder();
        // UTF-8 BOM 由 File.WriteAllText 的 encoding 参数处理
        sb.AppendLine("原文,译文,选择语言,识别语言,目标语言,翻译器,时间");

        foreach (var entry in _cache.Values.OrderByDescending(e => e.Timestamp))
        {
            var langNames = LanguageInfo.SupportedLanguages;
            var selectedName = langNames.FirstOrDefault(l => l.Language == entry.SelectedSourceLanguage)?.DisplayName ?? entry.SelectedSourceLanguage.ToString();
            var detectedName = langNames.FirstOrDefault(l => l.Language == entry.SourceLanguage)?.DisplayName ?? entry.SourceLanguage.ToString();
            var targetName = langNames.FirstOrDefault(l => l.Language == entry.TargetLanguage)?.DisplayName ?? entry.TargetLanguage.ToString();

            // CSV 转义：包含逗号、引号、换行的字段用双引号包裹
            sb.AppendLine($"{CsvEscape(entry.OriginalText)},{CsvEscape(entry.TranslatedText)},{selectedName},{detectedName},{targetName},{CsvEscape(entry.TranslatorName)},{entry.Timestamp:yyyy-MM-dd HH:mm:ss}");
        }

        File.WriteAllText(filePath, sb.ToString(), new UTF8Encoding(true));
    }

    /// <summary>导出为 JSON（格式化可读）</summary>
    public void ExportToJson(string filePath)
    {
        var entries = _cache.Values.OrderByDescending(e => e.Timestamp).ToList();
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        var json = JsonSerializer.Serialize(entries, options);
        File.WriteAllText(filePath, json, Encoding.UTF8);
    }

    /// <summary>从 CSV 导入（跳过表头，支持追加多义词）</summary>
    public int ImportFromCsv(string filePath)
    {
        int imported = 0;
        var lines = File.ReadAllLines(filePath, Encoding.UTF8);
        var langNames = LanguageInfo.SupportedLanguages;

        for (int i = 1; i < lines.Length; i++) // 跳过表头
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            var fields = ParseCsvLine(line);
            if (fields.Count < 5) continue;

            var originalText = fields[0].Trim();
            var translatedText = fields[1].Trim();
            var selectedLangName = fields[2].Trim();
            var detectedLangName = fields[3].Trim();
            var targetLangName = fields[4].Trim();
            var translatorName = fields.Count > 5 ? fields[5].Trim() : "手动导入";

            var selectedLang = langNames.FirstOrDefault(l => l.DisplayName == selectedLangName)?.Language ?? Language.Auto;
            var detectedLang = langNames.FirstOrDefault(l => l.DisplayName == detectedLangName)?.Language ?? Language.Chinese;
            var targetLang = langNames.FirstOrDefault(l => l.DisplayName == targetLangName)?.Language ?? Language.English;

            if (string.IsNullOrEmpty(originalText) || string.IsNullOrEmpty(translatedText))
                continue;

            var key = BuildKey(originalText, detectedLang, targetLang);
            if (_cache.ContainsKey(key))
            {
                // 已存在 → 追加译文（多义词）
                AppendTranslation(originalText, detectedLang, targetLang, translatedText);
            }
            else
            {
                Save(originalText, translatedText, selectedLang, detectedLang, targetLang, translatorName);
            }
            imported++;
        }

        if (imported > 0) SaveToFile();
        return imported;
    }

    /// <summary>从 JSON 导入（完整备份恢复）</summary>
    public int ImportFromJson(string filePath)
    {
        int imported = 0;
        var json = File.ReadAllText(filePath, Encoding.UTF8);
        var entries = JsonSerializer.Deserialize<List<HistoryEntry>>(json, _jsonOptions);
        if (entries == null) return 0;

        foreach (var entry in entries)
        {
            if (string.IsNullOrEmpty(entry.OriginalText) || string.IsNullOrEmpty(entry.TranslatedText))
                continue;

            var key = BuildKey(entry.OriginalText, entry.SourceLanguage, entry.TargetLanguage);
            if (!_cache.ContainsKey(key))
            {
                _cache[key] = entry;
                imported++;
            }
        }

        if (imported > 0)
        {
            RebuildReverseIndex();
            SaveToFile();
        }
        return imported;
    }

    private static string CsvEscape(string text)
    {
        if (text.Contains(',') || text.Contains('"') || text.Contains('\n') || text.Contains(';'))
            return $"\"{text.Replace("\"", "\"\"")}\"";
        return text;
    }

    /// <summary>简单的 CSV 行解析（支持引号包裹的字段）</summary>
    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++; // 跳过转义的引号
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else
            {
                if (c == '"')
                    inQuotes = true;
                else if (c == ',')
                {
                    fields.Add(current.ToString());
                    current.Clear();
                }
                else
                    current.Append(c);
            }
        }
        fields.Add(current.ToString());
        return fields;
    }

    // ========== 获取所有条目（供查看用）==========

    /// <summary>获取所有缓存条目（按时间降序）</summary>
    public List<HistoryEntry> GetAllEntries()
        => _cache.Values.OrderByDescending(e => e.Timestamp).ToList();

    // ========== 持久化 ==========

    public void LoadFromFile()
    {
        _cache.Clear();
        _reverseIndex.Clear();

        if (!File.Exists(HistoryFilePath)) return;

        try
        {
            var json = File.ReadAllText(HistoryFilePath, Encoding.UTF8);
            var entries = JsonSerializer.Deserialize<List<HistoryEntry>>(json, _jsonOptions);
            if (entries != null)
            {
                int cleaned = 0;
                foreach (var entry in entries)
                {
                    // 清理同语言的错误条目（源文本=译文，且源语言和目标语言相同）
                    if (entry.OriginalText == entry.TranslatedText
                        && LanguageDetector.IsSameLanguage(entry.OriginalText, entry.SourceLanguage, entry.TargetLanguage))
                    {
                        cleaned++;
                        continue; // 跳过此条目
                    }

                    // 清理原文含占位符模式的脏数据（旧版术语库预处理的残留）
                    if (ContainsPlaceholderPattern(entry.OriginalText) || ContainsPlaceholderPattern(entry.TranslatedText))
                    {
                        cleaned++;
                        continue;
                    }

                    var key = BuildKey(entry.OriginalText, entry.SourceLanguage, entry.TargetLanguage);
                    _cache[key] = entry;

                    // 重建反向索引
                    var revKey = entry.TranslatedText.ToLowerInvariant();
                    if (!_reverseIndex.ContainsKey(revKey))
                        _reverseIndex[revKey] = [];
                    _reverseIndex[revKey].Add(key);
                }

                // 如果有清理，自动保存清理后的结果
                if (cleaned > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[TranslationHistory] 清理了 {cleaned} 条同语言错误条目");
                    // 延迟保存：在加载完成后触发，避免在加载过程中写文件
                    _needsSaveAfterLoad = true;
                }

                // 迁移：将源语言/目标语言为 Auto 的条目解析为实际语言
                int migrated = 0;
                var keysToUpdate = new List<(string oldKey, HistoryEntry entry)>();

                foreach (var (key, entry) in _cache)
                {
                    if (entry.SourceLanguage == Language.Auto || entry.TargetLanguage == Language.Auto)
                    {
                        keysToUpdate.Add((key, entry));
                    }
                }

                foreach (var (oldKey, entry) in keysToUpdate)
                {
                    var newSource = entry.SourceLanguage;
                    var newTarget = entry.TargetLanguage;

                    if (newSource == Language.Auto)
                    {
                        var detected = LanguageDetector.Detect(entry.OriginalText);
                        if (detected != Language.Auto)
                            newSource = detected;
                    }

                    if (newTarget == Language.Auto)
                    {
                        var detected = LanguageDetector.Detect(entry.TranslatedText);
                        if (detected != Language.Auto)
                            newTarget = detected;
                    }

                    if (newSource != entry.SourceLanguage || newTarget != entry.TargetLanguage)
                    {
                        entry.SourceLanguage = newSource;
                        entry.TargetLanguage = newTarget;

                        // 用新 key 替换旧 key
                        var newKey = BuildKey(entry.OriginalText, newSource, newTarget);
                        if (newKey != oldKey)
                        {
                            _cache.Remove(oldKey);
                            _cache[newKey] = entry;
                        }

                        migrated++;
                    }
                }

                if (migrated > 0)
                {
                    RebuildReverseIndex();
                    _needsSaveAfterLoad = true;
                    System.Diagnostics.Debug.WriteLine($"[TranslationHistory] 迁移了 {migrated} 条 Auto 语言条目为实际语言");
                }

                // 迁移 SelectedSourceLanguage：旧条目没有此字段（默认0=Auto）
                // 如果 SourceLanguage 不是 Auto，说明用户明确选择了该语言
                bool needsSelectedLangSave = false;
                foreach (var entry in _cache.Values)
                {
                    if (entry.SelectedSourceLanguage == Language.Auto && entry.SourceLanguage != Language.Auto)
                    {
                        entry.SelectedSourceLanguage = entry.SourceLanguage;
                        needsSelectedLangSave = true;
                    }
                }
                if (needsSelectedLangSave)
                {
                    _needsSaveAfterLoad = true;
                }

                // 清理矛盾的反向条目：
                // 如果存在 A(langX)→B(langY) 的正向记录，
                // 同时存在 B(langY)→A'(langX) 且 A'≠A 的记录，则 A' 是错误的 API 翻译，应清除
                var contradictions = new List<string>();
                foreach (var (key, entry) in _cache)
                {
                    // 检查 entry.TranslatedText 是否作为某条记录的原文存在（同语言对）
                    var reverseLookupKey = BuildKey(entry.TranslatedText, entry.TargetLanguage, entry.SourceLanguage);
                    if (_cache.TryGetValue(reverseLookupKey, out var reverseEntry))
                    {
                        // 正向 A→B 和反向 B→A' 都存在
                        // 如果 A'（reverseEntry.TranslatedText）≠ A（entry.OriginalText），且 A' 是 API 翻译（非链式/缓存）
                        if (!reverseEntry.TranslatedText.Equals(entry.OriginalText, StringComparison.OrdinalIgnoreCase)
                            && !reverseEntry.TranslatorName.StartsWith("(")
                            && !reverseEntry.TranslatorName.StartsWith("链式"))
                        {
                            contradictions.Add(reverseLookupKey);
                        }
                    }
                }
                if (contradictions.Count > 0)
                {
                    foreach (var key in contradictions)
                        _cache.Remove(key);
                    RebuildReverseIndex();
                    _needsSaveAfterLoad = true;
                    System.Diagnostics.Debug.WriteLine($"[TranslationHistory] 清理了 {contradictions.Count} 条矛盾反向条目");
                }
            }
        }
        catch
        {
            // 文件损坏时忽略
        }

        // 加载完成后，如果清理了错误条目，自动保存
        if (_needsSaveAfterLoad)
        {
            _needsSaveAfterLoad = false;
            SaveToFile();
        }
    }

    public void SaveToFile()
    {
        try
        {
            var dir = Path.GetDirectoryName(HistoryFilePath)!;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var entries = _cache.Values.ToList();
            var json = JsonSerializer.Serialize(entries, _jsonOptions);
            File.WriteAllText(HistoryFilePath, json, Encoding.UTF8);
        }
        catch
        {
            // 保存失败不影响主流程
        }
    }

    // ========== 内部方法 ==========

    private static string BuildKey(string text, Language source, Language target)
        => $"{text}|{source}|{target}";

    private void CleanupOldEntries()
    {
        var sorted = _cache.OrderBy(kv => kv.Value.Timestamp).ToList();
        int removeCount = sorted.Count / 2;

        for (int i = 0; i < removeCount; i++)
        {
            _cache.Remove(sorted[i].Key);
        }

        // 重建反向索引
        RebuildReverseIndex();
    }

    private void RebuildReverseIndex()
    {
        _reverseIndex.Clear();
        foreach (var (key, entry) in _cache)
        {
            var revKey = entry.TranslatedText.ToLowerInvariant();
            if (!_reverseIndex.ContainsKey(revKey))
                _reverseIndex[revKey] = [];
            _reverseIndex[revKey].Add(key);
        }
    }

    /// <summary>检查文本是否包含旧版占位符模式（{{G0}}、__PH0__、GZERO 等）</summary>
    private static bool ContainsPlaceholderPattern(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;

        // 旧版格式: {{G0}}, {{G1}} 等
        if (text.Contains("{{G")) return true;

        // 中间格式: __PH0__, __PH1__, __G0__, __ PH0__ 等
        if (text.Contains("__PH") || text.Contains("__G")) return true;
        if (text.Contains("__ PH") || text.Contains("__ph")) return true;

        // 新版格式（作为独立词出现时说明原文被污染了）: GZERO, GONE, GTWO 等
        var newPlaceholders = new[] { "GZERO", "GONE", "GTWO", "GTHREE", "GFOUR", "GFIVE" };
        foreach (var ph in newPlaceholders)
        {
            if (text.Contains(ph)) return true;
        }

        return false;
    }
}

/// <summary>
/// 历史缓存条目
/// </summary>
public class HistoryEntry
{
    public string OriginalText { get; set; } = "";
    public string TranslatedText { get; set; } = "";
    /// <summary>用户选择的源语言（如：自动识别）</summary>
    public Language SelectedSourceLanguage { get; set; }
    /// <summary>检测到的实际源语言（如：中文）</summary>
    public Language SourceLanguage { get; set; }
    public Language TargetLanguage { get; set; }
    public string TranslatorName { get; set; } = "";
    public DateTime Timestamp { get; set; }
}
