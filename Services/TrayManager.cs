namespace MyTranslate.Services;

/// <summary>
/// 系统托盘管理器 — 托盘图标、右键菜单、状态切换
/// </summary>
public class TrayManager : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private bool _isTranslationEnabled;
    private bool _disposed;

    // ========== 事件 ==========

    /// <summary>用户点击「开关翻译」时触发</summary>
    public event EventHandler<bool>? ToggleTranslationRequested;

    /// <summary>用户点击「打开主窗口」时触发</summary>
    public event EventHandler? ShowMainWindowRequested;

    /// <summary>用户点击「设置」时触发</summary>
    public event EventHandler? OpenSettingsRequested;

    /// <summary>用户点击「退出」时触发</summary>
    public event EventHandler? ExitRequested;

    // ========== 构造函数 ==========

    public TrayManager()
    {
        _notifyIcon = new NotifyIcon
        {
            Visible = false,
            Text = "MyTranslate 翻译工具",
        };

        // 右键菜单
        var contextMenu = new ContextMenuStrip();

        var toggleItem = new ToolStripMenuItem("开启翻译 (Ctrl+Y)")
        {
            Name = "toggleItem",
        };
        toggleItem.Click += (s, e) =>
        {
            _isTranslationEnabled = !_isTranslationEnabled;
            UpdateToggleMenuItem();
            ToggleTranslationRequested?.Invoke(this, _isTranslationEnabled);
        };

        var showItem = new ToolStripMenuItem("打开主窗口");
        showItem.Click += (s, e) => ShowMainWindowRequested?.Invoke(this, EventArgs.Empty);

        var settingsItem = new ToolStripMenuItem("设置");
        settingsItem.Click += (s, e) => OpenSettingsRequested?.Invoke(this, EventArgs.Empty);

        var exitItem = new ToolStripMenuItem("退出");
        exitItem.Click += (s, e) => ExitRequested?.Invoke(this, EventArgs.Empty);

        contextMenu.Items.AddRange([toggleItem, new ToolStripSeparator(), showItem, settingsItem, new ToolStripSeparator(), exitItem]);
        _notifyIcon.ContextMenuStrip = contextMenu;

        // 双击托盘图标 = 打开主窗口
        _notifyIcon.DoubleClick += (s, e) => ShowMainWindowRequested?.Invoke(this, EventArgs.Empty);

        // 设置初始图标
        UpdateIcon();
    }

    // ========== 公共方法 ==========

    /// <summary>显示托盘图标</summary>
    public void Show()
    {
        _notifyIcon.Visible = true;
    }

    /// <summary>隐藏托盘图标</summary>
    public void Hide()
    {
        _notifyIcon.Visible = false;
    }

    /// <summary>设置翻译开关状态（同步更新图标和菜单文字）</summary>
    public void SetTranslationEnabled(bool enabled)
    {
        _isTranslationEnabled = enabled;
        UpdateToggleMenuItem();
        UpdateIcon();
        UpdateTooltip();
    }

    /// <summary>获取当前翻译开关状态</summary>
    public bool IsTranslationEnabled => _isTranslationEnabled;

    /// <summary>显示气泡提示（如快捷键触发时的通知）</summary>
    public void ShowBalloonTip(string title, string text, int timeoutMs = 2000)
    {
        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = text;
        _notifyIcon.ShowBalloonTip(timeoutMs);
    }

    // ========== 内部方法 ==========

    private void UpdateToggleMenuItem()
    {
        if (_notifyIcon.ContextMenuStrip?.Items["toggleItem"] is ToolStripMenuItem item)
        {
            item.Text = _isTranslationEnabled ? "关闭翻译 (Ctrl+Y)" : "开启翻译 (Ctrl+Y)";
        }
    }

    private void UpdateIcon()
    {
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "app_icon.ico");
            if (File.Exists(iconPath))
            {
                _notifyIcon.Icon = new Icon(iconPath, 16, 16);
            }
            else
            {
                _notifyIcon.Icon = SystemIcons.Application;
            }
        }
        catch
        {
            _notifyIcon.Icon = SystemIcons.Application;
        }
    }

    private void UpdateTooltip()
    {
        var status = _isTranslationEnabled ? "已开启" : "已关闭";
        _notifyIcon.Text = $"MyTranslate - 翻译{status}\nCtrl+Y 切换";
    }

    // ========== 释放 ==========

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        GC.SuppressFinalize(this);
    }
}
