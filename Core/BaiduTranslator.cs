namespace MyTranslate.Core;

using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

/// <summary>
/// 百度翻译实现 — 基于百度翻译开放平台 HTTP API
/// </summary>
public class BaiduTranslator : ITranslator
{
    private readonly string _appId;
    private readonly string _secretKey;
    private static readonly HttpClient _httpClient = new();

    public string Name => "百度翻译";
    public string Id => "baidu";

    public BaiduTranslator(string appId, string secretKey)
    {
        _appId = appId ?? throw new ArgumentNullException(nameof(appId));
        _secretKey = secretKey ?? throw new ArgumentNullException(nameof(secretKey));
    }

    public async Task<TranslationResult> TranslateAsync(string text, Language source, Language target)
    {
        if (string.IsNullOrWhiteSpace(text))
            return TranslationResult.Fail(text, "文本为空", source, target, Name);

        if (!IsConfigured())
            return TranslationResult.Fail(text, "API 密钥未配置", source, target, Name);

        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            var sourceInfo = LanguageInfo.GetByLanguage(source);
            var targetInfo = LanguageInfo.GetByLanguage(target);
            if (sourceInfo == null || targetInfo == null)
                return TranslationResult.Fail(text, "不支持的语言", source, target, Name);

            string from = sourceInfo.BaiduCode;
            string to = targetInfo.BaiduCode;
            string salt = DateTime.Now.Millisecond.ToString();

            // 生成签名: md5(appid + q + salt + key)
            string signStr = _appId + text + salt + _secretKey;
            string sign = ComputeMd5(signStr);

            // 构建请求 URL
            string url = $"https://fanyi-api.baidu.com/api/trans/vip/translate"
                + $"?q={Uri.EscapeDataString(text)}"
                + $"&from={from}&to={to}"
                + $"&appid={_appId}&salt={salt}&sign={sign}";

            var response = await _httpClient.GetStringAsync(url);
            sw.Stop();

            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            // 检查错误
            if (root.TryGetProperty("error_code", out var errorCode))
            {
                string errorMsg = root.TryGetProperty("error_msg", out var errorMsgEl)
                    ? errorMsgEl.GetString() ?? "未知错误"
                    : "未知错误";
                return TranslationResult.Fail(text, $"百度翻译错误 [{errorCode}]: {errorMsg}", source, target, Name);
            }

            // 提取翻译结果
            if (root.TryGetProperty("trans_result", out var transResults) && transResults.GetArrayLength() > 0)
            {
                var firstResult = transResults[0];
                if (firstResult.TryGetProperty("dst", out var dst))
                {
                    string translated = dst.GetString() ?? "";
                    return TranslationResult.Ok(text, translated, source, target, Name, sw.Elapsed);
                }
            }

            return TranslationResult.Fail(text, "未获取到翻译结果", source, target, Name);
        }
        catch (Exception ex)
        {
            return TranslationResult.Fail(text, ex.Message, source, target, Name);
        }
    }

    public bool IsConfigured()
        => !string.IsNullOrEmpty(_appId) && !string.IsNullOrEmpty(_secretKey);

    private static string ComputeMd5(string input)
    {
        using var md5 = MD5.Create();
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] hashBytes = md5.ComputeHash(inputBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
