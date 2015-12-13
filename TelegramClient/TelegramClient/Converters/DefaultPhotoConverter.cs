using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Caliburn.Micro;
using Microsoft.Phone;
using Telegram.Api;
using Telegram.Api.Extensions;
using Telegram.Api.Services.FileManager;
using Telegram.Api.TL;
using TelegramClient.Helpers;
using TelegramClient.Services;
using TelegramClient.Views.Dialogs;
using TelegramClient.Views.Media;
#if WP81
using Windows.Graphics.Imaging;
#endif
#if WP8
using TelegramClient_WebP.LibWebP;
#endif

namespace TelegramClient.Converters
{
    public class PhotoConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var timer = Stopwatch.StartNew();

            var photoSize = value as TLPhotoSize;
            if (photoSize != null)
            {
                var location = photoSize.Location as TLFileLocation;
                if (location != null)
                {
                    return DefaultPhotoConverter.ReturnImage(timer, location);
                }
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ProfileSmallPhotoConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var timer = Stopwatch.StartNew();

            var userProfilePhoto = value as TLUserProfilePhoto;
            if (userProfilePhoto != null)
            {
                var location = userProfilePhoto.PhotoSmall as TLFileLocation;
                if (location != null)
                {
                    return DefaultPhotoConverter.ReturnOrEnqueueImage(timer, location, userProfilePhoto, new TLInt(0));
                }
            }

            var chatPhoto = value as TLChatPhoto;
            if (chatPhoto != null)
            {
                var location = chatPhoto.PhotoSmall as TLFileLocation;
                if (location != null)
                {
                    return DefaultPhotoConverter.ReturnOrEnqueueImage(timer, location, chatPhoto, new TLInt(0));
                }
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ProfileBigPhotoConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var timer = Stopwatch.StartNew();

            var userProfilePhoto = value as TLUserProfilePhoto;
            if (userProfilePhoto != null)
            {
                var location = userProfilePhoto.PhotoBig as TLFileLocation;
                if (location != null)
                {
                    return DefaultPhotoConverter.ReturnOrEnqueueImage(timer, location, userProfilePhoto, new TLInt(0));
                }
            }

            var chatPhoto = value as TLChatPhoto;
            if (chatPhoto != null)
            {
                var location = chatPhoto.PhotoBig as TLFileLocation;
                if (location != null)
                {
                    return DefaultPhotoConverter.ReturnOrEnqueueImage(timer, location, chatPhoto, new TLInt(0));
                }
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }


    public class DefaultPhotoConverter : IValueConverter
    {
        public static BitmapImage ReturnOrEnqueueImage(Stopwatch timer, TLEncryptedFile location, TLObject owner)
        {
            var fileName = String.Format("{0}_{1}_{2}.jpg",
                        location.Id,
                        location.DCId,
                        location.AccessHash);

            using (var store = IsolatedStorageFile.GetUserStoreForApplication())
            {
                if (!store.FileExists(fileName))
                {
                    var fileManager = IoC.Get<IEncryptedFileManager>();
                    ThreadPool.QueueUserWorkItem(state =>
                    {
                        fileManager.DownloadFile(location, owner);
                    });
                }
                else
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

            return null;
        }

        public static BitmapImage ReturnImage(Stopwatch timer, TLFileLocation location)
        {
            //return null;

            var fileName = String.Format("{0}_{1}_{2}.jpg",
                        location.VolumeId,
                        location.LocalId,
                        location.Secret);

            using (var store = IsolatedStorageFile.GetUserStoreForApplication())
            {
                if (!store.FileExists(fileName))
                {

                }
                else
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


                    //TLUtils.WritePerformance("DefaultPhotoConverter time: " + timer.Elapsed);
                    return imageSource;
                }
            }

            return null;
        }

        public static BitmapImage ReturnOrEnqueueImage(Stopwatch timer, TLFileLocation location, TLObject owner, TLInt fileSize)
        {
            var fileName = String.Format("{0}_{1}_{2}.jpg",
                        location.VolumeId,
                        location.LocalId,
                        location.Secret);

            using (var store = IsolatedStorageFile.GetUserStoreForApplication())
            {
                if (!store.FileExists(fileName))
                {
                    if (fileSize != null)
                    {
                        var fileManager = IoC.Get<IFileManager>();
                        ThreadPool.QueueUserWorkItem(state =>
                        {
                            fileManager.DownloadFile(location, owner, fileSize);
                        });
                    }
                }
                else
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

            return null;
        }
        #region Profile Photo

        private static readonly Dictionary<string, WeakReference> _cachedSources = new Dictionary<string, WeakReference>();

        public static BitmapSource ReturnOrEnqueueProfileImage(Stopwatch timer, TLFileLocation location, TLObject owner, TLInt fileSize)
        {
            var fileName = String.Format("{0}_{1}_{2}.jpg",
                        location.VolumeId,
                        location.LocalId,
                        location.Secret);

            using (var store = IsolatedStorageFile.GetUserStoreForApplication())
            {
                if (!store.FileExists(fileName))
                {
                    if (fileSize != null)
                    {
                        var fileManager = IoC.Get<IFileManager>();
                        ThreadPool.QueueUserWorkItem(state =>
                        {
                            fileManager.DownloadFile(location, owner, fileSize);
                        });
                    }
                }
                else
                {
                    BitmapSource imageSource;
                    WeakReference weakImageSource;
                    if (_cachedSources.TryGetValue(fileName, out weakImageSource))
                    {
                        if (weakImageSource.IsAlive)
                        {
                            imageSource = weakImageSource.Target as BitmapSource;

                            return imageSource;
                        }
                    }

                    try
                    {
                        using (var stream = store.OpenFile(fileName, FileMode.Open, FileAccess.Read))
                        {
                            stream.Seek(0, SeekOrigin.Begin);
                            var image = new BitmapImage();
                            image.SetSource(stream);
                            imageSource = image;
                        }

                        _cachedSources[fileName] = new WeakReference(imageSource);
                    }
                    catch (Exception)
                    {
                        return null;
                    }

                    return imageSource;
                }
            }

            return null;
        }
        #endregion

#if WP8

        private static readonly Dictionary<string, WeakReference<WriteableBitmap>> _cachedWebPImages = new Dictionary<string, WeakReference<WriteableBitmap>>();

        private static ImageSource DecodeWebPImage(string cacheKey, byte[] buffer, System.Action faultCallback = null)
        {
            try
            {
                WeakReference<WriteableBitmap> reference;
                if (_cachedWebPImages.TryGetValue(cacheKey, out reference))
                {
                    WriteableBitmap cachedBitmap;
                    if (reference.TryGetTarget(out cachedBitmap))
                    {
                        return cachedBitmap;
                    }
                }

                var decoder = new WebPDecoder();
                int width = 0, height = 0;
                byte[] decoded = null;
                try
                {
                    decoded = decoder.DecodeRgbA(buffer, out width, out height);
                }
                catch (Exception ex)
                {
                    faultCallback.SafeInvoke();
                    // не получается сконвертировать, битый файл
                    //store.DeleteFile(documentLocalFileName);
                    Telegram.Api.Helpers.Execute.ShowDebugMessage("WebPDecoder.DecodeRgbA ex " + ex);
                }

                if (decoded == null) return null;

                var wb = new WriteableBitmap(width, height);
                for (var i = 0; i < decoded.Length / 4; i++)
                {
                    int r = decoded[4 * i];
                    int g = decoded[4 * i + 1];
                    int b = decoded[4 * i + 2];
                    int a = decoded[4 * i + 3];

                    a <<= 24;
                    r <<= 16;
                    g <<= 8;
                    int iPixel = a | r | g | b;

                    wb.Pixels[i] = iPixel;
                }

                _cachedWebPImages[cacheKey] = new WeakReference<WriteableBitmap>(wb);

                return wb;
            }
            catch (Exception ex)
            {
                TLUtils.WriteException("WebPDecode ex ", ex);
            }

            return null;
        }

        public static ImageSource ReturnOrEnqueueStickerPreview(TLFileLocation location, TLObject owner, TLInt fileSize)
        {
            var fileName =
                String.Format("{0}_{1}_{2}.jpg",
                location.VolumeId,
                location.LocalId,
                location.Secret);

            using (var store = IsolatedStorageFile.GetUserStoreForApplication())
            {
                if (!store.FileExists(fileName))
                {
                    if (fileSize != null)
                    {
                        var fileManager = IoC.Get<IFileManager>();
                        Telegram.Api.Helpers.Execute.BeginOnThreadPool(() =>
                        {
                            fileManager.DownloadFile(location, owner, fileSize);
                        });
                    }
                }
                else
                {
                    byte[] buffer;
                    using (var file = store.OpenFile(fileName, FileMode.Open))
                    {
                        buffer = new byte[file.Length];
                        file.Read(buffer, 0, buffer.Length);
                    }

                    return DecodeWebPImage(fileName, buffer,
                        () =>
                        {
                            using (var localStore = IsolatedStorageFile.GetUserStoreForApplication())
                            {
                                localStore.DeleteFile(fileName);
                            }
                        });
                }
            }

            return null;
        }

        private static ImageSource ReturnOrEnqueueSticker(TLDecryptedMessageMediaExternalDocument document, TLDecryptedMessage owner)
        {
            if (document == null) return null;

            var documentLocalFileName = document.GetFileName();

            using (var store = IsolatedStorageFile.GetUserStoreForApplication())
            {
                if (!store.FileExists(documentLocalFileName))
                {
                    // 1. download full size
                    IoC.Get<IDocumentFileManager>().DownloadFileAsync(document.FileName, document.DCId, document.ToInputFileLocation(), owner, document.Size, progress => { });

                    // 2. download preview
                    var thumbCachedSize = document.Thumb as TLPhotoCachedSize;
                    if (thumbCachedSize != null)
                    {
                        var fileName = "cached" + document.GetFileName();
                        var buffer = thumbCachedSize.Bytes.Data;
                        if (buffer == null) return null;

                        return DecodeWebPImage(fileName, buffer, () => { });
                    }

                    var thumbPhotoSize = document.Thumb as TLPhotoSize;
                    if (thumbPhotoSize != null)
                    {
                        var location = thumbPhotoSize.Location as TLFileLocation;
                        if (location != null)
                        {
                            return ReturnOrEnqueueStickerPreview(location, owner, thumbPhotoSize.Size);
                        }
                    }
                }
                else
                {
                    if (document.Size.Value > 0
                        && document.Size.Value < Telegram.Api.Constants.StickerMaxSize)
                    {
                        byte[] buffer;
                        using (var file = store.OpenFile(documentLocalFileName, FileMode.Open))
                        {
                            buffer = new byte[file.Length];
                            file.Read(buffer, 0, buffer.Length);
                        }

                        return DecodeWebPImage(documentLocalFileName, buffer,
                            () =>
                            {
                                using (var localStore = IsolatedStorageFile.GetUserStoreForApplication())
                                {
                                    localStore.DeleteFile(documentLocalFileName);
                                }
                            });
                    }
                }
            }

            return null;
        }

        private static ImageSource ReturnOrEnqueueSticker(TLDocument22 document, TLStickerItem sticker)
        {
            if (document == null) return null;

            var documentLocalFileName = document.GetFileName();

            using (var store = IsolatedStorageFile.GetUserStoreForApplication())
            {
                if (!store.FileExists(documentLocalFileName))
                {
                    TLObject owner = document;
                    if (sticker != null)
                    {
                        owner = sticker;
                    }

                    // 1. download full size
                    IoC.Get<IDocumentFileManager>().DownloadFileAsync(document.FileName, document.DCId, document.ToInputFileLocation(), owner, document.Size, progress => { });

                    // 2. download preview
                    var thumbCachedSize = document.Thumb as TLPhotoCachedSize;
                    if (thumbCachedSize != null)
                    {
                        var fileName = "cached" + document.GetFileName();
                        var buffer = thumbCachedSize.Bytes.Data;
                        if (buffer == null) return null;

                        return DecodeWebPImage(fileName, buffer, () => { });
                    }

                    var thumbPhotoSize = document.Thumb as TLPhotoSize;
                    if (thumbPhotoSize != null)
                    {
                        var location = thumbPhotoSize.Location as TLFileLocation;
                        if (location != null)
                        {
                            return ReturnOrEnqueueStickerPreview(location, sticker, thumbPhotoSize.Size);
                        }
                    }
                }
                else
                {
                    if (document.DocumentSize > 0
                        && document.DocumentSize < Telegram.Api.Constants.StickerMaxSize)
                    {
                        byte[] buffer;
                        using (var file = store.OpenFile(documentLocalFileName, FileMode.Open))
                        {
                            buffer = new byte[file.Length];
                            file.Read(buffer, 0, buffer.Length);
                        }

                        return DecodeWebPImage(documentLocalFileName, buffer,
                            () =>
                            {
                                using (var localStore = IsolatedStorageFile.GetUserStoreForApplication())
                                {
                                    localStore.DeleteFile(documentLocalFileName);
                                }
                            });
                    }
                }
            }

            return null;
        }
        
#endif

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var timer = Stopwatch.StartNew();

            var encryptedPhoto = value as TLEncryptedFile;
            if (encryptedPhoto != null)
            {
                return ReturnOrEnqueueImage(timer, encryptedPhoto, encryptedPhoto);
            }

            var userProfilePhoto = value as TLUserProfilePhoto;
            if (userProfilePhoto != null)
            {
                var location = userProfilePhoto.PhotoSmall as TLFileLocation;
                if (location != null)
                {
                    return ReturnOrEnqueueProfileImage(timer, location, userProfilePhoto, new TLInt(0));
                }              
            }

            var chatPhoto = value as TLChatPhoto;
            if (chatPhoto != null)
            {
                var location = chatPhoto.PhotoSmall as TLFileLocation;
                if (location != null)
                {
                    return ReturnOrEnqueueProfileImage(timer, location, chatPhoto, new TLInt(0));
                }
            }

            var decrypteMedia = value as TLDecryptedMessageMediaBase;
            if (decrypteMedia != null)
            {
                var decryptedMediaVideo = value as TLDecryptedMessageMediaVideo;
                if (decryptedMediaVideo != null)
                {
                    var buffer = decryptedMediaVideo.Thumb.Data;

                    if (buffer.Length > 0
                        && decryptedMediaVideo.ThumbW.Value > 0
                        && decryptedMediaVideo.ThumbH.Value > 0)
                    {
                        var memoryStream = new MemoryStream(buffer);
                        var bitmap = PictureDecoder.DecodeJpeg(memoryStream);

                        bitmap.BoxBlur(37);

                        var blurredStream = new MemoryStream();
                        bitmap.SaveJpeg(blurredStream, decryptedMediaVideo.ThumbW.Value, decryptedMediaVideo.ThumbH.Value, 0, 70);

                        return ImageUtils.CreateImage(blurredStream.ToArray());
                    }

                    return ImageUtils.CreateImage(buffer);
                }

                var decryptedMediaDocument = value as TLDecryptedMessageMediaDocument;
                if (decryptedMediaDocument != null)
                {
                    var location = decryptedMediaDocument.File as TLEncryptedFile;
                    if (location != null)
                    {
                        var fileName = String.Format("{0}_{1}_{2}.jpg",
                        location.Id,
                        location.DCId,
                        location.AccessHash);

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
                    }

                    var buffer = decryptedMediaDocument.Thumb.Data;
                    return ImageUtils.CreateImage(buffer);
                }

                var file = decrypteMedia.File as TLEncryptedFile;
                if (file != null)
                {
                    return ReturnOrEnqueueImage(timer, file, decrypteMedia);
                }
            }

            var decryptedMessage = value as TLDecryptedMessage17;
            if (decryptedMessage != null)
            {
                var decryptedMediaExternalDocument = decryptedMessage.Media as TLDecryptedMessageMediaExternalDocument;
                if (decryptedMediaExternalDocument != null)
                {
#if WP8
                    return ReturnOrEnqueueSticker(decryptedMediaExternalDocument, decryptedMessage);
#endif

                    return null;
                }
                
            }

            var photoMedia = value as TLMessageMediaPhoto;
            if (photoMedia != null)
            {
                value = photoMedia.Photo;
            }

            var photo = value as TLPhoto;
            if (photo != null)
            {
                var width = 311.0;
                double result;
                if (Double.TryParse((string) parameter, out result))
                {
                    width = result;
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
                    var location = size.Location as TLFileLocation;
                    if (location != null)
                    {
                        return ReturnOrEnqueueImage(timer, location, photo, size.Size);
                    }  
                }
            }

#if WP8
            var sticker = value as TLStickerItem;
            if (sticker != null)
            {
                var document22 = sticker.Document as TLDocument22;
                if (document22 == null) return null;

                var thumbCachedSize = document22.Thumb as TLPhotoCachedSize;
                if (thumbCachedSize != null)
                {
                    var fileName = "cached" + document22.GetFileName();
                    var buffer = thumbCachedSize.Bytes.Data;
                    if (buffer == null) return null;

                    return DecodeWebPImage(fileName, buffer, () => { });
                }

                var thumbPhotoSize = document22.Thumb as TLPhotoSize;
                if (thumbPhotoSize != null)
                {
                    var location = thumbPhotoSize.Location as TLFileLocation;
                    if (location != null)
                    {
                        return ReturnOrEnqueueStickerPreview(location, sticker, thumbPhotoSize.Size);
                    }
                }

                if (TLMessageBase.IsSticker(document22))
                {
                    return ReturnOrEnqueueSticker(document22, sticker);
                }
            }
#endif

            var document = value as TLDocument;
            if (document != null)
            {
#if WP8
                if (TLMessageBase.IsSticker(document))
                {
                    if (parameter != null &&
                        string.Equals(parameter.ToString(), "ignoreStickers", StringComparison.OrdinalIgnoreCase))
                    {
                        return null;
                    }

                    return ReturnOrEnqueueSticker((TLDocument22)document, null);
                }
#endif

                var thumbPhotoSize = document.Thumb as TLPhotoSize;
                if (thumbPhotoSize != null)
                {
                    var location = thumbPhotoSize.Location as TLFileLocation;
                    if (location != null)
                    {
                        return ReturnOrEnqueueImage(timer, location, document, thumbPhotoSize.Size);
                    }
                }

                var thumbCachedSize = document.Thumb as TLPhotoCachedSize;
                if (thumbCachedSize != null)
                {
                    var buffer = thumbCachedSize.Bytes.Data;

                    return ImageUtils.CreateImage(buffer);
                }
            }

            var videoMedia = value as TLMessageMediaVideo;
            if (videoMedia != null)
            {
                value = videoMedia.Video;
            }

            var video = value as TLVideo;
            if (video != null)
            {
                var thumbPhotoSize = video.Thumb as TLPhotoSize;

                if (thumbPhotoSize != null)
                {
                    var location = thumbPhotoSize.Location as TLFileLocation;
                    if (location != null)
                    {
                        return ReturnOrEnqueueImage(timer, location, video, thumbPhotoSize.Size);
                    }
                }

                var thumbCachedSize = video.Thumb as TLPhotoCachedSize;
                if (thumbCachedSize != null)
                {
                    var buffer = thumbCachedSize.Bytes.Data;
                    return ImageUtils.CreateImage(buffer);
                }
            }

            var webPageMedia = value as TLMessageMediaWebPage;
            if (webPageMedia != null)
            {
                value = webPageMedia.WebPage;
            }

            var webPage = value as TLWebPage;
            if (webPage != null)
            {
                var webPagePhoto = webPage.Photo as TLPhoto;
                if (webPagePhoto != null)
                {
                    var width = 311.0;
                    double result;
                    if (Double.TryParse((string)parameter, out result))
                    {
                        width = result;
                    }

                    TLPhotoSize size = null;
                    var sizes = webPagePhoto.Sizes.OfType<TLPhotoSize>();
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
                        var location = size.Location as TLFileLocation;
                        if (location != null)
                        {
                            return ReturnOrEnqueueImage(timer, location, webPage, size.Size);
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

    public class ImageViewerPhotoConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var photoMedia = value as TLMessageMediaPhoto;
            if (photoMedia != null)
            {
                value = photoMedia.Photo;
            }

            var webPageMedia = value as TLMessageMediaWebPage;
            if (webPageMedia != null)
            {
                var webPage = webPageMedia.WebPage as TLWebPage;
                if (webPage != null)
                {
                    value = webPage.Photo;
                }
            }

            var photo = value as TLPhoto;
            if (photo != null)
            {
                var width = 320.0;
                var image = ReturnImageBySize(photo, width);
                if (image != null) return image;

                width = 99.0;
                image = ReturnImageBySize(photo, width);
                if (image != null) return image;

                image = new PhotoToThumbConverter().Convert(photoMedia, targetType, parameter, culture) as BitmapImage;
                return image;
            }

            return null;
        }

        private static BitmapImage ReturnImageBySize(TLPhoto photo, double width)
        {
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
                var location = size.Location as TLFileLocation;
                if (location != null)
                {
                    var timer = Stopwatch.StartNew();
                    var image = DefaultPhotoConverter.ReturnImage(timer, location);
                    if (image != null)
                    {
                        return image;
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


    public class PhotoThumbConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            BitmapImage image = null;

            if (value != null)
            {
                var photoFile = value as PhotoFile;
                if (photoFile != null)
                {
                    var thumbnail = photoFile.Thumbnail;
                    if (thumbnail != null)
                    {
                        image = new BitmapImage();
                        image.CreateOptions = BitmapCreateOptions.None;
                        image.SetSource(thumbnail.AsStream());

                        Deployment.Current.Dispatcher.BeginInvoke(() => MultiImageEditorView.ImageOpened(photoFile));
                    }
                    else
                    {
                        //Task.Factory.StartNew(async () =>
                        //{
                        //    thumbnail = await photoFile.File.GetThumbnailAsync(ThumbnailMode.ListView, 99, ThumbnailOptions.None);
                        //    photoFile.Thumbnail = thumbnail;
                        //    Deployment.Current.Dispatcher.BeginInvoke(() => photoFile.RaisePropertyChanged("Self"));
                        //});
                    }
                }
            }



            return (image);
        }

        //public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        //{
        //    throw new NotImplementedException();
        //}

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();

            //var thumbnail = await item.File.GetThumbnailAsync(ThumbnailMode.ListView, 99, ThumbnailOptions.None);
            //item.Thumbnail = thumbnail;
            //item.RaisePropertyChanged("Thumbnail");
        }
    }

    public class PhotoFileToTemplateConverter : IValueConverter
    {
        public DataTemplate PhotoTemplate { get; set; }

        public DataTemplate ButtonTemplate { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var photoFile = value as PhotoFile;
            if (photoFile != null)
            {
                return photoFile.IsButton ? ButtonTemplate : PhotoTemplate;
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
