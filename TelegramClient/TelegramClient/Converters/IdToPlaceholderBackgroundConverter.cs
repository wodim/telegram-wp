using System;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Data;
using Caliburn.Micro;
using TelegramClient.Services;

namespace TelegramClient.Converters
{
    public class IdToPlaceholderBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is int) || ((int)value) == -1) return Application.Current.Resources["PhoneChromeBrush2"];

            if (((int)value) == -2) return Application.Current.Resources["PhoneAccentBrush"];

            var currentUserId = IoC.Get<IStateService>().CurrentUserId;

            var number = Math.Abs(MD5Core.GetHash(string.Format("{0}{1}", value, currentUserId))[Math.Abs((int)value % 16)]) % 8;

            //var number = (int) value % 8;
            switch (number)
            {
                case 0:
                    return Application.Current.Resources["BlueBrush"];
                case 1:
                    return Application.Current.Resources["CyanBrush"];
                case 2:
                    return Application.Current.Resources["GreenBrush"];
                case 3:
                    return Application.Current.Resources["OrangeBrush"];
                case 4:
                    return Application.Current.Resources["PinkBrush"];
                case 5:
                    return Application.Current.Resources["PurpleBrush"];
                case 6:
                    return Application.Current.Resources["RedBrush"];
                case 7:
                    return Application.Current.Resources["YellowBrush"];
            }

            return Application.Current.Resources["PhoneChromeBrush2"];
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
