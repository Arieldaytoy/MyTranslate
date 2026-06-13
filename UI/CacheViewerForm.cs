namespace MyTranslate.UI;

using MyTranslate.Core;

/// <summary>
/// 翻译缓存查看器 — 查看/编辑/导出/导入缓存，支持多义词
/// </summary>
public class CacheViewerForm : Form
{
    private readonly TranslationHistoryManager _history;
    private DataGridView _grid = null!;
    private TextBox _searchBox = null!;
    private Label _statusLabel = null!;
    private bool _isDirty;

    // 语言显示名称列表（不含"自动识别"，用于编辑列的下拉框）
    private static readonly string[] _langDisplayNames =
        LanguageInfo.SupportedLanguages.Select(l => l.DisplayName).ToArray();

    private static readonly string[] _langDisplayNamesNoAuto =
        LanguageInfo.SupportedLanguages.Where(l => l.Language != Language.Auto).Select(l => l.DisplayName).ToArray();

    public CacheViewerForm(TranslationHistoryManager history)
    {
        _history = history;
        InitializeComponents();
        LoadEntries();
    }

    private void InitializeComponents()
    {
        Text = "翻译缓存历史";
        Size = new Size(950, 550);
        MinimumSize = new Size(700, 400);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Microsoft YaHei UI", 10f);

        // ===== 顶部工具栏（两行） =====
        var toolPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 80,
            Padding = new Padding(5, 3, 5, 3),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
        };

        _searchBox = new TextBox { Width = 180, PlaceholderText = "搜索原文或译文..." };
        _searchBox.TextChanged += OnSearchChanged;

        // 第一行按钮
        var refreshBtn = new Button { Text = "刷新", AutoSize = true };
        refreshBtn.Click += (s, e) => { SavePendingChanges(); LoadEntries(); };

        var saveBtn = new Button { Text = "保存修改", AutoSize = true, ForeColor = Color.Green };
        saveBtn.Click += OnSaveClicked;

        var addBtn = new Button { Text = "+ 新增词条", AutoSize = true };
        addBtn.Click += OnAddClicked;

        var deleteBtn = new Button { Text = "删除选中", AutoSize = true, ForeColor = Color.Red };
        deleteBtn.Click += OnDeleteClicked;

        // 第二行按钮
        var exportCsvBtn = new Button { Text = "导出 CSV", AutoSize = true };
        exportCsvBtn.Click += OnExportCsvClicked;

        var exportJsonBtn = new Button { Text = "导出 JSON", AutoSize = true };
        exportJsonBtn.Click += OnExportJsonClicked;

        var importCsvBtn = new Button { Text = "导入 CSV", AutoSize = true };
        importCsvBtn.Click += OnImportCsvClicked;

        var importJsonBtn = new Button { Text = "导入 JSON", AutoSize = true };
        importJsonBtn.Click += OnImportJsonClicked;

        var clearBtn = new Button { Text = "清空缓存", AutoSize = true, ForeColor = Color.Red };
        clearBtn.Click += OnClearClicked;

        toolPanel.Controls.AddRange([
            new Label { Text = "搜索：", AutoSize = true, Padding = new Padding(0, 5, 0, 0) },
            _searchBox, refreshBtn, saveBtn, addBtn, deleteBtn,
            exportCsvBtn, exportJsonBtn, importCsvBtn, importJsonBtn, clearBtn,
        ]);

