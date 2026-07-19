namespace RoboCopyGUI.Models;

public class CopyResult
{
    public bool Success { get; set; }
    public TimeSpan Elapsed { get; set; }
    public long TotalFiles { get; set; }
    public long TotalBytes { get; set; }
    public long CopiedFiles { get; set; }
    public long SkippedFiles { get; set; }
    public long FailedFiles { get; set; }
    public int RobocopyExitCode { get; set; }
    public string ErrorMessage { get; set; } = "";
}

public class CopyProgress
{
    public string CurrentFile { get; set; } = "";
    public long FilesProcessed { get; set; }
    public long TotalFiles { get; set; }
    public long BytesProcessed { get; set; }
    public long TotalBytes { get; set; }
    public string StatusLine { get; set; } = "";
    public bool IsCompleted { get; set; }
    public bool HasError { get; set; }
    public string ErrorMessage { get; set; } = "";

    // 优先按字节数算进度；字节数不可用时退化到文件数
    public double Percentage =>
        TotalBytes > 0
            ? Math.Min(100.0, (double)BytesProcessed / TotalBytes * 100)
            : TotalFiles > 0
                ? Math.Min(100.0, (double)FilesProcessed / TotalFiles * 100)
                : 0;
}
