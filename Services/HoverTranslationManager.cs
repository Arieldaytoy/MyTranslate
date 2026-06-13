namespace MyTranslate.Services;

using MyTranslate.Capture;
using MyTranslate.Core;
using MyTranslate.Overlay;

/// <summary>
/// 悬停翻译管理器 — 串联鼠标悬停 → 读取文字 → 翻译 → 浮窗显示
/// </summary>
public class HoverTranslationManager : IDisposable
{
    private readonly GlobalMouseHook _mouseHook;
    private readonly UIAutomationReader _reader;
    private readonly TranslationEngine _engine;
    private readonly AppConfig _config;

    private HoverOverlay? _overlay;
    private bool _disposed;
    private System.Windows.Forms.Timer? _autoHideTimer;

    // 防抖：上次翻译完成的时间
    private DateTime _lastTranslationTime = DateTime.MinValue;

    // 正在翻译的标记（防止并发）
    private volatile bool _isTranslating;

    // 上次悬停翻译的文本（避免重复翻译同一段文字）
    private string? _lastHoveredText;

    // 上次悬停翻译的原始完整文本（用于重翻）
    private string? _lastHoveredOriginalText;

    // 上次悬停的鼠标位置
    private Point _lastHoverPoint;

    /// <summary>悬停翻译是否处于活动状态</summary>
    public bool IsActive { get; private set; }

    /// <summary>悬停翻译完成时触发</summary>
    public event EventHandler<TranslationResult>? TranslationCompleted;

    public HoverTranslationManager(
        GlobalMouseHook mouseHook,
        UIAutomationReader reader,
        TranslationEngine engine,
        AppConfig config)
    {
        _mouseHook = mouseHook;
        _reader = reader;
        _engine = engine;
        _config = config;
    }

    /// <summary>启动悬停翻译监听</summary>
    public void Start()
    {
        if (IsActive) return;
        IsActive = true;

        _mouseHook.MouseHovered += OnMouseHovered;

        EnsureOverlay();
    }

    /// <summary>停止悬停翻译监听</summary>
    public void Stop()
    {
        if (!IsActive) return;
        IsActive = false;

        _mouseHook.MouseHovered -= OnMouseHovered;

        HideOverlay();
        _lastHoveredText = null;
    }

    // ========== 事件处理 ==========

    private void OnMouseHovered(object? sender, Point screenPoint)
    {
        if (!IsActive || _isTranslating) return;

        _lastHoverPoint = screenPoint;

        // 异步执行悬停翻译，不阻塞钩子回调
        _ = Task.Run(async () => await TryTranslateAtPointAsync(screenPoint));
    }

    // ========== 核心翻译流程 ==========

