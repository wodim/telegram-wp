using System;
using System.Globalization;
using System.IO;
using System.IO.IsolatedStorage;
using System.Windows.Data;
using ImageTools;

namespace TelegramClient.Converters
{
    public class ExtendedImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var imageSource = value as string;
            if (imageSource != null)
            {
                using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                {
                    if (store.FileExists(imageSource))
                    {
                        var file = store.OpenFile(imageSource, FileMode.Open, FileAccess.Read);
                        {

                            var image = new ExtendedImage();
                            image.LoadingCompleted += (sender, args) =>
                            {
                                var count = image.Frames.Count;
                            };
                            image.LoadingFailed += (sender, args) =>
                            {

                            };
                            image.SetSource(file);
                            return image;
                        }
                    }
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