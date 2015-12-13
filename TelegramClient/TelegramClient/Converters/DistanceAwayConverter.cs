using System;
using System.Globalization;
using System.Windows.Data;
using TelegramClient.Resources;

namespace TelegramClient.Converters
{
    public class DistanceAwayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is double)) return value;

            var distance = (double) value;

            if (distance < 1000)
            {
                return string.Format(AppResources.DistanceAway, (int) distance + AppResources.MetersShort);
            }

            return string.Format(AppResources.DistanceAway, String.Format("{0:0.#}", distance / 1000) + AppResources.KilometersShort);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
