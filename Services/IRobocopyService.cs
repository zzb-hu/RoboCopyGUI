using RoboCopyGUI.Models;

namespace RoboCopyGUI.Services;

/// <summary>
/// robocopy 调用抽象。便于在测试中 mock，也便于将来替换实现（如 FastCopy）。
/// </summary>
public interface IRobocopyService
{
    /// <summary>
    /// 启动 robocopy 子进程复制文件，实时通过 progress 报告进度。
    /// ct 取消时会真正 Kill 子进程。
    /// </summary>
    Task<CopyResult> RunCopyAsync(
        string source, string dest, string args,
        IProgress<CopyProgress>? progress, CancellationToken ct);
}
