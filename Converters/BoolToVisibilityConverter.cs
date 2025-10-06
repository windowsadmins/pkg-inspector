using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace PkgInspector.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? Visibility.Collapsed : Visibility.Visible;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class SignatureBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush SignedBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80)); // Green #4CAF50
    private static readonly SolidColorBrush UnsignedBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(158, 158, 158)); // Gray #9E9E9E

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isSigned)
        {
            return isSigned ? SignedBrush : UnsignedBrush;
        }
        return UnsignedBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
