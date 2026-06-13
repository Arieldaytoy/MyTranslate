namespace MyTranslate.Capture;

/// <summary>
/// OCR 调度管理器 — 统一 OCR 入口，支持多引擎切换和降级
/// </summary>
public class OcrManager
{
    private readonly Dictionary<string, IOcrProvider> _providers = [];
    private IOcrProvider? _primaryProvider;
    private IOcrProvider? _fallbackProvider;

    /// <summary>当前主 OCR 引擎</summary>
    public IOcrProvider? PrimaryProvider => _primaryProvider;

    /// <summary>注册一个 OCR 引擎</summary>
    public void RegisterProvider(IOcrProvider provider)
    {
        _providers[provider.Id] = provider;
    }

    /// <summary>设置主 OCR 引擎</summary>
    public bool SetPrimaryProvider(string providerId)
    {
        if (_providers.TryGetValue(providerId, out var provider))
        {
            _primaryProvider = provider;
            return true;
        }
        return false;
    }

    /// <summary>设置备用 OCR 引擎（主引擎失败时自动降级）</summary>
    public bool SetFallbackProvider(string providerId)
    {
        if (_providers.TryGetValue(providerId, out var provider))
        {
            _fallbackProvider = provider;
            return true;
        }
        return false;
    }

    /// <summary>
    /// 识别图像中的文字（自动降级：主引擎失败 → 备用引擎）
    /// </summary>
    public async Task<(string? text, string? providerName)> RecognizeWithSourceAsync(Bitmap image)
    {
        // 尝试主引擎
        if (_primaryProvider != null && _primaryProvider.IsConfigured())
        {
            try
            {
                var result = await _primaryProvider.RecognizeAsync(image);
                if (!string.IsNullOrWhiteSpace(result))
                    return (result, _primaryProvider.Name);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OcrManager] 主引擎 {_primaryProvider.Name} 失败: {ex.Message}");
            }
        }

        // 尝试备用引擎
        if (_fallbackProvider != null && _fallbackProvider.IsConfigured())
        {
            try
            {
                var result = await _fallbackProvider.RecognizeAsync(image);
                if (!string.IsNullOrWhiteSpace(result))
                    return (result, _fallbackProvider.Name);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OcrManager] 备用引擎 {_fallbackProvider.Name} 失败: {ex.Message}");
            }
        }

        return (null, null);
    }

    /// <summary>
    /// 识别图像中的文字（自动降级：主引擎失败 → 备用引擎）
    /// </summary>
    public async Task<string?> RecognizeAsync(Bitmap image)
    {
        // 尝试主引擎
        if (_primaryProvider != null && _primaryProvider.IsConfigured())
        {
            try
            {
                var result = await _primaryProvider.RecognizeAsync(image);
                if (!string.IsNullOrWhiteSpace(result))
                    return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OcrManager] 主引擎 {_primaryProvider.Name} 失败: {ex.Message}");
            }
        }

        // 尝试备用引擎
        if (_fallbackProvider != null && _fallbackProvider.IsConfigured())
        {
            try
            {
                return await _fallbackProvider.RecognizeAsync(image);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OcrManager] 备用引擎 {_fallbackProvider.Name} 失败: {ex.Message}");
            }
        }

        return null;
    }

    /// <summary>获取所有已注册的 OCR 引擎名称</summary>
    public string[] GetProviderNames()
        => _providers.Values.Select(p => p.Name).ToArray();
}
