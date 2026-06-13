namespace MyTranslate;

using MyTranslate.Core;
using MyTranslate.Services;
using MyTranslate.UI;
using MyTranslate.Capture;
using CaptureNs = MyTranslate.Capture;

/// <summary>
/// 主窗口 — 手动翻译 + 全局翻译的控制中心
/// </summary>
public partial class Main_Translate : Form
{
    // ========== 核心服务 ==========
    private AppConfig _config = null!;
    private TranslationEngine _translationEngine = null!;
    private HotkeyManager _hotkeyManager = null!;
    private TrayManager _trayManager = null!;

    // ========== 划词翻译 ==========
    private GlobalMouseHook _mouseHook = null!;
    private UIAutomationReader _automationReader = null!;
    private SelectionTranslationManager _selectionManager = null!;

    // ========== 悬停翻译 ==========
    private HoverTranslationManager _hoverManager = null!;

    // ========== 截图翻译 ==========
    private OcrManager _ocrManager = null!;
    private CaptureTranslationManager _captureManager = null!;

    // ========== 全局快捷键 ID ==========
    private int _toggleHotkeyId = -1;
    private int _captureHotkeyId = -1;
    private int _toggleOcrHotkeyId = -1;

    // ========== 翻译状态 ==========
    private bool _isTranslationEnabled;
    private DateTime _lastToggleTime = DateTime.MinValue;

    // ========== 翻译历史 ==========
    private readonly List<TranslationResult> _history = [];

    // ========== 缓存自动保存 ==========
    private System.Threading.Timer? _cacheAutoSaveTimer;

    public Main_Translate()
    {
        InitializeComponent();
        LoadAppIcon();
        InitializeServices();
        WireUpEvents();
    }

