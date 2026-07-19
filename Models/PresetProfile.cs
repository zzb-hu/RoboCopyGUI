namespace RoboCopyGUI.Models;

public class PresetProfile
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Icon { get; set; } = "";
    public string FriendlyName { get; set; } = "";
    public string FriendlyDescription { get; set; } = "";
    public int? MultiThread { get; set; }
    public bool Mirror { get; set; }
    public bool IncludeSubdirs { get; set; } = true;
    public bool RestartMode { get; set; }
    public bool RestartModeFallback { get; set; }
    public int RetryCount { get; set; } = 1;
    public int RetryWait { get; set; } = 3;
    public bool CopyAll { get; set; }
    public bool CopyDirTimestamps { get; set; }
    public bool Verbose { get; set; }

    public string ToRobocopyArgs()
    {
        var args = new List<string>();
        if (IncludeSubdirs) args.Add("/E");
        if (Mirror) args.Add("/MIR");
        if (RestartMode) args.Add("/Z");
        if (RestartModeFallback) args.Add("/ZB");
        if (MultiThread.HasValue) args.Add($"/MT:{MultiThread.Value}");
        if (RetryCount != 1) args.Add($"/R:{RetryCount}");
        if (RetryWait != 3) args.Add($"/W:{RetryWait}");
        if (CopyAll) args.Add("/COPYALL");
        if (CopyDirTimestamps) args.Add("/DCOPY:T");
        if (Verbose) args.Add("/V");
        args.Add("/NJH");
        args.Add("/NJS");
        return string.Join(" ", args);
    }

    public override string ToString() => Name;

    public static List<PresetProfile> GetDefaults()
    {
        return new List<PresetProfile>
        {
            new()
            {
                Name = "快速多线程",
                Description = "多线程并行拷贝，速度优先",
                Icon = "🚀",
                FriendlyName = "快速复制",
                FriendlyDescription = "多线程加速\n大量小文件最快搞定",
                MultiThread = 16, IncludeSubdirs = true,
                RetryCount = 2, RetryWait = 2
            },
            new()
            {
                Name = "增量备份",
                Description = "MIR 镜像模式，只复制变化部分",
                Icon = "💾",
                FriendlyName = "增量备份",
                FriendlyDescription = "只复制新增和\n修改过的文件",
                Mirror = true, IncludeSubdirs = true,
                RetryCount = 3, RetryWait = 5, CopyDirTimestamps = true
            },
            new()
            {
                Name = "断点续传",
                Description = "Z 重启模式，中断可恢复",
                Icon = "⏸",
                FriendlyName = "断点续传",
                FriendlyDescription = "中断后接着传\n适合移动硬盘/网络盘",
                RestartMode = true, IncludeSubdirs = true,
                RetryCount = 5, RetryWait = 3
            },
            new()
            {
                Name = "完整镜像",
                Description = "保留所有属性权限时间戳",
                Icon = "🔄",
                FriendlyName = "完全同步",
                FriendlyDescription = "目标和源保持\n一模一样",
                Mirror = true, IncludeSubdirs = true,
                RetryCount = 2, RetryWait = 2,
                CopyAll = true, CopyDirTimestamps = true
            }
        };
    }
}
