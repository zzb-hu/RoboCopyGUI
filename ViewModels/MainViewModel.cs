using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Threading;
using RoboCopyGUI.Models;
using RoboCopyGUI.Services;
using RoboCopyGUI.Validation;

namespace RoboCopyGUI.ViewModels;

/// <summary>
/// 主窗口状态机：Idle → Preparing → Running → (Cancelling|Completed|Failed|Cancelled)
/// </summary>
public enum CopyState
{
    Idle,
    Preparing,
    Running,
    Cancelling,
    Completed,
    Cancelled,
    Failed
}

/// <summary>
/// 主窗口的 ViewModel。
/// 持有所有 UI 状态、命令、业务逻辑；不依赖任何 WPF UI 类型。
/// 通过 IRobocopyService / IDialogService / IConfigurationService 与外界交互。
/// </summary>
public class MainViewModel : ViewModelBase
{
    // ============ 依赖 ============
    private readonly IRobocopyService _robocopyService;
    private readonly IDialogService _dialogService;
    private readonly IConfigurationService? _configService;

    // ============ 内部状态 ============
    private readonly Stopwatch _elapsedSw = new();
    private CancellationTokenSource? _cts;
    private DateTime _lastProgressUpdate = DateTime.MinValue;
    private const int ProgressThrottleMs = 100;
    private const int MaxLogChars = 200_000;
    private readonly StringBuilder _logBuilder = new();
    private Dispatcher? _dispatcher; // 用于切回 UI 线程，由 View 注入
    private bool _isInternalUpdate; // ApplyPreset 期间设 true，避免 OnAdvancedParamChanged 误触发

    // ============ 路径 ============
    private string _sourcePath = "";
    private string _destPath = "";
    private string _lastDestPath = "";

    public string SourcePath { get => _sourcePath; set => SetProperty(ref _sourcePath, value); }
    public string DestPath { get => _destPath; set => SetProperty(ref _destPath, value); }

    // ============ 场景预设 ============
    public ObservableCollection<PresetProfile> Presets { get; } = new(PresetProfile.GetDefaults());

    private int? _selectedPresetIndex = 0;
    public int? SelectedPresetIndex
    {
        get => _selectedPresetIndex;
        set
        {
            if (SetProperty(ref _selectedPresetIndex, value))
            {
                if (value.HasValue && value.Value >= 0 && value.Value < Presets.Count)
                    ApplyPreset(Presets[value.Value]);
                UpdateMirrorWarning();
            }
        }
    }

    // ============ 高级参数 ============
    private bool _multiThread = true;
    private string _threadCount = "16";
    private bool _mirror;
    private bool _includeSubdirs = true;
    private bool _restartMode;
    private string _retryCount = "1";
    private string _retryWait = "3";
    private bool _copyAll;
    private bool _copyDirTimestamps;
    private bool _verbose;

    public bool MultiThread { get => _multiThread; set => SetProperty(ref _multiThread, value); }
    public string ThreadCount { get => _threadCount; set => SetProperty(ref _threadCount, value); }
    public bool Mirror
    {
        get => _mirror;
        set { if (SetProperty(ref _mirror, value)) UpdateMirrorWarning(); }
    }
    public bool IncludeSubdirs { get => _includeSubdirs; set => SetProperty(ref _includeSubdirs, value); }
    public bool RestartMode { get => _restartMode; set => SetProperty(ref _restartMode, value); }
    public string RetryCount { get => _retryCount; set => SetProperty(ref _retryCount, value); }
    public string RetryWait { get => _retryWait; set => SetProperty(ref _retryWait, value); }
    public bool CopyAll { get => _copyAll; set => SetProperty(ref _copyAll, value); }
    public bool CopyDirTimestamps { get => _copyDirTimestamps; set => SetProperty(ref _copyDirTimestamps, value); }
    public bool Verbose { get => _verbose; set => SetProperty(ref _verbose, value); }

    private bool _mirrorWarningVisible;
    public bool MirrorWarningVisible { get => _mirrorWarningVisible; set => SetProperty(ref _mirrorWarningVisible, value); }

