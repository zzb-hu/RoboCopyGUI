namespace RoboCopyGUI.Models;

/// <summary>
/// 应用持久化设置。保存在 %AppData%\RoboCopyGUI\settings.json。
/// 所有字段都有默认值，首次启动或文件丢失时使用默认。
/// </summary>
public class AppSettings
{
    /// <summary>最近用过的源路径（最多 5 个，最新在前）。</summary>
    public List<string> RecentSources { get; set; } = new();

    /// <summary>最近用过的目标路径（最多 5 个，最新在前）。</summary>
    public List<string> RecentDestinations { get; set; } = new();

    /// <summary>上次选择的预设场景名（"快速复制"等），空表示未选。</summary>
    public string LastPresetName { get; set; } = "";

    /// <summary>上次的高级参数（用户手动调整过的高级参数快照）。</summary>
    public PresetProfileSnapshot LastAdvancedParams { get; set; } = new();

    /// <summary>是否在目标路径末尾自动追加源文件夹名（让结果是 dest\source\... 而不是 dest\...）。</summary>
    public bool IncludeSourceFolderName { get; set; }

    /// <summary>主窗口宽度（像素）。</summary>
    public double WindowWidth { get; set; } = 900;

    /// <summary>主窗口高度（像素）。</summary>
    public double WindowHeight { get; set; } = 880;

    /// <summary>主窗口左上角 X（像素）。</summary>
    public double WindowLeft { get; set; } = double.NaN;

    /// <summary>主窗口左上角 Y（像素）。</summary>
    public double WindowTop { get; set; } = double.NaN;
}

/// <summary>
/// PresetProfile 中可持久化的高级参数部分。
/// 不含 Icon / FriendlyName 等纯展示字段。
/// </summary>
public class PresetProfileSnapshot
{
    public int? MultiThread { get; set; } = 16;
    public bool Mirror { get; set; }
    public bool IncludeSubdirs { get; set; } = true;
    public bool RestartMode { get; set; }
    public int RetryCount { get; set; } = 1;
    public int RetryWait { get; set; } = 3;
    public bool CopyAll { get; set; }
    public bool CopyDirTimestamps { get; set; }
    public bool Verbose { get; set; }
}