    private async Task TryTranslateAtPointAsync(Point screenPoint)
    {
        if (_isTranslating) return;
        _isTranslating = true;

        try
        {
            // 防抖
            var debounce = TimeSpan.FromMilliseconds(_config.HoverDebounceMs);
            if (DateTime.Now - _lastTranslationTime < debounce)
                return;

            // 1. 通过 UI Automation 读取鼠标位置的文字
            var text = _reader.ReadTextAtPoint(screenPoint);

            // 2. 文本无效则不翻译
            if (string.IsNullOrWhiteSpace(text))
                return;

            text = text.Trim();

            // 3. 最小长度过滤
            if (text.Length < _config.HoverMinTextLength)
                return;

            // 4. 过滤非翻译内容
            if (IsNonTranslatableText(text))
                return;

            // 5. 智能分段：按 [] 和 - 拆分，只翻译有意义的部分，其余原样保留
            bool needsSplit = text.Contains('[') || text.Contains(']')
                || text.Contains(" - ") || text.Contains(" — ") || text.Contains(" – ");

            if (needsSplit)
            {
                // 分段翻译：逐段翻译可翻译部分，不翻译部分原样保留
                var result = await TranslateBySegmentsAsync(text);
                if (result != null)
                {
                    _lastHoveredOriginalText = text;
                    // 在主线程显示浮窗（可配置）
                    if (_config.HoverShowOverlay)
                    {
                        InvokeOnUIThread(() => ShowOverlay(result, screenPoint));
                    }
                    TranslationCompleted?.Invoke(this, result);
                }
                return;
            }

            // 无分段需要：剥离扩展名后整句翻译
            var extension = "";
            var textToTranslate = StripFileExtension(text, out extension);

            if (string.IsNullOrWhiteSpace(textToTranslate))
                return;

            // 去重：与上次悬停文本相同则跳过（避免鼠标微抖重复翻译）
            if (textToTranslate.Equals(_lastHoveredText, StringComparison.OrdinalIgnoreCase))
                return;

            // 调用翻译引擎
            var sourceLang = _config.DefaultSourceLanguage;
            var targetLang = _config.DefaultTargetLanguage;

            var translationResult = await _engine.TranslateAsync(textToTranslate, sourceLang, targetLang);
            _lastTranslationTime = DateTime.Now;

            // 重建翻译结果
            if (translationResult.Success)
            {
                translationResult.TranslatedText =
                    !string.IsNullOrEmpty(extension) ? translationResult.TranslatedText + extension : translationResult.TranslatedText;
                translationResult.OriginalText = text;
                _lastHoveredText = textToTranslate;
                _lastHoveredOriginalText = text;
            }

            // 在主线程显示浮窗（可配置）
            if (_config.HoverShowOverlay)
            {
                InvokeOnUIThread(() => ShowOverlay(translationResult, screenPoint));
            }

            // 触发翻译完成事件（供主窗口历史记录使用）
            TranslationCompleted?.Invoke(this, translationResult);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HoverTranslation] 异常: {ex.Message}");
        }
        finally
        {
            _isTranslating = false;
        }
    }

    /// <summary>
    /// 分段翻译：将文本按 [] 和 - 拆分为段落，逐段翻译可翻译部分，不翻译部分原样保留。
    /// 不向翻译 API 发送任何占位符，避免与 API 内部占位符冲突。
    /// </summary>
    /// <remarks>如 "Code.txt [D:\Desktop] - Notepad4" → 翻译 "Code" → "代码.txt [D:\Desktop] - Notepad4"</remarks>
    private async Task<TranslationResult?> TranslateBySegmentsAsync(string text)
    {
        // 1. 拆分为段落列表
        var segments = ParseSegments(text);
        if (segments.Count == 0) return null;

        // 2. 从首个可翻译段落剥离扩展名（翻译后直接拼回该段落，不放到末尾）
        for (int i = 0; i < segments.Count; i++)
        {
            if (segments[i].skip) continue;
            var body = StripFileExtension(segments[i].text, out var ext);
            if (!string.IsNullOrEmpty(ext))
            {
                // 扩展名紧跟在首个可翻译段落后面，而非整个结果的末尾
                segments[i] = (body + ext, false);
            }
            break;
        }

        // 3. 收集可翻译段落
        var translatableSegments = segments.Where(s => !s.skip).ToList();
        if (translatableSegments.Count == 0) return null;

        // 4. 去重
        var dedupKey = string.Join("|", translatableSegments.Select(s => s.text));
        if (dedupKey.Equals(_lastHoveredText, StringComparison.OrdinalIgnoreCase))
            return null;

        // 5. 逐段翻译（每段独立调用引擎，充分利用缓存）
        var sourceLang = _config.DefaultSourceLanguage;
        var targetLang = _config.DefaultTargetLanguage;
        var translatedSegments = new List<string>();
        bool anyFromApi = false;

        foreach (var seg in translatableSegments)
        {
            var segResult = await _engine.TranslateAsync(seg.text, sourceLang, targetLang);
            translatedSegments.Add(segResult.Success ? segResult.TranslatedText : seg.text);
            if (!segResult.IsCached) anyFromApi = true;
        }

        _lastTranslationTime = DateTime.Now;
        _lastHoveredText = dedupKey;

        // 6. 重建：将翻译结果与不翻译段落按原始位置交错拼接
        var sb = new System.Text.StringBuilder();
        int transIdx = 0;
        for (int i = 0; i < segments.Count; i++)
        {
            if (i > 0) sb.Append(' ');

            if (segments[i].skip)
            {
                sb.Append(segments[i].text);
            }
            else
            {
                sb.Append(transIdx < translatedSegments.Count
                    ? translatedSegments[transIdx] : segments[i].text);
                transIdx++;
            }
        }

        // 7. 构建结果
        return TranslationResult.Ok(
            text, sb.ToString(),
            sourceLang, targetLang,
            _engine.CurrentTranslator?.Name ?? "?",
            TimeSpan.Zero,
            isCached: !anyFromApi,
            translationSource: anyFromApi ? TranslationSource.API : TranslationSource.Cache);
    }

    /// <summary>
    /// 将文本按方括号 [] 和破折号 - 拆分为段落列表
    /// </summary>
    private static List<(string text, bool skip)> ParseSegments(string text)
    {
        var segments = new List<(string text, bool skip)>();

        // 按 [] 拆分
        var bracketParts = new List<(string text, bool isBracket)>();
        int i = 0;
        while (i < text.Length)
        {
            int open = text.IndexOf('[', i);
            if (open < 0)
            {
                var rest = text[i..];
                if (rest.Length > 0) bracketParts.Add((rest, false));
                break;
            }
            if (open > i)
                bracketParts.Add((text[i..open], false));

            int close = text.IndexOf(']', open);
            if (close < 0)
            {
                bracketParts.Add((text[open..], true));
                break;
            }
            bracketParts.Add((text.Substring(open, close - open + 1), true));
            i = close + 1;
        }

        // 对每个非方括号部分，按破折号拆分
        foreach (var (partText, isBracket) in bracketParts)
        {
            if (isBracket)
            {
                segments.Add((partText, true));
                continue;
            }

            var dashParts = SplitWithSeparators(partText, new[] { " - ", " — ", " – " });
            foreach (var (segText, sep) in dashParts)
            {
                var trimmed = segText.Trim();
                if (trimmed.Length == 0) continue;
                bool skip = IsNonTranslatableText(trimmed);
                segments.Add((trimmed, skip));
            }
        }

        return segments;
    }

    /// <summary>按分隔符拆分字符串，保留分隔符在前一段末尾</summary>
    private static List<(string text, string? separator)> SplitWithSeparators(string text, string[] separators)
    {
        var result = new List<(string text, string? separator)>();
        int pos = 0;

        while (pos < text.Length)
        {
            int earliest = text.Length;
            string? foundSep = null;

            foreach (var sep in separators)
            {
                int idx = text.IndexOf(sep, pos, StringComparison.Ordinal);
                if (idx >= 0 && idx < earliest)
                {
                    earliest = idx;
                    foundSep = sep;
                }
            }

            if (foundSep == null)
            {
                result.Add((text[pos..], null));
                break;
            }

            result.Add((text[pos..earliest], foundSep));
            pos = earliest + foundSep.Length;
        }

        return result;
    }

    /// <summary>检查文本是否为非翻译内容（URL、邮箱、文件路径、纯数字等）</summary>
    private static bool IsNonTranslatableText(string text)
    {
        var trimmed = text.Trim();

        // URL / URI（http://、app://、file:// 等任何 scheme:// 格式）
        if (trimmed.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            return true;
        var schemeEnd = trimmed.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd > 0)
        {
            // scheme 部分全是字母，视为 URI
            bool allLetters = true;
            for (int i = 0; i < schemeEnd; i++)
            {
                if (!char.IsLetter(trimmed[i])) { allLetters = false; break; }
            }
            if (allLetters) return true;
        }

        // 电子表格单元格标识符（如 A5、B12、AA3、$C$7），不翻译
        if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^\$?[A-Za-z]+\$?\d+$"))
            return true;

        // 邮箱地址
        if (trimmed.Contains('@') && trimmed.Contains('.')
            && !trimmed.Contains(' ') && trimmed.Length < 100)
            return true;

        // 文件路径（盘符开头或 UNC 路径或 / 开头的绝对路径）
        if (trimmed.Length >= 2)
        {
            if ((char.IsLetter(trimmed[0]) && trimmed[1] == ':')
                || trimmed.StartsWith("\\\\")
                || trimmed.StartsWith("/"))
                return true;
        }

        // 剥离扩展名后检查：QQ123456.png → QQ123456 → 字母+纯数字，不翻译
        string bodyExt = "";
        var body = StripFileExtension(trimmed, out bodyExt);
        if (!string.IsNullOrEmpty(body) && body != trimmed)
        {
            int letters = 0, digits = 0, others = 0;
            foreach (char c in body)
            {
                if (char.IsLetter(c)) letters++;
                else if (char.IsDigit(c)) digits++;
                else others++;
            }

            // 字母+纯数字组合（如 QQ123456、ABC123），不翻译
            if (letters > 0 && digits > 0 && others == 0)
                return true;

            // 字母+混合数字（如 QQ20260614-002913），不翻译
            if (letters > 0 && digits > 0 && others <= 2)
                return true;

            // 主体纯数字，不翻译
            if (digits > 0 && letters == 0 && others <= 3)
                return true;
        }

        // 纯数字（无扩展名的情况）
        {
            int letters = 0, digits = 0, others = 0;
            foreach (char c in trimmed)
            {
                if (char.IsLetter(c)) letters++;
                else if (char.IsDigit(c)) digits++;
                else others++;
            }

            if (digits > 0 && letters == 0 && others <= 3)
                return true;

            if (trimmed.Length > 0 && (double)others / trimmed.Length > 0.5 && letters < 3)
                return true;
        }

        return false;
    }

    /// <summary>剥离文件扩展名，返回 (主体, 扩展名)</summary>
    private static string StripFileExtension(string text, out string extension)
    {
        extension = "";
        var lastDot = text.LastIndexOf('.');
        if (lastDot <= 0) return text;

        var ext = text[lastDot..];
        var body = text[..lastDot];

        var commonExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
            ".pdf", ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".svg", ".ico",
            ".mp3", ".mp4", ".avi", ".mkv", ".wav", ".flac",
            ".zip", ".rar", ".7z", ".tar", ".gz",
            ".exe", ".msi", ".dll", ".bat", ".cmd", ".ps1",
            ".html", ".htm", ".css", ".js", ".json", ".xml", ".yaml", ".yml",
            ".cs", ".java", ".py", ".cpp", ".h", ".go", ".rs",
            ".sql", ".db", ".mdb",
            ".log", ".ini", ".cfg", ".conf",
            ".rtf", ".csv", ".tsv",
        };

        if (commonExtensions.Contains(ext))
        {
            extension = ext;
            return body;
        }

        return text;
    }

    // ========== 浮窗管理 ==========

    private void EnsureOverlay()
    {
        if (_overlay != null) return;

        InvokeOnUIThread(() =>
        {
            _overlay = new HoverOverlay();
            _overlay.Opacity = _config.OverlayOpacity;
            _overlay.CopyClicked += OnOverlayCopyClicked;
            _overlay.RetranslateClicked += OnOverlayRetranslateClicked;

            // 自动消失计时器：8秒无操作后隐藏浮窗
            _autoHideTimer = new System.Windows.Forms.Timer { Interval = 8000 };
            _autoHideTimer.Tick += (s, e) =>
            {
                _autoHideTimer.Stop();
                HideOverlay();
            };
        });
    }

    private void OnOverlayCopyClicked(object? sender, EventArgs e)
    {
        if (_overlay != null)
        {
            var text = _overlay.GetTranslatedText();
            if (!string.IsNullOrEmpty(text))
            {
                try { Clipboard.SetText(text); } catch { }
            }
        }
    }

    private async void OnOverlayRetranslateClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_lastHoveredOriginalText)) return;

        // 重置防抖，允许立即翻译
        _lastTranslationTime = DateTime.MinValue;
        // 清除去重缓存，允许重新翻译同一段文本
        _lastHoveredText = null;

        var sourceLang = _config.DefaultSourceLanguage;
        var targetLang = _config.DefaultTargetLanguage;

        // 对原始文本重新走完整翻译流程（ForceTranslateAsync 跳过缓存）
        var result = await _engine.ForceTranslateAsync(_lastHoveredOriginalText, sourceLang, targetLang);
        result.OriginalText = _lastHoveredOriginalText;

        // 更新浮窗
        if (_config.HoverShowOverlay)
        {
            InvokeOnUIThread(() =>
            {
                if (_overlay != null)
                {
                    _overlay.ShowTranslation(result);
                    ResetAutoHideTimer();
                }
            });
        }

        TranslationCompleted?.Invoke(this, result);
    }

    private void ShowOverlay(TranslationResult result, Point mousePosition)
    {
        EnsureOverlay();
        if (_overlay == null) return;

        _overlay.Opacity = _config.OverlayOpacity;
        _overlay.ShowTranslation(result);
        _overlay.PositionNear(mousePosition);

        if (!_overlay.Visible) _overlay.Show();

        ResetAutoHideTimer();
    }

    private void ResetAutoHideTimer()
    {
        if (_autoHideTimer != null)
        {
            _autoHideTimer.Stop();
            _autoHideTimer.Start();
        }
    }

    private void HideOverlay()
    {
        _autoHideTimer?.Stop();
        if (_overlay?.Visible == true)
        {
            InvokeOnUIThread(() => _overlay.Hide());
        }
    }

    // ========== 辅助 ==========

    private static void InvokeOnUIThread(Action action)
    {
        if (Application.OpenForms.Count > 0)
        {
            var mainForm = Application.OpenForms[0]!;
            if (mainForm.InvokeRequired)
                mainForm.Invoke(action);
            else
                action();
        }
    }

    // ========== 释放 ==========

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Stop();

        _autoHideTimer?.Stop();
        _autoHideTimer?.Dispose();
        _autoHideTimer = null;

        if (_overlay != null)
        {
            InvokeOnUIThread(() =>
            {
                _overlay.Dispose();
                _overlay = null;
            });
        }

        GC.SuppressFinalize(this);
    }
}
