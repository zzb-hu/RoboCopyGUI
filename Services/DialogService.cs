using System.Windows;
using System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;
using MessageBoxImage = System.Windows.MessageBoxImage;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxResult = System.Windows.MessageBoxResult;
using MessageBoxOptions = System.Windows.MessageBoxOptions;

namespace RoboCopyGUI.Services;

/// <summary>
/// IDialogService 的 WPF 实现，包装 MessageBox 和 FolderBrowserDialog。
/// </summary>
public class DialogService : IDialogService
{
    public void ShowMessage(string title, string content)
        => MessageBox.Show(content, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public bool Confirm(string message, string title, bool isDanger = false)
    {
        var image = isDanger ? MessageBoxImage.Warning : MessageBoxImage.Question;
        var defaultResult = isDanger ? MessageBoxResult.No : MessageBoxResult.Yes;
        return MessageBox.Show(message, title, MessageBoxButton.YesNo, image, defaultResult) == MessageBoxResult.Yes;
    }

    public bool ConfirmDangerTwice(string firstMessage, string firstTitle,
                                   string secondMessage, string secondTitle)
    {
        // 第一道：黄色警告，默认 No（防误点回车）
        var r1 = MessageBox.Show(firstMessage, firstTitle,
            MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (r1 != MessageBoxResult.Yes) return false;

        // 第二道：红色 Error，默认 No
        var r2 = MessageBox.Show(secondMessage, secondTitle,
            MessageBoxButton.YesNo, MessageBoxImage.Error, MessageBoxResult.No);
        return r2 == MessageBoxResult.Yes;
    }

    public string? PickFolder(string description, bool allowCreateNew)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = description,
            ShowNewFolderButton = allowCreateNew
        };
        return dialog.ShowDialog() == DialogResult.OK
            ? dialog.SelectedPath
            : null;
    }
}
