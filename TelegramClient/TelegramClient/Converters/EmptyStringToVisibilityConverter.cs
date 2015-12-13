using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Telegram.Api.TL;

namespace TelegramClient.Converters
{
    public class EmptyStringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var visibility = value == null;

            var s = value as string;
            if (s != null)
            {
                visibility = string.IsNullOrEmpty(s);
            }

            if (string.Equals(System.Convert.ToString(parameter), "invert", StringComparison.OrdinalIgnoreCase))
            {
                visibility = !visibility;
            }

            return visibility ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class EmptyTLStringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var visibility = value == null;

            var s = value as TLString;
            if (s != null)
            {
                visibility = string.IsNullOrEmpty(s.ToString());
            }

            if (string.Equals(System.Convert.ToString(parameter), "invert", StringComparison.OrdinalIgnoreCase))
            {
                visibility = !visibility;
            }

            return visibility ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
