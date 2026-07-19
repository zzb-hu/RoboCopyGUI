using RoboCopyGUI.Parsing;

namespace RoboCopyGUI.Tests;

/// <summary>
/// RobocopyOutputParser 单元测试。
/// 用真实的 robocopy 输出样本作为输入，断言解析结果。
/// 不依赖文件系统、不依赖进程。
/// </summary>
public class RobocopyOutputParserTests
{
    [Fact]
    public void ParseLine_NullOrWhitespace_ReturnsNull()
    {
        Assert.Null(RobocopyOutputParser.ParseLine(""));
        Assert.Null(RobocopyOutputParser.ParseLine("   "));
        Assert.Null(RobocopyOutputParser.ParseLine(null!));
    }

    [Fact]
    public void ParseLine_HorizontalRule_ReturnsNull()
    {
        Assert.Null(RobocopyOutputParser.ParseLine("------------------------------------------------------------------------------"));
    }

    [Fact]
    public void ParseLine_RobocopyBanner_ReturnsNull()
    {
        Assert.Null(RobocopyOutputParser.ParseLine("ROBOCOPY     ::     Windows 的可靠文件复制"));
        Assert.Null(RobocopyOutputParser.ParseLine("   ROBOCOPY     ::     Robust File Copy for Windows"));
    }

    [Theory]
    [InlineData("   New File    1234   C:\\source\\file.txt", true, "C:\\source\\file.txt", 1234L)]
    [InlineData("   Newer       5678   C:\\source\\newer.txt", true, "C:\\source\\newer.txt", 5678L)]
    [InlineData("   Older       9999   C:\\source\\older.txt", true, "C:\\source\\older.txt", 9999L)]
    public void ParseLine_EnglishStatusLines_ReturnsParsedFile(
        string line, bool expectedIsFile, string expectedFileName, long expectedBytes)
    {
        var result = RobocopyOutputParser.ParseLine(line);
        Assert.NotNull(result);
        Assert.True(result!.IsFileLine);
        Assert.Equal(expectedFileName, result.FileName);
        Assert.Equal(expectedBytes, result.FileBytes);
    }

    [Fact]
    public void ParseLine_ExtraFileStarPrefix_ReturnsParsedFile()
    {
        var line = "*EXTRA File          5678   D:\\dest\\extra.txt";
        var result = RobocopyOutputParser.ParseLine(line);
        Assert.NotNull(result);
        Assert.True(result!.IsFileLine);
        Assert.Equal("D:\\dest\\extra.txt", result.FileName);
    }

    [Theory]
    [InlineData("   新文件    1234   C:\\源\\文件.txt", "C:\\源\\文件.txt", 1234L)]
    [InlineData("   较新      5678   C:\\源\\较新.txt", "C:\\源\\较新.txt", 5678L)]
    [InlineData("   额外文件  9999   D:\\目标\\额外.txt", "D:\\目标\\额外.txt", 9999L)]
    public void ParseLine_ChineseStatusLines_ReturnsParsedFile(
        string line, string expectedFileName, long expectedBytes)
    {
        var result = RobocopyOutputParser.ParseLine(line);
        Assert.NotNull(result);
        Assert.True(result!.IsFileLine);
        Assert.Equal(expectedFileName, result.FileName);
    }

    [Fact]
    public void ParseLine_PathWithForwardSlash_ReturnsParsedFile()
    {
        var line = "   New File    100   source/path/file.txt";
        var result = RobocopyOutputParser.ParseLine(line);
        Assert.NotNull(result);
        Assert.True(result!.IsFileLine);
        Assert.Equal("source/path/file.txt", result.FileName);
    }

    [Fact]
    public void ParseLine_SummaryLine_ReturnsNull()
    {
        // robocopy 结尾的统计行，不应被识别为文件行
        Assert.Null(RobocopyOutputParser.ParseLine("              总计        复制        跳过       不匹配        失败        Extras"));
        Assert.Null(RobocopyOutputParser.ParseLine("   目录 :        10"));
        Assert.Null(RobocopyOutputParser.ParseLine("   文件 :        100"));
    }
}
