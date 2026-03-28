using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using SoftBoxLauncher.Models;

namespace SoftBoxLauncher.Converters;

public sealed class AppStateToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush InstalledBrush = new(Color.FromRgb(95, 224, 150));
    private static readonly SolidColorBrush InProgressBrush = new(Color.FromRgb(124, 202, 255));
    private static readonly SolidColorBrush ErrorBrush = new(Color.FromRgb(255, 117, 117));
    private static readonly SolidColorBrush DefaultBrush = new(Color.FromRgb(214, 222, 236));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not AppInstallState state)
        {
            return DefaultBrush;
        }

        return state switch
        {
            AppInstallState.Installed => InstalledBrush,
            AppInstallState.Downloading => InProgressBrush,
            AppInstallState.Installing => InProgressBrush,
            AppInstallState.Error => ErrorBrush,
            _ => DefaultBrush
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
