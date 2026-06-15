namespace MyTranslate.Services;

using MyTranslate.Capture;
using MyTranslate.Core;
using MyTranslate.Overlay;

/// <summary>
/// 截图翻译管理器 — 串联快捷键 → 框选 → 截图 → OCR → 浮窗显示结果
/// </summary>
public class CaptureTranslationManager : IDisposable
{
    private readonly OcrManager _ocrManager;
    private readonly AppConfig _config;

    private CaptureOverlay? _overlay;
    private SelectionOverlay? _resultOverlay;
    private bool _disposed;

    /// <summary>OCR 识别完成时触发，参数为识别到的文字</summary>
    public event EventHandler<string>? OcrCompleted;

    /// <summary>状态消息回调</summary>
    public Action<string>? StatusCallback { get; set; }

    public CaptureTranslationManager(
        OcrManager ocrManager,
        TranslationEngine engine,
        AppConfig config)
    {
        _ocrManager = ocrManager;
        _config = config;
    }

    /// <summary>执行截图 OCR 流程，将识别文字通过事件返回</summary>
    public async Task CaptureAndOcrAsync()
    {
        try
        {
            EnsureOverlay();

            StatusCallback?.Invoke("请拖拽选择截图区域...");
            var region = await _overlay!.StartCaptureAsync();
            if (region == null)
            {
                StatusCallback?.Invoke("截图已取消");
                return;
            }

            var captureRect = region.Value;
            System.Diagnostics.Debug.WriteLine($"[CaptureTranslation] 选区: {captureRect}");

            StatusCallback?.Invoke("正在截图...");
            using var bitmap = ScreenCaptureHelper.CaptureRegion(captureRect);
            System.Diagnostics.Debug.WriteLine($"[CaptureTranslation] 截图完成: {bitmap.Width}x{bitmap.Height}");

            StatusCallback?.Invoke("正在 OCR 识别...");
            var (ocrText, ocrSource) = await _ocrManager.RecognizeWithSourceAsync(bitmap);
            System.Diagnostics.Debug.WriteLine($"[CaptureTranslation] OCR 结果: '{ocrText}' (来源: {ocrSource})");

            if (string.IsNullOrWhiteSpace(ocrText))
            {
                StatusCallback?.Invoke("OCR 未识别到文字");
                return;
            }

            var sourceName = ocrSource ?? "未知";
            var sourceTag = ocrSource == "Windows OCR" ? "内置" : "API";
            StatusCallback?.Invoke($"OCR 识别成功 [{sourceTag}]，已填入输入框");

            // 显示浮窗
            bool isBuiltIn = ocrSource == "Windows OCR";
            ShowOcrResult(ocrText.Trim(), isBuiltIn, captureRect, sourceName);

            OcrCompleted?.Invoke(this, ocrText.Trim());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CaptureTranslation] 截图 OCR 异常: {ex}");
            StatusCallback?.Invoke($"截图 OCR 出错: {ex.Message}");
        }
    }

    private void EnsureOverlay()
    {
        if (_overlay == null)
            _overlay = new CaptureOverlay();
    }

    private void ShowOcrResult(string ocrText, bool isBuiltIn, Rectangle captureRect, string providerName)
    {
        if (_resultOverlay == null)
        {
            _resultOverlay = new SelectionOverlay();
            _resultOverlay.Opacity = _config.OverlayOpacity;
            _resultOverlay.CopyClicked += OnCopyClicked;
        }

        //var sourceTag = isBuiltIn ? "内置" : "API";
        var providerTag = isBuiltIn ? "内置OCR" : providerName ?? "未知";

        var result = new TranslationResult
        {
            OriginalText = ocrText,
            TranslatedText = ocrText,
            Success = true,
            InputTag = $"[{providerTag}]",//[{sourceTag}]
            SourceLanguage = Language.Auto,
            TargetLanguage = Language.Auto,
            TranslatorName = providerName,
        };

        _resultOverlay.Opacity = _config.OverlayOpacity;
        _resultOverlay.ShowOcr(result, captureRect, isBuiltIn);
    }

    private void OnCopyClicked(object? sender, EventArgs e)
    {
        if (_resultOverlay != null)
        {
            var text = _resultOverlay.GetTranslatedText();
            if (!string.IsNullOrEmpty(text))
            {
                try { Clipboard.SetText(text); } catch { }
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _overlay?.Dispose();
        _overlay = null;

        _resultOverlay?.Dispose();
        _resultOverlay = null;

        GC.SuppressFinalize(this);
    }
}
