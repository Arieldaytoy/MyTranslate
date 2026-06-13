namespace MyTranslate.Overlay;

using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

/// <summary>
/// 区域截图浮窗 — 悬停自动描边窗口 + 拖拽框选 + 点击捕获
/// </summary>
public class CaptureOverlay : Form
{
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

    private const int MinSelectionSize = 10;
    private const int HoverThreshold = 8;
    private const int HoverDelayMs = 200;

    private enum CaptureState { Idle, Selecting }

    private CaptureState _state = CaptureState.Idle;
    private Point _startPoint;
    private Point _currentPoint;
    private bool _isDragging;
    private Rectangle? _hoveredWindow;
    private Point _lastMousePos;
    private DateTime _mouseStopTime;
    private bool _mouseStopped;
    private readonly System.Windows.Forms.Timer _inputTimer;
    private TaskCompletionSource<Rectangle?>? _tcs;

    public CaptureOverlay()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.Black;
        Opacity = 0.3;
        DoubleBuffered = true;
        Cursor = Cursors.Cross;

        var vs = SystemInformation.VirtualScreen;
        Location = vs.Location;
        Size = vs.Size;

        _inputTimer = new System.Windows.Forms.Timer { Interval = 16 };
        _inputTimer.Tick += OnInputTick;
    }

    public Task<Rectangle?> StartCaptureAsync()
    {
        _tcs = new TaskCompletionSource<Rectangle?>();
        _state = CaptureState.Idle;
        _startPoint = Point.Empty;
        _currentPoint = Point.Empty;
        _isDragging = false;
        _hoveredWindow = null;
        _lastMousePos = Cursor.Position;
        _mouseStopTime = DateTime.Now;
        _mouseStopped = false;

        var vs = SystemInformation.VirtualScreen;
        Location = vs.Location;
        Size = vs.Size;

        Show();
        BringToFront();
        _inputTimer.Start();
        return _tcs.Task;
    }

    private void OnInputTick(object? sender, EventArgs e)
    {
        if (_tcs == null || _tcs.Task.IsCompleted) return;

        // ESC 取消
        if ((NativeMethods.GetAsyncKeyState(0x1B) & 0x8000) != 0)
        {
            Complete(null);
            return;
        }

        bool lButtonDown = (NativeMethods.GetAsyncKeyState(0x01) & 0x8000) != 0;
        var mousePos = Cursor.Position;

        switch (_state)
        {
            case CaptureState.Idle:
                // 鼠标移动时重置计时，停止后才检测窗口
                if (mousePos != _lastMousePos)
                {
                    _lastMousePos = mousePos;
                    _mouseStopTime = DateTime.Now;
                    _mouseStopped = false;
                    _hoveredWindow = null;
                    Invalidate();
                }
                else if (!_mouseStopped && (DateTime.Now - _mouseStopTime).TotalMilliseconds >= HoverDelayMs)
                {
                    _mouseStopped = true;
                    var winRect = GetWindowAtPoint(mousePos);
                    if (winRect != _hoveredWindow)
                    {
                        _hoveredWindow = winRect;
                        Invalidate();
                    }
                }

                if (lButtonDown)
                {
                    _startPoint = mousePos;
                    _currentPoint = mousePos;
                    _isDragging = false;
                    _state = CaptureState.Selecting;
                }
                break;

            case CaptureState.Selecting:
                _currentPoint = mousePos;

                if (!_isDragging)
                {
                    int dx = Math.Abs(_currentPoint.X - _startPoint.X);
                    int dy = Math.Abs(_currentPoint.Y - _startPoint.Y);
                    if (dx > HoverThreshold || dy > HoverThreshold)
                        _isDragging = true;
                }

                if (_isDragging)
                    Invalidate();

                if (!lButtonDown)
                {
                    if (_isDragging)
                    {
                        var rect = GetSelectionRect();
                        if (rect.Width >= MinSelectionSize && rect.Height >= MinSelectionSize)
                            Complete(rect);
                        else
                            Complete(null);
                    }
                    else
                    {
                        // 点击：捕获悬停窗口
                        if (_hoveredWindow.HasValue)
                            Complete(_hoveredWindow.Value);
                        else
                            Complete(null);
                    }
                }
                break;
        }
    }

    private Rectangle GetSelectionRect()
    {
        int x = Math.Min(_startPoint.X, _currentPoint.X);
        int y = Math.Min(_startPoint.Y, _currentPoint.Y);
        int w = Math.Abs(_currentPoint.X - _startPoint.X);
        int h = Math.Abs(_currentPoint.Y - _startPoint.Y);
        return new Rectangle(x, y, w, h);
    }

    private Rectangle? GetWindowAtPoint(Point screenPoint)
    {
        // 临时隐藏遮罩，检测下方窗口
        bool wasVisible = Visible;
        if (wasVisible) base.Visible = false;

        var pt = new NativeMethods.POINT { x = screenPoint.X, y = screenPoint.Y };
        IntPtr hWnd = NativeMethods.WindowFromPoint(pt);

        if (wasVisible) base.Visible = true;

        if (hWnd == IntPtr.Zero) return null;

        hWnd = NativeMethods.GetAncestor(hWnd, NativeMethods.GA_ROOTOWNER);
        if (hWnd == IntPtr.Zero) return null;

        if (NativeMethods.GetWindowRect(hWnd, out var rect))
        {
            int w = rect.Right - rect.Left;
            int h = rect.Bottom - rect.Top;
            if (w > 10 && h > 10)
                return new Rectangle(rect.Left, rect.Top, w, h);
        }

        return null;
    }

    private void Complete(Rectangle? result)
    {
        _inputTimer.Stop();
        Hide();
        _tcs?.TrySetResult(result);
        _state = CaptureState.Idle;
        _hoveredWindow = null;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        if (_state == CaptureState.Idle && _hoveredWindow.HasValue)
        {
            // 悬停模式：绘制窗口高亮边框
            DrawWindowHighlight(g, _hoveredWindow.Value);
        }
        else if (_state == CaptureState.Selecting && _isDragging)
        {
            // 拖拽模式：绘制选区
            var rect = GetSelectionRect();
            if (rect.Width > 0 && rect.Height > 0)
                DrawSelectionRect(g, rect);
        }
    }

    private void DrawWindowHighlight(Graphics g, Rectangle windowRect)
    {
        // 半透明蓝色边框
        using var pen = new Pen(Color.FromArgb(220, 33, 150, 243), 3);
        g.DrawRectangle(pen, windowRect);

        // 四角标记
        int markLen = 20;
        using var markPen = new Pen(Color.FromArgb(255, 33, 150, 243), 4);

        // 左上
        g.DrawLine(markPen, windowRect.Left, windowRect.Top, windowRect.Left + markLen, windowRect.Top);
        g.DrawLine(markPen, windowRect.Left, windowRect.Top, windowRect.Left, windowRect.Top + markLen);
        // 右上
        g.DrawLine(markPen, windowRect.Right, windowRect.Top, windowRect.Right - markLen, windowRect.Top);
        g.DrawLine(markPen, windowRect.Right, windowRect.Top, windowRect.Right, windowRect.Top + markLen);
        // 左下
        g.DrawLine(markPen, windowRect.Left, windowRect.Bottom, windowRect.Left + markLen, windowRect.Bottom);
        g.DrawLine(markPen, windowRect.Left, windowRect.Bottom, windowRect.Left, windowRect.Bottom - markLen);
        // 右下
        g.DrawLine(markPen, windowRect.Right, windowRect.Bottom, windowRect.Right - markLen, windowRect.Bottom);
        g.DrawLine(markPen, windowRect.Right, windowRect.Bottom, windowRect.Right, windowRect.Bottom - markLen);

        // 尺寸文字
        var sizeText = $"{windowRect.Width} × {windowRect.Height}";
        using var font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold);
        var textSize = g.MeasureString(sizeText, font);
        float textX = windowRect.Left + (windowRect.Width - textSize.Width) / 2;
        float textY = windowRect.Bottom + 8;

        var screen = Screen.FromPoint(new Point(windowRect.Left, windowRect.Bottom)).WorkingArea;
        if (textY + textSize.Height > screen.Bottom)
            textY = windowRect.Top - textSize.Height - 8;

        using var bgBrush = new SolidBrush(Color.FromArgb(200, 33, 150, 243));
        g.FillRectangle(bgBrush, textX - 6, textY - 2, textSize.Width + 12, textSize.Height + 4);
        using var textBrush = new SolidBrush(Color.White);
        g.DrawString(sizeText, font, textBrush, textX, textY);
    }

    private void DrawSelectionRect(Graphics g, Rectangle rect)
    {
        using var pen = new Pen(Color.White, 2) { DashStyle = DashStyle.Dash, DashCap = DashCap.Round };
        g.DrawRectangle(pen, rect);

        using var shadowPen = new Pen(Color.FromArgb(120, 0, 0, 0), 1);
        g.DrawRectangle(shadowPen, rect.X - 1, rect.Y - 1, rect.Width + 2, rect.Height + 2);

        var sizeText = $"{rect.Width} × {rect.Height}";
        using var font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold);
        var textSize = g.MeasureString(sizeText, font);

        float textX = rect.Right + 8;
        float textY = rect.Bottom + 4;
        var screen = Screen.FromPoint(new Point(rect.Right, rect.Bottom)).WorkingArea;
        if (textX + textSize.Width > screen.Right) textX = rect.Right - textSize.Width;
        if (textY + textSize.Height > screen.Bottom) textY = rect.Top - textSize.Height - 4;

        using var bgBrush = new SolidBrush(Color.FromArgb(180, 0, 0, 0));
        g.FillRectangle(bgBrush, textX - 4, textY - 2, textSize.Width + 8, textSize.Height + 4);
        using var textBrush = new SolidBrush(Color.White);
        g.DrawString(sizeText, font, textBrush, textX, textY);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inputTimer?.Stop();
            _inputTimer?.Dispose();
        }
        base.Dispose(disposing);
    }
}
