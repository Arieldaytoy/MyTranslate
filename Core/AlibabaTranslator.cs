namespace MyTranslate.Core;

/// <summary>
/// 阿里翻译实现 — 预留，后续实现
/// </summary>
public class AlibabaTranslator : ITranslator
{
    private readonly string _accessKeyId;
    private readonly string _accessKeySecret;

    public string Name => "阿里翻译";
    public string Id => "alibaba";

    public AlibabaTranslator(string accessKeyId, string accessKeySecret)
    {
        _accessKeyId = accessKeyId;
        _accessKeySecret = accessKeySecret;
    }

    public Task<TranslationResult> TranslateAsync(string text, Language source, Language target)
    {
        // TODO: 接入阿里翻译 API
        // 阿里云机器翻译: https://www.aliyun.com/product/alimt
        // 使用 SDK 或 HTTP 请求
        throw new NotImplementedException("阿里翻译尚未实现，将在后续阶段完成。");
    }

    public bool IsConfigured()
        => !string.IsNullOrEmpty(_accessKeyId) && !string.IsNullOrEmpty(_accessKeySecret);
}
