namespace MyTranslate.Core;

/// <summary>
/// 腾讯翻译实现 — 基于腾讯云 TMT 文本翻译 API
/// </summary>
public class TencentTranslator : ITranslator
{
    private readonly string _secretId;
    private readonly string _secretKey;
    private TencentCloud.Tmt.V20180321.TmtClient? _client;

    public string Name => "腾讯翻译";
    public string Id => "tencent";

    public TencentTranslator(string secretId, string secretKey)
    {
        _secretId = secretId ?? throw new ArgumentNullException(nameof(secretId));
        _secretKey = secretKey ?? throw new ArgumentNullException(nameof(secretKey));

        // 预创建 SDK 客户端，避免首次调用时的初始化延迟
        if (IsConfigured())
        {
            var cred = new TencentCloud.Common.Credential
            {
                SecretId = _secretId,
                SecretKey = _secretKey,
            };
            _client = new TencentCloud.Tmt.V20180321.TmtClient(cred, "ap-guangzhou");
        }
    }

    /// <summary>
    /// 预热 SDK 连接 — 发送一个极短的翻译请求来初始化网络连接
    /// </summary>
    public async Task WarmUpAsync()
    {
        if (_client == null) return;
        try
        {
            var req = new TencentCloud.Tmt.V20180321.Models.TextTranslateRequest
            {
                SourceText = "hi",
                Source = "en",
                Target = "zh",
                ProjectId = 0,
            };
            await _client.TextTranslate(req);
        }
        catch
        {
            // 预热失败不影响后续使用
        }
    }

    public async Task<TranslationResult> TranslateAsync(string text, Language source, Language target)
    {
        if (string.IsNullOrWhiteSpace(text))
            return TranslationResult.Fail(text, "文本为空", source, target, Name);

        if (!IsConfigured() || _client == null)
            return TranslationResult.Fail(text, "API 密钥未配置", source, target, Name);

        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            // 获取语言代码
            var sourceInfo = LanguageInfo.GetByLanguage(source);
            var targetInfo = LanguageInfo.GetByLanguage(target);
            if (sourceInfo == null || targetInfo == null)
                return TranslationResult.Fail(text, "不支持的语言", source, target, Name);

            var req = new TencentCloud.Tmt.V20180321.Models.TextTranslateRequest
            {
                SourceText = text,
                Source = sourceInfo.TencentCode,
                Target = targetInfo.TencentCode,
                ProjectId = 0,
            };

            var resp = await _client.TextTranslate(req);
            sw.Stop();

            return TranslationResult.Ok(
                text, resp.TargetText, source, target, Name,
                sw.Elapsed);
        }
        catch (Exception ex)
        {
            return TranslationResult.Fail(text, ex.Message, source, target, Name);
        }
    }

    public bool IsConfigured()
        => !string.IsNullOrEmpty(_secretId) && !string.IsNullOrEmpty(_secretKey);
}
