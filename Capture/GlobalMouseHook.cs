namespace MyTranslate.Capture;

using System.Runtime.InteropServices;

/// <summary>
/// 全局鼠标钩子 — 监听全系统的鼠标事件
/// </summary>
public class GlobalMouseHook : IDisposable
{
    // ========== Win32 常量 ==========
    private const int WH_MOUSE_LL = 14;

    private const int WM_MOUSEMOVE = 0x0200;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_MOUSEWHEEL = 0x020A;

    // ========== 事件 ==========

    /// <summary>鼠标移动时触发，参数为屏幕坐标</summary>
    public event EventHandler<Point>? MouseMoved;

    /// <summary>鼠标停留超过指定时间后触发，参数为屏幕坐标</summary>
    public event EventHandler<Point>? MouseHovered;

    /// <summary>鼠标左键点击时触发</summary>
    public event EventHandler<Point>? MouseClicked;

    /// <summary>鼠标左键释放时触发（用于检测选词完成）</summary>
    public event EventHandler<Point>? MouseReleased;

    // ========== 内部状态 ==========

    private IntPtr _hookId = IntPtr.Zero;
    private NativeMethods.LowLevelMouseProc? _hookProc; // 保持引用防止 GC 回收
    private System.Windows.Forms.Timer? _hoverTimer;
    private Point _lastPosition;
    private readonly int _hoverDelayMs;
    private bool _disposed;

    /// <summary>
    /// 创建全局鼠标钩子
    /// </summary>
    /// <param name="hoverDelayMs">悬停检测延迟（毫秒），默认 500</param>
    public GlobalMouseHook(int hoverDelayMs = 500)
    {
        _hoverDelayMs = hoverDelayMs;
    }

    /// <summary>启动鼠标钩子</summary>
    public void Start()
    {
        if (_hookId != IntPtr.Zero) return;

        _hookProc = HookCallback;
        _hookId = SetHook(_hookProc);

        // 初始化悬停计时器
        _hoverTimer = new System.Windows.Forms.Timer { Interval = _hoverDelayMs };
        _hoverTimer.Tick += OnHoverTimerTick;
    }

    /// <summary>停止鼠标钩子</summary>
    public void Stop()
    {
        _hoverTimer?.Stop();
        _hoverTimer?.Dispose();
        _hoverTimer = null;

        if (_hookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }

    /// <summary>更新悬停延迟时间（需要 Stop + Start 后生效）</summary>
    public void UpdateHoverDelay(int delayMs)
    {
        if (_hoverTimer != null)
            _hoverTimer.Interval = delayMs;
    }

    // ========== 内部实现 ==========

    private static IntPtr SetHook(NativeMethods.LowLevelMouseProc proc)
    {
        using var curProcess = System.Diagnostics.Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule!;
        return NativeMethods.SetWindowsHookEx(
            WH_MOUSE_LL, proc,
            NativeMethods.GetModuleHandle(curModule.ModuleName), 0);
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var hookStruct = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
            var point = new Point(hookStruct.pt.x, hookStruct.pt.y);
            int msg = wParam.ToInt32();

            switch (msg)
            {
                case WM_MOUSEMOVE:
                    HandleMouseMove(point);
                    break;
                case WM_LBUTTONDOWN:
                    _hoverTimer?.Stop();
                    MouseClicked?.Invoke(this, point);
                    break;
                case WM_LBUTTONUP:
                    MouseReleased?.Invoke(this, point);
                    break;
                case WM_RBUTTONDOWN:
                case WM_MOUSEWHEEL:
                    _hoverTimer?.Stop();
                    break;
            }
        }

        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private void HandleMouseMove(Point currentPos)
    {
        // 鼠标位置变化时才重置计时器
        if (currentPos != _lastPosition)
        {
            _lastPosition = currentPos;
            MouseMoved?.Invoke(this, currentPos);

            _hoverTimer?.Stop();
            _hoverTimer?.Start();
        }
    }

    private void OnHoverTimerTick(object? sender, EventArgs e)
    {
        _hoverTimer?.Stop();
        MouseHovered?.Invoke(this, _lastPosition);
    }

    // ========== 释放 ==========

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        GC.SuppressFinalize(this);
    }

    ~GlobalMouseHook() => Dispose();
}

/// <summary>
/// Win32 API 声明
/// </summary>
internal static class NativeMethods
{
    public delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn,
        IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
        IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr GetModuleHandle(string lpModuleName);

    // Hotkey 相关
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // Window style 相关
    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_NOACTIVATE = 0x08000000;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_TOPMOST = 0x00000008;

    [DllImport("user32.dll")]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}
