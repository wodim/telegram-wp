using System;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Windows;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using TelegramClient.Resources;
using TelegramClient.Views.Controls;

namespace TelegramClient.ViewModels.Dialogs
{
    public partial class DialogDetailsViewModel
    {
        public void SendAudio(AudioEventArgs args)
        {
            if (string.IsNullOrEmpty(args.OggFileName)) return;

            CacheService.CheckDisabledFeature(With,
                Constants.FeaturePMUploadAudio,
                Constants.FeatureChatUploadAudio,
                Constants.FeatureBigChatUploadAudio,
                () =>
                {
                    var id = TLLong.Random();
                    var accessHash = TLLong.Random();

                    var oggFileName = string.Format("audio{0}_{1}.mp3", id, accessHash);
                    var wavFileName = Path.GetFileNameWithoutExtension(oggFileName) + ".wav";

                    long size = 0;
                    using (var storage = IsolatedStorageFile.GetUserStoreForApplication())
                    {
                        storage.MoveFile(args.OggFileName, oggFileName);
                        using (var file = storage.OpenFile(oggFileName, FileMode.Open, FileAccess.Read))
                        {
                            size = file.Length;
                        }

                        var wavStream = args.PcmStream.GetWavAsMemoryStream(16000, 1, 16);
                        using (var file = new IsolatedStorageFileStream(wavFileName, FileMode.OpenOrCreate, storage))
                        {
                            wavStream.Seek(0, SeekOrigin.Begin);
                            wavStream.CopyTo(file);
                            file.Flush();
                        }
                    }

                    var audio = new TLAudio
                    {
                        Id = id,
                        AccessHash = accessHash,
                        Date = TLUtils.DateToUniversalTimeTLInt(MTProtoService.ClientTicksDelta, DateTime.Now),
                        UserId = new TLInt(StateService.CurrentUserId),
                        Duration = new TLInt((int)args.Duration),
                        MimeType = new TLString("audio/ogg"),
                        Size = new TLInt((int)size),
                        DCId = new TLInt(0),
                    };

                    var media = new TLMessageMediaAudio {Audio = audio, IsoFileName = oggFileName, NotListened = true};

                    var message = TLUtils.GetMessage(
                        new TLInt(StateService.CurrentUserId),
                        TLUtils.InputPeerToPeer(Peer, StateService.CurrentUserId),
                        With is TLBroadcastChat ? MessageStatus.Broadcast : MessageStatus.Sending,
                        TLBool.True,
                        TLBool.True,
                        TLUtils.DateToUniversalTimeTLInt(MTProtoService.ClientTicksDelta, DateTime.Now),
                        TLString.Empty,
                        media,
                        TLLong.Random(),
                        new TLInt(0)
                    );

                    message.NotListened = true;

                    BeginOnUIThread(() =>
                    {
                        var previousMessage = InsertSendingMessage(message);
                        message.NotifyOfPropertyChange(() => message.Media);
                        IsEmptyDialog = Items.Count == 0 && LazyItems.Count == 0;

                        BeginOnThreadPool(() =>
                            CacheService.SyncSendingMessage(
                               message, previousMessage,
                               TLUtils.InputPeerToPeer(Peer, StateService.CurrentUserId),
                               m => SendAudioInternal(message, args)));
                    });
                },
                disabledFeature => Execute.BeginOnUIThread(() => MessageBox.Show(disabledFeature.Description.ToString(), AppResources.AppName, MessageBoxButton.OK)));
        }

        private void SendAudioInternal(TLMessage message, AudioEventArgs args = null)
        {
            var audioMedia = message.Media as TLMessageMediaAudio;
            if (audioMedia == null) return;

            var fileName = audioMedia.IsoFileName;
            if (string.IsNullOrEmpty(fileName)) return;

            var audio = audioMedia.Audio as TLAudio;
            if (audio == null) return;

            if (args != null)
            {
                var fileId = args.FileId ?? TLLong.Random();
                message.Media.FileId = fileId;
                message.Media.UploadingProgress = 0.001;
                UploadAudioFileManager.UploadFile(fileId, message, fileName, args.Parts);
            }
            else
            {
                var fileId = TLLong.Random();
                message.Media.FileId = fileId;
                message.Media.UploadingProgress = 0.001;
                UploadAudioFileManager.UploadFile(fileId, message, fileName);
            }
        }
    }
}
