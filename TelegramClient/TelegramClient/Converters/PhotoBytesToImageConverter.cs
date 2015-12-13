using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace TelegramClient.Converters
{
    public class PhotoBytesToImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var buffer = value as byte[];
            if (buffer == null) return null;

            BitmapImage imageSource;

            try
            {
                using (var stream = new MemoryStream(buffer))
                {
                    stream.Seek(0, SeekOrigin.Begin);
                    var b = new BitmapImage();
                    b.SetSource(stream);
                    imageSource = b;
                }
            }
            catch (Exception)
            {
                return null;
            }

            return imageSource;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
