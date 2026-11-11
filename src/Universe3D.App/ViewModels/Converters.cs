using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Universe3D.App.ViewModels;

/// <summary>Binds a radio button to an enum value: <c>IsChecked="{Binding Mode, Converter=..., ConverterParameter=Observed}"</c>.</summary>
public sealed class EnumMatchConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value?.ToString() == parameter as string;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true && parameter is string name ? Enum.Parse(targetType, name) : Binding.DoNothing;
}

/// <summary>Collapsed for null, an empty string or an empty list.</summary>
public sealed class EmptyToCollapsedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is null || value is string { Length: 0 } || value is System.Collections.ICollection { Count: 0 }
            ? Visibility.Collapsed
            : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
