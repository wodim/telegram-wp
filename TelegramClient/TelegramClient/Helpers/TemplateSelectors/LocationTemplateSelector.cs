using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Telegram.Api.TL;

namespace TelegramClient.Helpers.TemplateSelectors
{
    public class LocationTemplateSelector : IValueConverter
    {
        public DataTemplate GeoTemplate { get; set; }

        public DataTemplate VenueTemplate { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var message = value as TLMessage25;
            if (message != null)
            {
                if (message.Media is TLMessageMediaVenue)
                {
                    return VenueTemplate;
                }

                if (message.Media is TLMessageMediaGeo)
                {
                    return GeoTemplate;
                }
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
