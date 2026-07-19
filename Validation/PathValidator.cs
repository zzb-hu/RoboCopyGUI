using System.IO;

namespace RoboCopyGUI.Validation;

/// <summary>
/// 路径校验结果。Success 表示通过；Failure 带 ErrorMessage 给用户看。
/// </summary>
public sealed class ValidationResult
{
    public bool IsValid { get; }
    public string ErrorMessage { get; }
    public string FullSource { get; }
    public string FullDest { get; }

    private ValidationResult(bool isValid, string errorMessage, string fullSource, string fullDest)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
        FullSource = fullSource;
        FullDest = fullDest;
    }

    public static ValidationResult Success(string fullSource, string fullDest) =>
        new(true, "", fullSource, fullDest);

    public static ValidationResult Failure(string message) =>
        new(false, message, "", "");
}

/// <summary>
/// 纯逻辑校验器：检查源/目标路径是否合法、是否会陷入死循环等。
/// 不弹对话框、不调 UI，便于单元测试。
/// </summary>
public static class PathValidator
{
    /// <summary>
    /// 校验源/目标路径。返回 ValidationResult。
    /// 通过时 FullSource / FullDest 已规范化（末尾带 \）。
    /// </summary>
    public static ValidationResult Validate(string source, string dest)
    {
        if (string.IsNullOrWhiteSpace(source))
            return ValidationResult.Failure("请先选择要复制的文件夹。");
        if (!Directory.Exists(source))
            return ValidationResult.Failure("源文件夹不存在，请检查一下路径是不是输错了。");
        if (string.IsNullOrWhiteSpace(dest))
            return ValidationResult.Failure("请选择复制到哪个文件夹。");

        var (fullSource, fullDest) = NormalizePaths(source, dest);

        if (string.Equals(fullSource, fullDest, StringComparison.OrdinalIgnoreCase))
            return ValidationResult.Failure("源文件夹和目标不能是同一个。");
        if (fullDest.StartsWith(fullSource, StringComparison.OrdinalIgnoreCase))
            return ValidationResult.Failure("目标文件夹不能在源文件夹里面，不然会陷入死循环。");

        return ValidationResult.Success(fullSource, fullDest);
    }

    /// <summary>
    /// 把路径规范化为末尾带 \ 的绝对路径形式。
    /// </summary>
    public static (string fullSource, string fullDest) NormalizePaths(string source, string dest)
    {
        var fullSource = Path.GetFullPath(source).TrimEnd('\\') + "\\";
        var fullDest = Path.GetFullPath(dest).TrimEnd('\\') + "\\";
        return (fullSource, fullDest);
    }

    /// <summary>
    /// 判断路径是否是盘符根目录（如 "E:\"、"F:\"）。
    /// 这种路径作为 /MIR 目标会删除整个磁盘里不属于源的内容，需特别警告。
    /// </summary>
    public static bool IsDriveRoot(string fullPath)
    {
        if (string.IsNullOrEmpty(fullPath)) return false;
        if (fullPath.Length != 3) return false;
        if (!char.IsLetter(fullPath[0])) return false;
        if (fullPath[1] != ':') return false;
        return fullPath[2] == '\\' || fullPath[2] == '/';
    }
}
