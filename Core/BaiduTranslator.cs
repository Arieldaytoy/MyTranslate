namespace MyTranslate.Core;

/// <summary>
/// 百度翻译实现 — 预留，后续实现
/// </summary>
public class BaiduTranslator : ITranslator
{
    private readonly string _appId;
    private readonly string _secretKey;

    public string Name => "百度翻译";
    public string Id => "baidu";

    public BaiduTranslator(string appId, string secretKey)
    {
        _appId = appId;
        _secretKey = secretKey;
    }

    public Task<TranslationResult> TranslateAsync(string text, Language source, Language target)
    {
        // TODO: 接入百度翻译 API
        // 百度翻译开放平台: https://api.fanyi.baidu.com/
        // 使用 HTTP 请求: appid + q + from + to + salt + sign
        throw new NotImplementedException("百度翻译尚未实现，将在后续阶段完成。");
    }

    public bool IsConfigured()
        => !string.IsNullOrEmpty(_appId) && !string.IsNullOrEmpty(_secretKey);
}
