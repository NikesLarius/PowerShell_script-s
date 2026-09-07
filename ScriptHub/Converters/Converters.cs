using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using ScriptHub.Models;
using Wpf.Ui.Controls;

namespace ScriptHub.Converters;

public class ScriptTypeToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ScriptType type)
        {
            return type switch
            {
                ScriptType.PowerShell => new SolidColorBrush(Color.FromRgb(0, 120, 212)), // #0078D4
                ScriptType.Batch => new SolidColorBrush(Color.FromRgb(216, 59, 1)),       // #D83B01
                ScriptType.Cmd => new SolidColorBrush(Color.FromRgb(16, 124, 65)),        // #107C41
                _ => new SolidColorBrush(Color.FromRgb(136, 23, 152))                     // #881798
            };
        }
        return new SolidColorBrush(Color.FromRgb(0, 120, 212));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class ScriptTypeToIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ScriptType type)
        {
            return type switch
            {
                ScriptType.PowerShell => SymbolRegular.Flash24,
                ScriptType.Batch => SymbolRegular.Apps24,
                ScriptType.Cmd => SymbolRegular.WindowConsole20,
                _ => SymbolRegular.Document24
            };
        }
        return SymbolRegular.Document24;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class StatusToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ExecutionStatus status)
        {
            return status switch
            {
                ExecutionStatus.Running => new SolidColorBrush(Color.FromRgb(0, 120, 212)),  // Blue
                ExecutionStatus.Success => new SolidColorBrush(Color.FromRgb(16, 124, 65)),  // Green
                ExecutionStatus.Failed => new SolidColorBrush(Color.FromRgb(232, 17, 35)),   // Red
                ExecutionStatus.Cancelled => new SolidColorBrush(Color.FromRgb(247, 99, 12)),// Orange
                _ => new SolidColorBrush(Color.FromRgb(128, 128, 128))                       // Gray
            };
        }
        return new SolidColorBrush(Colors.Transparent);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class StatusToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ExecutionStatus status)
        {
            return status switch
            {
                ExecutionStatus.Running => "Выполняется...",
                ExecutionStatus.Success => "Успешно",
                ExecutionStatus.Failed => "Ошибка",
                ExecutionStatus.Cancelled => "Остановлен",
                _ => "Готов"
            };
        }
        return "";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class BoolToVisibilityConverter : IValueConverter
{
    public bool Inverted { get; set; } = false;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool b = value is true;
        if (Inverted) b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class NullOrEmptyToVisibilityConverter : IValueConverter
{
    public bool Inverted { get; set; } = false;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isEmpty = value == null || string.IsNullOrWhiteSpace(value.ToString());
        if (Inverted) isEmpty = !isEmpty;
        return isEmpty ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class HexToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrWhiteSpace(hex))
        {
            try
            {
                return (SolidColorBrush)new BrushConverter().ConvertFrom(hex)!;
            }
            catch { }
        }
        return new SolidColorBrush(Color.FromRgb(0, 120, 212));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class StringEqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null) return false;
        return string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true && parameter != null)
        {
            return parameter.ToString()!;
        }
        return Binding.DoNothing;
    }
}
