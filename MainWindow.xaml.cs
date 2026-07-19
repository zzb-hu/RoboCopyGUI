using RoboCopyGUI.Models;
using RoboCopyGUI.Services;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Brush = System.Windows.Media.Brush;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using RadioButton = System.Windows.Controls.RadioButton;
using TextBox = System.Windows.Controls.TextBox;

namespace RoboCopyGUI;

public partial class MainWindow : Window
{
    private readonly RobocopyService _service = new();
    private readonly List<PresetProfile> _presets = PresetProfile.GetDefaults();
    private readonly List<RadioButton> _cards = new();
    private readonly Stopwatch _elapsedSw = new();
    private CancellationTokenSource? _cts;
    private bool _isInternalUpdate;

    public MainWindow()
    {
        InitializeComponent();
        _cards.AddRange(new[] { Card0, Card1, Card2, Card3 });
        for (var i = 0; i < _cards.Count; i++)
            _cards[i].DataContext = _presets[i];
        _cards[0].IsChecked = true;
    }

    // 深色标题栏（Win10 1809+ / Win11）
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var useDark = 1;
        _ = DwmSetWindowAttribute(hwnd, 20, ref useDark, sizeof(int));
    }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        System.IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    // ============ 第 1 步：文件夹选择 ============

    private void BrowseSource_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "选择要复制的文件夹",
            ShowNewFolderButton = false
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            SourcePathBox.Text = dialog.SelectedPath;
    }

    private void BrowseDest_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "选择复制到哪个文件夹",
            ShowNewFolderButton = true
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            DestPathBox.Text = dialog.SelectedPath;
    }

    private void PathBox_PreviewDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void SourcePathBox_Drop(object sender, DragEventArgs e) => HandlePathDrop(e, SourcePathBox);

    private void DestPathBox_Drop(object sender, DragEventArgs e) => HandlePathDrop(e, DestPathBox);

    private void HandlePathDrop(DragEventArgs e, TextBox box)
    {
        e.Handled = true;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length == 0)
            return;
        if (Directory.Exists(paths[0]))
            box.Text = paths[0];
        else
            ShowMessage("提示", "拖进来的不是文件夹，请拖入一个文件夹。");
    }

    // ============ 第 2 步：场景卡片 ============

    private void ScenarioCard_Checked(object sender, RoutedEventArgs e)
    {
        if (_isInternalUpdate) return;
        if (sender is RadioButton { DataContext: PresetProfile preset })
        {
            ApplyPreset(preset);
            UpdateMirrorWarning();
        }
    }

    private void ApplyPreset(PresetProfile preset)
    {
        _isInternalUpdate = true;
        MultiThreadCheck.IsChecked = preset.MultiThread.HasValue;
        ThreadCountBox.Text = (preset.MultiThread ?? 16).ToString();
        MirrorCheck.IsChecked = preset.Mirror;
        SubdirsCheck.IsChecked = preset.IncludeSubdirs;
        RestartCheck.IsChecked = preset.RestartMode;
        RetryCountBox.Text = preset.RetryCount.ToString();
        RetryWaitBox.Text = preset.RetryWait.ToString();
        CopyAllCheck.IsChecked = preset.CopyAll;
        DirTimeCheck.IsChecked = preset.CopyDirTimestamps;
        VerboseCheck.IsChecked = preset.Verbose;
        _isInternalUpdate = false;
    }

    private void ParamChanged(object sender, RoutedEventArgs e)
    {
        if (_isInternalUpdate) return;
        // 手动改了高级参数 → 不再匹配任何场景卡
        foreach (var card in _cards)
            card.IsChecked = false;
        UpdateMirrorWarning();
    }

    private void UpdateMirrorWarning()
    {
        MirrorWarning.Visibility = MirrorCheck.IsChecked == true
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void NumberOnly_Preview(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$");
    }

    // ============ 第 3 步：开始 / 取消 ============

    private async void StartCopy_Click(object sender, RoutedEventArgs e)
    {
        var source = SourcePathBox.Text.Trim();
        var dest = DestPathBox.Text.Trim();

        // 傻瓜化校验：大白话提示
        if (string.IsNullOrEmpty(source))
        {
            ShowMessage("提示", "请先选择要复制的文件夹。");
            return;
        }
        if (!Directory.Exists(source))
        {
            ShowMessage("提示", "源文件夹不存在，请检查一下路径是不是输错了。");
            return;
        }
        if (string.IsNullOrEmpty(dest))
        {
            ShowMessage("提示", "请选择复制到哪个文件夹。");
            return;
        }
        var fullSource = Path.GetFullPath(source).TrimEnd('\\') + "\\";
        var fullDest = Path.GetFullPath(dest).TrimEnd('\\') + "\\";
        if (string.Equals(fullSource, fullDest, StringComparison.OrdinalIgnoreCase))
        {
            ShowMessage("提示", "源文件夹和目标不能是同一个。");
            return;
        }
        if (fullDest.StartsWith(fullSource, StringComparison.OrdinalIgnoreCase))
        {
            ShowMessage("提示", "目标文件夹不能在源文件夹里面，不然会陷入死循环。");
            return;
        }
        if (MirrorCheck.IsChecked == true)
        {
            var confirm = System.Windows.MessageBox.Show(
                "完全同步 / 增量备份会删除目标文件夹里多出来的文件，\n确定继续吗？",
                "删除确认",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes)
                return;
        }

        Directory.CreateDirectory(dest);
        var preset = BuildCurrentParams();
        var args = preset.ToRobocopyArgs();

        ResetProgressUi();
        CollapseScenarioPanel();
        AppendLog($"开始复制: \"{source}\" → \"{dest}\"");
        AppendLog($"参数: {args}");

        StartBtn.IsEnabled = false;
        CancelBtn.Visibility = Visibility.Visible;
        _cts = new CancellationTokenSource();
        _elapsedSw.Restart();
        StatusText.Text = "正在复制...";
        SetStatusDot("AccentBrush");

        var progress = new Progress<CopyProgress>(p =>
            Dispatcher.Invoke(() => UpdateProgress(p)));

        try
        {
            var result = await Task.Run(() =>
                _service.RunCopyAsync(source, dest, args, progress, _cts.Token));
            _elapsedSw.Stop();
            ShowResult(result);
        }
        catch (Exception ex)
        {
            _elapsedSw.Stop();
            AppendLog($"异常: {ex.Message}");
            ShowFailure("复制过程中出了点问题", ex.Message);
        }

        StartBtn.IsEnabled = true;
        CancelBtn.Visibility = Visibility.Collapsed;
        RestoreScenarioPanel();
    }

    private void CancelCopy_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        AppendLog("用户请求取消...");
        StatusText.Text = "正在取消...";
    }

    // ============ 进度 / 结果展示 ============

    private void UpdateProgress(CopyProgress p)
    {
        if (p.IsCompleted)
        {
            CopyProgressBar.Value = 100;
            ProgressPercent.Text = "100%";
            return;
        }

        CopyProgressBar.Value = p.Percentage;
        ProgressPercent.Text = $"{p.Percentage:F0}%";
        ProgressFiles.Text = $"已完成 {p.FilesProcessed:N0} / {p.TotalFiles:N0} 个文件";
        ProgressTime.Text = $"耗时 {_elapsedSw.Elapsed.TotalSeconds:F1} 秒";

        if (!string.IsNullOrEmpty(p.CurrentFile))
        {
            ProgressFile.Text = $"当前: {p.CurrentFile}";
            if (VerboseCheck.IsChecked == true)
                AppendLog($"  {p.CurrentFile}");
        }
    }

    private void ShowResult(CopyResult result)
    {
        ResultPanel.Visibility = Visibility.Visible;
        if (result.Success)
        {
            StatusHeader.Text = "复制完成";
            ResultIcon.Text = "✓";
            ResultBadge.Background = (Brush)FindResource("SuccessBrush");
            ResultTitle.Text = "复制完成！";
            ResultSummary.Text = $"共处理 {result.TotalFiles:N0} 个文件，耗时 {result.Elapsed.TotalSeconds:F1} 秒。";
            StatusText.Text = "复制完成";
            SetStatusDot("SuccessBrush");
            AppendLog($"复制完成！耗时 {result.Elapsed.TotalSeconds:F1}s，退出码 {result.RobocopyExitCode}");
        }
        else if (result.ErrorMessage == "用户取消")
        {
            StatusHeader.Text = "已取消";
            ResultIcon.Text = "■";
            ResultBadge.Background = (Brush)FindResource("TextMutedBrush");
            ResultTitle.Text = "已取消";
            ResultSummary.Text = "复制已取消，已经复制过去的文件会保留在目标文件夹里。";
            StatusText.Text = "已取消";
            SetStatusDot("TextMutedBrush");
            AppendLog("复制已取消");
        }
        else
        {
            ShowFailure("复制失败", FriendlyError(result));
        }
    }

    private void ShowFailure(string title, string detail)
    {
        ResultPanel.Visibility = Visibility.Visible;
        StatusHeader.Text = title;
        ResultIcon.Text = "✗";
        ResultBadge.Background = (Brush)FindResource("ErrorBrush");
        ResultTitle.Text = title;
        ResultSummary.Text = detail;
        StatusText.Text = title;
        SetStatusDot("ErrorBrush");
        AppendLog($"{title}: {detail}");
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
        ProgressCard.Visibility = Visibility.Visible;
        ResultPanel.Visibility = Visibility.Collapsed;
        StatusHeader.Text = "正在复制...";
        CopyProgressBar.Value = 0;
        ProgressPercent.Text = "0%";
        ProgressFiles.Text = "已完成 0 / 0 个文件";
        ProgressTime.Text = "耗时 0.0 秒";
        ProgressFile.Text = "";
        LogBox.Text = "";
    }

    // 拷贝进行中：场景卡片折叠为一行摘要（腾出空间给进度卡片，避免滚动条）
    private void CollapseScenarioPanel()
    {
        var preset = _cards.FirstOrDefault(c => c.IsChecked == true)?.DataContext as PresetProfile;
        ScenarioSummary.Text = preset != null
            ? $"✓ 已选场景：{preset.FriendlyName} — {preset.FriendlyDescription.Split('\n')[0]}"
            : "✓ 已选场景：自定义参数";
        ScenarioFullPanel.Visibility = Visibility.Collapsed;
        ScenarioSummary.Visibility = Visibility.Visible;
    }

    private void RestoreScenarioPanel()
    {
        ScenarioFullPanel.Visibility = Visibility.Visible;
        ScenarioSummary.Visibility = Visibility.Collapsed;
    }

    private void SetStatusDot(string brushKey)
    {
        StatusDot.Fill = (Brush)FindResource(brushKey);
    }

    // ============ 参数构建 / 日志 ============

    private PresetProfile BuildCurrentParams()
    {
        int.TryParse(ThreadCountBox.Text, out var mt);
        int.TryParse(RetryCountBox.Text, out var retry);
        int.TryParse(RetryWaitBox.Text, out var wait);

        return new PresetProfile
        {
            Name = "自定义",
            MultiThread = MultiThreadCheck.IsChecked == true && mt > 0 ? mt : null,
            Mirror = MirrorCheck.IsChecked == true,
            IncludeSubdirs = SubdirsCheck.IsChecked != false,
            RestartMode = RestartCheck.IsChecked == true,
            RetryCount = retry > 0 ? retry : 1,
            RetryWait = wait > 0 ? wait : 3,
            CopyAll = CopyAllCheck.IsChecked == true,
            CopyDirTimestamps = DirTimeCheck.IsChecked == true,
            Verbose = VerboseCheck.IsChecked == true
        };
    }

    private void AppendLog(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var line = $"[{timestamp}] {message}";
        LogBox.Text = LogBox.Text.Length > 30000
            ? line + "\n... (日志已截断) ...\n"
            : line + "\n" + LogBox.Text;
    }

    private void ShowMessage(string title, string content)
    {
        System.Windows.MessageBox.Show(content, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
