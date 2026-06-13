namespace MyTranslate.Capture;

using System.Text;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

/// <summary>
/// Windows 内置 OCR 实现 — 使用 CsWinRT 官方投影调用 Windows.Media.Ocr API
/// 需要 Windows 10 1903+ 才有 OCR 支持，低版本系统 IsConfigured() 返回 false
/// </summary>
public class WindowsOcrProvider : IOcrProvider
{
    public string Name => "Windows OCR";
    public string Id => "windows_ocr";

    /// <summary>WinRT OCR 是否在当前系统上可用</summary>
    public bool IsConfigured()
    {
        try
        {
            return OcrEngine.AvailableRecognizerLanguages.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> RecognizeAsync(Bitmap image)
    {
        // 获取可用语言列表
        var langs = GetAvailableLanguages();
        if (langs == null || langs.Count == 0)
            throw new InvalidOperationException("系统未安装任何 OCR 语言包。请在 设置 → 时间和语言 → 语言 中添加语言包。");

        // 优先中文，其次第一个可用语言
        var langTag = langs.FirstOrDefault(l => l.StartsWith("zh")) ?? langs[0];

        // 创建 OCR 引擎
        var language = new Language(langTag);
        var engine = OcrEngine.TryCreateFromLanguage(language);
        if (engine == null)
            throw new InvalidOperationException($"无法创建 OCR 引擎（语言: {langTag}）。请检查语言包是否完整。");

        // 转换 Bitmap 为 SoftwareBitmap
        var softwareBitmap = await ConvertToSoftwareBitmapAsync(image);

        // 识别
        var ocrResult = await engine.RecognizeAsync(softwareBitmap);

        // 拼接结果
        var sb = new StringBuilder();
        foreach (var line in ocrResult.Lines)
        {
            if (sb.Length > 0) sb.AppendLine();
            sb.Append(line.Text);
        }
        return sb.ToString();
    }

    /// <summary>获取系统已安装的 OCR 识别语言标签列表</summary>
    public static List<string>? GetAvailableLanguages()
    {
        try
        {
            var result = new List<string>();
            foreach (var lang in OcrEngine.AvailableRecognizerLanguages)
            {
                result.Add(lang.LanguageTag);
            }
            return result;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>检查系统是否安装了指定语言的 OCR 包</summary>
    public static bool IsLanguageAvailable(string languageTag)
    {
        var langs = GetAvailableLanguages();
        return langs?.Any(l => l.StartsWith(languageTag, StringComparison.OrdinalIgnoreCase)) == true;
    }

    // ========== Bitmap → SoftwareBitmap 转换 ==========

    private static async Task<SoftwareBitmap> ConvertToSoftwareBitmapAsync(Bitmap image)
    {
        // 将 Bitmap 编码为 PNG 字节
        using var ms = new MemoryStream();
        image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        ms.Position = 0;

        // 创建 WinRT 内存流并写入图像数据
        using var randomAccessStream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(randomAccessStream.GetOutputStreamAt(0)))
        {
            writer.WriteBytes(ms.ToArray());
            await writer.StoreAsync();
            await writer.FlushAsync();
        }
        randomAccessStream.Seek(0);

        // 解码并转换为 SoftwareBitmap
        var decoder = await BitmapDecoder.CreateAsync(randomAccessStream);
        var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied);

        return softwareBitmap;
    }
}
