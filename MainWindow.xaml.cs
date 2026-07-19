using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using RoboCopyGUI.ViewModels;
using Brush = System.Windows.Media.Brush;
using DataFormats = System.Windows.DataFormats;
using DataObject = System.Windows.DataObject;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using RadioButton = System.Windows.Controls.RadioButton;
using TextBox = System.Windows.Controls.TextBox;

namespace RoboCopyGUI;

/// <summary>
/// 主窗口的薄 code-behind。
/// 所有业务逻辑在 <see cref="MainViewModel"/>。本类只做：
/// 1. 构造 + DataContext 绑定
/// 2. 深色标题栏 P/Invoke
/// 3. UI 事件 → ViewModel 方法转发（拖拽、粘贴校验、RadioButton/CheckBox 状态变化）
/// 视觉树完全不变。
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly List<RadioButton> _cards = new();

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.SetDispatcher(Dispatcher);

        // 场景卡片 DataContext 仍由 code-behind 设置（保持视觉行为不变）
        _cards.AddRange(new[] { Card0, Card1, Card2, Card3 });
        for (var i = 0; i < _cards.Count; i++)
            _cards[i].DataContext = viewModel.Presets[i];
        _cards[0].IsChecked = true;

        // ContextMenu 不在视觉树里，不会继承 Window 的 DataContext，手动设
        SourcePathMenu.DataContext = viewModel;
        DestPathMenu.DataContext = viewModel;

        // 窗口关闭时保存设置（最近路径、场景、窗口尺寸）
        Closing += MainWindow_Closing;
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _viewModel.SaveWindowSettings(ActualWidth, ActualHeight, Left, Top);
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

    // ============ 拖拽：转发到 ViewModel 的 SourcePath / DestPath ============

    private void PathBox_PreviewDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void SourcePathBox_Drop(object sender, DragEventArgs e) => HandlePathDrop(e, isSource: true);
    private void DestPathBox_Drop(object sender, DragEventArgs e) => HandlePathDrop(e, isSource: false);

    private void HandlePathDrop(DragEventArgs e, bool isSource)
    {
        e.Handled = true;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length == 0)
            return;

        if (paths.Length > 1)
        {
            _viewModel_sourcePathMultiDropWarning(paths[0]);
        }

        if (System.IO.Directory.Exists(paths[0]))
        {
            if (isSource)
                _viewModel.SourcePath = paths[0];
            else
                _viewModel.DestPath = paths[0];
        }
        else
        {
            // 复用 ViewModel 通过 IDialogService 弹提示（避免 code-behind 直接调 MessageBox）
            _viewModel.ShowDragNotFolderWarning();
        }
    }

    // 临时辅助：拖拽多文件提示和"非文件夹"提示都通过 ViewModel 走 IDialogService
    // 用方法名区分，便于阅读
    private void _viewModel_sourcePathMultiDropWarning(string firstPath)
        => _viewModel.ShowDragMultiFolderWarning(firstPath);

    // ============ 数字输入校验：纯 UI 行为，保留在 code-behind ============

    private void NumberOnly_Preview(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$");
    }

    private void NumberOnly_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetDataPresent(DataFormats.Text))
        {
            var text = e.DataObject.GetData(DataFormats.Text) as string ?? "";
            if (!Regex.IsMatch(text, "^[0-9]+$"))
                e.CancelCommand();
        }
        else
        {
            e.CancelCommand();
        }
    }

    // ============ 场景卡片 / 高级参数变化：转发到 ViewModel ============

    private void ScenarioCard_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton rb) return;
        // 找出是第几个卡片
        var index = _cards.IndexOf(rb);
        if (index >= 0 && rb.IsChecked == true)
            _viewModel.SelectPreset(index);
    }

    private void ParamChanged(object sender, RoutedEventArgs e)
    {
        _viewModel.OnAdvancedParamChanged();
    }
}
