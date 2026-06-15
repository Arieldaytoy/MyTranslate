namespace MyTranslate.UI;

using MyTranslate.Core;
using MyTranslate.Services;
using CaptureNs = MyTranslate.Capture;

/// <summary>
/// 设置窗口 — API设置 / 术语库 / 通用设置 / 快捷键
/// </summary>
public class SettingsForm : Form
{
    private readonly AppConfig _config;
    private readonly TranslationEngine _engine;
    private readonly Main_Translate? _mainForm;
    private TabControl _tabControl = null!;

    // ========== API设置页签控件 ==========
    private ComboBox _translatorComboBox = null!;
    private Panel _tencentApiPanel = null!;
    private Panel _baiduApiPanel = null!;
    private Panel _alibabaApiPanel = null!;
    private TextBox _tencentSecretIdTextBox = null!;
    private TextBox _tencentSecretKeyTextBox = null!;
    private TextBox _baiduAppIdTextBox = null!;
    private TextBox _baiduSecretKeyTextBox = null!;
    private TextBox _alibabaKeyIdTextBox = null!;
    private TextBox _alibabaKeySecretTextBox = null!;
    private Label _transTestResultLabel = null!;
    private Button _transTestButton = null!;
    private ComboBox _sourceLanguageComboBox = null!;
    private ComboBox _targetLanguageComboBox = null!;

    // OCR 方案控件（在 API 设置页签中）
    private RadioButton _windowsOcrRadio = null!;
    private RadioButton _cloudOcrRadio = null!;
    private Label _ocrTestResultLabel = null!;
    private Button _ocrTestButton = null!;

    // 每个供应商的 OCR 密钥
    private TextBox _tencentOcrIdTextBox = null!;
    private TextBox _tencentOcrKeyTextBox = null!;
    private TextBox _baiduOcrIdTextBox = null!;
    private TextBox _baiduOcrKeyTextBox = null!;
    private TextBox _alibabaOcrIdTextBox = null!;
    private TextBox _alibabaOcrKeyTextBox = null!;

    // ========== 通用设置页签控件 ==========
    private CheckBox _autoStartCheckBox = null!;
    private CheckBox _minimizeToTrayCheckBox = null!;
    private NumericUpDown _hoverDelayNumeric = null!;
    private TrackBar _opacityTrackBar = null!;
    private CheckBox _hoverEnabledCheckBox = null!;
    private CheckBox _selectionEnabledCheckBox = null!;
    private NumericUpDown _selectionMinLengthNumeric = null!;
    private CheckBox _selectionClipboardCheckBox = null!;
    private CheckBox _selectionHistoryCheckBox = null!;
    private CheckBox _selectionOverlayCheckBox = null!;
    private NumericUpDown _hoverMinLengthNumeric = null!;
    private CheckBox _hoverHistoryCheckBox = null!;
    private CheckBox _hoverOverlayCheckBox = null!;

    // ========== 缓存自动保存控件 ==========
    private CheckBox _cacheAutoSaveCheckBox = null!;
    private NumericUpDown _cacheAutoSaveIntervalNumeric = null!;
    private CheckBox _cacheAutoSaveJsonCheckBox = null!;
    private CheckBox _cacheAutoSaveCsvCheckBox = null!;
    private TextBox _cacheAutoSaveDirTextBox = null!;

    // ========== 快捷键页签控件 ==========
    private TextBox _toggleHotkeyTextBox = null!;
    private TextBox _captureHotkeyTextBox = null!;
    private TextBox _toggleOcrHotkeyTextBox = null!;

    // ========== 术语库页签控件 ==========
    private DataGridView _glossaryGrid = null!;

    // ========== 底部按钮 ==========
    private Button _okButton = null!;
    private Button _cancelButton = null!;

    /// <summary>设置保存后触发</summary>
    public event EventHandler<AppConfig>? SettingsSaved;

    public SettingsForm(AppConfig config, TranslationEngine engine, Main_Translate? mainForm = null)
    {
        _config = config;
        _engine = engine;
        _mainForm = mainForm;
        InitializeComponents();
        LoadSettings();

        // 监听主窗口的 OCR 方案变更，实时同步单选按钮
        if (_mainForm != null)
        {
            _mainForm.OcrSchemeChanged += OnExternalOcrSchemeChanged;
            this.FormClosed += (s, e) => _mainForm.OcrSchemeChanged -= OnExternalOcrSchemeChanged;
        }
    }

    /// <summary>主窗口通过快捷键切换 OCR 时，同步更新本窗口的单选按钮</summary>
    private void OnExternalOcrSchemeChanged(object? sender, string schemeName)
    {
        if (_windowsOcrRadio == null || _cloudOcrRadio == null) return;
        // 确保在 UI 线程执行
        if (InvokeRequired)
            Invoke(() => ApplyOcrSchemeToRadio(schemeName));
        else
            ApplyOcrSchemeToRadio(schemeName);
    }

    private void ApplyOcrSchemeToRadio(string schemeName)
    {
        bool isBuiltIn = schemeName == "内置";
        _windowsOcrRadio.Checked = isBuiltIn;
        _cloudOcrRadio.Checked = !isBuiltIn;
    }

    private void InitializeComponents()
    {
        Text = "设置";
        Size = new Size(620, 540);
        MinimumSize = new Size(580, 500);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Microsoft YaHei UI", 10f);

        _tabControl = new TabControl
        {
            Dock = DockStyle.Fill,
            Padding = new Point(12, 8),
        };

        _tabControl.TabPages.Add(CreateApiTab());
        _tabControl.TabPages.Add(CreateGlossaryTab());
        _tabControl.TabPages.Add(CreateGeneralTab());
        _tabControl.TabPages.Add(CreateHotkeyTab());

        _tabControl.SelectedIndexChanged += OnTabChanged;

        // ========== 底部按钮面板 ==========
        var buttonPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 50,
        };

        _okButton = new Button
        {
            Text = "确定",
            Size = new Size(85, 32),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
        };
        _okButton.Location = new Point(buttonPanel.Width - 95, 9);
        _okButton.Click += OnOkClicked;

