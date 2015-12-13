using System;
using System.Globalization;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using Microsoft.Phone;
using Telegram.Api.TL;
using TelegramClient.Helpers;

namespace TelegramClient.Converters
{
    public class PhotoToThumbConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var decryptedMediaPhoto = value as TLDecryptedMessageThumbMediaBase;
            if (decryptedMediaPhoto != null)
            {
                var buffer = decryptedMediaPhoto.Thumb.Data;

                if (buffer.Length > 0
                    && decryptedMediaPhoto.ThumbW.Value > 0
                    && decryptedMediaPhoto.ThumbH.Value > 0)
                {
                    var memoryStream = new MemoryStream(buffer);
                    var bitmap = PictureDecoder.DecodeJpeg(memoryStream);

                    bitmap.BoxBlur(37);

                    var blurredStream = new MemoryStream();
                    bitmap.SaveJpeg(blurredStream, decryptedMediaPhoto.ThumbW.Value, decryptedMediaPhoto.ThumbH.Value, 0, 70);

                    return ImageUtils.CreateImage(blurredStream.ToArray());
                }

                return null;
            }

            var mediaPhoto = value as TLMessageMediaPhoto;
            if (mediaPhoto == null)
            {
                return null;
            }

            var photo = mediaPhoto.Photo as TLPhoto;
            if (photo == null)
            {
                return null;
            }

            var cachedSize = (TLPhotoCachedSize)photo.Sizes.FirstOrDefault(x => x is TLPhotoCachedSize);
            if (cachedSize != null)
            {
                var buffer = cachedSize.Bytes.Data;

                //return ImageUtils.CreateImage(buffer);

                if (buffer != null && buffer.Length > 0)
                {
                    var fileName = string.Format("preview{0}.jpg", photo.Id);
                    using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                    {
                        if (store.FileExists(fileName))
                        {
                            BitmapImage imageSource;

                            try
                            {
                                using (var stream = store.OpenFile(fileName, FileMode.Open, FileAccess.Read))
                                {
                                    stream.Seek(0, SeekOrigin.Begin);
                                    var image = new BitmapImage();
                                    image.SetSource(stream);
                                    imageSource = image;
                                }
                            }
                            catch (Exception)
                            {
                                return null;
                            }

                            return imageSource;
                        }
                    }

                    var memoryStream = new MemoryStream(buffer);
                    var bitmap = PictureDecoder.DecodeJpeg(memoryStream);

                    bitmap.BoxBlur(7);

                    var blurredStream = new MemoryStream();
                    bitmap.SaveJpeg(blurredStream, cachedSize.W.Value, cachedSize.H.Value, 0, 70);

                    Telegram.Api.Helpers.Execute.BeginOnThreadPool(() =>
                    {
                        using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                        {
                            try
                            {
                                using (var stream = store.OpenFile(fileName, FileMode.OpenOrCreate, FileAccess.Write))
                                {
                                    buffer = blurredStream.ToArray();
                                    stream.Seek(0, SeekOrigin.Begin);
                                    stream.Write(buffer, 0, buffer.Length);
                                }
                            }
                            catch (Exception)
                            {

                            }
                        }
                    });

                    return ImageUtils.CreateImage(blurredStream.ToArray());
                }

                return null;
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
