namespace MyTranslate.Core;

using System.Text.RegularExpressions;

/// <summary>
/// 技术模式保护器 — 在翻译前保护路径、URL、扩展名等不可翻译内容，翻译后还原
/// 防止翻译 API 将 D:\Desktop 改为 D：\Desktop，或将 .txt 翻译为其他文本
/// </summary>
public static partial class TextProtector
{
    // 保护用的占位符前缀（纯大写英文，翻译 API 不会修改）
    private const string Prefix = "PROT";

    /// <summary>
    /// 保护文本中的技术模式，返回 (保护后的文本, 还原映射)
    /// </summary>
    public static (string protectedText, Dictionary<string, string> map) Protect(string text)
    {
        var map = new Dictionary<string, string>();
        var counter = new int[] { 0 }; // 用数组替代 ref，可在 lambda 中捕获
        var result = text;

        // 按优先级依次保护各种模式（长模式优先，避免子串冲突）

        // 1. Windows UNC 路径: \\server\share\path
        result = ProtectPattern(result, map, counter,
            @"\\\\[^\s]+");

        // 2. Windows 盘符路径: D:\Desktop, C:\Users\name\file.txt
        result = ProtectPattern(result, map, counter,
            @"[A-Za-z]:\\[^\s]*");

        // 3. URL: http://..., https://..., ftp://..., www....
        result = ProtectPattern(result, map, counter,
            @"(?:https?://|ftp://|www\.)[^\s]+");

        // 4. 邮箱地址: user@domain.com
        result = ProtectPattern(result, map, counter,
            @"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}");

        // 5. 文件扩展名: .txt, .exe, .json, .cs（前面有字母或数字时）
        result = ProtectPattern(result, map, counter,
            @"(?<=[A-Za-z0-9])\.[A-Za-z0-9]{1,6}(?=\s|$|[^A-Za-z0-9.])");

        return (result, map);
    }

    /// <summary>
    /// 还原文本中被保护的技术模式
    /// </summary>
    public static string Restore(string text, Dictionary<string, string> map)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var result = text;

        if (map.Count > 0)
        {
            // 按占位符长度降序排列，避免子串替换冲突
            foreach (var (placeholder, original) in map.OrderByDescending(kv => kv.Key.Length))
            {
                result = result.Replace(placeholder, original);
            }

            // 额外处理：API 可能把占位符变成小写或加了空格
            foreach (var (placeholder, original) in map)
            {
                if (result.Contains(placeholder))
                    continue;

                var idx = result.IndexOf(placeholder, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    result = result.Remove(idx, placeholder.Length).Insert(idx, original);
                }
            }
        }

        // 安全网：清理 API 自行生成的 __PH 占位符（如 __PH0__、__PH1__、__PH 0__ 等变体）
        // 这些是翻译 API 内部用于保护不可翻译内容的占位符，但有时未正确还原
        result = Regex.Replace(result, @"__\s*PH\s*\d+\s*__\s*", "", RegexOptions.IgnoreCase);

        return result;
    }

    /// <summary>使用正则匹配并替换为保护占位符</summary>
    private static string ProtectPattern(string text, Dictionary<string, string> map,
        int[] counter, string regexPattern)
    {
        return Regex.Replace(text, regexPattern, match =>
        {
            var placeholder = $"{Prefix}{counter[0]}";
            map[placeholder] = match.Value;
            counter[0]++;
            return placeholder;
        });
    }
}
