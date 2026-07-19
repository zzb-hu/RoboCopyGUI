using RoboCopyGUI.Models;
using RoboCopyGUI.Services;
using RoboCopyGUI.ViewModels;

namespace RoboCopyGUI.Tests;

/// <summary>
/// 手写 mock，不引 Moq 包，保持零依赖。
/// </summary>
internal sealed class MockDialogService : IDialogService
{
    public string? PickedFolderResult { get; set; }
    public bool ConfirmResult { get; set; } = true;
    public bool ConfirmDangerResult { get; set; } = true;
    public List<string> ShownMessages { get; } = new();
    public List<string> Confirms { get; } = new();

    public void ShowMessage(string title, string content) => ShownMessages.Add($"{title}: {content}");
    public bool Confirm(string message, string title, bool isDanger = false)
    {
        Confirms.Add($"Confirm: {title}");
        return ConfirmResult;
    }
    public bool ConfirmDangerTwice(string firstMessage, string firstTitle,
                                   string secondMessage, string secondTitle)
    {
        Confirms.Add($"Danger1: {firstTitle}");
        Confirms.Add($"Danger2: {secondTitle}");
        return ConfirmDangerResult;
    }
    public string? PickFolder(string description, bool allowCreateNew) => PickedFolderResult;
}

/// <summary>
/// Mock IRobocopyService：可控的 Result、记录所有调用。
/// </summary>
internal sealed class MockRobocopyService : IRobocopyService
{
    public CopyResult Result { get; set; } = new() { Success = true, TotalFiles = 5, Elapsed = TimeSpan.FromSeconds(1) };
    public List<(string source, string dest, string args)> Calls { get; } = new();
    public Exception? ThrowException { get; set; }

    public async Task<CopyResult> RunCopyAsync(string source, string dest, string args,
        IProgress<CopyProgress>? progress, CancellationToken ct)
    {
        Calls.Add((source, dest, args));
        if (ThrowException != null) throw ThrowException;

        // 模拟进度上报
        progress?.Report(new CopyProgress { FilesProcessed = 5, TotalFiles = 5, IsCompleted = true });

        await Task.Yield();
        return Result;
    }
}

public class MainViewModelTests
{
    private static MainViewModel CreateVm(
        MockRobocopyService? robocopy = null,
        MockDialogService? dialog = null)
    {
        robocopy ??= new MockRobocopyService();
        dialog ??= new MockDialogService();
        return new MainViewModel(robocopy, dialog);
    }

    [Fact]
    public void Constructor_InitialPresetIsApplied()
    {
        var vm = CreateVm();
        // 第一个预设是"快速多线程"，应启用 MultiThread
        Assert.True(vm.MultiThread);
        Assert.Equal("16", vm.ThreadCount);
        Assert.Equal(0, vm.SelectedPresetIndex);
    }

    [Fact]
    public void Constructor_AllCommandsNotNull()
    {
        var vm = CreateVm();
        Assert.NotNull(vm.StartCopyCommand);
        Assert.NotNull(vm.CancelCopyCommand);
        Assert.NotNull(vm.BrowseSourceCommand);
        Assert.NotNull(vm.BrowseDestCommand);
        Assert.NotNull(vm.OpenDestCommand);
        Assert.NotNull(vm.UseRecentSourceCommand);
        Assert.NotNull(vm.UseRecentDestCommand);
    }

    [Fact]
    public void Constructor_InitialStateIsIdle()
    {
        var vm = CreateVm();
        Assert.Equal(CopyState.Idle, vm.CurrentState);
        Assert.True(vm.StartBtnEnabled);
        Assert.False(vm.CancelBtnVisible);
        Assert.False(vm.ProgressCardVisible);
    }

    [Fact]
    public void BrowseSource_PickFolderReturnsPath_SourcePathUpdated()
    {
        var dialog = new MockDialogService { PickedFolderResult = "C:\\MyFolder" };
        var vm = CreateVm(dialog: dialog);

        vm.BrowseSourceCommand.Execute(null);

        Assert.Equal("C:\\MyFolder", vm.SourcePath);
    }

    [Fact]
    public void BrowseSource_PickFolderReturnsNull_SourcePathUnchanged()
    {
        var dialog = new MockDialogService { PickedFolderResult = null };
        var vm = CreateVm(dialog: dialog);
        var original = vm.SourcePath;

        vm.BrowseSourceCommand.Execute(null);

        Assert.Equal(original, vm.SourcePath);
    }

