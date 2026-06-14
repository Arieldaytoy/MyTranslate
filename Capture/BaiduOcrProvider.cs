namespace MyTranslate.Capture;

using System.Net.Http;
using System.Text.Json;

/// <summary>
/// 百度 OCR 实现 — 基于百度云通用文字识别 API
/// </summary>
public class BaiduOcrProvider : IOcrProvider
{
    private readonly string _apiKey;
    private readonly string _secretKey;
    private static readonly HttpClient _httpClient = new();
    private string? _accessToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    public string Name => "百度 OCR";
    public string Id => "baidu_ocr";

    public BaiduOcrProvider(string apiKey, string secretKey)
    {
        _apiKey = apiKey;
        _secretKey = secretKey;
    }

    public async Task<string> RecognizeAsync(Bitmap image)
    {
        if (string.IsNullOrEmpty(_apiKey) || string.IsNullOrEmpty(_secretKey))
            throw new InvalidOperationException("百度 OCR API Key 未配置");

        // 获取 access_token
        if (string.IsNullOrEmpty(_accessToken) || DateTime.Now >= _tokenExpiry)
        {
            await RefreshAccessTokenAsync();
        }

        // Bitmap → Base64
        using var ms = new MemoryStream();
        image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        string base64 = Convert.ToBase64String(ms.ToArray());

        // 调用 OCR API
        string url = $"https://aip.baidubce.com/rest/2.0/ocr/v1/general_basic?access_token={_accessToken}";
        var content = new StringContent($"image={Uri.EscapeDataString(base64)}", System.Text.Encoding.UTF8, "application/x-www-form-urlencoded");

        var response = await _httpClient.PostAsync(url, content);
        string responseBody = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;

        if (root.TryGetProperty("error_code", out var errorCode))
        {
            string errorMsg = root.TryGetProperty("error_msg", out var errorMsgEl)
                ? errorMsgEl.GetString() ?? "未知错误"
                : "未知错误";
            throw new Exception($"百度 OCR 错误 [{errorCode}]: {errorMsg}");
        }

        if (root.TryGetProperty("words_result", out var wordsResult))
        {
            var lines = new List<string>();
            foreach (var item in wordsResult.EnumerateArray())
            {
                if (item.TryGetProperty("words", out var words))
                {
                    lines.Add(words.GetString() ?? "");
                }
            }
            return string.Join("\n", lines);
        }

        return "";
    }

    public bool IsConfigured()
        => !string.IsNullOrEmpty(_apiKey) && !string.IsNullOrEmpty(_secretKey);

    private async Task RefreshAccessTokenAsync()
    {
        string url = $"https://aip.baidubce.com/oauth/2.0/token?grant_type=client_credentials&client_id={_apiKey}&client_secret={_secretKey}";
        var response = await _httpClient.PostAsync(url, null);
        string responseBody = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;

        if (root.TryGetProperty("access_token", out var tokenEl))
        {
            _accessToken = tokenEl.GetString();
            int expiresIn = root.TryGetProperty("expires_in", out var expEl) ? expEl.GetInt32() : 2592000;
            _tokenExpiry = DateTime.Now.AddSeconds(expiresIn - 300); // 提前5分钟刷新
        }
        else
        {
            string error = root.TryGetProperty("error_description", out var errEl)
                ? errEl.GetString() ?? "未知错误"
                : "获取 access_token 失败";
            throw new Exception($"百度 OCR 认证失败: {error}");
        }
    }
}