        _cancelButton = new Button
        {
            Text = "取消",
            Size = new Size(85, 32),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
        };
        _cancelButton.Location = new Point(buttonPanel.Width - 190, 9);
        _cancelButton.Click += (s, e) => Close();

        buttonPanel.Controls.Add(_okButton);
        buttonPanel.Controls.Add(_cancelButton);

        buttonPanel.Resize += (s, e) =>
        {
            _cancelButton.Location = new Point(buttonPanel.Width - 190, 9);
            _okButton.Location = new Point(buttonPanel.Width - 95, 9);
        };

        var separator = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 1,
            BackColor = Color.FromArgb(220, 220, 220),
        };

        Controls.Add(_tabControl);
        Controls.Add(separator);
        Controls.Add(buttonPanel);

        AcceptButton = _okButton;
        CancelButton = _cancelButton;

        // 确保初始在 API 设置页
        _tabControl.SelectedIndex = 0;
    }

    // ========== API 设置页签 ==========

    private TabPage CreateApiTab()
    {
        var page = new TabPage("API 设置");
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 30,
            Padding = new Padding(10),
            AutoScroll = true,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        int rowHeight = 34;
        int row = 0;

        // 帮助链接
        var helpLink = new LinkLabel
        {
            Text = "如何获取 API 密钥？点击这里查看帮助",
            AutoSize = true,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        helpLink.LinkClicked += (s, e) => ShowApiKeyHelp();
        layout.Controls.Add(helpLink, 0, row);
        layout.SetColumnSpan(helpLink, 2);
        row++;

        // 供应商
        AddNormalRow(layout, ref row, rowHeight, "供应商：");
        _translatorComboBox = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        _translatorComboBox.Items.AddRange(["腾讯", "百度", "阿里"]);
        _translatorComboBox.SelectedIndexChanged += OnTranslatorComboChanged;
        layout.Controls.Add(_translatorComboBox, 1, row - 1);

        // 翻译 API 标题
        AddTitleRow(layout, ref row, "翻译 API");

        // SecretId / AppId / AccessKeyId
        AddNormalRow(layout, ref row, rowHeight, "SecretId：");
        _tencentSecretIdTextBox = new TextBox { Dock = DockStyle.Fill };
        _baiduAppIdTextBox = new TextBox { Dock = DockStyle.Fill };
        _alibabaKeyIdTextBox = new TextBox { Dock = DockStyle.Fill };
        layout.Controls.Add(_tencentSecretIdTextBox, 1, row - 1);
        layout.Controls.Add(_baiduAppIdTextBox, 1, row - 1);
        layout.Controls.Add(_alibabaKeyIdTextBox, 1, row - 1);

        // SecretKey
        AddNormalRow(layout, ref row, rowHeight, "SecretKey：");
        _tencentSecretKeyTextBox = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
        _baiduSecretKeyTextBox = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
        _alibabaKeySecretTextBox = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
        layout.Controls.Add(_tencentSecretKeyTextBox, 1, row - 1);
        layout.Controls.Add(_baiduSecretKeyTextBox, 1, row - 1);
        layout.Controls.Add(_alibabaKeySecretTextBox, 1, row - 1);

        _tencentApiPanel = MakePairPanel(_tencentSecretIdTextBox, _tencentSecretKeyTextBox);
        _baiduApiPanel = MakePairPanel(_baiduAppIdTextBox, _baiduSecretKeyTextBox);
        _alibabaApiPanel = MakePairPanel(_alibabaKeyIdTextBox, _alibabaKeySecretTextBox);

        // 翻译测试按钮 + 结果
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        var transTestFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        _transTestButton = new Button { Text = "测试翻译", AutoSize = true };
        _transTestButton.Click += OnTransTestClicked;
        _transTestResultLabel = new Label
        {
            AutoSize = true,
            ForeColor = Color.Gray,
            Text = "",
            Padding = new Padding(8, 5, 0, 0),
            MaximumSize = new Size(350, 0),
        };
        transTestFlow.Controls.Add(_transTestButton);
        transTestFlow.Controls.Add(_transTestResultLabel);
        layout.Controls.Add(transTestFlow, 0, row);
        layout.SetColumnSpan(transTestFlow, 2);
        row++;

        // OCR 方案 标题
        AddTitleRow(layout, ref row, "OCR 方案");

        // OCR 方案选择
        AddNormalRow(layout, ref row, rowHeight, "OCR 方案：");
        var ocrRadioPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        _windowsOcrRadio = new RadioButton { Text = "内置", AutoSize = true };
        _cloudOcrRadio = new RadioButton { Text = "API", AutoSize = true };
        _windowsOcrRadio.CheckedChanged += OnOcrSchemeChanged;
        _cloudOcrRadio.CheckedChanged += OnOcrSchemeChanged;
        ocrRadioPanel.Controls.Add(_windowsOcrRadio);
        ocrRadioPanel.Controls.Add(_cloudOcrRadio);
        layout.Controls.Add(ocrRadioPanel, 1, row - 1);

        // OCR SecretId（每个供应商不同）
        AddNormalRow(layout, ref row, rowHeight, "OCR Id：");
        _tencentOcrIdTextBox = new TextBox { Dock = DockStyle.Fill };
        _baiduOcrIdTextBox = new TextBox { Dock = DockStyle.Fill };
        _alibabaOcrIdTextBox = new TextBox { Dock = DockStyle.Fill };
        layout.Controls.Add(_tencentOcrIdTextBox, 1, row - 1);
        layout.Controls.Add(_baiduOcrIdTextBox, 1, row - 1);
        layout.Controls.Add(_alibabaOcrIdTextBox, 1, row - 1);

        // OCR SecretKey（每个供应商不同）
        AddNormalRow(layout, ref row, rowHeight, "OCR Key：");
        _tencentOcrKeyTextBox = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
        _baiduOcrKeyTextBox = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
        _alibabaOcrKeyTextBox = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
        layout.Controls.Add(_tencentOcrKeyTextBox, 1, row - 1);
        layout.Controls.Add(_baiduOcrKeyTextBox, 1, row - 1);
        layout.Controls.Add(_alibabaOcrKeyTextBox, 1, row - 1);

        // OCR 提示文字
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        var ocrHintLabel = new Label
        {
            Text = "提示：留空则使用翻译 API 的密钥",
            AutoSize = true,
            ForeColor = Color.Gray,
            Font = new Font("Microsoft YaHei UI", 8f),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        layout.Controls.Add(ocrHintLabel, 0, row);
        layout.SetColumnSpan(ocrHintLabel, 2);
        row++;

        // OCR 测试按钮 + 结果
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        var ocrTestFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        _ocrTestButton = new Button { Text = "测试 OCR", AutoSize = true };
        _ocrTestButton.Click += OnOcrTestClicked;
        _ocrTestResultLabel = new Label
        {
            AutoSize = true,
            ForeColor = Color.Gray,
            Text = "",
            Padding = new Padding(8, 5, 0, 0),
            MaximumSize = new Size(350, 0),
        };
        ocrTestFlow.Controls.Add(_ocrTestButton);
        ocrTestFlow.Controls.Add(_ocrTestResultLabel);
        layout.Controls.Add(ocrTestFlow, 0, row);
        layout.SetColumnSpan(ocrTestFlow, 2);
        row++;

        // 默认源语言
        AddNormalRow(layout, ref row, rowHeight, "默认源语言：");
        _sourceLanguageComboBox = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        layout.Controls.Add(_sourceLanguageComboBox, 1, row - 1);

        // 默认目标语言
        AddNormalRow(layout, ref row, rowHeight, "默认目标语言：");
        _targetLanguageComboBox = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        layout.Controls.Add(_targetLanguageComboBox, 1, row - 1);

        page.Controls.Add(layout);
        return page;
    }

    /// <summary>创建一对隐藏面板（不放入 layout，仅用于显隐控制）</summary>
    private static Panel MakePairPanel(Control ctrl1, Control ctrl2)
    {
        var panel = new Panel { Visible = false };
        panel.Tag = (ctrl1, ctrl2);
        return panel;
    }

    // ========== OCR 测试 ==========

    private async void OnOcrTestClicked(object? sender, EventArgs e)
    {
        _ocrTestButton.Enabled = false;
        _ocrTestResultLabel.ForeColor = Color.Gray;
        _ocrTestResultLabel.Text = "正在测试...";

        try
        {
            if (_windowsOcrRadio.Checked)
            {
                var langs = CaptureNs.WindowsOcrProvider.GetAvailableLanguages();
                if (langs == null || langs.Count == 0)
                {
                    _ocrTestResultLabel.ForeColor = Color.Red;
                    _ocrTestResultLabel.Text = "系统未安装 OCR 语言包";
                    return;
                }
                bool hasChinese = langs.Any(l => l.StartsWith("zh", StringComparison.OrdinalIgnoreCase));
                _ocrTestResultLabel.ForeColor = Color.Green;
                _ocrTestResultLabel.Text = $"Windows OCR 可用（{langs.Count} 种语言{(hasChinese ? "，含中文" : "")}）";
            }
            else
            {
                // 获取当前供应商的 OCR 密钥
                int providerIdx = _translatorComboBox.SelectedIndex;
                string secretId = "", secretKey = "";

                if (providerIdx == 0) // 腾讯
                {
                    secretId = _tencentOcrIdTextBox.Text.Trim();
                    secretKey = _tencentOcrKeyTextBox.Text.Trim();
                    if (string.IsNullOrEmpty(secretId)) secretId = _tencentSecretIdTextBox.Text.Trim();
                    if (string.IsNullOrEmpty(secretKey)) secretKey = _tencentSecretKeyTextBox.Text.Trim();
                }
                else if (providerIdx == 1) // 百度
                {
                    secretId = _baiduOcrIdTextBox.Text.Trim();
                    secretKey = _baiduOcrKeyTextBox.Text.Trim();
                }
                else // 阿里
                {
                    secretId = _alibabaOcrIdTextBox.Text.Trim();
                    secretKey = _alibabaOcrKeyTextBox.Text.Trim();
                    if (string.IsNullOrEmpty(secretId)) secretId = _alibabaKeyIdTextBox.Text.Trim();
                    if (string.IsNullOrEmpty(secretKey)) secretKey = _alibabaKeySecretTextBox.Text.Trim();
                }

                if (string.IsNullOrEmpty(secretId) || string.IsNullOrEmpty(secretKey))
                {
                    _ocrTestResultLabel.ForeColor = Color.Red;
                    _ocrTestResultLabel.Text = "请先填写 OCR API 密钥";
                    return;
                }

                CaptureNs.IOcrProvider provider = providerIdx switch
                {
                    0 => new CaptureNs.CloudOcrProvider(secretId, secretKey),
                    1 => new CaptureNs.BaiduOcrProvider(secretId, secretKey),
                    2 => new CaptureNs.AlibabaOcrProvider(secretId, secretKey),
                    _ => new CaptureNs.CloudOcrProvider(secretId, secretKey),
                };

                using var bmp = new Bitmap(200, 50);
                using var g = Graphics.FromImage(bmp);
                g.Clear(Color.White);
                g.DrawString("Test", new Font("Arial", 16), Brushes.Black, 10, 10);

                var text = await provider.RecognizeAsync(bmp);
                _ocrTestResultLabel.ForeColor = Color.Green;
                _ocrTestResultLabel.Text = $"识别成功：{text?.Trim() ?? "(空)"}";
            }
        }
        catch (Exception ex)
        {
            _ocrTestResultLabel.ForeColor = Color.Red;
            _ocrTestResultLabel.Text = $"出错: {ex.Message}";
        }
        finally
        {
            _ocrTestButton.Enabled = true;
        }
    }

    /// <summary>添加一行标签+空白单元格，返回 row-1</summary>
    private static void AddNormalRow(TableLayoutPanel layout, ref int row, int height, string labelText)
    {
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        layout.Controls.Add(CreateLabel(labelText), 0, row);
        row++;
    }

    /// <summary>添加标题行</summary>
    private static void AddTitleRow(TableLayoutPanel layout, ref int row, string text)
    {
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        var label = new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold),
            ForeColor = Color.FromArgb(60, 60, 60),
            TextAlign = ContentAlignment.MiddleLeft,
        };
        layout.Controls.Add(label, 0, row);
        layout.SetColumnSpan(label, 2);
        row++;
    }

    // ========== 翻译器切换联动 ==========

    private void OnTranslatorComboChanged(object? sender, EventArgs e)
    {
        UpdateApiPanelVisibility();
    }

    private void UpdateApiPanelVisibility()
    {
        if (_tencentApiPanel == null) return; // 初始化期间不处理

        bool showTencent = _translatorComboBox.SelectedIndex == 0;
        bool showBaidu = _translatorComboBox.SelectedIndex == 1;
        bool showAlibaba = _translatorComboBox.SelectedIndex == 2;

        // 翻译密钥
        SetPairPanelVisibility(_tencentApiPanel, showTencent);
        SetPairPanelVisibility(_baiduApiPanel, showBaidu);
        SetPairPanelVisibility(_alibabaApiPanel, showAlibaba);

        // OCR 密钥
        _tencentOcrIdTextBox.Visible = showTencent;
        _tencentOcrKeyTextBox.Visible = showTencent;
        _baiduOcrIdTextBox.Visible = showBaidu;
        _baiduOcrKeyTextBox.Visible = showBaidu;
        _alibabaOcrIdTextBox.Visible = showAlibaba;
        _alibabaOcrKeyTextBox.Visible = showAlibaba;

        _transTestResultLabel.Text = "";
    }

    private static void SetPairPanelVisibility(Panel panel, bool visible)
    {
        if (panel?.Tag is (Control c1, Control c2))
        {
            c1.Visible = visible;
            c2.Visible = visible;
        }
    }

    private void OnOkClicked(object? sender, EventArgs e)
    {
        _config.CurrentTranslatorId = _translatorComboBox.SelectedIndex switch
        {
            0 => "tencent",
            1 => "baidu",
            2 => "alibaba",
            _ => "tencent",
        };

        _config.TencentSecretId = _tencentSecretIdTextBox.Text.Trim();
        _config.TencentSecretKey = _tencentSecretKeyTextBox.Text.Trim();
        _config.BaiduAppId = _baiduAppIdTextBox.Text.Trim();
        _config.BaiduSecretKey = _baiduSecretKeyTextBox.Text.Trim();
        _config.AlibabaAccessKeyId = _alibabaKeyIdTextBox.Text.Trim();
        _config.AlibabaAccessKeySecret = _alibabaKeySecretTextBox.Text.Trim();

        _config.DefaultSourceLanguage = (Language)_sourceLanguageComboBox.SelectedIndex;
        _config.DefaultTargetLanguage = (Language)_targetLanguageComboBox.SelectedIndex;

        // OCR 设置
        _config.OcrProvider = _windowsOcrRadio.Checked
            ? CaptureNs.OcrProvider.WindowsBuiltIn
            : CaptureNs.OcrProvider.TencentOcr;

        // OCR 密钥（每个供应商独立）
        _config.TencentOcrSecretId = _tencentOcrIdTextBox.Text.Trim();
        _config.TencentOcrSecretKey = _tencentOcrKeyTextBox.Text.Trim();
        _config.BaiduOcrApiKey = _baiduOcrIdTextBox.Text.Trim();
        _config.BaiduOcrSecretKey = _baiduOcrKeyTextBox.Text.Trim();
        _config.AlibabaOcrAccessKeyId = _alibabaOcrIdTextBox.Text.Trim();
        _config.AlibabaOcrAccessKeySecret = _alibabaOcrKeyTextBox.Text.Trim();

        // 通用设置
        _config.AutoStart = _autoStartCheckBox.Checked;
        _config.MinimizeToTray = _minimizeToTrayCheckBox.Checked;
        _config.HoverDelayMs = (int)_hoverDelayNumeric.Value;
        _config.OverlayOpacity = _opacityTrackBar.Value / 100.0;
        _config.HoverTranslationEnabled = _hoverEnabledCheckBox.Checked;
        _config.SelectionTranslationEnabled = _selectionEnabledCheckBox.Checked;
        _config.SelectionMinTextLength = (int)_selectionMinLengthNumeric.Value;
        _config.SelectionClipboardFallback = _selectionClipboardCheckBox.Checked;
        _config.SelectionShowHistory = _selectionHistoryCheckBox.Checked;
        _config.SelectionShowOverlay = _selectionOverlayCheckBox.Checked;
        _config.HoverMinTextLength = (int)_hoverMinLengthNumeric.Value;
        _config.HoverShowHistory = _hoverHistoryCheckBox.Checked;
        _config.HoverShowOverlay = _hoverOverlayCheckBox.Checked;

        // 快捷键
        _config.ToggleHotkey = _toggleHotkeyTextBox.Text.Trim();
        _config.CaptureHotkey = _captureHotkeyTextBox.Text.Trim();
        _config.ToggleOcrHotkey = _toggleOcrHotkeyTextBox.Text.Trim();

        // 缓存自动保存
        _config.CacheAutoSaveEnabled = _cacheAutoSaveCheckBox.Checked;
        _config.CacheAutoSaveIntervalMinutes = (int)_cacheAutoSaveIntervalNumeric.Value;
        _config.CacheAutoSaveExportJson = _cacheAutoSaveJsonCheckBox.Checked;
        _config.CacheAutoSaveExportCsv = _cacheAutoSaveCsvCheckBox.Checked;
        _config.CacheAutoSaveDirectory = _cacheAutoSaveDirTextBox.Text.Trim();

        // 术语库
        var glossaryManager = BuildGlossaryFromGrid();
        _engine.Glossary.Clear();
        foreach (var entry in glossaryManager.Entries)
            _engine.Glossary.Add(entry);
        _engine.Glossary.Save();

        _config.Save();
        SettingsSaved?.Invoke(this, _config);

        DialogResult = DialogResult.OK;
        Close();
    }

    // ========== 术语库页签 ==========

    private TabPage CreateGlossaryTab()
    {
        var page = new TabPage("术语库");

        var toolPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 40,
            Padding = new Padding(5),
        };

        var addButton = new Button { Text = "添加", AutoSize = true };
        addButton.Click += OnGlossaryAdd;

        var deleteButton = new Button { Text = "删除", AutoSize = true };
        deleteButton.Click += OnGlossaryDelete;

        var importCsvButton = new Button { Text = "导入 CSV", AutoSize = true };
        importCsvButton.Click += OnGlossaryImportCsv;

        var importJsonButton = new Button { Text = "导入 JSON", AutoSize = true };
        importJsonButton.Click += OnGlossaryImportJson;

        var exportCsvButton = new Button { Text = "导出 CSV", AutoSize = true };
        exportCsvButton.Click += OnGlossaryExportCsv;

        var exportJsonButton = new Button { Text = "导出 JSON", AutoSize = true };
        exportJsonButton.Click += OnGlossaryExportJson;

        var searchBox = new TextBox
        {
            Width = 120,
            PlaceholderText = "搜索术语...",
        };
        searchBox.TextChanged += OnGlossarySearch;

        toolPanel.Controls.AddRange([searchBox,
            new Label { Text = "│", AutoSize = true, ForeColor = Color.Gray, Padding = new Padding(5, 5, 5, 0) },
            addButton, deleteButton,
            new Label { Text = "│", AutoSize = true, ForeColor = Color.Gray, Padding = new Padding(5, 5, 5, 0) },
            importCsvButton, importJsonButton, exportCsvButton, exportJsonButton]);

        _glossaryGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            RowHeadersVisible = false,
            MultiSelect = false,
            EditMode = DataGridViewEditMode.EditOnEnter,
            Font = new Font("Microsoft YaHei UI", 10f),
        };

        _glossaryGrid.Columns.AddRange(
            new DataGridViewTextBoxColumn { Name = "SourceTerm", HeaderText = "原文术语", FillWeight = 35 },
            new DataGridViewTextBoxColumn { Name = "TargetTerm", HeaderText = "期望翻译", FillWeight = 35 },
            new DataGridViewComboBoxColumn
            {
                Name = "Direction", HeaderText = "方向", FillWeight = 15,
                Items = { "中→英", "英→中", "双向" }, FlatStyle = FlatStyle.Flat,
            },
            new DataGridViewTextBoxColumn { Name = "Note", HeaderText = "备注", FillWeight = 15 }
        );

        page.Controls.Add(_glossaryGrid);
        page.Controls.Add(toolPanel);
        return page;
    }

    // ========== 通用设置页签 ==========

    private TabPage CreateGeneralTab()
    {
        var page = new TabPage("通用设置");
        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(10),
        };

        _autoStartCheckBox = new CheckBox { Text = "开机自动启动", AutoSize = true };
        _minimizeToTrayCheckBox = new CheckBox { Text = "关闭窗口时最小化到托盘", AutoSize = true };
        _hoverEnabledCheckBox = new CheckBox { Text = "启用悬停翻译", AutoSize = true };
        _selectionEnabledCheckBox = new CheckBox { Text = "启用选词翻译", AutoSize = true };

        var minLengthPanel = new FlowLayoutPanel { AutoSize = true };
        minLengthPanel.Controls.Add(new Label { Text = "选词最小长度：", AutoSize = true });
        _selectionMinLengthNumeric = new NumericUpDown { Minimum = 1, Maximum = 20, Increment = 1, Width = 60 };
        minLengthPanel.Controls.Add(_selectionMinLengthNumeric);
        minLengthPanel.Controls.Add(new Label { Text = "个字符", AutoSize = true });

        _selectionClipboardCheckBox = new CheckBox { Text = "UI Automation 读不到时使用剪贴板兜底", AutoSize = true };
        _selectionHistoryCheckBox = new CheckBox { Text = "划词翻译结果记录到历史面板", AutoSize = true };
        _selectionOverlayCheckBox = new CheckBox { Text = "划词翻译显示浮窗", AutoSize = true };

        var hoverMinLengthPanel = new FlowLayoutPanel { AutoSize = true };
        hoverMinLengthPanel.Controls.Add(new Label { Text = "悬停最小长度：", AutoSize = true });
        _hoverMinLengthNumeric = new NumericUpDown { Minimum = 1, Maximum = 20, Increment = 1, Width = 60 };
        hoverMinLengthPanel.Controls.Add(_hoverMinLengthNumeric);
        hoverMinLengthPanel.Controls.Add(new Label { Text = "个字符", AutoSize = true });

        _hoverHistoryCheckBox = new CheckBox { Text = "悬停翻译结果记录到历史面板", AutoSize = true };
        _hoverOverlayCheckBox = new CheckBox { Text = "悬停翻译显示浮窗", AutoSize = true };

        var delayPanel = new FlowLayoutPanel { AutoSize = true };
        delayPanel.Controls.Add(new Label { Text = "悬停延迟：", AutoSize = true });
        _hoverDelayNumeric = new NumericUpDown { Minimum = 200, Maximum = 2000, Increment = 100, Width = 80 };
        delayPanel.Controls.Add(_hoverDelayNumeric);
        delayPanel.Controls.Add(new Label { Text = "毫秒", AutoSize = true });

        var opacityPanel = new FlowLayoutPanel { AutoSize = true };
        opacityPanel.Controls.Add(new Label { Text = "浮窗透明度：", AutoSize = true });
        _opacityTrackBar = new TrackBar { Minimum = 30, Maximum = 100, TickFrequency = 10, Width = 200 };
        opacityPanel.Controls.Add(_opacityTrackBar);

        layout.Controls.Add(_autoStartCheckBox);
        layout.Controls.Add(_minimizeToTrayCheckBox);
        layout.Controls.Add(new Label { Height = 10, AutoSize = false });
        layout.Controls.Add(_hoverEnabledCheckBox);
        layout.Controls.Add(_selectionEnabledCheckBox);
        layout.Controls.Add(minLengthPanel);
        layout.Controls.Add(_selectionClipboardCheckBox);
        layout.Controls.Add(_selectionHistoryCheckBox);
        layout.Controls.Add(_selectionOverlayCheckBox);
        layout.Controls.Add(new Label { Height = 10, AutoSize = false });
        layout.Controls.Add(hoverMinLengthPanel);
        layout.Controls.Add(_hoverHistoryCheckBox);
        layout.Controls.Add(_hoverOverlayCheckBox);
        layout.Controls.Add(new Label { Height = 10, AutoSize = false });
        layout.Controls.Add(delayPanel);
        layout.Controls.Add(opacityPanel);
        layout.Controls.Add(new Label { Height = 10, AutoSize = false });

        _cacheAutoSaveCheckBox = new CheckBox { Text = "启用缓存自动保存", AutoSize = true };

        var cacheIntervalPanel = new FlowLayoutPanel { AutoSize = true };
        cacheIntervalPanel.Controls.Add(new Label { Text = "保存间隔：", AutoSize = true });
        _cacheAutoSaveIntervalNumeric = new NumericUpDown { Minimum = 5, Maximum = 1440, Increment = 5, Width = 70 };
        cacheIntervalPanel.Controls.Add(_cacheAutoSaveIntervalNumeric);
        cacheIntervalPanel.Controls.Add(new Label { Text = "分钟", AutoSize = true });

        _cacheAutoSaveJsonCheckBox = new CheckBox { Text = "自动导出 JSON", AutoSize = true };
        _cacheAutoSaveCsvCheckBox = new CheckBox { Text = "自动导出 CSV", AutoSize = true };

        var cacheDirPanel = new FlowLayoutPanel { AutoSize = true };
        cacheDirPanel.Controls.Add(new Label { Text = "导出目录：", AutoSize = true });
        _cacheAutoSaveDirTextBox = new TextBox { Width = 250, PlaceholderText = "留空则使用\"文档\"目录" };
        cacheDirPanel.Controls.Add(_cacheAutoSaveDirTextBox);
        var cacheDirBrowseBtn = new Button { Text = "浏览", AutoSize = true };
        cacheDirBrowseBtn.Click += OnCacheAutoSaveDirBrowse;
        cacheDirPanel.Controls.Add(cacheDirBrowseBtn);

        layout.Controls.Add(_cacheAutoSaveCheckBox);
        layout.Controls.Add(cacheIntervalPanel);
        layout.Controls.Add(_cacheAutoSaveJsonCheckBox);
        layout.Controls.Add(_cacheAutoSaveCsvCheckBox);
        layout.Controls.Add(cacheDirPanel);

        page.Controls.Add(layout);
        return page;
    }

    // ========== 快捷键页签 ==========

    private TabPage CreateHotkeyTab()
    {
        var page = new TabPage("快捷键");
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(10),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        int row = 0;

        layout.Controls.Add(CreateLabel("开关翻译："), 0, row);
        _toggleHotkeyTextBox = new TextBox { Dock = DockStyle.Fill };
        layout.Controls.Add(_toggleHotkeyTextBox, 1, row++);

        layout.Controls.Add(CreateLabel("区域截图翻译："), 0, row);
        _captureHotkeyTextBox = new TextBox { Dock = DockStyle.Fill };
        layout.Controls.Add(_captureHotkeyTextBox, 1, row++);

        layout.Controls.Add(CreateLabel("切换 OCR 方案："), 0, row);
        _toggleOcrHotkeyTextBox = new TextBox { Dock = DockStyle.Fill };
        layout.Controls.Add(_toggleOcrHotkeyTextBox, 1, row++);

        var tipLabel = new Label
        {
            Text = "格式示例：Ctrl+Y、Ctrl+Shift+T、Alt+Q\n支持修饰键：Ctrl、Alt、Shift、Win",
            AutoSize = true,
            ForeColor = Color.Gray,
            Font = new Font("Microsoft YaHei UI", 9f),
        };
        layout.Controls.Add(tipLabel, 0, row);
        layout.SetColumnSpan(tipLabel, 2);

        page.Controls.Add(layout);
        return page;
    }

    // ========== 术语库操作 ==========

    private void OnGlossarySearch(object? sender, EventArgs e)
    {
        var searchText = ((TextBox)sender!).Text.Trim().ToLowerInvariant();
        foreach (DataGridViewRow row in _glossaryGrid.Rows)
        {
            if (string.IsNullOrEmpty(searchText)) { row.Visible = true; continue; }
            var source = (row.Cells["SourceTerm"].Value?.ToString() ?? "").ToLowerInvariant();
            var target = (row.Cells["TargetTerm"].Value?.ToString() ?? "").ToLowerInvariant();
            var note = (row.Cells["Note"].Value?.ToString() ?? "").ToLowerInvariant();
            row.Visible = source.Contains(searchText) || target.Contains(searchText) || note.Contains(searchText);
        }
    }

    private void OnGlossaryAdd(object? sender, EventArgs e)
    {
        _glossaryGrid.Rows.Add("", "", "双向", "");
        _glossaryGrid.CurrentCell = _glossaryGrid.Rows[_glossaryGrid.Rows.Count - 1].Cells[0];
        _glossaryGrid.BeginEdit(true);
    }

    private void OnGlossaryDelete(object? sender, EventArgs e)
    {
        if (_glossaryGrid.SelectedRows.Count > 0)
        {
            foreach (DataGridViewRow row in _glossaryGrid.SelectedRows)
                if (!row.IsNewRow) _glossaryGrid.Rows.Remove(row);
        }
    }

    private void OnGlossaryImportCsv(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog { Filter = "CSV 文件|*.csv|所有文件|*.*", Title = "导入术语库 (CSV)" };
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            var manager = BuildGlossaryFromGrid();
            int count = manager.ImportFromCsv(dialog.FileName);
            LoadGlossaryToGrid(manager);
            MessageBox.Show($"成功导入 {count} 条术语", "导入完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void OnGlossaryImportJson(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog { Filter = "JSON 文件|*.json|所有文件|*.*", Title = "导入术语库 (JSON)" };
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            var manager = BuildGlossaryFromGrid();
            int count = manager.ImportFromJson(dialog.FileName);
            LoadGlossaryToGrid(manager);
            MessageBox.Show($"成功导入 {count} 条术语", "导入完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void OnGlossaryExportCsv(object? sender, EventArgs e)
    {
        using var dialog = new SaveFileDialog { Filter = "CSV 文件|*.csv", Title = "导出术语库 (CSV)", FileName = "glossary.csv" };
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            var manager = BuildGlossaryFromGrid();
            manager.ExportToCsv(dialog.FileName);
            MessageBox.Show("术语库已导出", "导出完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void OnGlossaryExportJson(object? sender, EventArgs e)
    {
        using var dialog = new SaveFileDialog { Filter = "JSON 文件|*.json", Title = "导出术语库 (JSON)", FileName = "glossary.json" };
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            var manager = BuildGlossaryFromGrid();
            manager.ExportToJson(dialog.FileName);
            MessageBox.Show("术语库已导出", "导出完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private GlossaryManager BuildGlossaryFromGrid()
    {
        var manager = new GlossaryManager();
        foreach (DataGridViewRow row in _glossaryGrid.Rows)
        {
            var source = row.Cells["SourceTerm"].Value?.ToString()?.Trim() ?? "";
            var target = row.Cells["TargetTerm"].Value?.ToString()?.Trim() ?? "";
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target)) continue;
            var dirText = row.Cells["Direction"].Value?.ToString() ?? "双向";
            var direction = dirText switch
            {
                "中→英" => GlossaryDirection.ChineseToEnglish,
                "英→中" => GlossaryDirection.EnglishToChinese,
                _ => GlossaryDirection.Both,
            };
            manager.Add(new GlossaryEntry
            {
                SourceTerm = source, TargetTerm = target,
                Direction = direction, Note = row.Cells["Note"].Value?.ToString(),
            });
        }
        return manager;
    }

    private void LoadGlossaryToGrid(GlossaryManager manager)
    {
        _glossaryGrid.Rows.Clear();
        foreach (var entry in manager.Entries)
        {
            var dirText = entry.Direction switch
            {
                GlossaryDirection.ChineseToEnglish => "中→英",
                GlossaryDirection.EnglishToChinese => "英→中",
                _ => "双向",
            };
            _glossaryGrid.Rows.Add(entry.SourceTerm, entry.TargetTerm, dirText, entry.Note ?? "");
        }
    }

    // ========== 缓存自动保存目录浏览 ==========

    private void OnCacheAutoSaveDirBrowse(object? sender, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = "选择缓存自动保存的导出目录",
            UseDescriptionForTitle = true,
        };
        if (!string.IsNullOrEmpty(_cacheAutoSaveDirTextBox.Text) && Directory.Exists(_cacheAutoSaveDirTextBox.Text))
            dlg.SelectedPath = _cacheAutoSaveDirTextBox.Text;
        if (dlg.ShowDialog() == DialogResult.OK)
            _cacheAutoSaveDirTextBox.Text = dlg.SelectedPath;
    }

    // ========== 辅助方法 ==========

    private static Label CreateLabel(string text)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
        };
    }

    // ========== 缺失的方法 ==========

    private void LoadSettings()
    {
        _translatorComboBox.SelectedIndex = _config.CurrentTranslatorId switch
        {
            "tencent" => 0,
            "baidu" => 1,
            "alibaba" => 2,
            _ => 0,
        };

        _tencentSecretIdTextBox.Text = _config.TencentSecretId;
        _tencentSecretKeyTextBox.Text = _config.TencentSecretKey;
        _baiduAppIdTextBox.Text = _config.BaiduAppId;
        _baiduSecretKeyTextBox.Text = _config.BaiduSecretKey;
        _alibabaKeyIdTextBox.Text = _config.AlibabaAccessKeyId;
        _alibabaKeySecretTextBox.Text = _config.AlibabaAccessKeySecret;

        _tencentOcrIdTextBox.Text = _config.TencentOcrSecretId;
        _tencentOcrKeyTextBox.Text = _config.TencentOcrSecretKey;
        _baiduOcrIdTextBox.Text = _config.BaiduOcrApiKey;
        _baiduOcrKeyTextBox.Text = _config.BaiduOcrSecretKey;
        _alibabaOcrIdTextBox.Text = _config.AlibabaOcrAccessKeyId;
        _alibabaOcrKeyTextBox.Text = _config.AlibabaOcrAccessKeySecret;

        UpdateApiPanelVisibility();

        _windowsOcrRadio.Checked = _config.OcrProvider == CaptureNs.OcrProvider.WindowsBuiltIn;
        _cloudOcrRadio.Checked = _config.OcrProvider != CaptureNs.OcrProvider.WindowsBuiltIn;
        OnOcrSchemeChanged(null, EventArgs.Empty);

        var languageNames = LanguageInfo.GetDisplayNames();
        _sourceLanguageComboBox.Items.AddRange(languageNames);
        _targetLanguageComboBox.Items.AddRange(languageNames);
        _sourceLanguageComboBox.SelectedIndex = (int)_config.DefaultSourceLanguage;
        _targetLanguageComboBox.SelectedIndex = (int)_config.DefaultTargetLanguage;

        _autoStartCheckBox.Checked = _config.AutoStart;
        _minimizeToTrayCheckBox.Checked = _config.MinimizeToTray;
        _hoverDelayNumeric.Value = _config.HoverDelayMs;
        _opacityTrackBar.Value = (int)(_config.OverlayOpacity * 100);
        _hoverEnabledCheckBox.Checked = _config.HoverTranslationEnabled;
        _selectionEnabledCheckBox.Checked = _config.SelectionTranslationEnabled;
        _selectionMinLengthNumeric.Value = _config.SelectionMinTextLength;
        _selectionClipboardCheckBox.Checked = _config.SelectionClipboardFallback;
        _selectionHistoryCheckBox.Checked = _config.SelectionShowHistory;
        _selectionOverlayCheckBox.Checked = _config.SelectionShowOverlay;
        _hoverMinLengthNumeric.Value = _config.HoverMinTextLength;
        _hoverHistoryCheckBox.Checked = _config.HoverShowHistory;
        _hoverOverlayCheckBox.Checked = _config.HoverShowOverlay;

        _toggleHotkeyTextBox.Text = _config.ToggleHotkey;
        _captureHotkeyTextBox.Text = _config.CaptureHotkey;
        _toggleOcrHotkeyTextBox.Text = _config.ToggleOcrHotkey;

        _cacheAutoSaveCheckBox.Checked = _config.CacheAutoSaveEnabled;
        _cacheAutoSaveIntervalNumeric.Value = _config.CacheAutoSaveIntervalMinutes;
        _cacheAutoSaveJsonCheckBox.Checked = _config.CacheAutoSaveExportJson;
        _cacheAutoSaveCsvCheckBox.Checked = _config.CacheAutoSaveExportCsv;
        _cacheAutoSaveDirTextBox.Text = _config.CacheAutoSaveDirectory;
    }

    private void OnTabChanged(object? sender, EventArgs e)
    {
        if (_transTestResultLabel == null) return; // 初始化期间不处理
        bool isApiTab = _tabControl.SelectedIndex == 0;
        if (!isApiTab)
        {
            _transTestResultLabel.Text = "";
            _ocrTestResultLabel.Text = "";
        }
    }

    private void OnOcrSchemeChanged(object? sender, EventArgs e)
    {
        if (_tencentOcrIdTextBox == null) return; // 初始化期间不处理
        bool isCloud = _cloudOcrRadio.Checked;
        _tencentOcrIdTextBox.Enabled = isCloud;
        _tencentOcrKeyTextBox.Enabled = isCloud;
        _baiduOcrIdTextBox.Enabled = isCloud;
        _baiduOcrKeyTextBox.Enabled = isCloud;
        _alibabaOcrIdTextBox.Enabled = isCloud;
        _alibabaOcrKeyTextBox.Enabled = isCloud;
    }

    private async void OnTransTestClicked(object? sender, EventArgs e)
    {
        _transTestButton.Enabled = false;
        _transTestResultLabel.ForeColor = Color.Gray;
        _transTestResultLabel.Text = "正在测试...";

        try
        {
            var (translatorId, id, key) = _translatorComboBox.SelectedIndex switch
            {
                0 => ("tencent", _tencentSecretIdTextBox.Text.Trim(), _tencentSecretKeyTextBox.Text.Trim()),
                1 => ("baidu", _baiduAppIdTextBox.Text.Trim(), _baiduSecretKeyTextBox.Text.Trim()),
                2 => ("alibaba", _alibabaKeyIdTextBox.Text.Trim(), _alibabaKeySecretTextBox.Text.Trim()),
                _ => ("", "", ""),
            };

            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(key))
            {
                _transTestResultLabel.ForeColor = Color.Red;
                _transTestResultLabel.Text = "请先填写完整的 API 密钥";
                return;
            }

            ITranslator? translator = CreateTranslatorFromFields(id, key, translatorId);
            if (translator == null)
            {
                _transTestResultLabel.ForeColor = Color.Red;
                _transTestResultLabel.Text = "未知翻译器类型";
                return;
            }

            TranslationResult result;
            try
            {
                result = await translator.TranslateAsync("hello", Language.English, Language.Chinese);
            }
            catch
            {
                await Task.Delay(300);
                result = await translator.TranslateAsync("hello", Language.English, Language.Chinese);
            }

            if (result.Success)
            {
                _transTestResultLabel.ForeColor = Color.Green;
                _transTestResultLabel.Text = $"成功！hello → {result.TranslatedText}";
            }
            else
            {
                _transTestResultLabel.ForeColor = Color.Red;
                _transTestResultLabel.Text = $"失败: {result.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            _transTestResultLabel.ForeColor = Color.Red;
            _transTestResultLabel.Text = $"出错: {ex.Message}";
        }
        finally
        {
            _transTestButton.Enabled = true;
        }
    }

    private ITranslator? CreateTranslatorFromFields(string id, string key, string type)
    {
        return type switch
        {
            "tencent" => new TencentTranslator(id, key),
            "baidu" => new BaiduTranslator(id, key),
            "alibaba" => new AlibabaTranslator(id, key),
            _ => null,
        };
    }

    private void ShowApiKeyHelp()
    {
        var helpText = @"【翻译 API 密钥获取指南】

● 腾讯翻译
  1. 访问 https://console.cloud.tencent.com/tmt
  2. 开通「文本翻译」服务
  3. 在「访问管理」→「API 密钥管理」中获取 SecretId 和 SecretKey

● 百度翻译
  1. 访问 https://fanyi-api.baidu.com/
  2. 注册账号并开通「通用翻译」
  3. 在「开发者信息」中获取 AppId 和 Secret Key

● 阿里翻译
  1. 访问 https://ram.console.aliyun.com
  2. 创建用户并授权「机器翻译」权限
  3. 在「认证管理」中获取 AccessKeyId 和 AccessKeySecret

【OCR API 密钥获取指南】

● 腾讯 OCR
  1. 访问 https://console.cloud.tencent.com/ocr
  2. 开通「通用文字识别」
  3. 使用与翻译相同的 SecretId 和 SecretKey（或单独配置）

● 百度 OCR
  1. 访问 https://console.bce.baidu.com/ai/#/ai/ocr/overview/index
  2. 开通「通用文字识别」
  3. 在「应用列表」中获取 API Key 和 Secret Key

● Windows 内置 OCR
  无需密钥，但需安装语言包：
  设置 → 时间和语言 → 语言 → 添加语言 → 勾选「语言包」";

        MessageBox.Show(helpText, "API 密钥获取帮助",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
