using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RoboCopyGUI.ViewModels;

/// <summary>
/// INotifyPropertyChanged 实现基类。
/// 手写避免引入 CommunityToolkit.Mvvm，保持项目零 NuGet 依赖。
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 设置属性值，仅在值变化时触发 PropertyChanged。
    /// 返回 true 表示值已更新。
    /// </summary>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
