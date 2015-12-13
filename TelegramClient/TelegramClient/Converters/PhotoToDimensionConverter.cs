using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using Telegram.Api;
using Telegram.Api.TL;

namespace TelegramClient.Converters
{
    public class WebPageToDimensionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            const double width = 311.0-12.0;
            if (string.Equals((string)parameter, "Width", StringComparison.OrdinalIgnoreCase))
            {
                return width;
            }

            var mediaWebPage = value as TLMessageMediaWebPage;
            if (mediaWebPage == null) return null;

            var webPage = mediaWebPage.WebPage as TLWebPage;
            if (webPage == null) return null;

            var photo = webPage.Photo as TLPhoto;
            if (photo == null)
            {
                return double.NaN;
            }

            TLPhotoSize size = null;
            var sizes = photo.Sizes.OfType<TLPhotoSize>();
            foreach (var photoSize in sizes)
            {
                if (size == null
                    || Math.Abs(width - size.W.Value) > Math.Abs(width - photoSize.W.Value))
                {
                    size = photoSize;
                }
            }

            if (size != null)
            {
                return width / size.W.Value * size.H.Value; //* 0.75;
            }

            return double.NaN;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class DocumentToDimensionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            const double width = Constants.DefaultMessageContentWidth;
            if (string.Equals((string)parameter, "Width", StringComparison.OrdinalIgnoreCase))
            {
                return width;
            }

            var media = value as TLMessageMediaDocument;
            if (media == null)
            {
                return double.NaN;
            }

            var document = media.Document as TLDocument;
            if (document == null)
            {
                return double.NaN;
            }

            if (string.Equals(document.MimeType.ToString(), "image/webp", StringComparison.OrdinalIgnoreCase))
            {
                return double.NaN;
            }

            var size = document.Thumb as TLPhotoSize;
            if (size != null)
            {
                return width / size.W.Value * size.H.Value;
            }

            var cachedSize = document.Thumb as TLPhotoCachedSize;
            if (cachedSize != null)
            {
                return width / cachedSize.W.Value * cachedSize.H.Value;
            }

            return double.NaN;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class VideoToDimensionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            const double width = Constants.DefaultMessageContentWidth;
            if (string.Equals((string)parameter, "Width", StringComparison.OrdinalIgnoreCase))
            {
                return width;
            }

            var media = value as TLMessageMediaVideo;
            if (media == null)
            {
                return double.NaN;
            }

            var video = media.Video as TLVideo;
            if (video == null)
            {
                return double.NaN;
            }

            var size = video.Thumb as TLPhotoSize;
            if (size != null)
            {
                return width / size.W.Value * size.H.Value;
            }

            var cachedSize = video.Thumb as TLPhotoCachedSize;
            if (cachedSize != null)
            {
                return width / cachedSize.W.Value * cachedSize.H.Value;
            }

            return double.NaN;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class PhotoToDimensionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            const double width = Constants.DefaultMessageContentWidth;
            if (string.Equals((string)parameter, "Width", StringComparison.OrdinalIgnoreCase))
            {
                return width;
            }

            var mediaPhoto = value as TLMessageMediaPhoto;
            if (mediaPhoto == null)
            {
                var decryptedMediaPhoto = value as TLDecryptedMessageMediaPhoto;
                if (decryptedMediaPhoto != null)
                {
                    return width / decryptedMediaPhoto.ThumbW.Value * decryptedMediaPhoto.ThumbH.Value;
                }

                return double.NaN;
            }

            var photo = mediaPhoto.Photo as TLPhoto;
            if (photo == null)
            {
                return double.NaN;
            }

            TLPhotoSize size = null;
            var sizes = photo.Sizes.OfType<TLPhotoSize>();
            foreach (var photoSize in sizes)
            {
                if (size == null
                    || Math.Abs(width - size.W.Value) > Math.Abs(width - photoSize.W.Value))
                {
                    size = photoSize;
                }
            }

            if (size != null)
            {
                return width / size.W.Value * size.H.Value;
            }

            return double.NaN;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StickerToDimensionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var isWidth = string.Equals((string) parameter, "Width", StringComparison.OrdinalIgnoreCase);
            
            var sticker = value as IAttributes;
            if (sticker != null)
            {
                TLDocumentAttributeImageSize imageSizeAttribute = null;
                for (var i = 0; i < sticker.Attributes.Count; i++)
                {
                    imageSizeAttribute = sticker.Attributes[i] as TLDocumentAttributeImageSize;
                    if (imageSizeAttribute != null)
                    {
                        break;
                    }
                }

                if (imageSizeAttribute != null)
                {
                    var width = imageSizeAttribute.W.Value;
                    var height = imageSizeAttribute.H.Value;

                    var maxDimension = Math.Max(width, height);
                    if (maxDimension > Constants.MaxStickerDimension)
                    {
                        var scaleFactor = Constants.MaxStickerDimension / maxDimension;

                        return isWidth ? scaleFactor * width : scaleFactor * height;
                    }

                    return isWidth ? width : height;
                }
            }

            return isWidth ? double.NaN : Constants.MaxStickerDimension;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
