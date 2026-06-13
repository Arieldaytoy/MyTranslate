namespace MyTranslate.Capture;

/// <summary>
/// OCR 服务接口 — 所有 OCR 引擎都需要实现此接口
/// </summary>
public interface IOcrProvider
{
    /// <summary>OCR 引擎显示名称</summary>
    string Name { get; }

    /// <summary>OCR 引擎唯一标识</summary>
    string Id { get; }

    /// <summary>
    /// 从图像中识别文字
    /// </summary>
    /// <param name="image">待识别的图像</param>
    /// <returns>识别出的文字内容</returns>
    Task<string> RecognizeAsync(Bitmap image);

    /// <summary>
    /// 检查 OCR 引擎是否已正确配置
    /// </summary>
    bool IsConfigured();
}

/// <summary>
/// OCR 提供商枚举
/// </summary>
public enum OcrProvider
{
    WindowsBuiltIn,  // Windows 内置 OCR（免费）
    BaiduOcr,        // 百度 OCR
    TencentOcr,      // 腾讯 OCR
}