    [Fact]
    public async Task StartCopy_EmptySource_ShowsMessage_DoesNotCallService()
    {
        var robocopy = new MockRobocopyService();
        var dialog = new MockDialogService();
        var vm = new MainViewModel(robocopy, dialog)
        {
            SourcePath = "",
            DestPath = "C:\\dest"
        };

        await vm.StartCopyCommand.ExecuteAsync();

        Assert.Empty(robocopy.Calls);
        Assert.Single(dialog.ShownMessages);
        Assert.Contains("请先选择", dialog.ShownMessages[0]);
        Assert.Equal(CopyState.Idle, vm.CurrentState);
    }

    [Fact]
    public async Task StartCopy_NonExistentSource_ShowsMessage_DoesNotCallService()
    {
        var robocopy = new MockRobocopyService();
        var dialog = new MockDialogService();
        var vm = new MainViewModel(robocopy, dialog)
        {
            SourcePath = "C:\\this_does_not_exist_xyz_123",
            DestPath = "C:\\dest"
        };

        await vm.StartCopyCommand.ExecuteAsync();

        Assert.Empty(robocopy.Calls);
        Assert.Contains(dialog.ShownMessages, m => m.Contains("不存在"));
    }

    [Fact]
    public async Task StartCopy_SuccessfulCopy_StateGoesToCompleted()
    {
        // 用当前目录作为存在的源
        var existingDir = Directory.GetCurrentDirectory();
        var robocopy = new MockRobocopyService
        {
            Result = new CopyResult { Success = true, TotalFiles = 5, Elapsed = TimeSpan.FromSeconds(2) }
        };
        var dialog = new MockDialogService();
        var vm = new MainViewModel(robocopy, dialog)
        {
            SourcePath = existingDir,
            DestPath = Path.Combine(Path.GetTempPath(), "robocopy_test_dest_" + Guid.NewGuid().ToString("N"))
        };

        try
        {
            await vm.StartCopyCommand.ExecuteAsync();

            Assert.Single(robocopy.Calls);
            Assert.Equal(CopyState.Completed, vm.CurrentState);
            Assert.True(vm.ResultVisible);
            Assert.Equal("✓", vm.ResultIcon);
            Assert.Contains("复制完成", vm.ResultTitle);
        }
        finally
        {
            if (Directory.Exists(vm.DestPath)) Directory.Delete(vm.DestPath, recursive: true);
        }
    }

    [Fact]
    public async Task StartCopy_MirrorMode_TriggersConfirm()
    {
        var existingDir = Directory.GetCurrentDirectory();
        var robocopy = new MockRobocopyService();
        var dialog = new MockDialogService { ConfirmResult = false }; // 用户点 No
        var vm = new MainViewModel(robocopy, dialog)
        {
            SourcePath = existingDir,
            DestPath = Path.Combine(Path.GetTempPath(), "robocopy_test_dest_2"),
            Mirror = true
        };

        await vm.StartCopyCommand.ExecuteAsync();

        // 用户拒绝 → 不调用 robocopy
        Assert.Empty(robocopy.Calls);
        Assert.NotEmpty(dialog.Confirms);
    }

    [Fact]
    public async Task StartCopy_Success_AddsToRecentHistory()
    {
        var existingDir = Directory.GetCurrentDirectory();
        var destDir = Path.Combine(Path.GetTempPath(), "robocopy_recent_test_" + Guid.NewGuid().ToString("N"));
        var robocopy = new MockRobocopyService { Result = new CopyResult { Success = true } };
        var dialog = new MockDialogService();
        var vm = new MainViewModel(robocopy, dialog)
        {
            SourcePath = existingDir,
            DestPath = destDir
        };

        try
        {
            await vm.StartCopyCommand.ExecuteAsync();

            Assert.Contains(existingDir, vm.RecentSources);
            Assert.Contains(destDir, vm.RecentDestinations);
        }
        finally
        {
            if (Directory.Exists(destDir)) Directory.Delete(destDir, recursive: true);
        }
    }

    [Fact]
    public void SelectPreset_Index1_AppliesPreset1()
    {
        var vm = CreateVm();
        vm.SelectPreset(1); // 增量备份

        Assert.Equal(1, vm.SelectedPresetIndex);
        Assert.True(vm.Mirror); // 增量备份有 MIR
        Assert.True(vm.MirrorWarningVisible);
    }

    [Fact]
    public void OnAdvancedParamChanged_ClearsSelectedPreset()
    {
        var vm = CreateVm();
        vm.SelectPreset(2); // 断点续传

        vm.OnAdvancedParamChanged();

        Assert.Null(vm.SelectedPresetIndex);
    }

    [Fact]
    public void Mirror_SetToTrue_MirrorWarningVisible()
    {
        var vm = CreateVm();
        vm.Mirror = false;
        Assert.False(vm.MirrorWarningVisible);

        vm.Mirror = true;
        Assert.True(vm.MirrorWarningVisible);
    }

