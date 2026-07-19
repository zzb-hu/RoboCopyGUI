namespace RoboCopyGUI.Services;

/// <summary>
/// UI 弹窗抽象。把 ViewModel 跟具体的 MessageBox/FolderBrowserDialog 解耦，
/// 便于在测试中 mock，也便于将来换 UI 框架。
/// </summary>
public interface IDialogService
{
    /// <summary>普通信息提示框。</summary>
    void ShowMessage(string title, string content);

    /// <summary>Yes/No 确认框，返回 true 表示用户选 Yes。</summary>
    bool Confirm(string message, string title, bool isDanger = false);

    /// <summary>
    /// 两道强确认（用于危险操作）。任一道点 No 都返回 false。
    /// </summary>
    bool ConfirmDangerTwice(string firstMessage, string firstTitle,
                            string secondMessage, string secondTitle);

    /// <summary>弹文件夹选择对话框，返回所选路径；用户取消返回 null。</summary>
    string? PickFolder(string description, bool allowCreateNew);
}
