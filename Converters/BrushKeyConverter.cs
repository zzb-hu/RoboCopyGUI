using System.Globalization;
using System.Windows.Data;

namespace RoboCopyGUI.Converters;

/// <summary>
/// 把资源 key 字符串（如 "SuccessBrush"）转成 App.xaml 中的 Brush 资源。
/// 让 ViewModel 用字符串而非 Brush 对象，避免引用 WPF 类型。
/// </summary>
public class BrushKeyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string key && !string.IsNullOrEmpty(key))
        {
            if (System.Windows.Application.Current?.TryFindResource(key) is System.Windows.Media.Brush brush)
                return brush;
        }
        return System.Windows.Media.Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
