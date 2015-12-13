using System;
using System.Globalization;
using System.Windows.Data;
using Telegram.Api.TL;

namespace TelegramClient.Converters
{
    public class GeoPointToStaticGoogleMapsConverter : IValueConverter
    {
        private const int DefaultWidth = 311;
        private const int DefaultHeight = 150;

        public int Width { get; set; }

        public int Height { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            NumberFormatInfo nfi = new NumberFormatInfo();
            nfi.NumberDecimalSeparator = ".";

            var geoPoint = value as TLGeoPoint;

            if (geoPoint == null) return null;

            var width = Width != 0 ? Width : DefaultWidth;
            var height = Height != 0 ? Height : DefaultHeight;
            return string.Format(Constants.StaticGoogleMap, geoPoint.Lat.Value.ToString(nfi), geoPoint.Long.Value.ToString(nfi), width, height);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
