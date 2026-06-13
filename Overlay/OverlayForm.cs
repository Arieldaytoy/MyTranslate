namespace MyTranslate.Overlay;

using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

/// <summary>
/// 浮窗基类 — 无边框、置顶、不抢焦点、圆角、淡入淡出
/// </summary>
public class OverlayForm : Form
{
    // ========== 窗口样式 ==========

    /// <summary>不抢焦点 + 工具窗口样式</summary>
    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= NativeMethods.WS_EX_NOACTIVATE
                       | NativeMethods.WS_EX_TOOLWINDOW
                       | NativeMethods.WS_EX_TOPMOST;
            return cp;
        }
    }

    // ========== 构造函数 ==========

    public OverlayForm()
    {
        // 基础窗口属性
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(245, 245, 250);  // 浅灰白背景
        Opacity = 0.9;
        DoubleBuffered = true;
        Padding = new Padding(12, 8, 12, 8);

        // ESC 关闭检测：因 WS_EX_NOACTIVATE 无法接收键盘事件，用轮询 GetAsyncKeyState
        _escTimer = new System.Windows.Forms.Timer { Interval = 100 };
        _escTimer.Tick += (s, e) =>
        {
            if (Visible && NativeMethods.IsEscPressed())
                Hide();
        };
    }

    private readonly System.Windows.Forms.Timer _escTimer;

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible) _escTimer.Start();
        else _escTimer.Stop();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _escTimer?.Stop();
            _escTimer?.Dispose();
        }
        base.Dispose(disposing);
    }

    // ========== 圆角绘制 ==========

    /// <summary>圆角半径（像素）</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int CornerRadius { get; set; } = 8;

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        // 绘制圆角边框
        using var pen = new Pen(Color.FromArgb(200, 200, 210), 1);
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = CreateRoundedRectPath(rect, CornerRadius);
        e.Graphics.DrawPath(pen, path);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);

        // 应用圆角区域
        var rect = new Rectangle(0, 0, Width, Height);
        using var path = CreateRoundedRectPath(rect, CornerRadius);
        Region = new Region(path);
    }

    /// <summary>创建圆角矩形路径</summary>
    protected static GraphicsPath CreateRoundedRectPath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;

        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();

        return path;
    }

    // ========== 位置计算 ==========

    /// <summary>
    /// 将浮窗定位到指定点附近，自动避让屏幕边缘
    /// </summary>
    /// <param name="anchorPoint">锚点（屏幕坐标）</param>
    /// <param name="offsetX">X 偏移</param>
    /// <param name="offsetY">Y 偏移</param>
    public void PositionNear(Point anchorPoint, int offsetX = 15, int offsetY = 15)
    {
        var screen = Screen.FromPoint(anchorPoint).WorkingArea;

        int x = anchorPoint.X + offsetX;
        int y = anchorPoint.Y + offsetY;

        // 右边界溢出：移到左侧
        if (x + Width > screen.Right)
            x = anchorPoint.X - Width - offsetX;

        // 下边界溢出：移到上方
        if (y + Height > screen.Bottom)
            y = anchorPoint.Y - Height - offsetY;

        // 确保不超出左/上边界
        x = Math.Max(screen.Left, x);
        y = Math.Max(screen.Top, y);

        Location = new Point(x, y);
    }

    /// <summary>
    /// 将浮窗定位到指定矩形的下方
    /// </summary>
    /// <param name="anchorRect">锚定矩形（屏幕坐标）</param>
    /// <param name="gap">与矩形的间距</param>
    public void PositionBelow(Rectangle anchorRect, int gap = 5)
    {
        var screen = Screen.FromPoint(anchorRect.Location).WorkingArea;

        int x = anchorRect.Left;
        int y = anchorRect.Bottom + gap;

        // 下边界溢出：移到上方
        if (y + Height > screen.Bottom)
            y = anchorRect.Top - Height - gap;

        // 右边界溢出
        if (x + Width > screen.Right)
            x = screen.Right - Width;

        x = Math.Max(screen.Left, x);
        y = Math.Max(screen.Top, y);

        Location = new Point(x, y);
    }

    // ========== 淡入淡出 ==========

    /// <summary>淡入显示</summary>
    public async Task FadeInAsync(int durationMs = 150)
    {
        Opacity = 0;
        Show();

        var steps = 10;
        var delay = durationMs / steps;

        for (int i = 1; i <= steps; i++)
        {
            Opacity = (double)i / steps;
            await Task.Delay(delay);
        }
    }

    /// <summary>淡出隐藏</summary>
    public async Task FadeOutAsync(int durationMs = 100)
    {
        var steps = 10;
        var delay = durationMs / steps;

        for (int i = steps; i >= 0; i--)
        {
            Opacity = (double)i / steps;
            await Task.Delay(delay);
        }

        Hide();
    }
}

/// <summary>
/// 浮窗所需的 Win32 API 声明（与 Capture 层的 NativeMethods 分开，避免循环引用）
/// </summary>
internal static class NativeMethods
{
    public const int WS_EX_NOACTIVATE = 0x08000000;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_TOPMOST = 0x00000008;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);

    /// <summary>检测 ESC 键是否被按下（VK_ESCAPE = 0x1B）</summary>
    public static bool IsEscPressed() => (GetAsyncKeyState(0x1B) & 0x8000) != 0;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern bool SetProcessDPIAware();

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern IntPtr WindowFromPoint(POINT point);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [System.Runtime.InteropServices.StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int x; public int y; }

    [System.Runtime.InteropServices.StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    public const uint GA_ROOTOWNER = 3;
}
