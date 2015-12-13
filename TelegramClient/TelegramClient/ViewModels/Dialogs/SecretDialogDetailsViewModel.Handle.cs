using System;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using Caliburn.Micro;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache.EventArgs;
using Telegram.Api.Services.FileManager;
using Telegram.Api.TL;
using TelegramClient.ViewModels.Contacts;
#if WP8
using Windows.Storage;
using TelegramClient_Opus;
#endif

namespace TelegramClient.ViewModels.Dialogs
{
    public partial class SecretDialogDetailsViewModel : 
        Telegram.Api.Aggregator.IHandle<MessagesRemovedEventArgs>,
        Telegram.Api.Aggregator.IHandle<TLEncryptedChatBase>,
        Telegram.Api.Aggregator.IHandle<TLDecryptedMessageBase>,
        Telegram.Api.Aggregator.IHandle<TLUpdateEncryptedMessagesRead>,
        Telegram.Api.Aggregator.IHandle<UploadProgressChangedEventArgs>,
        Telegram.Api.Aggregator.IHandle<ProgressChangedEventArgs>,
        Telegram.Api.Aggregator.IHandle<DownloadingCanceledEventArgs>,
        Telegram.Api.Aggregator.IHandle<UploadingCanceledEventArgs>,
        Telegram.Api.Aggregator.IHandle<DownloadableItem>,
        Telegram.Api.Aggregator.IHandle<SetMessagesTTLEventArgs>,
        Telegram.Api.Aggregator.IHandle<TLUserBase>,
        Telegram.Api.Aggregator.IHandle<TLUpdateEncryptedChatTyping>,
        Telegram.Api.Aggregator.IHandle<TLUpdatePrivacy>
    {
        public void Handle(MessagesRemovedEventArgs args)
        {
            //if (With == args.Dialog.With && args.DecryptedMessage != null)
            //{
            //    BeginOnUIThread(() =>
            //    {
            //        Items.Remove(args.Message);

            //        IsEmptyDialog = Items.Count == 0 && LazyItems.Count == 0;
            //    });
            //}
        }

        public void Handle(ProgressChangedEventArgs args)
        {
            var media = args.Item.Owner as TLDecryptedMessageMediaBase;
            if (media != null)
            {
                var delta = args.Progress - media.DownloadingProgress;

                if (delta > 0.0)
                {
                    media.DownloadingProgress = args.Progress;
                }
            }
        }

        public void Handle(DownloadableItem item)
        {
            var decryptedMessage = item.Owner as TLDecryptedMessage;
            if (decryptedMessage != null)
            {
                var mediaExternalDocument = decryptedMessage.Media as TLDecryptedMessageMediaExternalDocument;
                if (mediaExternalDocument != null)
                {
                    decryptedMessage.NotifyOfPropertyChange(() => decryptedMessage.Self);
                }
            }

            var decryptedMedia = item.Owner as TLDecryptedMessageMediaBase;
            if (decryptedMedia != null)
            {
                decryptedMessage = Items.OfType<TLDecryptedMessage>().FirstOrDefault(x => x.Media == decryptedMedia);
                if (decryptedMessage != null)
                {
                    var mediaPhoto = decryptedMessage.Media as TLDecryptedMessageMediaPhoto;
                    if (mediaPhoto != null)
                    {
                        var fileName = item.IsoFileName;
                        using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                        {
                            byte[] buffer;
                            using (var file = store.OpenFile(fileName, FileMode.Open, FileAccess.Read))
                            {
                                buffer = new byte[file.Length];
                                file.Read(buffer, 0, buffer.Length);
                            }
                            var fileLocation = decryptedMessage.Media.File as TLEncryptedFile;
                            if (fileLocation == null) return;
                            var decryptedBuffer = Telegram.Api.Helpers.Utils.AesIge(buffer, mediaPhoto.Key.Data,
                                mediaPhoto.IV.Data, false);

                            var newFileName = String.Format("{0}_{1}_{2}.jpg",
                                fileLocation.Id,
                                fileLocation.DCId,
                                fileLocation.AccessHash);

                            using (var file = store.OpenFile(newFileName, FileMode.OpenOrCreate, FileAccess.Write))
                            {
                                file.Write(decryptedBuffer, 0, decryptedBuffer.Length);
                            }

                            store.DeleteFile(fileName);
                        }

                        decryptedMedia.NotifyOfPropertyChange("Self");

                    }

                    var mediaVideo = decryptedMessage.Media as TLDecryptedMessageMediaVideo;
                    if (mediaVideo != null)
                    {
                        mediaVideo.DownloadingProgress = 0.0;
                        var fileName = item.IsoFileName;
                        using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                        {
                            byte[] buffer;
                            using (var file = store.OpenFile(fileName, FileMode.Open, FileAccess.Read))
                            {
                                buffer = new byte[file.Length];
                                file.Read(buffer, 0, buffer.Length);
                            }
                            var fileLocation = decryptedMessage.Media.File as TLEncryptedFile;
                            if (fileLocation == null) return;
                            var decryptedBuffer = Telegram.Api.Helpers.Utils.AesIge(buffer, mediaVideo.Key.Data, mediaVideo.IV.Data, false);

                            var newFileName = String.Format("{0}_{1}_{2}.mp4",
                                fileLocation.Id,
                                fileLocation.DCId,
                                fileLocation.AccessHash);

                            using (var file = store.OpenFile(newFileName, FileMode.OpenOrCreate, FileAccess.Write))
                            {
                                file.Write(decryptedBuffer, 0, decryptedBuffer.Length);
                            }

                            store.DeleteFile(fileName);
                        }
                    }

                    var mediaAudio = decryptedMessage.Media as TLDecryptedMessageMediaAudio;
                    if (mediaAudio != null)
                    {
                        mediaAudio.DownloadingProgress = 0.0;

                        var fileLocation = decryptedMessage.Media.File as TLEncryptedFile;
                        if (fileLocation == null) return;

                        var fileName = item.IsoFileName;
                        var decryptedFileName = String.Format("audio{0}_{1}.mp3",
                            fileLocation.Id,
                            fileLocation.AccessHash);
                        using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                        {
                            byte[] buffer;
                            using (var file = store.OpenFile(fileName, FileMode.Open, FileAccess.Read))
                            {
                                buffer = new byte[file.Length];
                                file.Read(buffer, 0, buffer.Length);
                            }
                            var decryptedBuffer = Telegram.Api.Helpers.Utils.AesIge(buffer, mediaAudio.Key.Data, mediaAudio.IV.Data, false);

                            using (var file = store.OpenFile(decryptedFileName, FileMode.OpenOrCreate, FileAccess.Write))
                            {
                                file.Write(decryptedBuffer, 0, decryptedBuffer.Length);
                            }

                            store.DeleteFile(fileName);
                        }

                        var wavFileName = Path.GetFileNameWithoutExtension(decryptedFileName) + ".wav";
#if WP8
                        var component = new WindowsPhoneRuntimeComponent();
                        var result = component.InitPlayer(ApplicationData.Current.LocalFolder.Path + "\\" + decryptedFileName);
                        if (result == 1)
                        {
                            var buffer = new byte[16 * 1024];
                            var args = new int[3];
                            var pcmStream = new MemoryStream();
                            while (true)
                            {
                                component.FillBuffer(buffer, buffer.Length, args);
                                var count = args[0];
                                var offset = args[1];
                                var endOfStream = args[2] == 1;

                                pcmStream.Write(buffer, 0, count);
                                if (endOfStream)
                                {
                                    break;
                                }
                            }

                            var wavStream = Wav.GetWavAsMemoryStream(pcmStream, 48000, 1, 16);
                            using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                            {
                                using (var file = new IsolatedStorageFileStream(wavFileName, FileMode.OpenOrCreate, store))
                                {
                                    wavStream.Seek(0, SeekOrigin.Begin);
                                    wavStream.CopyTo(file);
                                    file.Flush();
                                }
                            }
                        }
#endif
                    }

                    var mediaDocument = decryptedMessage.Media as TLDecryptedMessageMediaDocument;
                    if (mediaDocument != null)
                    {
                        mediaDocument.DownloadingProgress = 0.0;
                        var fileName = item.IsoFileName;
                        using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                        {
                            byte[] buffer;
                            using (var file = store.OpenFile(fileName, FileMode.Open, FileAccess.Read))
                            {
                                buffer = new byte[file.Length];
                                file.Read(buffer, 0, buffer.Length);
                            }
                            var fileLocation = decryptedMessage.Media.File as TLEncryptedFile;
                            if (fileLocation == null) return;
                            var decryptedBuffer = Telegram.Api.Helpers.Utils.AesIge(buffer, mediaDocument.Key.Data, mediaDocument.IV.Data, false);

                            var newFileName = String.Format("{0}_{1}_{2}.{3}",
                                fileLocation.Id,
                                fileLocation.DCId,
                                fileLocation.AccessHash,
                                fileLocation.FileExt);

                            using (var file = store.OpenFile(newFileName, FileMode.OpenOrCreate, FileAccess.Write))
                            {
                                file.Write(decryptedBuffer, 0, decryptedBuffer.Length);
                            }

                            store.DeleteFile(fileName);
                        }
                    }
                }
            }
        }

        public void Handle(UploadingCanceledEventArgs args)
        {
            var owner = args.Item.Owner;
            var messageLayer = owner as TLDecryptedMessageLayer;
            if (messageLayer != null)
            {
                owner = messageLayer.Message;
            }

            var message = owner as TLDecryptedMessage;
            if (message != null)
            {
                var photo = message.Media as TLDecryptedMessageMediaPhoto;
                if (photo != null)
                {
                    message.Media.UploadingProgress = 0.0;
                    message.Status = MessageStatus.Failed;
                }

                var video = message.Media as TLDecryptedMessageMediaVideo;
                if (video != null)
                {
                    message.Media.UploadingProgress = 0.0;
                    message.Status = MessageStatus.Failed;
                }

                var document = message.Media as TLDecryptedMessageMediaDocument;
                if (document != null)
                {
                    message.Media.UploadingProgress = 0.0;
                    message.Status = MessageStatus.Failed;
                }

                var audio = message.Media as TLDecryptedMessageMediaAudio;
                if (audio != null)
                {
                    message.Media.UploadingProgress = 0.0;
                    message.Status = MessageStatus.Failed;
                }
                message.NotifyOfPropertyChange(() => message.Status);
            }
        }

        public void Handle(DownloadingCanceledEventArgs args)
        {
            var media = args.Item.Owner as TLDecryptedMessageMediaBase;
            if (media != null)
            {
                media.DownloadingProgress = 0.0;
            }
        }

        public void Handle(SetMessagesTTLEventArgs args)
        {
            if (Chat != null
                && args.Chat.Id.Value == Chat.Id.Value)
            {
                Execute.BeginOnUIThread(() =>
                {
                    Items.Insert(0, args.Message);
                    NotifyOfPropertyChange(() => DescriptionVisibility);
                });
            }
        }

        public void Handle(TLUserBase user)
        {
            if (With != null
                && With.Index == user.Index)
            {
                Subtitle = GetSubtitle(user);
                NotifyOfPropertyChange(() => Subtitle);
                NotifyOfPropertyChange(() => With);
            }
        }

        public void Handle(TLUpdateEncryptedChatTyping encryptedChatTyping)
        {

            if (Chat != null
                && With != null
                && Chat.Index == encryptedChatTyping.ChatId.Value)
            {
                BeginOnThreadPool(() => AddTypingUser(With.Index));
            }
        }

        public void Handle(UploadProgressChangedEventArgs args)
        {
            var message = GetDecryptedMessage(args.Item.Owner);
            if (message != null)
            {
                var media = message.Media;
                if (media != null)
                {
                    var delta = args.Progress - media.UploadingProgress;

                    if (delta > 0.0)
                    {
                        media.UploadingProgress = args.Progress;
                    }
                    return;
                }
            }

            var photo = args.Item.Owner as TLDecryptedMessageMediaPhoto;
            if (photo != null)
            {
                var delta = args.Progress - photo.UploadingProgress;

                if (delta > 0.0)
                {
                    photo.UploadingProgress = args.Progress;
                }
                return;
            }
        }

        public void Handle(TLDecryptedMessageBase decryptedMessage)
        {
            if (Chat != null
                && decryptedMessage.ChatId.Value == Chat.Id.Value)
            {
                var serviceMessage = decryptedMessage as TLDecryptedMessageService;
                if (serviceMessage != null)
                {
                    var action = serviceMessage.Action;
                    var setMessageTTLAction = action as TLDecryptedMessageActionSetMessageTTL;
                    if (setMessageTTLAction != null)
                    {
                        Chat.MessageTTL = setMessageTTLAction.TTLSeconds;
                    }
                    
                    var flushHistoryAction = action as TLDecryptedMessageActionFlushHistory;
                    if (flushHistoryAction != null)
                    {
                        Execute.BeginOnUIThread(() => Items.Clear());
                        CacheService.ClearDecryptedHistoryAsync(Chat.Id);
                    }

                    var readMessagesAction = action as TLDecryptedMessageActionReadMessages;
                    if (readMessagesAction != null)
                    {
                        Execute.BeginOnUIThread(() =>
                        {
                            foreach (var randomId in readMessagesAction.RandomIds)
                            {
                                foreach (var item in Items)
                                {
                                    if (item.RandomId.Value == randomId.Value)
                                    {
                                        item.Status = MessageStatus.Read;
                                        if (item.TTL != null && item.TTL.Value > 0)
                                        {
                                            item.DeleteDate = new TLLong(DateTime.Now.Ticks + Chat.MessageTTL.Value * TimeSpan.TicksPerSecond);
                                        }

                                        var decryptedMessage17 = item as TLDecryptedMessage17;
                                        if (decryptedMessage17 != null)
                                        {
                                            var decryptedMediaPhoto = decryptedMessage17.Media as TLDecryptedMessageMediaPhoto;
                                            if (decryptedMediaPhoto != null)
                                            {
                                                if (decryptedMediaPhoto.TTLParams == null)
                                                {
                                                    var ttlParams = new TTLParams();
                                                    ttlParams.IsStarted = true;
                                                    ttlParams.Total = decryptedMessage17.TTL.Value;
                                                    ttlParams.StartTime = DateTime.Now;
                                                    ttlParams.Out = decryptedMessage17.Out.Value;

                                                    decryptedMediaPhoto.TTLParams = ttlParams;
                                                }
                                            }

                                            var decryptedMediaVideo17 = decryptedMessage17.Media as TLDecryptedMessageMediaVideo17;
                                            if (decryptedMediaVideo17 != null)
                                            {
                                                if (decryptedMediaVideo17.TTLParams == null)
                                                {
                                                    var ttlParams = new TTLParams();
                                                    ttlParams.IsStarted = true;
                                                    ttlParams.Total = decryptedMessage17.TTL.Value;
                                                    ttlParams.StartTime = DateTime.Now;
                                                    ttlParams.Out = decryptedMessage17.Out.Value;

                                                    decryptedMediaVideo17.TTLParams = ttlParams;
                                                }
                                            }

                                            var decryptedMediaAudio17 = decryptedMessage17.Media as TLDecryptedMessageMediaAudio17;
                                            if (decryptedMediaAudio17 != null)
                                            {
                                                if (decryptedMediaAudio17.TTLParams == null)
                                                {
                                                    var ttlParams = new TTLParams();
                                                    ttlParams.IsStarted = true;
                                                    ttlParams.Total = decryptedMessage17.TTL.Value;
                                                    ttlParams.StartTime = DateTime.Now;
                                                    ttlParams.Out = decryptedMessage17.Out.Value;

                                                    decryptedMediaAudio17.TTLParams = ttlParams;
                                                }
                                            }
                                        }

                                        item.NotifyOfPropertyChange(() => item.Status);
                                        break;
                                    }
                                }
                            }
                        });
                    }

                    var deleteMessagesAction = action as TLDecryptedMessageActionDeleteMessages;
                    if (deleteMessagesAction != null)
                    {
                        Execute.BeginOnUIThread(() =>
                        {
                            foreach (var randomId in deleteMessagesAction.RandomIds)
                            {
                                for (var i = 0; i < Items.Count; i++)
                                {
                                    if (Items[i].RandomId.Value == randomId.Value)
                                    {
                                        Items.RemoveAt(i);
                                        break;
                                    }
                                }
                            }
                            CacheService.DeleteDecryptedMessages(deleteMessagesAction.RandomIds);
                        });
                    }
                }

                if (!TLUtils.IsDisplayedDecryptedMessage(decryptedMessage))
                {
                    return;
                }

                Execute.OnUIThread(() =>
                {
                    var position = InsertMessageInOrder(decryptedMessage);
                    NotifyOfPropertyChange(() => DescriptionVisibility);

                    if (position != -1)
                    {
                        ReadMessages(decryptedMessage);
                        if (decryptedMessage is TLDecryptedMessage)
                        {
                            RemoveTypingUser(With.Index);
                        }
                    }
                });
            }
        }

        public void Handle(TLEncryptedChatBase encryptedChat)
        {
            if (encryptedChat != null
                && Chat != null
                && encryptedChat.Id.Value == Chat.Id.Value)
            {
                Chat = encryptedChat;
                if (SecretChatDebug != null)
                {
                    SecretChatDebug.UpdateChat(encryptedChat);
                }
                NotifyOfPropertyChange(() => InputVisibility);
                NotifyOfPropertyChange(() => WaitingBarVisibility);
                NotifyOfPropertyChange(() => DeleteButtonVisibility);
                NotifyOfPropertyChange(() => IsApplicationBarVisible);
            }
        }

        public void Handle(TLUpdateEncryptedMessagesRead update)
        {
            return; //UpdatesService.ProcessUpdateInternal уже обработали там

            if (update != null
                && Chat != null
                && update.ChatId.Value == Chat.Id.Value)
            {
                Execute.BeginOnUIThread(() =>
                {
                    for (var i = 0; i < Items.Count; i++)
                    {
                        if (Items[i].Out.Value)
                        {
                            if (Items[i].Status == MessageStatus.Confirmed)
                            //&& Items[i].Date.Value <= update.MaxDate.Value) // здесь надо учитывать смещение по времени
                            {
                                Items[i].Status = MessageStatus.Read;
                                Items[i].NotifyOfPropertyChange("Status");
                                if (Items[i].TTL != null && Items[i].TTL.Value > 0)
                                {
                                    var decryptedMessage = Items[i] as TLDecryptedMessage17;
                                    if (decryptedMessage != null)
                                    {
                                        var decryptedPhoto = decryptedMessage.Media as TLDecryptedMessageMediaPhoto;
                                        if (decryptedPhoto != null && Items[i].TTL.Value <= 60.0)
                                        {
                                            continue;
                                        }

                                        var decryptedVideo17 = decryptedMessage.Media as TLDecryptedMessageMediaVideo17;
                                        if (decryptedVideo17 != null && Items[i].TTL.Value <= 60.0)
                                        {
                                            continue;
                                        }

                                        var decryptedAudio17 = decryptedMessage.Media as TLDecryptedMessageMediaAudio17;
                                        if (decryptedAudio17 != null && Items[i].TTL.Value <= 60.0)
                                        {
                                            continue;
                                        }
                                    }

                                    Items[i].DeleteDate = new TLLong(DateTime.Now.Ticks + Chat.MessageTTL.Value * TimeSpan.TicksPerSecond);
                                }
                            }
                            else if (Items[i].Status == MessageStatus.Read)
                            {
                                break;
                            }
                        }
                    }
                });
            }
        }

        public void Handle(TLUpdatePrivacy privacy)
        {
            MTProtoService.GetFullUserAsync((With).ToInputUser(),
                userFull =>
                {
                    With = userFull.User;
                    NotifyOfPropertyChange(() => With);
                    Subtitle = GetSubtitle(With);
                    NotifyOfPropertyChange(() => Subtitle);
                });
        }
    }
}
