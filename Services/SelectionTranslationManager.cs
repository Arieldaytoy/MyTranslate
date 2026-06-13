namespace MyTranslate.Services;

using MyTranslate.Capture;
using MyTranslate.Core;
using MyTranslate.Overlay;
using System.Runtime.InteropServices;

/// <summary>
/// 划词翻译管理器 — 串联鼠标释放 → 气泡图标 → 点击翻译 → 浮窗显示
/// </summary>
public class SelectionTranslationManager : IDisposable
{
    private readonly GlobalMouseHook _mouseHook;
    private readonly UIAutomationReader _reader;
    private readonly TranslationEngine _engine;
    private readonly AppConfig _config;

    private SelectionOverlay? _overlay;
    private SelectionBubble? _bubble;
    private bool _disposed;

    // 正在翻译的标记（防止并发）
    private volatile bool _isTranslating;

    // 上次选中的文本和位置（用于气泡点击后翻译）
    private string? _pendingText;
    private Func<string, string>? _pendingReconstruct;  // 重建函数：将翻译结果按原结构拼回
    private Point _pendingPoint;
    private Rectangle _pendingSelectionBounds;

    /// <summary>划词翻译是否处于活动状态</summary>
    public bool IsActive { get; private set; }

    /// <summary>划词翻译完成时触发（包括成功和失败）</summary>
    public event EventHandler<TranslationResult>? TranslationCompleted;

    public SelectionTranslationManager(
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

    /// <summary>启动划词翻译监听</summary>
    public void Start()
    {
        if (IsActive) return;
        IsActive = true;

        _mouseHook.MouseReleased += OnMouseReleased;
        _mouseHook.MouseClicked += OnMouseClicked;

        EnsureBubble();
    }

    /// <summary>停止划词翻译监听</summary>
    public void Stop()
    {
        if (!IsActive) return;
        IsActive = false;

        _mouseHook.MouseReleased -= OnMouseReleased;
        _mouseHook.MouseClicked -= OnMouseClicked;

        HideBubble();
        HideOverlay();
    }

    // ========== 事件处理 ==========

    private void OnMouseReleased(object? sender, Point screenPoint)
    {
        if (!IsActive || _isTranslating) return;

        // 忽略鼠标在自身窗口上的操作
        if (IsPointOnOwnWindow(screenPoint)) return;

        // 给 UI Automation 一点时间更新选区状态
        _ = Task.Run(async () =>
        {
            await Task.Delay(100);
            await DetectSelectionAsync(screenPoint);
        });
    }

    private void OnMouseClicked(object? sender, Point screenPoint)
    {
        // 点击时关闭翻译浮窗（点击浮窗外部时）
        if (_overlay?.Visible == true)
        {
            var overlayBounds = new Rectangle(_overlay.Location, _overlay.Size);
            if (!overlayBounds.Contains(screenPoint))
            {
                HideOverlay();
            }
        }

        // 点击时也关闭气泡（点击气泡外部时）
        if (_bubble?.Visible == true)
        {
            var bubbleBounds = new Rectangle(_bubble.Location, _bubble.Size);
            if (!bubbleBounds.Contains(screenPoint))
            {
                HideBubble();
            }
        }
    }

    // ========== 检测选中文本 ==========

    private async Task DetectSelectionAsync(Point screenPoint)
    {
        try
        {
            // 1. 通过 UI Automation 读取选中文本（不再使用剪贴板复制）
            var selectedText = _reader.GetSelectedText();

            // 2. 文本无效则不显示气泡
            if (string.IsNullOrWhiteSpace(selectedText) || selectedText.Length < _config.SelectionMinTextLength)
                return;

            // 3. 过滤非翻译内容（URL、邮箱、纯数字+扩展名等）
            if (IsNonTranslatableText(selectedText))
                return;

            // 4. 分段翻译（仅当文本包含 [] 或 " - " 时才分段）
            bool needsSplit = selectedText.Contains('[') || selectedText.Contains(']')
                || selectedText.Contains(" - ") || selectedText.Contains(" — ") || selectedText.Contains(" – ");

            string textToTranslate;
            Func<string, string> reconstruct;

            if (needsSplit)
            {
                (textToTranslate, reconstruct) = SplitForTranslation(selectedText.Trim());
                if (string.IsNullOrWhiteSpace(textToTranslate))
                    return;
            }
            else
            {
                var extension = "";
                textToTranslate = StripFileExtension(selectedText.Trim(), out extension);
                reconstruct = translated =>
                    !string.IsNullOrEmpty(extension) ? translated + extension : translated;
            }

            // 5. 保存选中文本、重建函数和位置，显示气泡图标
            _pendingText = textToTranslate;
            _pendingReconstruct = reconstruct;
            _pendingPoint = screenPoint;

            var selectionBounds = _reader.GetSelectionBoundingRect();
            _pendingSelectionBounds = selectionBounds ?? new Rectangle(screenPoint.X, screenPoint.Y, 1, 1);

            InvokeOnUIThread(() => ShowBubble(screenPoint));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SelectionTranslation] 检测选区异常: {ex.Message}");
        }
    }

