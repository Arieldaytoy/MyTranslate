namespace MyTranslate.Capture;

/// <summary>
/// 剪贴板辅助 — 通过模拟 Ctrl+C 获取选中文字（作为 UI Automation 的兜底方案）
/// </summary>
public class ClipboardHelper
{
    /// <summary>
    /// 尝试通过剪贴板获取当前选中的文字
    /// 原理：保存当前剪贴板内容 → 模拟 Ctrl+C → 读取新剪贴板内容 → 恢复原剪贴板
    /// </summary>
    /// <returns>选中的文字，获取失败返回 null</returns>
    public async Task<string?> GetSelectedTextViaClipboardAsync()
    {
        try
        {
            // 保存当前剪贴板内容
            var originalText = Clipboard.GetText();
            var hadText = Clipboard.ContainsText();

            // 清空剪贴板
            Clipboard.Clear();

            // 模拟 Ctrl+C
            SendKeys.SendWait("^c");

            // 等待剪贴板更新
            await Task.Delay(150);

            // 读取新内容
            string? selectedText = null;
            if (Clipboard.ContainsText())
            {
                selectedText = Clipboard.GetText();
            }

            // 恢复原剪贴板内容
            if (hadText && !string.IsNullOrEmpty(originalText))
            {
                Clipboard.SetText(originalText);
            }
            else
            {
                Clipboard.Clear();
            }

            return selectedText;
        }
        catch
        {
            return null;
        }
    }
}
