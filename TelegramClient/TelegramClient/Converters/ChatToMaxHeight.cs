using System;
using System.Globalization;
using System.Windows.Data;

namespace TelegramClient.Converters
{
    public class ChatToMaxHeight : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool) value ? 22.0 : 44.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