    // 是否在目标路径末尾自动追加源文件夹名（让结果是 dest\source\... 而不是 dest\...）
    private bool _includeSourceFolderName;
    public bool IncludeSourceFolderName
    {
        get => _includeSourceFolderName;
        set => SetProperty(ref _includeSourceFolderName, value);
    }

    // ============ 状态机 ============
    private CopyState _currentState = CopyState.Idle;
    public CopyState CurrentState
    {
        get => _currentState;
        private set
        {
            if (SetProperty(ref _currentState, value))
            {
                OnPropertyChanged(nameof(StartBtnEnabled));
                OnPropertyChanged(nameof(CancelBtnVisible));
                OnPropertyChanged(nameof(ProgressCardVisible));
                OnPropertyChanged(nameof(ScenarioFullVisible));
                OnPropertyChanged(nameof(ScenarioSummaryVisible));
            }
        }
    }

    public bool StartBtnEnabled => CurrentState is not (CopyState.Running or CopyState.Cancelling);
    public bool CancelBtnVisible => CurrentState is CopyState.Running or CopyState.Cancelling;
    public bool ProgressCardVisible => CurrentState != CopyState.Idle;
    public bool ScenarioFullVisible => CurrentState is CopyState.Idle or CopyState.Completed
                                              or CopyState.Cancelled or CopyState.Failed;
    public bool ScenarioSummaryVisible => CurrentState is CopyState.Preparing or CopyState.Running
                                                or CopyState.Cancelling;

    // ============ 场景折叠摘要 ============
    private string _scenarioSummary = "";
    public string ScenarioSummary { get => _scenarioSummary; set => SetProperty(ref _scenarioSummary, value); }

    // ============ 进度 ============
    private double _progressValue;
    private string _progressPercent = "0%";
    private string _progressFiles = "已完成 0 / 0 个文件";
    private string _progressBytes = "";
    private string _progressSpeed = "";
    private string _progressTime = "耗时 0.0 秒";
    private string _progressFile = "";

    public double ProgressValue { get => _progressValue; set => SetProperty(ref _progressValue, value); }
    public string ProgressPercent { get => _progressPercent; set => SetProperty(ref _progressPercent, value); }
    public string ProgressFiles { get => _progressFiles; set => SetProperty(ref _progressFiles, value); }
    public string ProgressBytes { get => _progressBytes; set => SetProperty(ref _progressBytes, value); }
    public string ProgressSpeed { get => _progressSpeed; set => SetProperty(ref _progressSpeed, value); }
    public string ProgressTime { get => _progressTime; set => SetProperty(ref _progressTime, value); }
    public string ProgressFile { get => _progressFile; set => SetProperty(ref _progressFile, value); }

    private string _statusHeader = "";
    public string StatusHeader { get => _statusHeader; set => SetProperty(ref _statusHeader, value); }

