namespace RoboCopyGUI.Parsing;

/// <summary>
/// 解析 robocopy 单行 stdout 输出的结果。
/// </summary>
public sealed class ParsedLine
{
    public bool IsFileLine { get; set; }
    public string? FileName { get; set; }
    public long FileBytes { get; set; }
}

/// <summary>
/// 纯逻辑解析器：把 robocopy stdout 一行文本转成 ParsedLine。
/// 不依赖文件系统、不依赖进程，便于单元测试。
/// 兼容中英文 robocopy 输出。
/// </summary>
public static class RobocopyOutputParser
{
    /// <summary>
    /// 解析 robocopy 单行输出。返回 null 表示该行不感兴趣（启动横幅、目录标题等）。
    /// 进度统计只看"文件状态行"——它们明确带有被处理的文件路径。
    /// </summary>
    public static ParsedLine? ParseLine(string rawLine)
    {
        if (string.IsNullOrWhiteSpace(rawLine)) return null;

        var line = rawLine.Trim();

        // 跳过启动横幅行 / 段标题行
        if (line.StartsWith('-'))
            return null;
        if (line.StartsWith("ROBOCOPY", StringComparison.OrdinalIgnoreCase))
            return null;

        // 状态行的特征：第一个 token 是文件状态关键字
        var firstSpace = line.IndexOf(' ');
        var firstToken = firstSpace > 0 ? line.Substring(0, firstSpace) : line;

        // robocopy 文件状态行的关键字
        // 英文: New File / Newer / Older / Extra File / same / *EXTRA File
        // 中文: 新文件 / 较新 / 较旧 / 额外文件 / 相同
        string fileStatusWord = firstToken;
        if (firstToken.StartsWith("*")) fileStatusWord = firstToken.Substring(1);

        bool isFileLine =
            fileStatusWord.Equals("New", StringComparison.OrdinalIgnoreCase) ||
            fileStatusWord.Equals("Newer", StringComparison.OrdinalIgnoreCase) ||
            fileStatusWord.Equals("Older", StringComparison.OrdinalIgnoreCase) ||
            fileStatusWord.Equals("Extra", StringComparison.OrdinalIgnoreCase) ||
            fileStatusWord.Equals("same", StringComparison.OrdinalIgnoreCase) ||
            fileStatusWord.Equals("新文件") ||
            fileStatusWord.Equals("较新") ||
            fileStatusWord.Equals("较旧") ||
            fileStatusWord.Equals("额外文件") ||
            fileStatusWord.Equals("相同");

        // 兼容 "New File" 这种两词状态：检查前两个 token
        if (!isFileLine && firstSpace > 0)
        {
            var secondSpace = line.IndexOf(' ', firstSpace + 1);
            if (secondSpace > 0)
            {
                var twoWords = line.Substring(0, secondSpace);
                isFileLine =
                    twoWords.Equals("New File", StringComparison.OrdinalIgnoreCase) ||
                    twoWords.Equals("Extra File", StringComparison.OrdinalIgnoreCase) ||
                    twoWords.Equals("same:", StringComparison.OrdinalIgnoreCase) ||
                    twoWords.Equals("*EXTRA", StringComparison.OrdinalIgnoreCase);
            }
        }

        if (!isFileLine) return null;

        var parsed = new ParsedLine { IsFileLine = true };

        // robocopy 状态行格式（带 /BYTES）：
        //   "    1234  New File   1234   source\path\file.txt"
        //   "*EXTRA File          5678   dest\extra.txt"
        // 提取策略：找最后一个含 \ 或 / 的 token 作为文件路径
        var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var i = tokens.Length - 1; i >= 0; i--)
        {
            var tok = tokens[i];
            if (tok.Contains('\\') || tok.Contains('/'))
            {
                parsed.FileName = tok;
                break;
            }
        }

        // 从前往后找第一个纯数字 token 作为字节数
        // *EXTRA 行没有第一个数字，需跳过 * 后的 EXTRA 关键字
        var startIdx = line.StartsWith("*") ? 1 : 0;
        for (var i = startIdx; i < tokens.Length; i++)
        {
            var t = tokens[i];
            if (long.TryParse(t, out var bytes))
            {
                parsed.FileBytes = bytes;
                break;
            }
            // 状态关键字后面才可能有字节数
            if (t.Equals("File", StringComparison.OrdinalIgnoreCase) ||
                t.Equals("文件") ||
                t.Equals("Newer", StringComparison.OrdinalIgnoreCase) ||
                t.Equals("Older", StringComparison.OrdinalIgnoreCase) ||
                t.Equals("Extra", StringComparison.OrdinalIgnoreCase) ||
                t.Equals("same", StringComparison.OrdinalIgnoreCase) ||
                t.Equals("same:", StringComparison.OrdinalIgnoreCase) ||
                t.Equals("较新") || t.Equals("较旧") || t.Equals("相同") || t.Equals("额外文件") || t.Equals("新文件"))
                continue;
        }

        return parsed;
    }
}
