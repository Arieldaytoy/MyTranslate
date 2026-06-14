namespace MyTranslate.Core;

using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

/// <summary>
/// 阿里翻译实现 — 基于阿里云机器翻译 OpenAPI
/// </summary>
public class AlibabaTranslator : ITranslator
{
    private readonly string _accessKeyId;
    private readonly string _accessKeySecret;
    private static readonly HttpClient _httpClient = new();

    public string Name => "阿里翻译";
    public string Id => "alibaba";

    public AlibabaTranslator(string accessKeyId, string accessKeySecret)
    {
        _accessKeyId = accessKeyId ?? throw new ArgumentNullException(nameof(accessKeyId));
        _accessKeySecret = accessKeySecret ?? throw new ArgumentNullException(nameof(accessKeySecret));
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

            string sourceLang = sourceInfo.AlibabaCode;
            string targetLang = targetInfo.AlibabaCode;

            // 构建请求参数（按字母排序）
            var parameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                { "Action", "TranslateGeneral" },
                { "Format", "JSON" },
                { "Version", "2018-10-12" },
                { "SourceText", text },
                { "SourceLanguage", sourceLang },
                { "TargetLanguage", targetLang },
                { "Scene", "general" },
            };

            // 构建公共参数
            var commonParams = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                { "AccessKeyId", _accessKeyId },
                { "Action", "TranslateGeneral" },
                { "Format", "JSON" },
                { "RegionId", "cn-hangzhou" },
                { "SignatureMethod", "HMAC-SHA1" },
                { "SignatureNonce", Guid.NewGuid().ToString() },
                { "SignatureVersion", "1.0" },
                { "Timestamp", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ") },
                { "Version", "2018-10-12" },
            };

            // 合并所有参数用于签名
            var allParams = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var kv in commonParams) allParams[kv.Key] = kv.Value;
            foreach (var kv in parameters) allParams[kv.Key] = kv.Value;
            allParams["Action"] = "TranslateGeneral";

            // 生成签名
            string signature = ComputeSignature(allParams);
            allParams.Add("Signature", signature);

            // 构建请求 URL
            string queryString = string.Join("&", allParams.Select(kv =>
                $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

            string url = $"https://mt.cn-hangzhou.aliyuncs.com/?{queryString}";

            var response = await _httpClient.PostAsync(url, null);
            sw.Stop();

            string responseBody = await response.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"[AlibabaTranslator] Response: {responseBody}");

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            // 检查错误
            if (root.TryGetProperty("Code", out var code))
            {
                string msg = root.TryGetProperty("Message", out var msgEl)
                    ? msgEl.GetString() ?? "未知错误"
                    : "未知错误";
                return TranslationResult.Fail(text, $"阿里翻译错误 [{code}]: {msg}", source, target, Name);
            }

            // 提取翻译结果
            if (root.TryGetProperty("Data", out var data) && data.TryGetProperty("Translated", out var translated))
            {
                string result = translated.GetString() ?? "";
                return TranslationResult.Ok(text, result, source, target, Name, sw.Elapsed);
            }

            return TranslationResult.Fail(text, "未获取到翻译结果", source, target, Name);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AlibabaTranslator] Error: {ex}");
            return TranslationResult.Fail(text, ex.Message, source, target, Name);
        }
    }

    public bool IsConfigured()
        => !string.IsNullOrEmpty(_accessKeyId) && !string.IsNullOrEmpty(_accessKeySecret);

    /// <summary>
    /// 计算阿里云 API 签名（HMAC-SHA1）
    /// </summary>
    private string ComputeSignature(SortedDictionary<string, string> parameters)
    {
        // 1. 构建规范化查询字符串（URL 编码 key 和 value）
        var canonicalized = new StringBuilder();
        foreach (var kv in parameters)
        {
            if (canonicalized.Length > 0) canonicalized.Append('&');
            canonicalized.Append(Uri.EscapeDataString(kv.Key));
            canonicalized.Append('=');
            canonicalized.Append(Uri.EscapeDataString(kv.Value));
        }

        // 2. 构建待签名字符串
        string stringToSign = "POST" + "&" + Uri.EscapeDataString("/") + "&" + Uri.EscapeDataString(canonicalized.ToString());

        System.Diagnostics.Debug.WriteLine($"[AlibabaTranslator] StringToSign: {stringToSign}");

        // 3. 使用 HMAC-SHA1 签名（key = AccessKeySecret + "&"）
        byte[] keyBytes = Encoding.UTF8.GetBytes(_accessKeySecret + "&");
        using var hmac = new HMACSHA1(keyBytes);
        byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign));
        return Convert.ToBase64String(hash);
    }
}
