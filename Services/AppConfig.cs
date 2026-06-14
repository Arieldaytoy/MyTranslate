namespace MyTranslate.Services;

using System.Text.Json;
using System.Text.Json.Serialization;
using MyTranslate.Core;
using MyTranslate.Capture;

/// <summary>
/// 应用配置 — JSON 持久化到本地
/// </summary>
public class AppConfig
{
    // ========== 翻译设置 ==========

    /// <summary>当前翻译器 ID</summary>
    public string CurrentTranslatorId { get; set; } = "tencent";

    // ========== 腾讯翻译 ==========

    /// <summary>腾讯翻译 SecretId</summary>
    public string TencentSecretId { get; set; } = "";

    /// <summary>腾讯翻译 SecretKey</summary>
    public string TencentSecretKey { get; set; } = "";

    /// <summary>腾讯 OCR SecretId（与翻译相同则留空）</summary>
    public string TencentOcrSecretId { get; set; } = "";

    /// <summary>腾讯 OCR SecretKey（与翻译相同则留空）</summary>
    public string TencentOcrSecretKey { get; set; } = "";

    // ========== 百度翻译 ==========

    /// <summary>百度翻译 AppId</summary>
    public string BaiduAppId { get; set; } = "";

    /// <summary>百度翻译 SecretKey</summary>
    public string BaiduSecretKey { get; set; } = "";

    /// <summary>百度 OCR API Key</summary>
    public string BaiduOcrApiKey { get; set; } = "";

    /// <summary>百度 OCR Secret Key</summary>
    public string BaiduOcrSecretKey { get; set; } = "";

    // ========== 阿里翻译 ==========

    /// <summary>阿里翻译 AccessKeyId</summary>
    public string AlibabaAccessKeyId { get; set; } = "";

    /// <summary>阿里翻译 AccessKeySecret</summary>
    public string AlibabaAccessKeySecret { get; set; } = "";

    /// <summary>阿里 OCR AccessKeyId（与翻译相同则留空）</summary>
    public string AlibabaOcrAccessKeyId { get; set; } = "";

    /// <summary>阿里 OCR AccessKeySecret（与翻译相同则留空）</summary>
    public string AlibabaOcrAccessKeySecret { get; set; } = "";

    // ========== 语言设置 ==========

    /// <summary>默认源语言</summary>
    public Language DefaultSourceLanguage { get; set; } = Language.Auto;

    /// <summary>默认目标语言</summary>
    public Language DefaultTargetLanguage { get; set; } = Language.English;

    // ========== OCR 设置 ==========

    /// <summary>OCR 提供商</summary>
    public OcrProvider OcrProvider { get; set; } = OcrProvider.WindowsBuiltIn;

    /// <summary>悬停/划词翻译读不到文字时是否自动 OCR 降级</summary>
    public bool OcrFallbackEnabled { get; set; } = true;

    /// <summary>OCR 降级截图半径（像素），悬停翻译周围截图范围</summary>
    public int OcrFallbackRadius { get; set; } = 150;

    // ========== 截图翻译设置 ==========

    /// <summary>区域截图翻译快捷键（默认 Ctrl+Shift+Y）</summary>
    public string CaptureHotkey { get; set; } = "Ctrl+Shift+Y";

    /// <summary>切换 OCR 方案快捷键（默认 Ctrl+Shift+O）</summary>
    public string ToggleOcrHotkey { get; set; } = "Ctrl+Shift+O";

    /// <summary>截图翻译是否在历史记录面板中显示</summary>
    public bool CaptureShowHistory { get; set; } = true;

    /// <summary>截图翻译是否显示浮窗</summary>
    public bool CaptureShowOverlay { get; set; } = true;

    // ========== 通用设置 ==========

    /// <summary>开机自启动</summary>
    public bool AutoStart { get; set; } = false;

    /// <summary>关闭时最小化到托盘而非退出</summary>
    public bool MinimizeToTray { get; set; } = true;

    /// <summary>悬停延迟（毫秒），鼠标停留多久后触发翻译</summary>
    public int HoverDelayMs { get; set; } = 500;

    /// <summary>浮窗透明度 (0.0 ~ 1.0)</summary>
    public double OverlayOpacity { get; set; } = 0.9;

    /// <summary>是否启用悬停翻译</summary>
    public bool HoverTranslationEnabled { get; set; } = true;

    /// <summary>悬停翻译：最小文本长度（低于此长度不触发翻译）</summary>
    public int HoverMinTextLength { get; set; } = 2;

    /// <summary>悬停翻译：是否在历史记录面板中显示</summary>
    public bool HoverShowHistory { get; set; } = true;

    /// <summary>悬停翻译：是否显示浮窗</summary>
    public bool HoverShowOverlay { get; set; } = true;

    /// <summary>悬停翻译：防抖间隔（毫秒）</summary>
    public int HoverDebounceMs { get; set; } = 300;

    /// <summary>是否启用选词翻译</summary>
    public bool SelectionTranslationEnabled { get; set; } = true;

    /// <summary>选词翻译：最小文本长度（低于此长度不触发翻译）</summary>
    public int SelectionMinTextLength { get; set; } = 2;

    /// <summary>选词翻译：UI Automation 读不到时是否使用剪贴板兜底</summary>
    public bool SelectionClipboardFallback { get; set; } = true;

    /// <summary>选词翻译：是否在历史记录面板中显示</summary>
    public bool SelectionShowHistory { get; set; } = true;

    /// <summary>选词翻译：是否显示浮窗</summary>
    public bool SelectionShowOverlay { get; set; } = true;

    /// <summary>选词翻译：防抖间隔（毫秒）</summary>
    public int SelectionDebounceMs { get; set; } = 300;

    // ========== 缓存自动保存 ==========

    /// <summary>是否启用缓存自动保存</summary>
    public bool CacheAutoSaveEnabled { get; set; } = false;

    /// <summary>缓存自动保存间隔（分钟），默认 30 分钟</summary>
    public int CacheAutoSaveIntervalMinutes { get; set; } = 30;

    /// <summary>缓存自动保存时是否同时导出 JSON</summary>
    public bool CacheAutoSaveExportJson { get; set; } = true;

    /// <summary>缓存自动保存时是否同时导出 CSV</summary>
    public bool CacheAutoSaveExportCsv { get; set; } = true;

    /// <summary>缓存自动保存的导出目录（空则使用文档目录）</summary>
    public string CacheAutoSaveDirectory { get; set; } = "";

    // ========== 快捷键 ==========

    /// <summary>开关翻译的快捷键（默认 Ctrl+Y）</summary>
    public string ToggleHotkey { get; set; } = "Ctrl+Y";

    // ========== 持久化逻辑 ==========

    /// <summary>配置文件路径（AppData/Local/MyTranslate/config.json）</summary>
    public static string ConfigFilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MyTranslate", "config.json");

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>保存配置到文件</summary>
    public void Save()
    {
        var dir = Path.GetDirectoryName(ConfigFilePath)!;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(this, _jsonOptions);
        File.WriteAllText(ConfigFilePath, json);
    }

    /// <summary>从文件加载配置，文件不存在则返回默认配置</summary>
    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigFilePath))
            {
                var json = File.ReadAllText(ConfigFilePath);
                return JsonSerializer.Deserialize<AppConfig>(json, _jsonOptions) ?? new AppConfig();
            }
        }
        catch
        {
            // 配置文件损坏时返回默认配置
        }

        return new AppConfig();
    }
}
