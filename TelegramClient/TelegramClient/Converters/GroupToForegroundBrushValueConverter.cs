using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Telegram.Api.TL;
using TelegramClient.Models;
using TelegramClient.ViewModels.Additional;

namespace TelegramClient.Converters
{
    public class GroupToForegroundBrushValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var group = value as AlphaKeyGroup<TLUserBase>;
            object result = null;

            if (group != null)
            {
                if (group.Count == 0)
                {
                    result = Application.Current.Resources["PhoneDisabledBrush"];
                }
                else
                {
                    result = new SolidColorBrush(Colors.White);
                }
            }

            return result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class CountryGroupToForegroundBrushValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var group = value as CountriesInGroup;
            object result = null;

            if (group != null)
            {
                if (group.Count == 0)
                {
                    result = Application.Current.Resources["PhoneDisabledBrush"];
                }
                else
                {
                    result = new SolidColorBrush(Colors.White);
                }
            }

            return result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
