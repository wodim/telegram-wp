using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Telegram.Api.TL;
using TelegramClient.Models;
using TelegramClient.ViewModels.Additional;

namespace TelegramClient.Converters
{
    public class GroupToBackgroundBrushValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var group = value as AlphaKeyGroup<TLUserBase>;
            object result = null;

            if (group != null)
            {
                if (group.Count == 0)
                {
                    result = Application.Current.Resources["PhoneChromeBrush"];
                }
                else
                {
                    result = Application.Current.Resources["PhoneAccentBrush"];
                }
            }

            return result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class CountryGroupToBackgroundBrushValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var group = value as CountriesInGroup;
            object result = null;

            if (group != null)
            {
                if (group.Count == 0)
                {
                    result = Application.Current.Resources["PhoneChromeBrush"];
                }
                else
                {
                    result = Application.Current.Resources["PhoneAccentBrush"];
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
