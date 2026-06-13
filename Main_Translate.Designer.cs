namespace MyTranslate
{
    partial class Main_Translate
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        // Dispose 方法已移至 Main_Translate.cs 统一管理

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Main_Translate));
            toolStrip1 = new ToolStrip();
            Set_toolStripButton = new ToolStripButton();
            Translator_toolStripLabel = new ToolStripLabel();
            Translator_toolStripComboBox = new ToolStripComboBox();
            toolStripSeparator1 = new ToolStripSeparator();
            statusStrip1 = new StatusStrip();
            State_toolStripStatusLabel = new ToolStripStatusLabel();
            StateInfo_toolStripStatusLabel = new ToolStripStatusLabel();
            splitContainer1 = new SplitContainer();
            splitContainer2 = new SplitContainer();
            SourcesTXT_richTextBox = new RichTextBox();
            splitContainer3 = new SplitContainer();
            Copy_button = new Button();
            SourcesLanguage_comboBox = new ComboBox();
            ChangeLanguage_button = new Button();
            Clean_button = new Button();
            TargetLanguage_comboBox = new ComboBox();
            Translate_button = new Button();
            ForceRetranslate_checkBox = new CheckBox();
            TargetTxt_richTextBox = new RichTextBox();
            History_richTextBox = new RichTextBox();
            toolTip1 = new ToolTip(components);
            toolStrip1.SuspendLayout();
            statusStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer1).BeginInit();
            splitContainer1.Panel1.SuspendLayout();
            splitContainer1.Panel2.SuspendLayout();
            splitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer2).BeginInit();
            splitContainer2.Panel1.SuspendLayout();
            splitContainer2.Panel2.SuspendLayout();
            splitContainer2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer3).BeginInit();
            splitContainer3.Panel1.SuspendLayout();
            splitContainer3.Panel2.SuspendLayout();
            splitContainer3.SuspendLayout();
            SuspendLayout();
            // 
            // toolStrip1
            // 
            toolStrip1.ImageScalingSize = new Size(20, 20);
            toolStrip1.Items.AddRange(new ToolStripItem[] { Set_toolStripButton, Translator_toolStripLabel, Translator_toolStripComboBox, toolStripSeparator1 });
            toolStrip1.Location = new Point(0, 0);
            toolStrip1.Name = "toolStrip1";
            toolStrip1.Size = new Size(964, 27);
            toolStrip1.TabIndex = 0;
            toolStrip1.Text = "toolStrip1";
            // 
            // Set_toolStripButton
            // 
            Set_toolStripButton.DisplayStyle = ToolStripItemDisplayStyle.Image;
            Set_toolStripButton.Image = Properties.Resources.set_image;
            Set_toolStripButton.ImageTransparentColor = Color.Magenta;
            Set_toolStripButton.Name = "Set_toolStripButton";
            Set_toolStripButton.Size = new Size(24, 24);
            Set_toolStripButton.Text = "设置";
            // 
            // Translator_toolStripLabel
            // 
            Translator_toolStripLabel.Font = new Font("Microsoft YaHei UI", 11.25F, FontStyle.Regular, GraphicsUnit.Point, 134);
            Translator_toolStripLabel.Name = "Translator_toolStripLabel";
            Translator_toolStripLabel.Size = new Size(54, 24);
            Translator_toolStripLabel.Text = "翻译器";
            // 
            // Translator_toolStripComboBox
            // 
            Translator_toolStripComboBox.Items.AddRange(new object[] { "", "腾讯翻译", "百度翻译", "阿里翻译" });
            Translator_toolStripComboBox.Name = "Translator_toolStripComboBox";
            Translator_toolStripComboBox.Size = new Size(105, 27);
            Translator_toolStripComboBox.Text = "腾讯翻译";
            Translator_toolStripComboBox.ToolTipText = "选择需求的翻译器。";
            // 
            // toolStripSeparator1
            // 
            toolStripSeparator1.Name = "toolStripSeparator1";
            toolStripSeparator1.Size = new Size(6, 27);
            // 
            // statusStrip1
            // 
            statusStrip1.ImageScalingSize = new Size(20, 20);
            statusStrip1.Items.AddRange(new ToolStripItem[] { State_toolStripStatusLabel, StateInfo_toolStripStatusLabel });
            statusStrip1.Location = new Point(0, 566);
            statusStrip1.Name = "statusStrip1";
            statusStrip1.Padding = new Padding(1, 0, 16, 0);
            statusStrip1.Size = new Size(964, 25);
            statusStrip1.TabIndex = 1;
            statusStrip1.Text = "statusStrip1";
            // 
            // State_toolStripStatusLabel
            // 
            State_toolStripStatusLabel.Font = new Font("Microsoft YaHei UI", 11F);
            State_toolStripStatusLabel.Name = "State_toolStripStatusLabel";
            State_toolStripStatusLabel.Size = new Size(54, 20);
            State_toolStripStatusLabel.Text = "状态：";
            // 
            // StateInfo_toolStripStatusLabel
            // 
            StateInfo_toolStripStatusLabel.DisplayStyle = ToolStripItemDisplayStyle.Text;
            StateInfo_toolStripStatusLabel.Font = new Font("Microsoft YaHei UI", 11F);
            StateInfo_toolStripStatusLabel.Name = "StateInfo_toolStripStatusLabel";
            StateInfo_toolStripStatusLabel.Size = new Size(893, 20);
            StateInfo_toolStripStatusLabel.Spring = true;
            StateInfo_toolStripStatusLabel.Text = "准备开始吧~";
            StateInfo_toolStripStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
            StateInfo_toolStripStatusLabel.ToolTipText = "状态描述。";
            // 
            // splitContainer1
            // 
            splitContainer1.Dock = DockStyle.Fill;
            splitContainer1.Location = new Point(0, 27);
            splitContainer1.Margin = new Padding(3, 4, 3, 4);
            splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            splitContainer1.Panel1.Controls.Add(splitContainer2);
            splitContainer1.Panel1MinSize = 630;
            // 
            // splitContainer1.Panel2
            // 
            splitContainer1.Panel2.Controls.Add(History_richTextBox);
            splitContainer1.Size = new Size(964, 539);
            splitContainer1.SplitterDistance = 632;
            splitContainer1.TabIndex = 2;
            // 
            // splitContainer2
            // 
            splitContainer2.Dock = DockStyle.Fill;
            splitContainer2.Location = new Point(0, 0);
            splitContainer2.Name = "splitContainer2";
            splitContainer2.Orientation = Orientation.Horizontal;
            // 
            // splitContainer2.Panel1
            // 
            splitContainer2.Panel1.Controls.Add(SourcesTXT_richTextBox);
            splitContainer2.Panel1MinSize = 100;
            // 
            // splitContainer2.Panel2
            // 
            splitContainer2.Panel2.Controls.Add(splitContainer3);
            splitContainer2.Panel2MinSize = 150;
            splitContainer2.Size = new Size(632, 539);
            splitContainer2.SplitterDistance = 152;
            splitContainer2.TabIndex = 1;
            // 
            // SourcesTXT_richTextBox
            // 
            SourcesTXT_richTextBox.Dock = DockStyle.Fill;
            SourcesTXT_richTextBox.Location = new Point(0, 0);
            SourcesTXT_richTextBox.Margin = new Padding(3, 4, 3, 4);
            SourcesTXT_richTextBox.Name = "SourcesTXT_richTextBox";
            SourcesTXT_richTextBox.ScrollBars = RichTextBoxScrollBars.Vertical;
            SourcesTXT_richTextBox.Size = new Size(632, 152);
            SourcesTXT_richTextBox.TabIndex = 0;
            SourcesTXT_richTextBox.Text = "在这里输入要翻译的内容。";
            toolTip1.SetToolTip(SourcesTXT_richTextBox, "在这里输入要翻译的内容。");
            // 
            // splitContainer3
            // 
            splitContainer3.Dock = DockStyle.Fill;
            splitContainer3.FixedPanel = FixedPanel.Panel1;
            splitContainer3.IsSplitterFixed = true;
            splitContainer3.Location = new Point(0, 0);
            splitContainer3.Name = "splitContainer3";
            splitContainer3.Orientation = Orientation.Horizontal;
            // 
            // splitContainer3.Panel1
            // 
            splitContainer3.Panel1.Controls.Add(Copy_button);
            splitContainer3.Panel1.Controls.Add(SourcesLanguage_comboBox);
            splitContainer3.Panel1.Controls.Add(ChangeLanguage_button);
            splitContainer3.Panel1.Controls.Add(Clean_button);
            splitContainer3.Panel1.Controls.Add(TargetLanguage_comboBox);
            splitContainer3.Panel1.Controls.Add(Translate_button);
            splitContainer3.Panel1.Controls.Add(ForceRetranslate_checkBox);
            splitContainer3.Panel1MinSize = 40;
            // 
            // splitContainer3.Panel2
            // 
            splitContainer3.Panel2.Controls.Add(TargetTxt_richTextBox);
            splitContainer3.Panel2MinSize = 100;
            splitContainer3.Size = new Size(632, 383);
            splitContainer3.SplitterDistance = 42;
            splitContainer3.TabIndex = 0;
            // 
            // Copy_button
            // 
            Copy_button.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            Copy_button.AutoSize = true;
            Copy_button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Copy_button.Location = new Point(542, 5);
            Copy_button.Margin = new Padding(3, 4, 3, 4);
            Copy_button.Name = "Copy_button";
            Copy_button.Size = new Size(84, 31);
            Copy_button.TabIndex = 8;
            Copy_button.Text = "复制结果";
            toolTip1.SetToolTip(Copy_button, "点击复制翻译结果到剪切板。");
            Copy_button.UseVisualStyleBackColor = true;
            // 
            // SourcesLanguage_comboBox
            // 
            SourcesLanguage_comboBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            SourcesLanguage_comboBox.FormattingEnabled = true;
            SourcesLanguage_comboBox.Location = new Point(6, 7);
            SourcesLanguage_comboBox.Margin = new Padding(3, 4, 3, 4);
            SourcesLanguage_comboBox.Name = "SourcesLanguage_comboBox";
            SourcesLanguage_comboBox.Size = new Size(123, 29);
            SourcesLanguage_comboBox.TabIndex = 3;
            toolTip1.SetToolTip(SourcesLanguage_comboBox, "选择源语言");
            // 
            // ChangeLanguage_button
            // 
            ChangeLanguage_button.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            ChangeLanguage_button.AutoSize = true;
            ChangeLanguage_button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            ChangeLanguage_button.BackgroundImageLayout = ImageLayout.None;
            ChangeLanguage_button.Location = new Point(139, 5);
            ChangeLanguage_button.Margin = new Padding(3, 4, 3, 4);
            ChangeLanguage_button.Name = "ChangeLanguage_button";
            ChangeLanguage_button.Size = new Size(84, 31);
            ChangeLanguage_button.TabIndex = 4;
            ChangeLanguage_button.Text = "语言切换";
            ChangeLanguage_button.UseVisualStyleBackColor = true;
            // 
            // Clean_button
            // 
            Clean_button.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            Clean_button.AutoSize = true;
            Clean_button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Clean_button.Location = new Point(480, 5);
            Clean_button.Margin = new Padding(3, 4, 3, 4);
            Clean_button.Name = "Clean_button";
            Clean_button.Size = new Size(52, 31);
            Clean_button.TabIndex = 7;
            Clean_button.Text = "清空";
            toolTip1.SetToolTip(Clean_button, "点击清空输入框和译文框内容。");
            Clean_button.UseVisualStyleBackColor = true;
            // 
            // TargetLanguage_comboBox
            // 
            TargetLanguage_comboBox.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            TargetLanguage_comboBox.FormattingEnabled = true;
            TargetLanguage_comboBox.Location = new Point(228, 7);
            TargetLanguage_comboBox.Margin = new Padding(3, 4, 3, 4);
            TargetLanguage_comboBox.Name = "TargetLanguage_comboBox";
            TargetLanguage_comboBox.Size = new Size(124, 29);
            TargetLanguage_comboBox.TabIndex = 6;
            toolTip1.SetToolTip(TargetLanguage_comboBox, "选择目标语言");
            // 
            // Translate_button
            // 
            Translate_button.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            Translate_button.AutoSize = true;
            Translate_button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Translate_button.Location = new Point(361, 5);
            Translate_button.Margin = new Padding(3, 4, 3, 4);
            Translate_button.Name = "Translate_button";
            Translate_button.Size = new Size(52, 31);
            Translate_button.TabIndex = 5;
            Translate_button.Text = "翻译";
            toolTip1.SetToolTip(Translate_button, "点击翻译");
            Translate_button.UseVisualStyleBackColor = true;
            // 
            // ForceRetranslate_checkBox
            // 
            ForceRetranslate_checkBox.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            ForceRetranslate_checkBox.AutoSize = true;
            ForceRetranslate_checkBox.Font = new Font("Microsoft YaHei UI", 9F);
            ForceRetranslate_checkBox.Location = new Point(425, 13);
            ForceRetranslate_checkBox.Name = "ForceRetranslate_checkBox";
            ForceRetranslate_checkBox.Size = new Size(46, 21);
            ForceRetranslate_checkBox.TabIndex = 9;
            ForceRetranslate_checkBox.Text = "API";
            toolTip1.SetToolTip(ForceRetranslate_checkBox, "勾选后跳过缓存，强制调用API重新翻译");
            ForceRetranslate_checkBox.UseVisualStyleBackColor = true;
            // 
            // TargetTxt_richTextBox
            // 
            TargetTxt_richTextBox.Dock = DockStyle.Fill;
            TargetTxt_richTextBox.Location = new Point(0, 0);
            TargetTxt_richTextBox.Margin = new Padding(3, 4, 3, 4);
            TargetTxt_richTextBox.Name = "TargetTxt_richTextBox";
            TargetTxt_richTextBox.ScrollBars = RichTextBoxScrollBars.Vertical;
            TargetTxt_richTextBox.Size = new Size(632, 337);
            TargetTxt_richTextBox.TabIndex = 1;
            TargetTxt_richTextBox.Text = "点击翻译后会在这里显示翻译内容。";
            toolTip1.SetToolTip(TargetTxt_richTextBox, "点击翻译后会在这里显示翻译内容。");
            // 
            // History_richTextBox
            // 
            History_richTextBox.Dock = DockStyle.Fill;
            History_richTextBox.Location = new Point(0, 0);
            History_richTextBox.Margin = new Padding(4);
            History_richTextBox.Name = "History_richTextBox";
            History_richTextBox.Size = new Size(328, 539);
            History_richTextBox.TabIndex = 0;
            History_richTextBox.Text = "历史记录";
            toolTip1.SetToolTip(History_richTextBox, "历史翻译记录");
            // 
            // Main_Translate
            // 
            AutoScaleDimensions = new SizeF(10F, 21F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = SystemColors.Control;
            ClientSize = new Size(964, 591);
            Controls.Add(splitContainer1);
            Controls.Add(statusStrip1);
            Controls.Add(toolStrip1);
            Font = new Font("微软雅黑", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Margin = new Padding(3, 4, 3, 4);
            MinimumSize = new Size(980, 630);
            Name = "Main_Translate";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Main";
            toolStrip1.ResumeLayout(false);
            toolStrip1.PerformLayout();
            statusStrip1.ResumeLayout(false);
            statusStrip1.PerformLayout();
            splitContainer1.Panel1.ResumeLayout(false);
            splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer1).EndInit();
            splitContainer1.ResumeLayout(false);
            splitContainer2.Panel1.ResumeLayout(false);
            splitContainer2.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer2).EndInit();
            splitContainer2.ResumeLayout(false);
            splitContainer3.Panel1.ResumeLayout(false);
            splitContainer3.Panel1.PerformLayout();
            splitContainer3.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer3).EndInit();
            splitContainer3.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private ToolStrip toolStrip1;
        private StatusStrip statusStrip1;
        private SplitContainer splitContainer1;
        private RichTextBox SourcesTXT_richTextBox;
        private RichTextBox TargetTxt_richTextBox;
        private Button ChangeLanguage_button;
        private Button Translate_button;
        private ToolStripButton Set_toolStripButton;
        private ToolStripStatusLabel State_toolStripStatusLabel;
        private ToolStripStatusLabel StateInfo_toolStripStatusLabel;
        private ToolTip toolTip1;
        private ToolStripLabel Translator_toolStripLabel;
        private ToolStripComboBox Translator_toolStripComboBox;
        private RichTextBox History_richTextBox;
        private ComboBox TargetLanguage_comboBox;
        private ToolStripSeparator toolStripSeparator1;
        private Button Copy_button;
        private Button Clean_button;
        private SplitContainer splitContainer2;
        private SplitContainer splitContainer3;
        private ComboBox SourcesLanguage_comboBox;
        private CheckBox ForceRetranslate_checkBox;
    }
}
