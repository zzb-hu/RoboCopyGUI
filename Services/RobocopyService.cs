using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using RoboCopyGUI.Models;
using RoboCopyGUI.Parsing;

namespace RoboCopyGUI.Services;

/// <summary>
/// 调用 Windows 内置 robocopy.exe 完成实际复制。
/// 只负责进程 I/O 和编码处理；输出解析委托给 <see cref="RobocopyOutputParser"/>。
/// </summary>
public class RobocopyService : IRobocopyService
{
    // 同步读取 stdout 必须用和 robocopy 实际输出一致的编码，
    // 中文 Windows 上 robocopy 输出是 GBK(936)，强行用 UTF-8 会乱码。
    private static readonly Encoding OutputEncoding = GetConsoleEncoding();

    public async Task<CopyResult> RunCopyAsync(
        string source, string dest, string args,
        IProgress<CopyProgress>? progress, CancellationToken ct)
    {
        var result = new CopyResult();
        var sw = Stopwatch.StartNew();

        Process? process = null;
        try
        {
            var psi = new ProcessStartInfo("robocopy", $"\"{source}\" \"{dest}\" {args}")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = OutputEncoding,
                StandardErrorEncoding = OutputEncoding
            };

            process = new Process { StartInfo = psi };

            // 关键修复：用户点取消时真正终止 robocopy 子进程，
            // 否则 UI 显示已取消但 robocopy 仍在后台继续跑。
            ct.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill(entireProcessTree: true);
                }
                catch { /* 进程可能已自行退出，忽略 */ }
            });

            // 异步预扫描源目录统计文件数和字节数，
            // 避免大目录卡 UI 线程，同时为速度/ETA 提供数据。
            var statsTask = Task.Run(() => CountSourceStats(source), ct);

            process.Start();

            // 预扫描和 robocopy 并行进行；预扫描完成后把统计值取出
            var stats = await statsTask.ConfigureAwait(false);
            result.TotalFiles = stats.FileCount;
            result.TotalBytes = stats.TotalBytes;

            long filesProcessed = 0;
            long bytesProcessed = 0;

            // 同步逐行读取 stdout，避免 ReadToEnd 死锁
            await Task.Run(() =>
            {
                string? line;
                while ((line = process.StandardOutput.ReadLine()) != null)
                {
                    ct.ThrowIfCancellationRequested();
                    var parsed = RobocopyOutputParser.ParseLine(line);
                    if (parsed == null) continue;

                    if (parsed.IsFileLine)
                    {
                        filesProcessed++;
                        if (parsed.FileBytes > 0)
                            bytesProcessed += parsed.FileBytes;
                    }

                    progress?.Report(new CopyProgress
                    {
                        CurrentFile = parsed.FileName ?? "",
                        FilesProcessed = Interlocked.Read(ref filesProcessed),
                        TotalFiles = stats.FileCount,
                        BytesProcessed = Interlocked.Read(ref bytesProcessed),
                        TotalBytes = stats.TotalBytes,
                        StatusLine = line
                    });
                }
            }, ct).ConfigureAwait(false);

            // stderr 必须读完，否则缓冲区满会让 robocopy 阻塞
            var stderr = await process.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
            process.WaitForExit();
            sw.Stop();

            result.Elapsed = sw.Elapsed;
            result.RobocopyExitCode = process.ExitCode;
            result.Success = process.ExitCode <= 3;

            progress?.Report(new CopyProgress
            {
                IsCompleted = true,
                FilesProcessed = result.TotalFiles,
                TotalFiles = result.TotalFiles,
                BytesProcessed = result.TotalBytes,
                TotalBytes = result.TotalBytes,
                StatusLine = process.ExitCode switch
                {
                    0 => "拷贝完成 - 无变化",
                    1 => "拷贝完成",
                    2 => "拷贝完成 - 目标存在额外文件",
                    3 => "拷贝完成 - 有变化且有额外文件",
                    _ => $"拷贝异常 - 退出码 {process.ExitCode}"
                }
            });

            if (!string.IsNullOrEmpty(stderr))
                result.ErrorMessage = stderr;
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            result.Success = false;
            result.ErrorMessage = "用户取消";
            result.Elapsed = sw.Elapsed;
            progress?.Report(new CopyProgress { IsCompleted = true, HasError = true, ErrorMessage = "用户取消" });
        }
        catch (Exception ex)
        {
            sw.Stop();
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.Elapsed = sw.Elapsed;
            progress?.Report(new CopyProgress { IsCompleted = true, HasError = true, ErrorMessage = ex.Message });
        }
        finally
        {
            process?.Dispose();
        }

        return result;
    }

    /// <summary>
    /// 异步统计源目录的文件数和总字节数。
    /// 单独线程跑，避免阻塞 UI；统计失败时返回 0，UI 自动退化为不计字节模式。
    /// </summary>
    private static SourceStats CountSourceStats(string sourceDir)
    {
        var stats = new SourceStats();
        if (!Directory.Exists(sourceDir)) return stats;

        try
        {
            long bytes = 0;
            long count = 0;
            foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                count++;
                try { bytes += new FileInfo(file).Length; }
                catch { /* 单个文件读不到大小忽略 */ }
            }
            stats.FileCount = count;
            stats.TotalBytes = bytes;
        }
        catch { /* 整体失败时保持 0/0，UI 退化为不计字节 */ }

        return stats;
    }

    /// <summary>
    /// 根据 Windows 控制台输出代码页动态选择 Encoding。
    /// 中文系统返回 936(GBK)，英文系统返回 437，UTF-8 是 65001。
    /// </summary>
    private static Encoding GetConsoleEncoding()
    {
        try
        {
            var cp = GetConsoleOutputCP();
            if (cp > 0)
                return Encoding.GetEncoding(cp);
        }
        catch { /* 极端情况下回退到 UTF-8 */ }
        return Encoding.UTF8;
    }

    [DllImport("kernel32.dll")]
    private static extern int GetConsoleOutputCP();

    private sealed class SourceStats
    {
        public long FileCount;
        public long TotalBytes;
    }
}
