namespace MyTranslate.Capture;

using TencentCloud.Common;
using TencentCloud.Common.Profile;
using TencentCloud.Ocr.V20181119;
using TencentCloud.Ocr.V20181119.Models;

/// <summary>
/// 云端 OCR 实现 — 腾讯云 OCR
/// </summary>
public class CloudOcrProvider : IOcrProvider
{
    public const string TencentId = "cloud_ocr_tencent";

    private readonly string _secretId;
    private readonly string _secretKey;

    public string Name => "腾讯 OCR";
    public string Id => TencentId;

    public CloudOcrProvider(string secretId, string secretKey)
    {
        _secretId = secretId;
        _secretKey = secretKey;
    }

    public async Task<string> RecognizeAsync(Bitmap image)
    {
        // Bitmap → byte[]
        using var ms = new MemoryStream();
        image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        var imageBytes = ms.ToArray();
        var base64 = Convert.ToBase64String(imageBytes);

        // 创建认证对象
        var cred = new Credential { SecretId = _secretId, SecretKey = _secretKey };
        var httpProfile = new HttpProfile { Endpoint = "ocr.tencentcloudapi.com" };
        var clientProfile = new ClientProfile { HttpProfile = httpProfile };

        var client = new OcrClient(cred, "", clientProfile);

        // 调用通用印刷体识别
        var request = new GeneralBasicOCRRequest
        {
            ImageBase64 = base64,
        };

        var response = await client.GeneralBasicOCR(request);

        // 拼接识别结果
        var lines = response.TextDetections?
            .Where(d => !string.IsNullOrEmpty(d.DetectedText))
            .Select(d => d.DetectedText)
            .ToList() ?? [];

        return string.Join("\n", lines);
    }

    public bool IsConfigured()
        => !string.IsNullOrEmpty(_secretId) && !string.IsNullOrEmpty(_secretKey);
}
