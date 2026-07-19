using System.Diagnostics;
using System.IO;
using System.Text;
using RoboCopyGUI.Models;

namespace RoboCopyGUI.Services;

public class RobocopyService
{
    public async Task<CopyResult> RunCopyAsync(
        string source, string dest, string args,
        IProgress<CopyProgress>? progress, CancellationToken ct)
    {
        var result = new CopyResult();
        var sw = Stopwatch.StartNew();

        try
        {
            var psi = new ProcessStartInfo("robocopy", $"\"{source}\" \"{dest}\" {args}")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var process = new Process { StartInfo = psi };
            var outputLines = new List<string>();
            long totalFiles = CountSourceFiles(source);
            result.TotalFiles = totalFiles;

            process.Start();

            await Task.Run(() =>
            {
                long filesProcessed = 0;
                string? line;
                while ((line = process.StandardOutput.ReadLine()) != null)
                {
                    ct.ThrowIfCancellationRequested();
                    outputLines.Add(line);
                    var parsed = ParseLine(line, ref filesProcessed);
                    if (parsed != null)
                    {
                        progress?.Report(new CopyProgress
                        {
                            CurrentFile = parsed,
                            FilesProcessed = Interlocked.Read(ref filesProcessed),
                            TotalFiles = totalFiles,
                            StatusLine = line
                        });
                    }
                }
            }, ct);

            var stderr = await process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            sw.Stop();

            result.Elapsed = sw.Elapsed;
            result.RobocopyExitCode = process.ExitCode;
            result.Success = process.ExitCode <= 3;

            progress?.Report(new CopyProgress
            {
                IsCompleted = true,
                FilesProcessed = totalFiles,
                TotalFiles = totalFiles,
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

        return result;
    }

    private static long CountSourceFiles(string sourceDir)
    {
        try
        {
            return !Directory.Exists(sourceDir) ? 0
                : Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories).LongCount();
        }
        catch { return 0; }
    }

    private static string? ParseLine(string line, ref long filesProcessed)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;
        line = line.Trim();
        if (line.StartsWith('-') || line.StartsWith("ROBOCOPY") || line.StartsWith("开始") ||
            line.StartsWith("源 ") || line.StartsWith("目标") || line.StartsWith("文件") ||
            line.StartsWith("选项"))
            return null;
        if (line.Contains('%')) filesProcessed++;
        // 小文件没有百分比行，按状态行计数（新文件/更新/较旧/多余文件）
        if (line.StartsWith("New File") || line.StartsWith("Newer") ||
            line.StartsWith("Older") || line.StartsWith("Extra File") ||
            line.StartsWith("新文件") || line.StartsWith("额外文件"))
            filesProcessed++;
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 0)
        {
            var last = parts[^1];
            if (last.Contains('\\') || last.Contains('.')) return last;
        }
        return parts.Length > 1 ? parts[^1] : null;
    }
}
