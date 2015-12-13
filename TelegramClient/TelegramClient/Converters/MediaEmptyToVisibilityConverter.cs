using System;
using System.Globalization;
using System.IO.IsolatedStorage;
using System.Windows;
using System.Windows.Data;
using Telegram.Api.TL;

namespace TelegramClient.Converters
{
    public class MediaEmptyToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var visibility = value is TLMessageMediaEmpty || value is TLMessageMediaWebPage;
            if (string.Equals((string) parameter, "invert", StringComparison.OrdinalIgnoreCase))
            {
                visibility = !visibility;
            }

            return visibility ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class DownloadMessageToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var message = value as TLMessage;
            if (message == null)
            {
                return Visibility.Collapsed;
            }

            return DownloadMediaToVisibilityConverter.MediaFileExists(message.Media) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class DownloadMediaToVisibilityConverter : IValueConverter
    {
        public static bool MediaFileExists(TLMessageMediaBase value)
        {
            var mediaDocument = value as TLMessageMediaDocument;
            if (mediaDocument == null)
            {
                return true;
            }

            if (!string.IsNullOrEmpty(mediaDocument.IsoFileName))
            {
                return true;
            }

            var document = mediaDocument.Document as TLDocument;
            if (document == null)
            {
                return true;
            }

            var fileName = document.GetFileName();

            using (var store = IsolatedStorageFile.GetUserStoreForApplication())
            {
                if (store.FileExists(fileName))
                {
                    return true;
                }
            }

            return false;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return MediaFileExists(value as TLMessageMediaBase) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
