using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace TelegramClient.Converters
{
    public class IsSelectedToBackgroundConverter : IValueConverter
    {
        public Color SelectedColor = Color.FromArgb(0, 0, 0, 0);

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var notSelectedColor = (Color)Application.Current.Resources["PhoneBackgroundColor"];
            notSelectedColor.A = 80;

            return (bool)value ? new SolidColorBrush(SelectedColor) : new SolidColorBrush(notSelectedColor);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
