using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace RealtimePlottingApp.Converters;

public class HexConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int intValue)
        {
            return intValue == 0 ? "" : intValue.ToString(); // Return empty if 0
        }
        return "";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string s) return 0; // Fallback if parse messes up
        s = s.Trim();
        if (string.IsNullOrEmpty(s)) return 0; // If empty, set to 0
            
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) // Hex input!
        {
            if (int.TryParse(s.AsSpan(2), NumberStyles.HexNumber, culture, out int hexResult))
                return hexResult;
        }
        else if (int.TryParse(s, out int decResult)) // Decimal input
        {
            return decResult;
        }
        return 0; // Fallback value if parsing fails
    }
}