    [Fact]
    public void UseRecentSourceCommand_SetsSourcePath()
    {
        var vm = CreateVm();
        vm.UseRecentSourceCommand.Execute("D:\\backup\\2024");
        Assert.Equal("D:\\backup\\2024", vm.SourcePath);
    }

    [Fact]
    public void UseRecentDestCommand_SetsDestPath()
    {
        var vm = CreateVm();
        vm.UseRecentDestCommand.Execute("E:\\dest");
        Assert.Equal("E:\\dest", vm.DestPath);
    }

    [Fact]
    public void CaptureSettings_InitialState_ReturnsDefaultSnapshot()
    {
        var vm = CreateVm();
        var settings = vm.CaptureSettings();

        Assert.NotNull(settings);
        Assert.Equal("快速多线程", settings.LastPresetName);
        Assert.Equal(16, settings.LastAdvancedParams.MultiThread);
    }

    [Fact]
    public void ApplySettings_RestoresRecentPaths()
    {
        var vm = CreateVm();
        var settings = new AppSettings
        {
            RecentSources = new List<string> { "C:\\src1", "C:\\src2" },
            RecentDestinations = new List<string> { "D:\\dest1" }
        };

        vm.ApplySettings(settings);

        Assert.Equal(2, vm.RecentSources.Count);
        Assert.Equal("C:\\src1", vm.SourcePath); // 第一个作为默认值
        Assert.Equal("D:\\dest1", vm.DestPath);
    }

    [Fact]
    public void ApplySettings_RestoresCustomPresetByName()
    {
        var vm = CreateVm();
        var settings = new AppSettings
        {
            LastPresetName = "断点续传"
        };

        vm.ApplySettings(settings);

        Assert.Equal(2, vm.SelectedPresetIndex); // 断点续传是第 3 个（index 2）
        Assert.True(vm.RestartMode); // 断点续传有 /Z
    }

    [Fact]
    public void SaveWindowSettings_DoesNotThrowWithoutConfigService()
    {
        // 不传 configService
        var vm = new MainViewModel(new MockRobocopyService(), new MockDialogService());
        var exception = Record.Exception(() => vm.SaveWindowSettings(1000, 800, 100, 50));
        Assert.Null(exception);
    }

    [Fact]
    public async Task StartCopy_IncludeSourceFolderName_AppendsSourceFolderNameToDest()
    {
        // 验证勾选"目标保留源文件夹名"后，传给 robocopy 的 dest 末尾追加了源文件夹名
        var existingDir = Directory.GetCurrentDirectory();  // 比如 C:\...\RoboCopyGUI
        var destBase = Path.Combine(Path.GetTempPath(), "robocopy_include_test_" + Guid.NewGuid().ToString("N"));
        var robocopy = new MockRobocopyService { Result = new CopyResult { Success = true } };
        var dialog = new MockDialogService();
        var vm = new MainViewModel(robocopy, dialog)
        {
            SourcePath = existingDir,
            DestPath = destBase,
            IncludeSourceFolderName = true
        };

        try
        {
            await vm.StartCopyCommand.ExecuteAsync();

            Assert.Single(robocopy.Calls);
            var expectedFolderName = System.IO.Path.GetFileName(existingDir.TrimEnd('\\', '/'));
            Assert.Equal(System.IO.Path.Combine(destBase, expectedFolderName), robocopy.Calls[0].dest);
        }
        finally
        {
            if (Directory.Exists(destBase)) Directory.Delete(destBase, recursive: true);
        }
    }

    [Fact]
    public async Task StartCopy_NotIncludeSourceFolderName_KeepsDestAsIs()
    {
        // 默认不勾选：dest 保持原样
        var existingDir = Directory.GetCurrentDirectory();
        var destBase = Path.Combine(Path.GetTempPath(), "robocopy_noinclude_test_" + Guid.NewGuid().ToString("N"));
        var robocopy = new MockRobocopyService { Result = new CopyResult { Success = true } };
        var dialog = new MockDialogService();
        var vm = new MainViewModel(robocopy, dialog)
        {
            SourcePath = existingDir,
            DestPath = destBase,
            IncludeSourceFolderName = false
        };

        try
        {
            await vm.StartCopyCommand.ExecuteAsync();

            Assert.Single(robocopy.Calls);
            Assert.Equal(destBase, robocopy.Calls[0].dest);
        }
        finally
        {
            if (Directory.Exists(destBase)) Directory.Delete(destBase, recursive: true);
        }
    }
}

/// <summary>AsyncRelayCommand 的测试辅助扩展，让 await 更自然。</summary>
internal static class AsyncRelayCommandExtensions
{
    public static Task ExecuteAsync(this AsyncRelayCommand cmd)
    {
        cmd.Execute(null);
        // AsyncRelayCommand.Execute 是 async void，无法直接 await
        // 给一点时间让内部 Task 完成
        return Task.Delay(50);
    }
}
