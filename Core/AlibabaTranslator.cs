namespace MyTranslate.Core;

/// <summary>
/// 阿里翻译实现 — 基于阿里云机器翻译 SDK
/// </summary>
public class AlibabaTranslator : ITranslator
{
    private readonly string _accessKeyId;
    private readonly string _accessKeySecret;
    private AlibabaCloud.SDK.Alimt20181012.Client? _client;

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

            // 创建客户端
            if (_client == null)
            {
                var credentialConfig = new Aliyun.Credentials.Models.Config
                {
                    Type = "access_key",
                    AccessKeyId = _accessKeyId,
                    AccessKeySecret = _accessKeySecret,
                };
                var credential = new Aliyun.Credentials.Client(credentialConfig);
                var config = new AlibabaCloud.OpenApiClient.Models.Config
                {
                    Credential = credential,
                    Endpoint = "mt.cn-hangzhou.aliyuncs.com",
                };
                _client = new AlibabaCloud.SDK.Alimt20181012.Client(config);
            }

            // 构建请求
            var request = new AlibabaCloud.SDK.Alimt20181012.Models.TranslateGeneralRequest
            {
                SourceText = text,
                SourceLanguage = sourceLang,
                TargetLanguage = targetLang,
                FormatType = "text",
                Scene = "general",
            };

            var runtime = new AlibabaCloud.TeaUtil.Models.RuntimeOptions();

            // 调用 API
            var response = await Task.Run(() => _client.TranslateGeneralWithOptions(request, runtime));

            sw.Stop();

            System.Diagnostics.Debug.WriteLine($"[AlibabaTranslator] Code: {response?.Body?.Code}, Message: {response?.Body?.Message}");

            // 检查错误
            if (response?.Body?.Code != null && response.Body.Code != 200)
            {
                string msg = response.Body.Message ?? "未知错误";
                return TranslationResult.Fail(text, $"阿里翻译错误 [{response.Body.Code}]: {msg}", source, target, Name);
            }

            // 提取翻译结果
            if (response?.Body?.Data?.Translated != null)
            {
                return TranslationResult.Ok(text, response.Body.Data.Translated, source, target, Name, sw.Elapsed);
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
}