    private void LoadAppIcon()
    {
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "app_icon.ico");
            if (File.Exists(iconPath))
                Icon = new Icon(iconPath);
        }
        catch { }
    }

    // ========== 初始化服务 ==========

    private void InitializeServices()
    {
        // 加载配置
        _config = AppConfig.Load();

        // 初始化翻译引擎
        _translationEngine = new TranslationEngine();
        _translationEngine.Glossary.Load();    // 加载术语库
        _translationEngine.History.LoadFromFile(); // 加载翻译历史缓存
        RegisterTranslators();

        // 初始化快捷键管理器
        _hotkeyManager = new HotkeyManager();

        // 初始化托盘管理器
        _trayManager = new TrayManager();

        // 初始化划词翻译组件
        _mouseHook = new GlobalMouseHook(_config.HoverDelayMs);
        _automationReader = new UIAutomationReader();
        _selectionManager = new SelectionTranslationManager(
            _mouseHook, _automationReader, _translationEngine, _config);
        _selectionManager.TranslationCompleted += OnSelectionTranslationCompleted;

        // 初始化悬停翻译组件
        _hoverManager = new HoverTranslationManager(
            _mouseHook, _automationReader, _translationEngine, _config);
        _hoverManager.TranslationCompleted += OnHoverTranslationCompleted;

        // 初始化截图翻译组件
        _ocrManager = new OcrManager();
        var winOcr = new WindowsOcrProvider();
        _ocrManager.RegisterProvider(winOcr);

        if (!string.IsNullOrEmpty(_config.CloudOcrSecretId) && !string.IsNullOrEmpty(_config.CloudOcrApiKey))
        {
            var cloudOcr = new CloudOcrProvider(_config.CloudOcrSecretId, _config.CloudOcrApiKey);
            _ocrManager.RegisterProvider(cloudOcr);
        }

        bool useBuiltinInit = _config.OcrProvider == CaptureNs.OcrProvider.WindowsBuiltIn;
        if (useBuiltinInit)
            _ocrManager.SetPrimaryProvider(winOcr.Id);
        else
            _ocrManager.SetPrimaryProvider(CaptureNs.CloudOcrProvider.TencentId);

        _captureManager = new CaptureTranslationManager(
            _ocrManager, _translationEngine, _config);
        _captureManager.OcrCompleted += OnCaptureOcrCompleted;
        _captureManager.StatusCallback = status =>
        {
            if (InvokeRequired)
                Invoke(() => StateInfo_toolStripStatusLabel.Text = status);
            else
                StateInfo_toolStripStatusLabel.Text = status;
        };

        // 检查 OCR 语言包
        var ocrLangs = WindowsOcrProvider.GetAvailableLanguages();
        if (ocrLangs.Count == 0)
        {
            StateInfo_toolStripStatusLabel.Text = "警告: 系统未安装 OCR 语言包，请在设置中添加";
        }

        // 初始化语言下拉框
        InitializeLanguageComboBoxes();

        // 在历史面板上方添加"查看缓存"按钮
        var historyPanel = History_richTextBox.Parent as SplitterPanel;
        if (historyPanel != null)
        {
            var cacheButton = new Button
            {
                Text = "查看翻译缓存",
                Dock = DockStyle.Top,
                Height = 28,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei UI", 9f),
            };
            cacheButton.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
            cacheButton.Click += (s, e) =>
            {
                var viewer = new UI.CacheViewerForm(_translationEngine.History);
                viewer.ShowDialog(this);
            };
            historyPanel.Controls.Add(cacheButton);
            cacheButton.BringToFront();
        }

        // 注册全局快捷键
        RegisterHotkeys();

        // 初始化缓存自动保存
        StartCacheAutoSaveTimer();
    }

    /// <summary>注册所有已配置密钥的翻译器</summary>
    private void RegisterTranslators()
    {
        // 腾讯翻译
        if (!string.IsNullOrEmpty(_config.TencentSecretId) && !string.IsNullOrEmpty(_config.TencentSecretKey))
        {
            var translator = new TencentTranslator(_config.TencentSecretId, _config.TencentSecretKey);
            _translationEngine.RegisterTranslator(translator);

            // 后台预热 SDK 连接（不阻塞 UI）
            _ = Task.Run(async () => await translator.WarmUpAsync());
        }

        // 百度翻译
        if (!string.IsNullOrEmpty(_config.BaiduAppId) && !string.IsNullOrEmpty(_config.BaiduSecretKey))
        {
            _translationEngine.RegisterTranslator(
                new BaiduTranslator(_config.BaiduAppId, _config.BaiduSecretKey));
        }

        // 阿里翻译
        if (!string.IsNullOrEmpty(_config.AlibabaAccessKeyId) && !string.IsNullOrEmpty(_config.AlibabaAccessKeySecret))
        {
            _translationEngine.RegisterTranslator(
                new AlibabaTranslator(_config.AlibabaAccessKeyId, _config.AlibabaAccessKeySecret));
        }

        // 设置当前翻译器
        _translationEngine.SetCurrentTranslator(_config.CurrentTranslatorId);
    }

    /// <summary>初始化语言下拉框</summary>
    private void InitializeLanguageComboBoxes()
    {
        var languageNames = LanguageInfo.GetDisplayNames();

        SourcesLanguage_comboBox.Items.Clear();
        SourcesLanguage_comboBox.Items.AddRange(languageNames);
        SourcesLanguage_comboBox.SelectedIndex = (int)_config.DefaultSourceLanguage;

        TargetLanguage_comboBox.Items.Clear();
        TargetLanguage_comboBox.Items.AddRange(languageNames);
        TargetLanguage_comboBox.SelectedIndex = (int)_config.DefaultTargetLanguage;

        // 翻译器下拉框同步 — 显示所有已配置密钥的翻译器
        var translatorNames = _translationEngine.GetTranslatorNames();
        Translator_toolStripComboBox.Items.Clear();
        Translator_toolStripComboBox.Items.AddRange(translatorNames);

        if (translatorNames.Length > 0)
        {
            // 尝试选中当前配置的翻译器
            var current = _translationEngine.CurrentTranslator;
            if (current != null)
            {
                int idx = Translator_toolStripComboBox.Items.IndexOf(current.Name);
                Translator_toolStripComboBox.SelectedIndex = idx >= 0 ? idx : 0;
            }
            else
            {
                Translator_toolStripComboBox.SelectedIndex = 0;
            }
        }
    }

    /// <summary>注册全局快捷键</summary>
    private void RegisterHotkeys()
    {
        _toggleHotkeyId = _hotkeyManager.Register(_config.ToggleHotkey, this);
        _captureHotkeyId = _hotkeyManager.Register(_config.CaptureHotkey, this);
        _toggleOcrHotkeyId = _hotkeyManager.Register(_config.ToggleOcrHotkey, this);
        _hotkeyManager.HotkeyPressed += OnHotkeyPressed;
    }

    // ========== 事件绑定 ==========

    private void WireUpEvents()
    {
        // 翻译按钮
        Translate_button.Click += OnTranslateClicked;

        // 清空按钮
        Clean_button.Click += OnCleanClicked;

        // 复制按钮
        Copy_button.Click += OnCopyClicked;

        // 语言互换按钮
        ChangeLanguage_button.Click += OnChangeLanguageClicked;

        // 设置按钮
        Set_toolStripButton.Click += OnSettingsClicked;

        // 翻译器切换
        Translator_toolStripComboBox.SelectedIndexChanged += OnTranslatorChanged;

        // 托盘事件
        _trayManager.ToggleTranslationRequested += OnTrayToggleTranslation;
        _trayManager.ShowMainWindowRequested += OnTrayShowMainWindow;
        _trayManager.OpenSettingsRequested += OnTrayOpenSettings;
        _trayManager.ExitRequested += OnTrayExit;

        // 窗口事件
        FormClosing += OnFormClosing;
        Resize += OnFormResize;
    }

    // ========== 翻译操作 ==========

    private async void OnTranslateClicked(object? sender, EventArgs e)
    {
        var text = SourcesTXT_richTextBox.Text.Trim();
        if (string.IsNullOrEmpty(text) || text == "在这里输入要翻译的内容。")
        {
            StateInfo_toolStripStatusLabel.Text = "请先输入要翻译的内容";
            return;
        }

        if (_translationEngine.CurrentTranslator == null)
        {
            StateInfo_toolStripStatusLabel.Text = "未配置翻译器，请先在设置中配置 API 密钥";
            return;
        }

        var sourceLang = (Language)SourcesLanguage_comboBox.SelectedIndex;
        var targetLang = (Language)TargetLanguage_comboBox.SelectedIndex;

        StateInfo_toolStripStatusLabel.Text = "翻译中...";
        Translate_button.Enabled = false;
        bool forceRetranslate = ForceRetranslate_checkBox.Checked;

        try
        {
            var result = forceRetranslate
                ? await _translationEngine.ForceTranslateAsync(text, sourceLang, targetLang)
                : await _translationEngine.TranslateAsync(text, sourceLang, targetLang);

            if (result.Success)
            {
                TargetTxt_richTextBox.Text = result.TranslatedText;
                StateInfo_toolStripStatusLabel.Text =
                    $"翻译完成 ({result.TranslatorName}, {result.Duration.TotalMilliseconds:F0}ms"
                    + (result.IsCached && !forceRetranslate ? ", 本地缓存命中" : "") + ")";

                // 添加历史记录
                result.InputTag = "[按钮]";
                AddHistory(result);
            }
            else
            {
                TargetTxt_richTextBox.Text = "";
                StateInfo_toolStripStatusLabel.Text = $"翻译失败: {result.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            StateInfo_toolStripStatusLabel.Text = $"翻译出错: {ex.Message}";
        }
        finally
        {
            Translate_button.Enabled = true;
        }
    }

    private void OnCleanClicked(object? sender, EventArgs e)
    {
        SourcesTXT_richTextBox.Clear();
        TargetTxt_richTextBox.Clear();
        StateInfo_toolStripStatusLabel.Text = "已清空";
    }

    private void OnCopyClicked(object? sender, EventArgs e)
    {
        var text = TargetTxt_richTextBox.Text.Trim();
        if (!string.IsNullOrEmpty(text) && text != "点击翻译后会在这里显示翻译内容。")
        {
            Clipboard.SetText(text);
            StateInfo_toolStripStatusLabel.Text = "已复制到剪贴板";
        }
    }

    private void OnChangeLanguageClicked(object? sender, EventArgs e)
    {
        // 互换源语言和目标语言
        var tempIndex = SourcesLanguage_comboBox.SelectedIndex;
        SourcesLanguage_comboBox.SelectedIndex = TargetLanguage_comboBox.SelectedIndex;
        TargetLanguage_comboBox.SelectedIndex = tempIndex;
    }

    // ========== 翻译器切换 ==========

    private void OnTranslatorChanged(object? sender, EventArgs e)
    {
        if (Translator_toolStripComboBox.SelectedIndex < 0) return;

        var selectedName = Translator_toolStripComboBox.SelectedItem?.ToString();
        if (selectedName == null) return;

        // 根据名称找到 ID
        foreach (var translator in _translationEngine.Translators.Values)
        {
            if (translator.Name == selectedName)
            {
                _translationEngine.SetCurrentTranslator(translator.Id);
                StateInfo_toolStripStatusLabel.Text = $"已切换到 {translator.Name}";
                break;
            }
        }
    }

    // ========== 设置窗口 ==========

    private void OnSettingsClicked(object? sender, EventArgs e)
    {
        var settingsForm = new SettingsForm(_config, _translationEngine);
        settingsForm.SettingsSaved += OnSettingsSaved;
        settingsForm.ShowDialog(this);
    }

    private void OnSettingsSaved(object? sender, AppConfig config)
    {
        // 重新注册翻译器（密钥可能已更改）
        _translationEngine.ClearCache();
        RegisterTranslators();
        _translationEngine.Glossary.Load();
        InitializeLanguageComboBoxes();

        // 重新注册快捷键
        _hotkeyManager.UnregisterAll(this);
        RegisterHotkeys();

        // 重新初始化 OCR 引擎（方案可能已更改）
        ReinitializeOcr();

        // 同步划词翻译和悬停翻译开关状态
        if (_isTranslationEnabled)
        {
            if (_config.SelectionTranslationEnabled && !_selectionManager.IsActive)
                _selectionManager.Start();
            else if (!_config.SelectionTranslationEnabled && _selectionManager.IsActive)
                _selectionManager.Stop();

            if (_config.HoverTranslationEnabled && !_hoverManager.IsActive)
                _hoverManager.Start();
            else if (!_config.HoverTranslationEnabled && _hoverManager.IsActive)
                _hoverManager.Stop();
        }

        StateInfo_toolStripStatusLabel.Text = "设置已保存";

        // 重新启动缓存自动保存定时器
        StartCacheAutoSaveTimer();
    }

    private void ReinitializeOcr()
    {
        _ocrManager = new OcrManager();

        var winOcr = new WindowsOcrProvider();
        _ocrManager.RegisterProvider(winOcr);

        if (!string.IsNullOrEmpty(_config.CloudOcrSecretId) && !string.IsNullOrEmpty(_config.CloudOcrApiKey))
        {
            var cloudOcr = new CloudOcrProvider(_config.CloudOcrSecretId, _config.CloudOcrApiKey);
            _ocrManager.RegisterProvider(cloudOcr);
        }

        bool useBuiltin = _config.OcrProvider == CaptureNs.OcrProvider.WindowsBuiltIn;

        if (useBuiltin)
        {
            // 内置：只用 Windows OCR，不降级
            _ocrManager.SetPrimaryProvider(winOcr.Id);
        }
        else
        {
            // API：只用腾讯 OCR，不降级
            _ocrManager.SetPrimaryProvider(CaptureNs.CloudOcrProvider.TencentId);
        }

        // 重建 CaptureTranslationManager 以使用新的 OcrManager
        _captureManager?.Dispose();
        _captureManager = new CaptureTranslationManager(
            _ocrManager, _translationEngine, _config);
        _captureManager.OcrCompleted += OnCaptureOcrCompleted;
        _captureManager.StatusCallback = status =>
        {
            if (InvokeRequired)
                Invoke(() => StateInfo_toolStripStatusLabel.Text = status);
            else
                StateInfo_toolStripStatusLabel.Text = status;
        };
    }

    // ========== 划词翻译事件 ==========

    private void OnSelectionTranslationCompleted(object? sender, TranslationResult result)
    {
        // 根据配置决定是否记录到历史面板
        if (!_config.SelectionShowHistory) return;

        // 在主线程上添加到历史记录
        result.InputTag = "[划词]";
        if (InvokeRequired)
            Invoke(() => AddHistory(result));
        else
            AddHistory(result);
    }

    // ========== 悬停翻译事件 ==========

    private void OnHoverTranslationCompleted(object? sender, TranslationResult result)
    {
        // 根据配置决定是否记录到历史面板
        if (!_config.HoverShowHistory) return;

        // 在主线程上添加到历史记录
        result.InputTag = "[悬停]";
        if (InvokeRequired)
            Invoke(() => AddHistory(result));
        else
            AddHistory(result);
    }

    // ========== 截图 OCR 事件 ==========

    private void OnCaptureOcrCompleted(object? sender, string ocrText)
    {
        if (InvokeRequired)
        {
            Invoke(() => FillOcrText(ocrText));
            return;
        }
        FillOcrText(ocrText);
    }

    private void FillOcrText(string ocrText)
    {
        SourcesTXT_richTextBox.Text = ocrText;
        StateInfo_toolStripStatusLabel.Text = $"OCR 识别完成，已填入输入框（{ocrText.Length} 字符）";
    }

    // ========== 历史记录 ==========

    private void AddHistory(TranslationResult result)
    {
        _history.Insert(0, result);

        // 最多保留 100 条
        if (_history.Count > 100)
            _history.RemoveAt(_history.Count - 1);

        // 更新历史面板
        RefreshHistoryDisplay();
    }

    private void RefreshHistoryDisplay()
    {
        History_richTextBox.Clear();
        History_richTextBox.AppendText("── 翻译历史 ──\n\n");

        for (int i = 0; i < _history.Count; i++)
        {
            var item = _history[i];
            var sourceInfo = LanguageInfo.GetByLanguage(item.SourceLanguage);
            var targetInfo = LanguageInfo.GetByLanguage(item.TargetLanguage);
            var time = item.Timestamp.ToString("HH:mm");
            var sourceLabel = item.SourceDisplay;

            History_richTextBox.AppendText($"[{time}] {sourceInfo?.DisplayName}→{targetInfo?.DisplayName} ({sourceLabel}){item.InputTag}\n");
            History_richTextBox.AppendText($"  {Truncate(item.OriginalText, 30)}\n");
            History_richTextBox.AppendText($"  → {Truncate(item.TranslatedText, 30)}\n\n");
        }
    }

    private static string Truncate(string text, int maxLength)
        => text.Length <= maxLength ? text : text[..maxLength] + "...";

    // ========== 全局快捷键处理 ==========

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        // Ctrl+Shift+Enter: 强制重翻（跳过缓存）
        if (keyData == (Keys.Control | Keys.Shift | Keys.Enter))
        {
            ForceRetranslate_checkBox.Checked = true;
            OnTranslateClicked(this, EventArgs.Empty);
            return true;
        }
        // Ctrl+Enter: 普通翻译
        if (keyData == (Keys.Control | Keys.Enter))
        {
            OnTranslateClicked(this, EventArgs.Empty);
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void WndProc(ref Message m)
    {
        _hotkeyManager?.ProcessMessage(m);
        base.WndProc(ref m);
    }

    private void OnHotkeyPressed(object? sender, int hotkeyId)
    {
        if (hotkeyId == _toggleHotkeyId)
        {
            ToggleTranslation();
        }
        else if (hotkeyId == _captureHotkeyId)
        {
            _ = _captureManager.CaptureAndOcrAsync();
        }
        else if (hotkeyId == _toggleOcrHotkeyId)
        {
            ToggleOcrScheme();
        }
    }

    private void ToggleOcrScheme()
    {
        _config.OcrProvider = _config.OcrProvider == CaptureNs.OcrProvider.WindowsBuiltIn
            ? CaptureNs.OcrProvider.TencentOcr
            : CaptureNs.OcrProvider.WindowsBuiltIn;

        ReinitializeOcr();

        var schemeName = _config.OcrProvider == CaptureNs.OcrProvider.WindowsBuiltIn ? "内置" : "API";
        _config.Save();
        StateInfo_toolStripStatusLabel.Text = $"OCR 方案已切换为：{schemeName}";
        _trayManager.ShowBalloonTip("MyTranslate", $"OCR 方案：{schemeName}");
    }

    private void ToggleTranslation()
    {
        // 防抖：500ms 内不重复切换（避免输入法等误触 Ctrl+Y）
        if ((DateTime.Now - _lastToggleTime).TotalMilliseconds < 500)
            return;
        _lastToggleTime = DateTime.Now;

        _isTranslationEnabled = !_isTranslationEnabled;
        _trayManager.SetTranslationEnabled(_isTranslationEnabled);

        var statusText = _isTranslationEnabled ? "翻译已开启 (Ctrl+Y 关闭)" : "翻译已关闭 (Ctrl+Y 开启)";
        StateInfo_toolStripStatusLabel.Text = statusText;

        _trayManager.ShowBalloonTip("MyTranslate",
            _isTranslationEnabled ? "全局翻译已开启" : "全局翻译已关闭");

        if (_isTranslationEnabled)
        {
            // 启动鼠标钩子
            _mouseHook.Start();

            // 启动划词翻译
            if (_config.SelectionTranslationEnabled)
                _selectionManager.Start();

            // 启动悬停翻译
            if (_config.HoverTranslationEnabled)
                _hoverManager.Start();
        }
        else
        {
            // 停止悬停翻译、划词翻译和鼠标钩子
            _hoverManager.Stop();
            _selectionManager.Stop();
            _mouseHook.Stop();
        }
    }

    // ========== 托盘事件 ==========

    private void OnTrayToggleTranslation(object? sender, bool enabled)
    {
        if (enabled != _isTranslationEnabled)
            ToggleTranslation();
    }

    private void OnTrayShowMainWindow(object? sender, EventArgs e)
    {
        ShowMainWindow();
    }

    private void OnTrayOpenSettings(object? sender, EventArgs e)
    {
        OnSettingsClicked(this, EventArgs.Empty);
    }

    private void OnTrayExit(object? sender, EventArgs e)
    {
        // 真正退出
        _trayManager.Hide();
        Application.Exit();
    }

    // ========== 窗口事件 ==========

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_config.MinimizeToTray && e.CloseReason == CloseReason.UserClosing)
        {
            // 最小化到托盘而非退出
            e.Cancel = true;
            MinimizeToTray();
        }
    }

    private void OnFormResize(object? sender, EventArgs e)
    {
        if (WindowState == FormWindowState.Minimized && _config.MinimizeToTray)
        {
            MinimizeToTray();
        }
    }

    // ========== 窗口控制 ==========

    /// <summary>最小化到托盘</summary>
    private void MinimizeToTray()
    {
        Hide();
        _trayManager.Show();
    }

    /// <summary>从托盘恢复主窗口</summary>
    private void ShowMainWindow()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    // ========== 缓存自动保存 ==========

    /// <summary>启动/重启缓存自动保存定时器</summary>
    private void StartCacheAutoSaveTimer()
    {
        _cacheAutoSaveTimer?.Dispose();
        _cacheAutoSaveTimer = null;

        if (!_config.CacheAutoSaveEnabled) return;

        var interval = TimeSpan.FromMinutes(_config.CacheAutoSaveIntervalMinutes);
        _cacheAutoSaveTimer = new System.Threading.Timer(
            _ => Invoke(CacheAutoSaveCallback),
            null,
            interval,
            interval);
    }

    /// <summary>缓存自动保存回调</summary>
    private void CacheAutoSaveCallback()
    {
        try
        {
            var history = _translationEngine.History;
            if (history.Count == 0) return;

            // 确定导出目录
            var dir = _config.CacheAutoSaveDirectory;
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                dir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            if (_config.CacheAutoSaveExportJson)
            {
                var jsonPath = Path.Combine(dir, $"翻译缓存_{timestamp}.json");
                history.ExportToJson(jsonPath);
            }

            if (_config.CacheAutoSaveExportCsv)
            {
                var csvPath = Path.Combine(dir, $"翻译缓存_{timestamp}.csv");
                history.ExportToCsv(csvPath);
            }

            StateInfo_toolStripStatusLabel.Text = $"缓存已自动保存到 {dir}";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CacheAutoSave] 自动保存失败: {ex.Message}");
        }
    }

    // ========== 清理 ==========

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // 停止悬停翻译
            _hoverManager?.Stop();
            _hoverManager?.Dispose();

            // 停止划词翻译
            _selectionManager?.Stop();
            _selectionManager?.Dispose();

            // 停止鼠标钩子
            _mouseHook?.Stop();
            _mouseHook?.Dispose();

            // 释放截图翻译
            _captureManager?.Dispose();

            // 停止缓存自动保存定时器
            _cacheAutoSaveTimer?.Dispose();

            // 退出前保存翻译历史
            _translationEngine?.SaveHistory();

            _hotkeyManager?.UnregisterAll(this);
            _hotkeyManager?.Dispose();
            _trayManager?.Dispose();
            components?.Dispose();
        }
        base.Dispose(disposing);
    }
}
