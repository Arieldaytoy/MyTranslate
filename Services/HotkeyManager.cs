namespace MyTranslate.Services;

using System.Runtime.InteropServices;

/// <summary>
/// 全局快捷键管理器 — 基于 Win32 RegisterHotKey API
/// </summary>
public class HotkeyManager : IDisposable
{
    // ========== 修饰键常量 ==========
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;

    public const int WM_HOTKEY = 0x0312;

    // ========== 内部状态 ==========

    private readonly Dictionary<int, string> _registeredHotkeys = []; // id → 快捷键名
    private int _nextId = 1;
    private bool _disposed;

    /// <summary>快捷键被按下时触发，参数为快捷键 ID</summary>
    public event EventHandler<int>? HotkeyPressed;

    // ========== 注册/注销 ==========

    /// <summary>
    /// 注册全局快捷键
    /// </summary>
    /// <param name="hotkeyString">快捷键字符串，如 "Ctrl+Y"、"Ctrl+Shift+T"</param>
    /// <param name="targetForm">用于接收 WM_HOTKEY 消息的窗口</param>
    /// <returns>快捷键 ID，失败返回 -1</returns>
    public int Register(string hotkeyString, Form targetForm)
    {
        var (modifiers, vk) = ParseHotkeyString(hotkeyString);
        if (vk == 0) return -1;

        int id = _nextId++;

        bool success = RegisterHotKey(targetForm.Handle, id, modifiers, vk);
        if (success)
        {
            _registeredHotkeys[id] = hotkeyString;
            return id;
        }

        return -1;
    }

    /// <summary>注销指定 ID 的快捷键</summary>
    public void Unregister(int hotkeyId, Form targetForm)
    {
        UnregisterHotKey(targetForm.Handle, hotkeyId);
        _registeredHotkeys.Remove(hotkeyId);
    }

    /// <summary>注销所有快捷键</summary>
    public void UnregisterAll(Form targetForm)
    {
        foreach (var id in _registeredHotkeys.Keys)
        {
            UnregisterHotKey(targetForm.Handle, id);
        }
        _registeredHotkeys.Clear();
    }

    /// <summary>
    /// 处理 WM_HOTKEY 消息 — 在主窗口的 WndProc 中调用此方法
    /// </summary>
    public void ProcessMessage(Message msg)
    {
        if (msg.Msg == WM_HOTKEY)
        {
            int hotkeyId = msg.WParam.ToInt32();
            if (_registeredHotkeys.ContainsKey(hotkeyId))
            {
                HotkeyPressed?.Invoke(this, hotkeyId);
            }
        }
    }

    // ========== 解析快捷键字符串 ==========

    /// <summary>
    /// 将 "Ctrl+Shift+Y" 解析为 Win32 修饰键和虚拟键码
    /// </summary>
    public static (uint modifiers, uint vk) ParseHotkeyString(string hotkeyString)
    {
        uint modifiers = 0;
        uint vk = 0;

        if (string.IsNullOrWhiteSpace(hotkeyString))
            return (0, 0);

        var parts = hotkeyString.Split('+', StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            switch (part.ToUpperInvariant())
            {
                case "CTRL":
                case "CONTROL":
                    modifiers |= MOD_CONTROL;
                    break;
                case "ALT":
                    modifiers |= MOD_ALT;
                    break;
                case "SHIFT":
                    modifiers |= MOD_SHIFT;
                    break;
                case "WIN":
                    modifiers |= MOD_WIN;
                    break;
                default:
                    // 最后一个部分应该是键名
                    vk = ParseKeyName(part);
                    break;
            }
        }

        return (modifiers, vk);
    }

    /// <summary>
    /// 将键名解析为虚拟键码
    /// </summary>
    private static uint ParseKeyName(string keyName)
    {
        if (string.IsNullOrEmpty(keyName)) return 0;

        // 单字母键
        if (keyName.Length == 1 && char.IsLetterOrDigit(keyName[0]))
            return (uint)char.ToUpper(keyName[0]);

        // F1-F12 功能键
        if (keyName.StartsWith('F') && int.TryParse(keyName[1..], out int fNum) && fNum >= 1 && fNum <= 12)
            return (uint)(0x70 + fNum - 1); // VK_F1 = 0x70

        // 特殊键
        return keyName.ToUpperInvariant() switch
        {
            "SPACE" => 0x20,
            "ENTER" => 0x0D,
            "TAB" => 0x09,
            "ESC" or "ESCAPE" => 0x1B,
            "BACKSPACE" => 0x08,
            "DELETE" => 0x2E,
            "INSERT" => 0x2D,
            "HOME" => 0x24,
            "END" => 0x23,
            "PAGEUP" => 0x21,
            "PAGEDOWN" => 0x22,
            "UP" => 0x26,
            "DOWN" => 0x28,
            "LEFT" => 0x25,
            "RIGHT" => 0x27,
            _ => 0,
        };
    }

    // ========== 释放 ==========

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _registeredHotkeys.Clear();
        GC.SuppressFinalize(this);
    }

    // ========== Win32 API ==========

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
