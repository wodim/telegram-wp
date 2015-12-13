using System;
using System.IO;
using System.IO.IsolatedStorage;
using System.Windows;
#if WP8
using Windows.Storage;
#endif
using Telegram.Api.TL;
using TelegramClient.Converters;
using TelegramClient.Helpers;
using TelegramClient.Resources;
using TelegramClient.Services;

namespace TelegramClient.ViewModels.Dialogs
{
    public partial class SecretDialogDetailsViewModel
    {
#if WP81
        public async void SendDocument(StorageFile file)
        {
            var chat = Chat as TLEncryptedChat;
            if (chat == null) return;

            if (file == null) return;

            var properties = await file.GetBasicPropertiesAsync();
            var size = properties.Size;

            if (!DialogDetailsViewModel.CheckDocumentSize(size))
            {
                MessageBox.Show(string.Format(AppResources.MaximumFileSizeExceeded, MediaSizeConverter.Convert((int)Telegram.Api.Constants.MaximumUploadedFileSize)), AppResources.Error, MessageBoxButton.OK);
                return;
            }

            DialogDetailsViewModel.AddFileToFutureAccessList(file);

            var thumb = await DialogDetailsViewModel.GetFileThumbAsync(file) as TLPhotoSize;

            var dcId = TLInt.Random();
            var id = TLLong.Random();
            var accessHash = TLLong.Random();

            var fileLocation = new TLEncryptedFile
            {
                Id = id,
                AccessHash = accessHash,
                DCId = dcId,
                Size = new TLInt((int)size),
                KeyFingerprint = new TLInt(0),
                FileName = new TLString(Path.GetFileName(file.Name))
            };

            var keyIV = GenerateKeyIV();

            var decryptedMediaDocument = new TLDecryptedMessageMediaDocument
            {
                Thumb = thumb != null? thumb.Bytes : TLString.Empty,
                ThumbW = thumb != null? thumb.W : new TLInt(0),
                ThumbH = thumb != null? thumb.H : new TLInt(0),
                FileName = new TLString(Path.GetFileName(file.Name)),
                MimeType = new TLString(file.ContentType),
                Size = new TLInt((int)size),
                Key = keyIV.Item1,
                IV = keyIV.Item2,

                File = fileLocation,
                StorageFile = file,

                UploadingProgress = 0.001
            };

            var decryptedTuple = GetDecryptedMessageAndObject(TLString.Empty, decryptedMediaDocument, chat, true);

            BeginOnUIThread(() =>
            {
                Items.Insert(0, decryptedTuple.Item1);
                RaiseScrollToBottom();
                NotifyOfPropertyChange(() => DescriptionVisibility);

                BeginOnThreadPool(() =>
                    CacheService.SyncDecryptedMessage(decryptedTuple.Item1, chat,
                        cachedMessage => SendDocumentInternal(file, decryptedTuple.Item2)));
            });
        }
#endif