    // ============ 状态栏 ============
    private string _statusText = "就绪 — 选好文件夹和场景后，点击开始复制";
    public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }

    // 用 key 字符串而非 Brush 对象，避免 ViewModel 引用 WPF。
    // View 端用 DynamicResource 或 IValueConverter 把字符串映射成 Brush。
    private string _statusDotBrushKey = "SuccessBrush";
    public string StatusDotBrushKey { get => _statusDotBrushKey; set => SetProperty(ref _statusDotBrushKey, value); }

    // ============ 结果 ============
    private bool _resultVisible;
    private string _resultIcon = "✓";
    private string _resultBadgeBrushKey = "SuccessBrush";
    private string _resultTitle = "";
    private string _resultSummary = "";

    public bool ResultVisible { get => _resultVisible; set => SetProperty(ref _resultVisible, value); }
    public string ResultIcon { get => _resultIcon; set => SetProperty(ref _resultIcon, value); }
    public string ResultBadgeBrushKey { get => _resultBadgeBrushKey; set => SetProperty(ref _resultBadgeBrushKey, value); }
    public string ResultTitle { get => _resultTitle; set => SetProperty(ref _resultTitle, value); }
    public string ResultSummary { get => _resultSummary; set => SetProperty(ref _resultSummary, value); }

    // ============ 日志 ============
    private string _logText = "";
    public string LogText { get => _logText; set => SetProperty(ref _logText, value); }

    // ============ 持久化 ============
    /// <summary>
    /// 从 AppSettings 恢复 ViewModel 状态（场景记忆、高级参数）。
    /// 由 App.OnStartup 调用。文件不存在或损坏时 settings 字段有默认值，安全。
    /// </summary>
    public void ApplySettings(AppSettings settings)
    {
        // 恢复"目标保留源文件夹名"偏好
        IncludeSourceFolderName = settings.IncludeSourceFolderName;

        // 恢复场景或自定义参数
        if (!string.IsNullOrEmpty(settings.LastPresetName) && settings.LastPresetName != "自定义")
        {
            var preset = Presets.FirstOrDefault(p => p.Name == settings.LastPresetName);
            if (preset != null)
            {
                var index = Presets.IndexOf(preset);
                SelectPreset(index); // 含 ApplyPreset
            }
        }
        else if (settings.LastAdvancedParams != null)
        {
            // 自定义参数：直接恢复，不匹配任何预设
            SelectedPresetIndex = null;
            ApplySnapshot(settings.LastAdvancedParams);
        }
    }

    /// <summary>
    /// 把 ViewModel 当前状态捕获成 AppSettings。
    /// 由 SaveCurrentSettings / SaveWindowSettings 调用，也可被单测使用。
    /// </summary>
    public AppSettings CaptureSettings()
    {
        int? mt = null;
        if (MultiThread && int.TryParse(ThreadCount, out var t) && t > 0) mt = t;
        int.TryParse(RetryCount, out var retry);
        int.TryParse(RetryWait, out var wait);

        return new AppSettings
        {
            LastPresetName = SelectedPresetIndex.HasValue
                ? Presets[SelectedPresetIndex.Value].Name
                : "自定义",
            IncludeSourceFolderName = IncludeSourceFolderName,
            LastAdvancedParams = new PresetProfileSnapshot
            {
                MultiThread = mt,
                Mirror = Mirror,
                IncludeSubdirs = IncludeSubdirs,
                RestartMode = RestartMode,
                RetryCount = retry > 0 ? retry : 1,
                RetryWait = wait > 0 ? wait : 3,
                CopyAll = CopyAll,
                CopyDirTimestamps = CopyDirTimestamps,
                Verbose = Verbose
            },
            WindowWidth = _windowWidth,
            WindowHeight = _windowHeight,
            WindowLeft = _windowLeft,
            WindowTop = _windowTop
        };
    }

    /// <summary>
    /// 保存当前设置 + 窗口尺寸到磁盘。由 MainWindow.Closing 调用。
    /// </summary>
    public void SaveWindowSettings(double width, double height, double left, double top)
    {
        _windowWidth = width;
        _windowHeight = height;
        _windowLeft = left;
        _windowTop = top;
        if (_configService == null) return;
        try { _configService.Save(CaptureSettings()); }
        catch { /* 设置保存失败不影响主流程 */ }
    }

    private double _windowWidth = 900;
    private double _windowHeight = 880;
    private double _windowLeft = double.NaN;
    private double _windowTop = double.NaN;

    /// <summary>从快照恢复高级参数（用于"自定义"场景的启动恢复）。</summary>
    private void ApplySnapshot(PresetProfileSnapshot snap)
    {
        MultiThread = snap.MultiThread.HasValue;
        ThreadCount = (snap.MultiThread ?? 16).ToString();
        Mirror = snap.Mirror;
        IncludeSubdirs = snap.IncludeSubdirs;
        RestartMode = snap.RestartMode;
        RetryCount = snap.RetryCount.ToString();
        RetryWait = snap.RetryWait.ToString();
        CopyAll = snap.CopyAll;
        CopyDirTimestamps = snap.CopyDirTimestamps;
        Verbose = snap.Verbose;
    }

    // ============ 命令 ============
    public AsyncRelayCommand StartCopyCommand { get; }
    public RelayCommand CancelCopyCommand { get; }
    public RelayCommand BrowseSourceCommand { get; }
    public RelayCommand BrowseDestCommand { get; }
    public RelayCommand OpenDestCommand { get; }

    // ============ 构造 ============
    public MainViewModel(IRobocopyService robocopyService, IDialogService dialogService,
                         IConfigurationService? configService = null)
    {
        _robocopyService = robocopyService;
        _dialogService = dialogService;
        _configService = configService;

        StartCopyCommand = new AsyncRelayCommand(StartCopyAsync,
            () => CurrentState is not (CopyState.Running or CopyState.Cancelling));
        CancelCopyCommand = new RelayCommand(CancelCopy, () => CurrentState == CopyState.Running);
        BrowseSourceCommand = new RelayCommand(BrowseSource);
        BrowseDestCommand = new RelayCommand(BrowseDest);
        OpenDestCommand = new RelayCommand(OpenDest, () => !string.IsNullOrEmpty(_lastDestPath));

        // 应用首个预设作为初始参数（与原 code-behind 行为一致）
        if (Presets.Count > 0)
            ApplyPreset(Presets[0]);
    }

    /// <summary>由 View 调用注入 Dispatcher，用于切回 UI 线程更新绑定属性。</summary>
    public void SetDispatcher(Dispatcher dispatcher) => _dispatcher = dispatcher;

    // ============ 命令实现 ============

    private void BrowseSource()
    {
        var path = _dialogService.PickFolder("选择要复制的文件夹", allowCreateNew: false);
        if (!string.IsNullOrEmpty(path))
            SourcePath = path;
    }

    private void BrowseDest()
    {
        var path = _dialogService.PickFolder("选择复制到哪个文件夹", allowCreateNew: true);
        if (!string.IsNullOrEmpty(path))
            DestPath = path;
    }

    private async Task StartCopyAsync()
    {
        var source = SourcePath.Trim();
        var dest = DestPath.Trim();

        // 路径校验
        var validation = PathValidator.Validate(source, dest);
        if (!validation.IsValid)
        {
            _dialogService.ShowMessage("提示", validation.ErrorMessage);
            return;
        }
        var fullSource = validation.FullSource;
        var fullDest = validation.FullDest;

        // 勾选了"目标保留源文件夹名"：自动给目标追加源文件夹名
        // 例：源 C:\a\source，目标 D:\backup → 实际目标 D:\backup\source
        // 源是盘符根目录（如 E:\）时拿不到文件夹名，跳过
        if (IncludeSourceFolderName)
        {
            var trimmedSource = source.TrimEnd('\\', '/');
            var sourceFolderName = System.IO.Path.GetFileName(trimmedSource);
            if (!string.IsNullOrEmpty(sourceFolderName))
            {
                dest = System.IO.Path.Combine(dest, sourceFolderName);
                fullDest = System.IO.Path.GetFullPath(dest).TrimEnd('\\') + "\\";
            }
        }

        // MIR 确认
        if (Mirror)
        {
            if (PathValidator.IsDriveRoot(fullDest))
            {
                var first = $"⚠️  危险操作！\n\n" +
                            $"目标路径是盘符根目录：\n  {fullDest}\n\n" +
                            $"镜像模式 /MIR 会删除该磁盘里【所有不属于源文件夹】的文件，\n" +
                            $"包括其他文件夹、隐藏文件、回收站等，且不可恢复！\n\n" +
                            $"确定要继续吗？";
                var second = $"再次确认：\n\n" +
                             $"目标：{fullDest}\n" +
                             $"源：{fullSource}\n\n" +
                             $"即将永久删除 {fullDest} 里所有不属于源的内容。\n" +
                             $"这是最后一次确认，无法撤销。\n\n" +
                             $"真的要继续吗？";
                if (!_dialogService.ConfirmDangerTwice(first, "⚠️ 危险操作", second, "最后确认"))
                    return;
            }
            else
            {
                if (!_dialogService.Confirm(
                        "完全同步 / 增量备份会删除目标文件夹里多出来的文件，\n确定继续吗？",
                        "删除确认", isDanger: true))
                    return;
            }
        }

        Directory.CreateDirectory(dest);
        _lastDestPath = dest;
        var preset = BuildCurrentParams();
        var args = preset.ToRobocopyArgs();

        // 进入 Running 状态，重置 UI
        ResetProgressUi();
        CollapseScenarioPanel();
        AppendLog($"开始复制: \"{source}\" → \"{dest}\"");
        AppendLog($"参数: {args}");

        CurrentState = CopyState.Running;
        _cts = new CancellationTokenSource();
        _elapsedSw.Restart();
        _lastProgressUpdate = DateTime.MinValue;
        StatusText = "正在复制...";
        StatusDotBrushKey = "AccentBrush";

        // 节流进度回调：完成/错误事件立即处理，中间进度按 100ms 节流
        var progress = new Progress<CopyProgress>(p =>
        {
            var now = DateTime.Now;
            if (!p.IsCompleted && !p.HasError && (now - _lastProgressUpdate).TotalMilliseconds < ProgressThrottleMs)
                return;
            _lastProgressUpdate = now;
            _dispatcher?.BeginInvoke(new Action(() => UpdateProgress(p)), DispatcherPriority.Background);
        });

        try
        {
            var result = await Task.Run(() =>
                _robocopyService.RunCopyAsync(source, dest, args, progress, _cts.Token));
            _elapsedSw.Stop();
            ShowResult(result);
        }
        catch (OperationCanceledException)
        {
            _elapsedSw.Stop();
            ShowCancelled();
        }
        catch (Exception ex)
        {
            _elapsedSw.Stop();
            AppendLog($"异常: {ex.Message}");
            ShowFailure("复制过程中出了点问题", ex.Message);
        }

        // 注意：不强制回 Idle。ShowResult/ShowCancelled/ShowFailure 已设置正确的终态
        // （Completed/Cancelled/Failed），StartBtnEnabled 派生于"非 Running/Cancelling"，
        // 用户可以再点开始触发下一次复制（StartCopyAsync 开头会调 ResetProgressUi 清空旧结果）。
        RestoreScenarioPanel();
    }

    private void CancelCopy()
    {
        _cts?.Cancel();
        CurrentState = CopyState.Cancelling;
        AppendLog("用户请求取消...");
        StatusText = "正在取消...";
    }

    private void OpenDest()
    {
        if (string.IsNullOrEmpty(_lastDestPath) || !Directory.Exists(_lastDestPath))
        {
            _dialogService.ShowMessage("提示", "目标文件夹不存在，无法打开。");
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", _lastDestPath)
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _dialogService.ShowMessage("提示", $"打开失败：{ex.Message}");
        }
    }

    // ============ 被 code-behind 调用：拖拽提示 ============

    /// <summary>拖入的不是文件夹时弹提示。</summary>
    public void ShowDragNotFolderWarning()
        => _dialogService.ShowMessage("提示", "拖进来的不是文件夹，请拖入一个文件夹。");

    /// <summary>拖入了多个文件夹时弹提示。</summary>
    public void ShowDragMultiFolderWarning(string firstPath)
        => _dialogService.ShowMessage("提示", $"一次只能拖入一个文件夹，已采用第一个：\n{firstPath}");

    // ============ 被 code-behind 调用：场景选择 / 参数变化 ============

    /// <summary>用户点了某个场景卡片（RadioButton Checked）。index=-1 表示全部未选。</summary>
    public void SelectPreset(int index)
    {
        if (_isInternalUpdate) return;
        _isInternalUpdate = true;
        try
        {
            SelectedPresetIndex = index;
            if (index >= 0 && index < Presets.Count)
                ApplyPreset(Presets[index]);
            UpdateMirrorWarning();
        }
        finally
        {
            _isInternalUpdate = false;
        }
    }

    /// <summary>用户手动改了高级参数 → 取消场景选择，更新镜像警告。</summary>
    public void OnAdvancedParamChanged()
    {
        if (_isInternalUpdate) return;
        SelectedPresetIndex = null;
        UpdateMirrorWarning();
    }

    // ============ 内部逻辑 ============

    private void ApplyPreset(PresetProfile preset)
    {
        // 注意：本方法在 SelectPreset 内部调用，期间 _isInternalUpdate=true，
        // 通过 Binding 改这些属性时 UI 触发的事件会被 OnAdvancedParamChanged 直接返回
        MultiThread = preset.MultiThread.HasValue;
        ThreadCount = (preset.MultiThread ?? 16).ToString();
        Mirror = preset.Mirror;
        IncludeSubdirs = preset.IncludeSubdirs;
        RestartMode = preset.RestartMode;
        RetryCount = preset.RetryCount.ToString();
        RetryWait = preset.RetryWait.ToString();
        CopyAll = preset.CopyAll;
        CopyDirTimestamps = preset.CopyDirTimestamps;
        Verbose = preset.Verbose;
    }

    private void UpdateMirrorWarning()
    {
        MirrorWarningVisible = Mirror;
    }

    private PresetProfile BuildCurrentParams()
    {
        int.TryParse(ThreadCount, out var mt);
        int.TryParse(RetryCount, out var retry);
        int.TryParse(RetryWait, out var wait);

        return new PresetProfile
        {
            Name = "自定义",
            MultiThread = MultiThread && mt > 0 ? mt : null,
            Mirror = Mirror,
            IncludeSubdirs = IncludeSubdirs,
            RestartMode = RestartMode,
            RetryCount = retry > 0 ? retry : 1,
            RetryWait = wait > 0 ? wait : 3,
            CopyAll = CopyAll,
            CopyDirTimestamps = CopyDirTimestamps,
            Verbose = Verbose
        };
    }

    private void UpdateProgress(CopyProgress p)
    {
        if (p.IsCompleted)
        {
            ProgressValue = 100;
            ProgressPercent = "100%";
            return;
        }

        ProgressValue = p.Percentage;
        ProgressPercent = $"{p.Percentage:F0}%";

        ProgressFiles = p.TotalFiles > 0
            ? $"已完成 {p.FilesProcessed:N0} / {p.TotalFiles:N0} 个文件"
            : $"已完成 {p.FilesProcessed:N0} 个文件";

        ProgressBytes = p.TotalBytes > 0
            ? $"{FormatBytes(p.BytesProcessed)} / {FormatBytes(p.TotalBytes)}"
            : "";

        var elapsedSec = _elapsedSw.Elapsed.TotalSeconds;
        if (elapsedSec > 0.5 && p.BytesProcessed > 0 && p.TotalBytes > 0)
        {
            var bytesPerSec = p.BytesProcessed / elapsedSec;
            ProgressSpeed = $"速度 {FormatBytes((long)bytesPerSec)}/s";
            if (bytesPerSec > 0)
            {
                var remainingBytes = p.TotalBytes - p.BytesProcessed;
                var etaSec = remainingBytes / bytesPerSec;
                ProgressSpeed += $"  ·  剩余 {FormatTime(etaSec)}";
            }
        }
        else
        {
            ProgressSpeed = "";
        }

        ProgressTime = $"耗时 {elapsedSec:F1} 秒";

        if (!string.IsNullOrEmpty(p.CurrentFile))
        {
            ProgressFile = $"当前: {p.CurrentFile}";
            if (Verbose)
                AppendLog($"  {p.CurrentFile}");
        }
    }

    private void ShowResult(CopyResult result)
    {
        ResultVisible = true;
        if (result.Success)
        {
            StatusHeader = "复制完成";
            ResultIcon = "✓";
            ResultBadgeBrushKey = "SuccessBrush";
            ResultTitle = "复制完成！";
            ResultSummary = $"共处理 {result.TotalFiles:N0} 个文件，耗时 {result.Elapsed.TotalSeconds:F1} 秒。";
            StatusText = "复制完成";
            StatusDotBrushKey = "SuccessBrush";
            AppendLog($"复制完成！耗时 {result.Elapsed.TotalSeconds:F1}s，退出码 {result.RobocopyExitCode}");
            CurrentState = CopyState.Completed;
        }
        else if (result.ErrorMessage == "用户取消")
        {
            ShowCancelled();
        }
        else
        {
            ShowFailure("复制失败", FriendlyError(result));
        }
    }

    private void ShowCancelled()
    {
        ResultVisible = true;
        StatusHeader = "已取消";
        ResultIcon = "■";
        ResultBadgeBrushKey = "TextMutedBrush";
        ResultTitle = "已取消";
        ResultSummary = "复制已取消，已经复制过去的文件会保留在目标文件夹里。";
        StatusText = "已取消";
        StatusDotBrushKey = "TextMutedBrush";
        AppendLog("复制已取消");
        CurrentState = CopyState.Cancelled;
    }

    private void ShowFailure(string title, string detail)
    {
        ResultVisible = true;
        StatusHeader = title;
        ResultIcon = "✗";
        ResultBadgeBrushKey = "ErrorBrush";
        ResultTitle = title;
        ResultSummary = detail;
        StatusText = title;
        StatusDotBrushKey = "ErrorBrush";
        AppendLog($"{title}: {detail}");
        CurrentState = CopyState.Failed;
    }

    private static string FriendlyError(CopyResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
            return result.ErrorMessage;
        return result.RobocopyExitCode switch
        {
            >= 16 => "出现严重错误，复制没有完成。可以展开下面的详细日志看看原因。",
            >= 8 => "有些文件没有复制成功，可以展开详细日志看看是哪些文件。",
            _ => $"复制异常结束（退出码 {result.RobocopyExitCode}）。"
        };
    }

    private void ResetProgressUi()
    {
        ResultVisible = false;
        StatusHeader = "正在复制...";
        ProgressValue = 0;
        ProgressPercent = "0%";
        ProgressFiles = "已完成 0 / 0 个文件";
        ProgressBytes = "";
        ProgressSpeed = "";
        ProgressTime = "耗时 0.0 秒";
        ProgressFile = "";
        _logBuilder.Clear();
        LogText = "";
    }

    private void CollapseScenarioPanel()
    {
        var preset = SelectedPresetIndex.HasValue && SelectedPresetIndex.Value >= 0
            ? Presets[SelectedPresetIndex.Value]
            : null;
        ScenarioSummary = preset != null
            ? $"✓ 已选场景：{preset.FriendlyName} — {preset.FriendlyDescription.Split('\n')[0]}"
            : "✓ 已选场景：自定义参数";
    }

    private void RestoreScenarioPanel()
    {
        // 视觉切换由 ScenarioFullVisible / ScenarioSummaryVisible 派生属性自动处理
    }

    private void AppendLog(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        _logBuilder.Insert(0, $"[{timestamp}] {message}\r\n");

        // 超出上限按行截断
        if (_logBuilder.Length > MaxLogChars)
        {
            var keep = _logBuilder.ToString(0, MaxLogChars);
            var lastNewline = keep.LastIndexOf('\n');
            if (lastNewline > 0)
                keep = keep.Substring(0, lastNewline + 1);
            _logBuilder.Clear();
            _logBuilder.Append(keep);
            _logBuilder.Append("... (更早的日志已截断) ...\r\n");
        }

        LogText = _logBuilder.ToString();
    }

    // ============ 静态工具方法 ============

    public static string FormatBytes(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        var size = (double)bytes;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }
        return unit == 0 ? $"{size:F0} {units[unit]}" : $"{size:F2} {units[unit]}";
    }

    public static string FormatTime(double seconds)
    {
        if (seconds < 1) return "不到 1 秒";
        if (seconds < 60) return $"{seconds:F0} 秒";
        if (seconds < 3600) return $"{(int)seconds / 60} 分 {((int)seconds) % 60} 秒";
        return $"{(int)seconds / 3600} 时 {((int)seconds) % 3600 / 60} 分";
    }
}
