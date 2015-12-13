using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Telegram.Api.TL;

namespace TelegramClient.Converters
{
    public class NotServiceMessageToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var isServiceMessage = value is TLMessageService;

            if (parameter != null
                && string.Equals(parameter.ToString(), "invert", StringComparison.OrdinalIgnoreCase))
            {
                isServiceMessage = !isServiceMessage;
            }

            return isServiceMessage ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
