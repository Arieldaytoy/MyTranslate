namespace MyTranslate.Overlay;

using MyTranslate.Core;

/// <summary>
/// 选词翻译浮窗 — 在选中文本的下方显示翻译结果
/// </summary>
public class SelectionOverlay : OverlayForm
{
    private readonly Label _headerLabel;     // 顶部：语言方向 + 来源 + 功能
    private readonly Label _contentLabel;    // 主体：翻译结果
    private readonly Button _copyButton;     // 右下角：复制按钮
    private readonly Button _retranslateButton; // 右下角：重翻按钮

    private const int MaxWidth = 400;
    private const int MinWidth = 150;

    /// <summary>复制按钮点击事件</summary>
    public event EventHandler? CopyClicked;

    /// <summary>重翻按钮点击事件（强制调用 API）</summary>
    public event EventHandler? RetranslateClicked;

    public SelectionOverlay()
    {
        Width = MinWidth;
        Height = 70;

        // 顶部标签：语言方向 + 来源 + 功能
        _headerLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            Font = new Font("Microsoft YaHei UI", 8f),
            ForeColor = Color.FromArgb(130, 130, 130),
            Text = "",
            Padding = new Padding(0, 0, 0, 2),
            MaximumSize = new Size(MaxWidth - 24, 0),
        };

        // 底部面板：复制 + 重翻按钮
        var bottomPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 28,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            AutoSize = false,
            Padding = new Padding(0),
            Margin = new Padding(0),
        };

        _copyButton = new Button
        {
            Text = "复制",
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Right,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Microsoft YaHei UI", 8f),
            ForeColor = Color.FromArgb(80, 80, 80),
            BackColor = Color.FromArgb(230, 230, 240),
            Padding = new Padding(6, 1, 6, 1),
            Margin = new Padding(2, 2, 0, 0),
            Cursor = Cursors.Hand,
        };
        _copyButton.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 210);
        _copyButton.Click += (s, e) => CopyClicked?.Invoke(this, EventArgs.Empty);

        _retranslateButton = new Button
        {
            Text = "重翻",
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Right,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Microsoft YaHei UI", 8f),
            ForeColor = Color.FromArgb(80, 80, 80),
            BackColor = Color.FromArgb(230, 230, 240),
            Padding = new Padding(6, 1, 6, 1),
            Margin = new Padding(4, 2, 0, 0),
            Cursor = Cursors.Hand,
        };
        _retranslateButton.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 210);
        _retranslateButton.Click += (s, e) => RetranslateClicked?.Invoke(this, EventArgs.Empty);

        bottomPanel.Controls.Add(_copyButton);
        bottomPanel.Controls.Add(_retranslateButton);

        // 主体标签：翻译结果
        _contentLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Font = new Font("Microsoft YaHei UI", 12f),
            ForeColor = Color.FromArgb(30, 30, 30),
            MaximumSize = new Size(MaxWidth - 24, 0),
            Text = "",
        };

        Controls.Add(_contentLabel);
        Controls.Add(bottomPanel);
        Controls.Add(_headerLabel);

        // 双击浮窗复制翻译结果
        DoubleClick += (s, e) => CopyClicked?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 显示翻译结果，并定位到选区下方
    /// </summary>
    public void ShowTranslation(TranslationResult result, Rectangle selectionBounds)
    {
        if (result.Success)
        {
            var sourceInfo = LanguageInfo.GetByLanguage(result.SourceLanguage);
            var targetInfo = LanguageInfo.GetByLanguage(result.TargetLanguage);
            var sourceTag = $"[{result.SourceDisplay}]";
            var providerTag = "";
            if (!string.IsNullOrEmpty(result.TranslatorName) && !result.TranslatorName.StartsWith("("))
                providerTag = $"({result.TranslatorName})";
            else if (!string.IsNullOrEmpty(result.TranslatorName))
                providerTag = result.TranslatorName;
            var inputTag = string.IsNullOrEmpty(result.InputTag) ? "[划词]" : result.InputTag;
            _headerLabel.Text = $"{sourceInfo?.DisplayName ?? "?"}→{targetInfo?.DisplayName ?? "?"} {sourceTag}{providerTag}{inputTag}";
            _contentLabel.Text = result.TranslatedText;
            _contentLabel.ForeColor = result.IsCached
                ? Color.FromArgb(30, 100, 30)
                : Color.FromArgb(30, 30, 30);
            _copyButton.Visible = true;
            _retranslateButton.Visible = true;
        }
        else
        {
            _headerLabel.Text = "翻译失败";
            _contentLabel.Text = result.ErrorMessage ?? "未知错误";
            _contentLabel.ForeColor = Color.FromArgb(200, 60, 60);
            _copyButton.Visible = false;
            _retranslateButton.Visible = true;
        }

        AdjustSize();
        PositionBelow(selectionBounds);
        if (!Visible) Show();
    }

    /// <summary>
    /// 显示 OCR 识别结果，标题为 [OCR][内置] 或 [OCR][API]
    /// </summary>
    public void ShowOcr(TranslationResult result, Rectangle selectionBounds, bool isBuiltIn)
    {
        _headerLabel.Text = result.InputTag ?? "[OCR]";

        if (string.IsNullOrWhiteSpace(result.TranslatedText))
        {
            _contentLabel.Text = "未识别到文字";
            _contentLabel.ForeColor = Color.FromArgb(200, 60, 60);
        }
        else
        {
            _contentLabel.Text = result.TranslatedText;
            _contentLabel.ForeColor = isBuiltIn
                ? Color.FromArgb(30, 100, 30)   // 内置：绿色
                : Color.FromArgb(30, 30, 30);   // API：黑色
        }

        _copyButton.Visible = !string.IsNullOrWhiteSpace(result.TranslatedText);
        _retranslateButton.Visible = false;

        AdjustSize();
        PositionBelow(selectionBounds);
        if (!Visible) Show();
    }

    /// <summary>
    /// 获取当前翻译结果文本（供复制用）
    /// </summary>
    public string GetTranslatedText() => _contentLabel.Text;

    /// <summary>根据内容自动调整窗口尺寸</summary>
    private void AdjustSize()
    {
        _headerLabel.PerformLayout();
        _contentLabel.PerformLayout();

        int headerHeight = _headerLabel.PreferredHeight;
        int contentWidth = Math.Min(_contentLabel.PreferredWidth + 24, MaxWidth);
        int contentHeight = _contentLabel.PreferredHeight;

        // 宽度取内容和头部的较大者
        int headerWidth = Math.Min(_headerLabel.PreferredWidth + 24, MaxWidth);
        Width = Math.Max(MinWidth, Math.Max(contentWidth, headerWidth));
        Height = headerHeight + contentHeight + 24 + Padding.Top + Padding.Bottom + 4;

        OnResize(EventArgs.Empty);
    }
}