    // ========== 气泡图标管理 ==========

    private void EnsureBubble()
    {
        if (_bubble != null) return;

        InvokeOnUIThread(() =>
        {
            _bubble = new SelectionBubble();
            _bubble.BubbleClicked += OnBubbleClicked;
        });
    }

    private void ShowBubble(Point screenPoint)
    {
        EnsureBubble();
        if (_bubble == null) return;

        _bubble.Opacity = _config.OverlayOpacity;
        _bubble.ShowAt(screenPoint);
    }

    private void HideBubble()
    {
        if (_bubble?.Visible == true)
        {
            InvokeOnUIThread(() => _bubble.Hide());
        }
    }

    private void OnBubbleClicked(object? sender, EventArgs e)
    {
        // 隐藏气泡，开始翻译
        HideBubble();

        if (string.IsNullOrWhiteSpace(_pendingText)) return;

        _ = Task.Run(async () => await TranslatePendingAsync());
    }

    // ========== 翻译流程 ==========

    private async Task TranslatePendingAsync()
    {
        if (_isTranslating) return;
        _isTranslating = true;

        try
        {
            var text = _pendingText!;
            var sourceLang = _config.DefaultSourceLanguage;
            var targetLang = _config.DefaultTargetLanguage;

            var result = await _engine.TranslateAsync(text, sourceLang, targetLang);

            // 重建翻译结果：将翻译后的段落按原结构拼回
            if (result.Success && _pendingReconstruct != null)
            {
                result.TranslatedText = _pendingReconstruct(result.TranslatedText);
            }

            // 在主线程显示浮窗
            if (result.Success && _config.SelectionShowOverlay)
            {
                var bounds = _pendingSelectionBounds;
                InvokeOnUIThread(() => ShowOverlay(result, bounds));
            }

            TranslationCompleted?.Invoke(this, result);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SelectionTranslation] 翻译异常: {ex.Message}");
        }
        finally
        {
            _isTranslating = false;
        }
    }

    // ========== 浮窗管理 ==========

    private void EnsureOverlay()
    {
        if (_overlay != null) return;

        InvokeOnUIThread(() =>
        {
            _overlay = new SelectionOverlay();
            _overlay.Opacity = _config.OverlayOpacity;
            _overlay.CopyClicked += OnOverlayCopyClicked;
            _overlay.RetranslateClicked += OnOverlayRetranslateClicked;
        });
    }

    private void ShowOverlay(TranslationResult result, Rectangle selectionBounds)
    {
        EnsureOverlay();
        if (_overlay == null) return;

        _overlay.Opacity = _config.OverlayOpacity;
        _overlay.ShowTranslation(result, selectionBounds);
    }

    private void HideOverlay()
    {
        if (_overlay?.Visible == true)
        {
            InvokeOnUIThread(() => _overlay.Hide());
        }
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
        if (string.IsNullOrEmpty(_pendingText)) return;

        var sourceLang = _config.DefaultSourceLanguage;
        var targetLang = _config.DefaultTargetLanguage;
        var result = await _engine.ForceTranslateAsync(_pendingText, sourceLang, targetLang);
        result.OriginalText = _pendingText;

        if (_overlay != null)
        {
            InvokeOnUIThread(() => _overlay.ShowTranslation(result, _pendingSelectionBounds));
        }

        TranslationCompleted?.Invoke(this, result);
    }

    // ========== 辅助方法 ==========

    /// <summary>
    /// 按方括号 [] 和破折号 - 拆分文本，只翻译有意义的段落，其余用占位符保留。
    /// </summary>
    private static (string translatable, Func<string, string> reconstruct) SplitForTranslation(string text)
    {
        // 1. 拆分为段落列表
        var segments = new List<(string text, bool skip)>();

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
                segments.Add((trimmed, IsNonTranslatableText(trimmed)));
            }
        }

        // 2. 剥离扩展名
        var extension = "";
        for (int idx = 0; idx < segments.Count; idx++)
        {
            if (segments[idx].skip) continue;
            var body = StripFileExtension(segments[idx].text, out var ext);
            if (!string.IsNullOrEmpty(ext)) extension = ext;
            segments[idx] = (body, false);
        }

        // 3. 占位符方案
        var placeholders = new Dictionary<string, string>();
        var sb = new System.Text.StringBuilder();
        int phIndex = 0;

        for (int idx = 0; idx < segments.Count; idx++)
        {
            if (idx > 0) sb.Append(' ');

            if (segments[idx].skip)
            {
                var ph = $"__PH{phIndex++}__";
                placeholders[ph] = segments[idx].text;
                sb.Append(ph);
            }
            else
            {
                sb.Append(segments[idx].text);
            }
        }

        var translatable = sb.ToString();

        return (translatable, translatedText =>
        {
            var result = translatedText;
            foreach (var (ph, original) in placeholders)
            {
                result = result.Replace(ph, original);
            }
            if (!string.IsNullOrEmpty(extension))
                result += extension;
            return result;
        });
    }

    /// <summary>按分隔符拆分字符串，保留分隔符</summary>
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

    /// <summary>检查鼠标是否在自身应用的窗口上</summary>
    private static bool IsPointOnOwnWindow(Point screenPoint)
    {
        try
        {
            var hWnd = SelectionNativeMethods.WindowFromPoint(new SelectionNativeMethods.POINT { x = screenPoint.X, y = screenPoint.Y });
            if (hWnd == IntPtr.Zero) return false;

            SelectionNativeMethods.GetWindowThreadProcessId(hWnd, out uint processId);
            return processId == Environment.ProcessId;
        }
        catch
        {
            return false;
        }
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
            // 有扩展名的情况下，检查主体是否是"字母+数字"模式（如 QQ123456）
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
    /// <remarks>如 "Code.xlsx" → ("Code", ".xlsx")，"Google" → ("Google", "")</remarks>
    private static string StripFileExtension(string text, out string extension)
    {
        extension = "";
        var lastDot = text.LastIndexOf('.');
        if (lastDot <= 0) return text;  // 没有点或点在开头

        var ext = text[lastDot..];  // 如 .xlsx
        var body = text[..lastDot]; // 如 Code

        // 只认常见扩展名（避免误剥离如 "3.14" 这种小数）
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

    /// <summary>在 UI 线程上执行操作</summary>
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

        if (_bubble != null)
        {
            InvokeOnUIThread(() =>
            {
                _bubble.Dispose();
                _bubble = null;
            });
        }

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

/// <summary>Win32 API 声明</summary>
internal static class SelectionNativeMethods
{
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int x; public int y; }

    [DllImport("user32.dll")]
    public static extern IntPtr WindowFromPoint(POINT point);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
}