        private void SendDocument(Photo p)
        {
            var chat = Chat as TLEncryptedChat;
            if (chat == null) return;

            var dcId = TLInt.Random();
            var id = TLLong.Random();
            var accessHash = TLLong.Random();

            var fileLocation = new TLEncryptedFile
            {
                Id = id,
                AccessHash = accessHash,
                DCId = dcId,
                Size = new TLInt(p.Bytes.Length),
                KeyFingerprint = new TLInt(0),
                FileName = new TLString(Path.GetFileName(p.FileName))
            };

            var fileName = String.Format("{0}_{1}_{2}.jpg",
                fileLocation.Id,
                fileLocation.DCId,
                fileLocation.AccessHash);

            using (var store = IsolatedStorageFile.GetUserStoreForApplication())
            {
                using (var fileStream = store.CreateFile(fileName))
                {
                    fileStream.Write(p.Bytes, 0, p.Bytes.Length);
                }
            }

            var keyIV = GenerateKeyIV();

            int thumbHeight;
            int thumbWidth;
            var thumb = ImageUtils.CreateThumb(p.Bytes, Constants.DocumentPreviewMaxSize, Constants.DocumentPreviewQuality, out thumbHeight, out thumbWidth);

            var decryptedMediaDocument = new TLDecryptedMessageMediaDocument
            {
                Thumb = TLString.FromBigEndianData(thumb),
                ThumbW = new TLInt(thumbWidth),
                ThumbH = new TLInt(thumbHeight),
                FileName = new TLString(Path.GetFileName(p.FileName)),
                MimeType = new TLString("image/jpeg"),
                Size = new TLInt(p.Bytes.Length),
                Key = keyIV.Item1,
                IV = keyIV.Item2,

                File = fileLocation,

                UploadingProgress = 0.001
            };

            var decryptedTuple = GetDecryptedMessageAndObject(TLString.Empty, decryptedMediaDocument, chat, true);

            Items.Insert(0, decryptedTuple.Item1);
            RaiseScrollToBottom();
            NotifyOfPropertyChange(() => DescriptionVisibility);

            BeginOnThreadPool(() => 
                CacheService.SyncDecryptedMessage(decryptedTuple.Item1, chat, 
                    cachedMessage => SendDocumentInternal(p.Bytes, decryptedTuple.Item2)));
        }

#if WP81
        private void SendDocumentInternal(StorageFile storageFile, TLObject obj)
        {
            var message = GetDecryptedMessage(obj);
            if (message == null) return;

            var media = message.Media as TLDecryptedMessageMediaDocument;
            if (media == null) return;

            var file = media.File as TLEncryptedFile;
            if (file == null) return;

            UploadDocumentFileManager.UploadFile(file.Id, obj, storageFile, media.Key, media.IV);
        }
#endif

        private void SendDocumentInternal(byte[] data, TLObject obj)
        {
            var message = GetDecryptedMessage(obj);
            if (message == null) return;

            var media = message.Media as TLDecryptedMessageMediaDocument;
            if (media == null) return;
            var file = media.File as TLEncryptedFile;
            if (file == null) return;

            if (data == null)
            {
                var fileName = String.Format("{0}_{1}_{2}.jpg",
                    file.Id,
                    file.DCId,
                    file.AccessHash);

                using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                {
                    using (var fileStream = store.OpenFile(fileName, FileMode.Open, FileAccess.Read))
                    {
                        data = new byte[fileStream.Length];
                        fileStream.Read(data, 0, data.Length);
                    }
                }
            }

            var encryptedBytes = Telegram.Api.Helpers.Utils.AesIge(data, media.Key.Data, media.IV.Data, true);
            UploadDocumentFileManager.UploadFile(file.Id, obj, encryptedBytes);
        }

        public void SendSticker(TLDocument22 document)
        {
            var chat = Chat as TLEncryptedChat;
            if (chat == null) return;

            var decryptedMediaExternalDocument = new TLDecryptedMessageMediaExternalDocument
            {
                Id = document.Id,
                AccessHash = document.AccessHash,
                Date= document.Date,
                MimeType = document.MimeType,
                Size = document.Size,
                Thumb = document.Thumb,
                DCId = document.DCId,
                Attributes = new TLVector<TLDocumentAttributeBase>
                {
                    new TLDocumentAttributeImageSize{ H = document.ImageSizeH, W = document.ImageSizeW },
                    new TLDocumentAttributeFileName{FileName = new TLString("sticker.webp")},
                    new TLDocumentAttributeSticker()
                }
            };

            var decryptedTuple = GetDecryptedMessageAndObject(TLString.Empty, decryptedMediaExternalDocument, chat);

            BeginOnUIThread(() =>
            {
                Items.Insert(0, decryptedTuple.Item1);
                RaiseScrollToBottom();
                NotifyOfPropertyChange(() => DescriptionVisibility);

                SendEncrypted(chat, decryptedTuple.Item2, MTProtoService, CacheService);
            });
        }
    }
}