        // ===== 状态栏 =====
        _statusLabel = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 25,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(5, 0, 0, 0),
            BackColor = Color.FromArgb(245, 245, 245),
        };

        // ===== 数据表格（可编辑） =====
        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            RowHeadersVisible = false,
            MultiSelect = true,
            EditMode = DataGridViewEditMode.EditOnEnter,
        };

        // 可编辑列
        var originalCol = new DataGridViewTextBoxColumn { Name = "Original", HeaderText = "原文", FillWeight = 22 };
        var translatedCol = new DataGridViewTextBoxColumn { Name = "Translated", HeaderText = "译文（多义词用分号分隔）", FillWeight = 28 };

        // 语言下拉列
        var selectedLangCol = new DataGridViewComboBoxColumn
        {
            Name = "SelectedLang", HeaderText = "选择语言", FillWeight = 12,
            DataSource = _langDisplayNames, FlatStyle = FlatStyle.Flat,
        };
        var detectedLangCol = new DataGridViewComboBoxColumn
        {
            Name = "DetectedLang", HeaderText = "识别语言", FillWeight = 12,
            DataSource = _langDisplayNamesNoAuto, FlatStyle = FlatStyle.Flat,
        };
        var targetLangCol = new DataGridViewComboBoxColumn
        {
            Name = "TargetLang", HeaderText = "目标语言", FillWeight = 12,
            DataSource = _langDisplayNamesNoAuto, FlatStyle = FlatStyle.Flat,
        };

        // 只读列
        var translatorCol = new DataGridViewTextBoxColumn { Name = "Translator", HeaderText = "翻译器", FillWeight = 8, ReadOnly = true };
        var timeCol = new DataGridViewTextBoxColumn { Name = "Time", HeaderText = "时间", FillWeight = 8, ReadOnly = true };

        _grid.Columns.AddRange([originalCol, translatedCol, selectedLangCol, detectedLangCol, targetLangCol, translatorCol, timeCol]);

        // 跟踪修改
        _grid.CellValueChanged += (s, e) => _isDirty = true;
        _grid.CurrentCellDirtyStateChanged += (s, e) =>
        {
            if (_grid.IsCurrentCellDirty)
                _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };

        Controls.Add(_grid);
        Controls.Add(toolPanel);
        Controls.Add(_statusLabel);

        // 关闭窗口时提示保存
        FormClosing += (s, e) =>
        {
            if (_isDirty)
            {
                var result = MessageBox.Show("有未保存的修改，是否保存？", "提示",
                    MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (result == DialogResult.Cancel) { e.Cancel = true; return; }
                if (result == DialogResult.Yes) SavePendingChanges();
            }
        };
    }

    // ========== 数据加载 ==========

    private void LoadEntries(string filter = "")
    {
        _grid.Rows.Clear();
        var entries = _history.GetAllEntries();
        int shown = 0;

        foreach (var entry in entries)
        {
            if (!string.IsNullOrEmpty(filter))
            {
                var f = filter.ToLowerInvariant();
                if (!entry.OriginalText.ToLowerInvariant().Contains(f)
                    && !entry.TranslatedText.ToLowerInvariant().Contains(f))
                    continue;
            }

            var selectedName = GetLangName(entry.SelectedSourceLanguage);
            var detectedName = GetLangName(entry.SourceLanguage);
            var tgtName = GetLangName(entry.TargetLanguage);

            _grid.Rows.Add(
                entry.OriginalText,
                entry.TranslatedText,
                selectedName,
                detectedName,
                tgtName,
                entry.TranslatorName,
                entry.Timestamp.ToString("MM-dd HH:mm")
            );
            shown++;
        }

        _isDirty = false;
        _statusLabel.Text = $"共 {_history.Count} 条缓存" +
            (string.IsNullOrEmpty(filter) ? "" : $"，筛选显示 {shown} 条");
    }

    // ========== 保存修改 ==========

    private void SavePendingChanges()
    {
        if (!_isDirty) return;

        _grid.EndEdit();
        int saved = 0;

        // 增量更新：只更新当前表格中的条目，不删除未显示的条目
        for (int i = 0; i < _grid.Rows.Count; i++)
        {
            var row = _grid.Rows[i];
            var original = (row.Cells["Original"].Value ?? "").ToString()?.Trim() ?? "";
            var translated = (row.Cells["Translated"].Value ?? "").ToString()?.Trim() ?? "";
            var selectedLangStr = (row.Cells["SelectedLang"].Value ?? "").ToString() ?? "";
            var detectedLangStr = (row.Cells["DetectedLang"].Value ?? "").ToString() ?? "";
            var targetLangStr = (row.Cells["TargetLang"].Value ?? "").ToString() ?? "";
            var translator = (row.Cells["Translator"].Value ?? "").ToString() ?? "手动编辑";

            if (string.IsNullOrEmpty(original) || string.IsNullOrEmpty(translated))
                continue;

            var selectedLang = ParseLangName(selectedLangStr);
            var detectedLang = ParseLangName(detectedLangStr);
            var targetLang = ParseLangName(targetLangStr);

            if (detectedLang == Language.Auto) detectedLang = Language.Chinese; // 默认
            if (targetLang == Language.Auto) targetLang = Language.English;

            // 用 UpdateTranslation 覆盖已有条目，或 Save 新增条目
            var existing = _history.Lookup(original, detectedLang, targetLang);
            if (existing != null && existing.TranslatedText != translated)
            {
                _history.UpdateTranslation(original, detectedLang, targetLang, translated);
            }
            else if (existing == null)
            {
                _history.Save(original, translated, selectedLang, detectedLang, targetLang, translator);
            }
            saved++;
        }

        _history.SaveToFile();
        _isDirty = false;
        _statusLabel.Text = $"已更新 {saved} 条缓存，共 {_history.Count} 条";
        LoadEntries(_searchBox.Text.Trim());
    }

    private void OnSaveClicked(object? sender, EventArgs e)
    {
        SavePendingChanges();
        MessageBox.Show("缓存已保存。", "保存成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    // ========== 新增词条 ==========

    private void OnAddClicked(object? sender, EventArgs e)
    {
        _grid.Rows.Insert(0,
            "",             // 原文
            "",             // 译文
            "自动识别",      // 选择语言
            "中文",          // 识别语言
            "英语",          // 目标语言
            "手动添加",      // 翻译器
            DateTime.Now.ToString("MM-dd HH:mm")
        );
        _grid.CurrentCell = _grid.Rows[0].Cells["Original"];
        _grid.BeginEdit(true);
        _isDirty = true;
    }

    // ========== 删除 ==========

    private void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (_grid.SelectedRows.Count == 0) return;

        var count = _grid.SelectedRows.Count;
        var result = MessageBox.Show(
            $"确定要删除选中的 {count} 条缓存吗？",
            "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

        if (result == DialogResult.Yes)
        {
            var rowsToDelete = _grid.SelectedRows.Cast<DataGridViewRow>().ToList();
            foreach (var row in rowsToDelete)
            {
                var original = (row.Cells["Original"].Value ?? "").ToString()?.Trim() ?? "";
                var detectedLangStr = (row.Cells["DetectedLang"].Value ?? "").ToString() ?? "";
                var targetLangStr = (row.Cells["TargetLang"].Value ?? "").ToString() ?? "";
                var detectedLang = ParseLangName(detectedLangStr);
                var targetLang = ParseLangName(targetLangStr);

                if (!string.IsNullOrEmpty(original))
                    _history.RemoveEntry(original, detectedLang, targetLang);

                _grid.Rows.Remove(row);
            }

            _history.SaveToFile();
            _isDirty = false;
            _statusLabel.Text = $"共 {_history.Count} 条缓存（已删除 {count} 条）";
        }
    }

    // ========== 导出 ==========

    private void OnExportCsvClicked(object? sender, EventArgs e)
    {
        SavePendingChanges();
        using var dlg = new SaveFileDialog
        {
            Filter = "CSV 文件|*.csv",
            FileName = $"翻译缓存_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
        };
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            try
            {
                _history.ExportToCsv(dlg.FileName);
                MessageBox.Show($"已导出 {_history.Count} 条缓存到：\n{dlg.FileName}",
                    "导出成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void OnExportJsonClicked(object? sender, EventArgs e)
    {
        SavePendingChanges();
        using var dlg = new SaveFileDialog
        {
            Filter = "JSON 文件|*.json",
            FileName = $"翻译缓存_{DateTime.Now:yyyyMMdd_HHmmss}.json",
        };
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            try
            {
                _history.ExportToJson(dlg.FileName);
                MessageBox.Show($"已导出 {_history.Count} 条缓存到：\n{dlg.FileName}",
                    "导出成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    // ========== 导入 ==========

    private void OnImportCsvClicked(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "CSV 文件|*.csv",
            Title = "选择要导入的 CSV 文件",
        };
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            try
            {
                var count = _history.ImportFromCsv(dlg.FileName);
                LoadEntries();
                MessageBox.Show($"已导入 {count} 条记录。",
                    "导入成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导入失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void OnImportJsonClicked(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "JSON 文件|*.json",
            Title = "选择要导入的 JSON 文件",
        };
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            try
            {
                var count = _history.ImportFromJson(dlg.FileName);
                LoadEntries();
                MessageBox.Show($"已导入 {count} 条记录。",
                    "导入成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导入失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    // ========== 清空 ==========

    private void OnClearClicked(object? sender, EventArgs e)
    {
        var result = MessageBox.Show(
            $"确定要清空所有 {_history.Count} 条翻译缓存吗？\n建议先导出备份再清空。",
            "确认清空", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

        if (result == DialogResult.Yes)
        {
            _history.Clear();
            _isDirty = false;
            LoadEntries();
        }
    }

    // ========== 搜索 ==========

    private void OnSearchChanged(object? sender, EventArgs e)
    {
        LoadEntries(_searchBox.Text.Trim());
    }

    // ========== 辅助方法 ==========

    private static string GetLangName(Language lang)
        => LanguageInfo.SupportedLanguages.FirstOrDefault(l => l.Language == lang)?.DisplayName ?? lang.ToString();

    private static Language ParseLangName(string name)
        => LanguageInfo.SupportedLanguages.FirstOrDefault(l => l.DisplayName == name)?.Language ?? Language.Auto;
}
