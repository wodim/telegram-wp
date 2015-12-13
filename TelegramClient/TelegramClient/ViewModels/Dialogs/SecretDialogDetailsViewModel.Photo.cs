using System;
using System.IO;
using System.IO.IsolatedStorage;
using Telegram.Api.TL;
using TelegramClient.Helpers;
using TelegramClient.Services;

namespace TelegramClient.ViewModels.Dialogs
{
    public partial class SecretDialogDetailsViewModel
    {
        private void SendPhotoInternal(byte[] data, TLObject obj)
        {
            var message = GetDecryptedMessage(obj);
            if (message == null) return;

            var media = message.Media as TLDecryptedMessageMediaPhoto;
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

            UploadFileManager.UploadFile(file.Id, obj, encryptedBytes);
        }

        private void SendPhoto(Photo p)
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
                KeyFingerprint = new TLInt(0)
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
            var thumb = ImageUtils.CreateThumb(p.Bytes, Constants.PhotoPreviewMaxSize, Constants.PhotoPreviewQuality, out thumbHeight, out thumbWidth);

            var decryptedMediaPhoto = new TLDecryptedMessageMediaPhoto
            {
                Thumb = TLString.FromBigEndianData(thumb),
                ThumbW = new TLInt(thumbWidth),
                ThumbH = new TLInt(thumbHeight),
                Key = keyIV.Item1,
                IV = keyIV.Item2,
                W = new TLInt(p.Width),
                H = new TLInt(p.Height),
                Size = new TLInt(p.Bytes.Length),

                File = fileLocation,

                UploadingProgress = 0.001
            };

            var decryptedTuple = GetDecryptedMessageAndObject(TLString.Empty, decryptedMediaPhoto, chat, true);

            Items.Insert(0, decryptedTuple.Item1);
            RaiseScrollToBottom();
            NotifyOfPropertyChange(() => DescriptionVisibility);

            BeginOnThreadPool(() => 
                CacheService.SyncDecryptedMessage(decryptedTuple.Item1, chat, 
                    cachedMessage => SendPhotoInternal(p.Bytes, decryptedTuple.Item2)));
        }

        public static Telegram.Api.WindowsPhone.Tuple<TLString, TLString> GenerateKeyIV()
        {
            var random = new Random();

            var key = new byte[32];
            var iv = new byte[32];
            random.NextBytes(key);
            random.NextBytes(iv);

            return new Telegram.Api.WindowsPhone.Tuple<TLString, TLString>(TLString.FromBigEndianData(key), TLString.FromBigEndianData(iv));
        }
    }
}
