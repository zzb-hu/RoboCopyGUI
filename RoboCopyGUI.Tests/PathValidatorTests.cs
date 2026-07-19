using RoboCopyGUI.Validation;

namespace RoboCopyGUI.Tests;

/// <summary>
/// PathValidator 单元测试。
/// 测试各种路径组合的校验结果，不依赖 UI。
/// </summary>
public class PathValidatorTests
{
    [Fact]
    public void Validate_EmptySource_ReturnsFailure()
    {
        var result = PathValidator.Validate("", "C:\\dest");
        Assert.False(result.IsValid);
        Assert.Contains("请先选择", result.ErrorMessage);
    }

    [Fact]
    public void Validate_WhitespaceSource_ReturnsFailure()
    {
        var result = PathValidator.Validate("   ", "C:\\dest");
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_NonExistentSource_ReturnsFailure()
    {
        var result = PathValidator.Validate("C:\\this_path_does_not_exist_xyz_123", "C:\\dest");
        Assert.False(result.IsValid);
        Assert.Contains("不存在", result.ErrorMessage);
    }

    [Fact]
    public void Validate_EmptyDest_ReturnsFailure()
    {
        // 用当前目录作为存在的源
        var existingDir = Directory.GetCurrentDirectory();
        var result = PathValidator.Validate(existingDir, "");
        Assert.False(result.IsValid);
        Assert.Contains("请选择", result.ErrorMessage);
    }

    [Fact]
    public void IsDriveRoot_ValidDriveRoot_ReturnsTrue()
    {
        Assert.True(PathValidator.IsDriveRoot("C:\\"));
        Assert.True(PathValidator.IsDriveRoot("D:\\"));
        Assert.True(PathValidator.IsDriveRoot("E:/"));
        Assert.True(PathValidator.IsDriveRoot("z:\\"));
    }

    [Fact]
    public void IsDriveRoot_Subdirectory_ReturnsFalse()
    {
        Assert.False(PathValidator.IsDriveRoot("C:\\Users"));
        Assert.False(PathValidator.IsDriveRoot("C:\\Users\\test"));
        Assert.False(PathValidator.IsDriveRoot("D:\\Backup\\2024"));
    }

    [Fact]
    public void IsDriveRoot_InvalidForms_ReturnsFalse()
    {
        Assert.False(PathValidator.IsDriveRoot(""));
        Assert.False(PathValidator.IsDriveRoot(null!));
        Assert.False(PathValidator.IsDriveRoot("C:"));        // 长度 2
        Assert.False(PathValidator.IsDriveRoot("CD\\"));      // 不是字母
        Assert.False(PathValidator.IsDriveRoot("1:\\"));      // 数字不是字母
        Assert.False(PathValidator.IsDriveRoot("C##"));       // 不是 :\ 格式
        Assert.False(PathValidator.IsDriveRoot("\\\\"));      // UNC 路径
        Assert.False(PathValidator.IsDriveRoot("CAB"));       // 长度对但格式不对
    }

    [Fact]
    public void NormalizePaths_AddsTrailingBackslash()
    {
        var existingDir = Directory.GetCurrentDirectory();
        var (fullSource, fullDest) = PathValidator.NormalizePaths(
            existingDir, "C:\\dest");

        Assert.EndsWith("\\", fullSource);
        Assert.EndsWith("\\", fullDest);
    }
}
