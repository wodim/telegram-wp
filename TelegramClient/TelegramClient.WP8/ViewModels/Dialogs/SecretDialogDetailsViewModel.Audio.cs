using System;
using System.IO;
using System.IO.IsolatedStorage;
using Telegram.Api.TL;
using TelegramClient.Views.Controls;

namespace TelegramClient.ViewModels.Dialogs
{
    public partial class SecretDialogDetailsViewModel
    {
        private void SendAudioInternal(TLObject obj)
        {
            var message = GetDecryptedMessage(obj);
            if (message == null) return;

            var media = message.Media as TLDecryptedMessageMediaAudio;
            if (media == null) return;

            var fileLocation = media.File as TLEncryptedFile;
            if (fileLocation == null) return;

            var fileName = String.Format("audio{0}_{1}.mp3",
                fileLocation.Id,
                fileLocation.AccessHash);

            byte[] data;
            using (var store = IsolatedStorageFile.GetUserStoreForApplication())
            {
                using (var fileStream = store.OpenFile(fileName, FileMode.Open, FileAccess.Read))
                {
                    data = new byte[fileStream.Length];
                    fileStream.Read(data, 0, data.Length);
                }
            }

            var encryptedBytes = Telegram.Api.Helpers.Utils.AesIge(data, media.Key.Data, media.IV.Data, true);
            using (var storage = IsolatedStorageFile.GetUserStoreForApplication())
            {
                using (var file = storage.OpenFile("encrypted." + fileName, FileMode.Create, FileAccess.Write))
                {
                    file.Write(encryptedBytes, 0, encryptedBytes.Length);
                }
            }

            UploadFileManager.UploadFile(fileLocation.Id, obj, encryptedBytes);
        }

        public void SendAudio(AudioEventArgs args)
        {
            var chat = Chat as TLEncryptedChat;
            if (chat == null) return;

            if (string.IsNullOrEmpty(args.OggFileName)) return;

            var dcId = TLInt.Random();
            var id = TLLong.Random();
            var accessHash = TLLong.Random();

            var oggFileName = String.Format("audio{0}_{1}.mp3", id, accessHash);
            var wavFileName = Path.GetFileNameWithoutExtension(oggFileName) + ".wav";

            long size = 0;
            using (var storage = IsolatedStorageFile.GetUserStoreForApplication())
            {
                storage.MoveFile(args.OggFileName, oggFileName);
                using (var file = storage.OpenFile(oggFileName, FileMode.Open, FileAccess.Read))
                {
                    size = file.Length;
                }

                var wavStream = Wav.GetWavAsMemoryStream(args.PcmStream, 16000, 1, 16);
                using (var file = new IsolatedStorageFileStream(wavFileName, FileMode.OpenOrCreate, storage))
                {
                    wavStream.Seek(0, SeekOrigin.Begin);
                    wavStream.CopyTo(file);
                    file.Flush();
                }
            }

            var fileLocation = new TLEncryptedFile
            {
                Id = id,
                AccessHash = accessHash,
                DCId = dcId,
                Size = new TLInt((int)size),
                KeyFingerprint = new TLInt(0),
                FileName = new TLString(Path.GetFileName(oggFileName))
            };

            var keyIV = GenerateKeyIV();
            TLDecryptedMessageMediaAudio decryptedMediaAudio;
            var encryptedChat17 = chat as TLEncryptedChat17;
            if (encryptedChat17 != null)
            {
                decryptedMediaAudio = new TLDecryptedMessageMediaAudio17
                {
                    Duration = new TLInt((int)args.Duration),
                    MimeType = new TLString("audio/ogg"),
                    Size = new TLInt((int)size),
                    Key = keyIV.Item1,
                    IV = keyIV.Item2,

                    UserId = new TLInt(StateService.CurrentUserId),
                    File = fileLocation,

                    UploadingProgress = 0.001
                };
            }
            else
            {
                decryptedMediaAudio = new TLDecryptedMessageMediaAudio
                {
                    Duration = new TLInt((int)args.Duration),
                    //MimeType = new TLString("audio/ogg"),
                    Size = new TLInt((int)size),
                    Key = keyIV.Item1,
                    IV = keyIV.Item2,

                    UserId = new TLInt(StateService.CurrentUserId),
                    File = fileLocation,

                    UploadingProgress = 0.001
                };
            }

            var decryptedTuple = GetDecryptedMessageAndObject(TLString.Empty, decryptedMediaAudio, chat, true);

            BeginOnUIThread(() =>
            {
                Items.Insert(0, decryptedTuple.Item1);
                NotifyOfPropertyChange(() => DescriptionVisibility);
            });

            BeginOnThreadPool(() =>
                CacheService.SyncDecryptedMessage(decryptedTuple.Item1, chat,
                    cachedMessage => SendAudioInternal(decryptedTuple.Item2)));
        }
    }
}
