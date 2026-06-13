namespace MyTranslate.Capture;

using System.Windows.Automation;
using TextUnitNS = System.Windows.Automation.Text;
using TextPatternRange = System.Windows.Automation.Text.TextPatternRange;

/// <summary>
/// UI Automation 读取器 — 通过 Windows UI Automation 接口读取窗口控件中的文字
/// </summary>
public class UIAutomationReader
{
    /// <summary>
    /// 根据屏幕坐标获取该位置处的文字
    /// </summary>
    /// <param name="screenPoint">屏幕坐标</param>
    /// <returns>读取到的文字，读不到返回 null</returns>
    public string? ReadTextAtPoint(Point screenPoint)
    {
        try
        {
            // 获取鼠标位置的 UI 元素
            var wpfPoint = new System.Windows.Point(screenPoint.X, screenPoint.Y);
            var element = AutomationElement.FromPoint(wpfPoint);

            if (element == null) return null;

            // 策略1：尝试通过 ValuePattern 读取（适用于 TextBox 等输入控件）
            if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var valueObj))
            {
                var value = ((ValuePattern)valueObj).Current.Value;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    // ValuePattern 返回整个控件文本，需要按鼠标位置截取当前行
                    return ExtractCurrentLine(value, wpfPoint, element);
                }
            }

            // 策略2：尝试通过 TextPattern 读取
            if (element.TryGetCurrentPattern(TextPattern.Pattern, out var textObj))
            {
                var textPattern = (TextPattern)textObj;
                var range = textPattern.RangeFromPoint(wpfPoint);

                if (range != null)
                {
                    // 先尝试获取选中的文字
                    var selections = textPattern.GetSelection();
                    if (selections.Length > 0)
                    {
                        var selectedText = selections[0].GetText(-1);
                        if (!string.IsNullOrWhiteSpace(selectedText))
                            return selectedText.Trim();
                    }

                    // 优先尝试 Line（比 Paragraph 更精确，按单行读取）
                    try
                    {
                        var lineRange = range.Clone();
                        lineRange.ExpandToEnclosingUnit(TextUnitNS.TextUnit.Line);
                        var lineText = lineRange.GetText(-1);
                        if (!string.IsNullOrWhiteSpace(lineText))
                            return lineText.Trim();
                    }
                    catch
                    {
                        // 某些控件不支持 Line，降级到 Paragraph
                    }

                    // 降级：获取光标所在段落
                    try
                    {
                        var paraRange = range.Clone();
                        paraRange.ExpandToEnclosingUnit(TextUnitNS.TextUnit.Paragraph);
                        var paragraphText = paraRange.GetText(-1);
                        if (!string.IsNullOrWhiteSpace(paragraphText))
                        {
                            // Paragraph 可能返回多行（记事本中 = 整个文档），按换行截取当前行
                            var currentLine = ExtractLineFromParagraph(paragraphText, wpfPoint, element, textPattern);
                            if (!string.IsNullOrWhiteSpace(currentLine))
                                return currentLine;
                        }
                    }
                    catch { }

                    // 降级：获取光标所在位置的单词
                    try
                    {
                        var wordRange = range.Clone();
                        wordRange.ExpandToEnclosingUnit(TextUnitNS.TextUnit.Word);
                        var wordText = wordRange.GetText(-1);
                        if (!string.IsNullOrWhiteSpace(wordText))
                        {
                            // 尝试从 Word 扩展到整行
                            var lineFromWord = ExpandToLine(textPattern, wordRange, wpfPoint, element);
                            if (lineFromWord != null)
                                return lineFromWord;
                            return wordText.Trim();
                        }
                    }
                    catch { }
                }
            }

            // 策略3：读取元素的 Name 属性（适用于按钮、标签等）
            var name = element.Current.Name;
            if (!string.IsNullOrWhiteSpace(name))
                return name;

            // 策略4：尝试读取父容器的文字
            var parent = TreeWalker.RawViewWalker.GetParent(element);
            if (parent != null)
            {
                if (parent.TryGetCurrentPattern(TextPattern.Pattern, out var parentTextObj))
                {
                    var parentTextPattern = (TextPattern)parentTextObj;
                    var range = parentTextPattern.RangeFromPoint(wpfPoint);
                    if (range != null)
                    {
                        try
                        {
                            var lineRange = range.Clone();
                            lineRange.ExpandToEnclosingUnit(TextUnitNS.TextUnit.Line);
                            var lineText = lineRange.GetText(-1);
                            if (!string.IsNullOrWhiteSpace(lineText))
                                return lineText.Trim();
                        }
                        catch { }

                        try
                        {
                            range.ExpandToEnclosingUnit(TextUnitNS.TextUnit.Paragraph);
                            var paragraphText = range.GetText(-1);
                            if (!string.IsNullOrWhiteSpace(paragraphText))
                                return ExtractLineFromParagraph(paragraphText, wpfPoint, parent, parentTextPattern);
                        }
                        catch { }

                        try
                        {
                            range.ExpandToEnclosingUnit(TextUnitNS.TextUnit.Word);
                            var text = range.GetText(-1);
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                // 尝试扩展到整行
                                var lineFromWord = ExpandToLine(parentTextPattern, range, wpfPoint, parent);
                                if (lineFromWord != null)
                                    return lineFromWord;
                                return text.Trim();
                            }
                        }
                        catch { }
                    }
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 从 ValuePattern 返回的全部文本中，根据鼠标 Y 坐标提取当前行
    /// </summary>
    private static string? ExtractCurrentLine(string fullText, System.Windows.Point point, AutomationElement element)
    {
        try
        {
            // 获取控件的高度和行数，计算鼠标所在行
            var bounds = element.Current.BoundingRectangle;
            if (bounds.Height <= 0) return fullText.Trim();

            var lines = fullText.Replace("\r\n", "\n").Split('\n');
            if (lines.Length <= 1) return fullText.Trim();

            // 根据鼠标 Y 坐标与控件顶部的偏移，估算行号
            double relativeY = point.Y - bounds.Y;
            double lineheight = bounds.Height / lines.Length;
            int lineIndex = (int)(relativeY / lineheight);

            if (lineIndex >= 0 && lineIndex < lines.Length)
                return lines[lineIndex].Trim();

            return fullText.Trim();
        }
        catch
        {
            return fullText.Trim();
        }
    }

    /// <summary>
    /// 从 Paragraph 文本中提取鼠标所在的单行（Paragraph 在记事本中可能返回整个文档）
    /// </summary>
    private static string? ExtractLineFromParagraph(string paragraphText, System.Windows.Point point,
        AutomationElement element, TextPattern textPattern)
    {
        try
        {
            var lines = paragraphText.Replace("\r\n", "\n").Split('\n');
            if (lines.Length <= 1) return paragraphText.Trim();

            // 过滤空行，只保留有内容的行
            var nonEmptyLines = lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
            if (nonEmptyLines.Count == 0) return null;
            if (nonEmptyLines.Count == 1) return nonEmptyLines[0].Trim();

            // 尝试用 RangeFromPoint 精确定位到行
            try
            {
                var exactRange = textPattern.RangeFromPoint(point);
                var wordRange = exactRange.Clone();
                wordRange.ExpandToEnclosingUnit(TextUnitNS.TextUnit.Word);
                var wordText = wordRange.GetText(-1)?.Trim();

                // 找到包含这个单词的行
                if (!string.IsNullOrWhiteSpace(wordText))
                {
                    foreach (var line in nonEmptyLines)
                    {
                        if (line.Contains(wordText, StringComparison.OrdinalIgnoreCase))
                            return line.Trim();
                    }
                }
            }
            catch { }

            // 降级：用鼠标 Y 坐标估算
            var bounds = element.Current.BoundingRectangle;
            if (bounds.Height > 0)
            {
                double relativeY = point.Y - bounds.Y;
                double lineHeight = bounds.Height / lines.Length;
                int lineIndex = (int)(relativeY / lineHeight);

                if (lineIndex >= 0 && lineIndex < lines.Length)
                {
                    var line = lines[lineIndex].Trim();
                    if (!string.IsNullOrWhiteSpace(line))
                        return line;
                }
            }

            return nonEmptyLines[0].Trim();
        }
        catch
        {
            return paragraphText.Trim();
        }
    }

    /// <summary>
    /// 从 Word 范围尝试扩展到整行（通过获取文档文本，找到当前单词所在行）
    /// </summary>
    private static string? ExpandToLine(TextPattern textPattern, TextPatternRange wordRange, System.Windows.Point point, AutomationElement element)
    {
        try
        {
            // 尝试用 RangeFromPoint 重新获取范围并扩展到行
            var lineRange = textPattern.RangeFromPoint(point);
            lineRange.ExpandToEnclosingUnit(TextUnitNS.TextUnit.Line);
            var lineText = lineRange.GetText(-1);
            if (!string.IsNullOrWhiteSpace(lineText))
                return lineText.Trim();
        }
        catch { }

        try
        {
            // 用 ValuePattern 获取全部文本，按 Y 坐标定位行
            if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var valueObj))
            {
                var fullText = ((ValuePattern)valueObj).Current.Value;
                if (!string.IsNullOrWhiteSpace(fullText))
                {
                    return ExtractCurrentLine(fullText, point, element);
                }
            }
        }
        catch { }

        return null;
    }

    /// <summary>
    /// 获取当前焦点控件的选中文字
    /// </summary>
    public string? GetSelectedText()
    {
        try
        {
            var focused = AutomationElement.FocusedElement;
            if (focused == null) return null;

            // 尝试 TextPattern
            if (focused.TryGetCurrentPattern(TextPattern.Pattern, out var textObj))
            {
                var textPattern = (TextPattern)textObj;
                var selections = textPattern.GetSelection();
                if (selections.Length > 0)
                {
                    var selectedText = selections[0].GetText(-1);
                    if (!string.IsNullOrWhiteSpace(selectedText))
                        return selectedText;
                }
            }

            // 尝试 ValuePattern
            if (focused.TryGetCurrentPattern(ValuePattern.Pattern, out var valueObj))
            {
                var value = ((ValuePattern)valueObj).Current.Value;
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 获取当前焦点控件的选区边界矩形
    /// </summary>
    public Rectangle? GetSelectionBoundingRect()
    {
        try
        {
            var focused = AutomationElement.FocusedElement;
            if (focused == null) return null;

            if (focused.TryGetCurrentPattern(TextPattern.Pattern, out var textObj))
            {
                var textPattern = (TextPattern)textObj;
                var selections = textPattern.GetSelection();
                if (selections.Length > 0)
                {
                    var boundingRects = selections[0].GetBoundingRectangles();
                    if (boundingRects.Length > 0)
                    {
                        var r = boundingRects[0];
                        return new Rectangle((int)r.X, (int)r.Y, (int)r.Width, (int)r.Height);
                    }
                }
            }

            var bounds = focused.Current.BoundingRectangle;
            if (!bounds.IsEmpty)
                return new Rectangle(
                    (int)bounds.X, (int)bounds.Y,
                    (int)bounds.Width, (int)bounds.Height);

            return null;
        }
        catch
        {
            return null;
        }
    }
}
