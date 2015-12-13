using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media.Animation;
using Caliburn.Micro;
using Telegram.Api;
using Telegram.Api.Extensions;
using Telegram.Api.Services.Cache.EventArgs;
using Telegram.Api.Services.FileManager;
using Telegram.Api.Services.Updates;
using Telegram.Api.TL;
using TelegramClient.ViewModels.Contacts;
using TelegramClient.ViewModels.Media;
using TelegramClient.Views.Dialogs;
#if WP8
using Windows.Storage;
using TelegramClient_Opus;
#endif
using Execute = Telegram.Api.Helpers.Execute;

namespace TelegramClient.ViewModels.Dialogs
{
    public partial class DialogDetailsViewModel :
        Telegram.Api.Aggregator.IHandle<TLMessageCommon>, 
        Telegram.Api.Aggregator.IHandle<TLUpdateUserTyping>, 
        Telegram.Api.Aggregator.IHandle<TLUpdateChatUserTyping>,
        Telegram.Api.Aggregator.IHandle<TLUserBase>,
        Telegram.Api.Aggregator.IHandle<DialogRemovedEventArgs>,
        Telegram.Api.Aggregator.IHandle<DownloadableItem>,
        //Telegram.Api.Aggregator.IHandle<UploadableItem>,
        Telegram.Api.Aggregator.IHandle<ProgressChangedEventArgs>,
        Telegram.Api.Aggregator.IHandle<UploadProgressChangedEventArgs>,
        Telegram.Api.Aggregator.IHandle<UploadingCanceledEventArgs>,
        Telegram.Api.Aggregator.IHandle<UpdateCompletedEventArgs>,
        Telegram.Api.Aggregator.IHandle<AddedToContactsEventArgs>,
        Telegram.Api.Aggregator.IHandle<TLUpdatePrivacy>,
        Telegram.Api.Aggregator.IHandle<DeleteMessagesEventArgs>,
        Telegram.Api.Aggregator.IHandle<TLUpdateNotifySettings>,
        Telegram.Api.Aggregator.IHandle<TLUpdateWebPage>,
        Telegram.Api.Aggregator.IHandle<TLUpdateUserBlocked>,
        Telegram.Api.Aggregator.IHandle<TLAllStickersBase>
        //Telegram.Api.Aggregator.IHandle<TopMessageUpdatedEventArgs>
    {

        private int _addedCount = 0;

        private void InsertMessage(TLMessageCommon message)
        {
            ProcessRepliesAndAudio(new List<TLMessageBase>{message});

            Execute.BeginOnUIThread(() =>
            {
                //if (LazyItems.Count > 0)
                //{
                //    for (var i = 0; i < LazyItems.Count; i++)
                //    {
                //        if (LazyItems[i].DateIndex == message.DateIndex
                //                 && LazyItems[i].Index == message.Index)
                //        {
                //            Execute.ShowDebugMessage("InsertMessage catch doubled message");
                //            return;
                //        }
                //        if (LazyItems[i].DateIndex < message.DateIndex)
                //        {
                //            break;
                //        }
                        
                //    }
                //}

                var position = TLDialog.InsertMessageInOrder(Items, message);
                _addedCount++;

                //if (Items.Count > 30 && _addedCount >= 20)
                //{
                //    _addedCount = 0;
                //    while (Items.Count > 30)
                //    {
                //        Items.RemoveAt(Items.Count - 1);
                //    }
                //}

                if (position != -1)
                {
                    var message31 = message as TLMessage31;
                    if (message31 != null && !message31.Out.Value && message31.ReplyMarkup != null)
                    {
                        var fromId = message31.FromId;
                        var user = CacheService.GetUser(fromId) as TLUser;
                        if (user != null && user.IsBot)
                        {
                            SetReplyMarkup(message31);
                        }
                    }

                    Execute.BeginOnThreadPool(() =>
                    {
                        MarkAsRead(message);

                        if (message is TLMessage)
                        {
                            InputTypingManager.RemoveTypingUser(message.FromId.Value);
                        }
                    });
                }
            });
        }

        public void Handle(TLMessageCommon message)
        {
            if (message == null) return;
#if WP8
            if (!_isFirstSliceLoaded)
            {
                Execute.ShowDebugMessage("DialogDetailsViewModel.Handle(TLMessageCommon) _isFirstSliceLoaded=false");
                return;
            }
#endif

            if (With is TLUserBase
                && message.ToId is TLPeerUser
                && !message.Out.Value
                && ((TLUserBase)With).Id.Value == message.FromId.Value)
            {
                InsertMessage(message);
            }
            else if (With is TLUserBase
                    && message.ToId is TLPeerUser
                    && message.Out.Value
                    && ((TLUserBase)With).Id.Value == message.ToId.Id.Value)
            {
                InsertMessage(message);
            }
            else if (With is TLChannel 
                    && message.ToId is TLPeerChannel
                    && ((TLChannel)With).Id.Value == message.ToId.Id.Value)
            {
                InsertMessage(message);
            }
            else if (With is TLChatBase 
                    && message.ToId is TLPeerChat
                    && ((TLChatBase)With).Id.Value == message.ToId.Id.Value)
            {
                InsertMessage(message);
                NotifyOfPropertyChange(() => With); // Title and appbar changing

                var messageService = message as TLMessageService;
                if (messageService != null) 
                {
                    var deleteUserAction = messageService.Action as TLMessageActionChatDeleteUser;
                    if (deleteUserAction != null)
                    {
                        // delete replyMarkupKeyboard
                        var userId = deleteUserAction.UserId;
                        if (_replyMarkupMessage != null && _replyMarkupMessage.FromId.Value == userId.Value)
                        {
                            SetReplyMarkup(null);
                        }

                        // remove botInfo
                        GetFullInfo();
                    }

                    // add botInfo
                    var addUserAction = messageService.Action as TLMessageActionChatAddUser;
                    if (addUserAction != null)
                    {
                        GetFullInfo();
                    }

                    // Update number of participants
                    Subtitle = GetSubtitle();
                }
            }
            else if (With is TLBroadcastChat
                     && message.ToId is TLPeerBroadcast
                     && ((TLChatBase) With).Id.Value == message.ToId.Id.Value)
            {
                if (message is TLMessageService) // Update number of participants
                {
                    Subtitle = GetSubtitle();
                }
            }

            IsEmptyDialog = Items.Count == 0 && LazyItems.Count == 0;
        }

        private void MarkAsRead(TLMessageCommon message)
        {
            if (!_isActive) return;

            if (message != null 
                && !message.Out.Value 
                && message.Unread.Value)
                //&& !message.IsAudioVideoMessage())
            {
                StateService.GetNotifySettingsAsync(settings =>
                {
                    if (settings.InvisibleMode) return;

                    _currentDialog = _currentDialog ?? CacheService.GetDialog(TLUtils.InputPeerToPeer(Peer, StateService.CurrentUserId));
                    if (_currentDialog == null)
                    {
                        var inputPeerChannel = Peer as TLInputPeerChannel;
                        if (inputPeerChannel != null)
                        {
                            _currentDialog = DialogsViewModel.GetChannel(inputPeerChannel.ChatId) as TLDialog;
                        }
                    }

                    var topMessage = _currentDialog.TopMessage as TLMessageCommon;
                    SetRead(topMessage, d => new TLInt(Math.Max(0, d.UnreadCount.Value - 1)));

                    var channel = With as TLChannel;
                    if (channel != null)
                    {
                        MTProtoService.ReadHistoryAsync(channel, message.Id,
                            result =>
                            {
                                Execute.ShowDebugMessage("channels.readHistory result=" + result);
                                //SetRead(topMessage, d => new TLInt(0));
                            },
                            error =>
                            {
                                Execute.ShowDebugMessage("channels.readHistory error " + error);
                            });
                    }
                    else
                    {
                        MTProtoService.ReadHistoryAsync(Peer, message.Id, new TLInt(0),
                            affectedHistory =>
                            {
                                //SetRead(topMessage, d => new TLInt(0));
                            },
                            error =>
                            {
                                Execute.ShowDebugMessage("messages.readHistory error " + error);
                            });
                    }
                });
            }
        }

        private void SetRead(TLMessageCommon topMessage, Func<TLDialogBase, TLInt> getUnreadCount)
        {
            Execute.BeginOnUIThread(() =>
            {
                for (var i = 0; i < Items.Count; i++)
                {
                    var message = Items[i] as TLMessageCommon;
                    if (IsIncomingUnread(message))
                    {
                        message.SetUnread(TLBool.False);
                    }
                }

                if (IsIncomingUnread(topMessage))
                {
                    topMessage.SetUnread(TLBool.False);
                }

                _currentDialog.UnreadCount = getUnreadCount(_currentDialog);
                _currentDialog.NotifyOfPropertyChange(() => _currentDialog.UnreadCount);
                _currentDialog.NotifyOfPropertyChange(() => _currentDialog.TopMessage);
                _currentDialog.NotifyOfPropertyChange(() => _currentDialog.Self);

                CacheService.Commit();
            });
        }

        private static bool IsIncomingUnread(TLMessageCommon message)
        {
            return message != null
                   && !message.Out.Value
                   && message.Unread.Value;
                   //&& !message.IsAudioVideoMessage();
        }

        public void Handle(TLUpdateUserTyping userTyping)
        {
            var user = With as TLUserBase;
            if (user != null
                && user.Index == userTyping.UserId.Value)
            {
                HandleTypingCommon(userTyping);
            }
        }

        public void Handle(TLUpdateChatUserTyping chatUserTyping)
        {
            var chat = With as TLChatBase;
            if (chat != null
                && chat.Index == chatUserTyping.ChatId.Value)
            {
                HandleTypingCommon(chatUserTyping);
            }
        }

        private void HandleTypingCommon(TLUpdateTypingBase updateTyping)
        {
            TLSendMessageActionBase action = new TLSendMessageTypingAction();
            var updateUserTyping17 = updateTyping as IUserTypingAction;
            if (updateUserTyping17 != null)
            {
                action = updateUserTyping17.Action;
            }

            if (action is TLSendMessageCancelAction)
            {
                InputTypingManager.RemoveTypingUser(updateTyping.UserId.Value);
            }
            else
            {
                InputTypingManager.AddTypingUser(updateTyping.UserId.Value, action);
            }
        }

        public void Handle(TLUserBase user)
        {
            if (With is TLUserBase
                && ((TLUserBase)With).Index == user.Index)
            {
                Subtitle = GetSubtitle();
                With = user;
                NotifyOfPropertyChange(() => With);
            }
        }

        public void Handle(AddedToContactsEventArgs args)
        {
            if (With is TLUserBase
                && ((TLUserBase)With).Index == args.Contact.Index)
            {
                ChangeUserAction();
            }
        }

        public void Handle(DialogRemovedEventArgs args)
        {
            if (With == args.Dialog.With)
            {
                BeginOnUIThread(() =>
                {
                    LazyItems.Clear();
                    Items.Clear();
                    IsEmptyDialog = true;
                });
            }
        }

        public void Handle(MessagesRemovedEventArgs args)
        {
            if (With == args.Dialog.With && args.Messages != null)
            {
                BeginOnUIThread(() =>
                {
                    foreach (var message in args.Messages)
                    {
                        Items.Remove(message);
                    }
                    IsEmptyDialog = Items.Count == 0 && LazyItems.Count == 0;
                });
            }
        }

        public void Handle(DownloadableItem item)
        {
            Execute.BeginOnUIThread(() =>
            {

                var chatPhoto = item.Owner as TLChatPhoto;
                if (chatPhoto != null)
                {
                    var channel = With as TLChannel;
                    if (channel != null)
                    {
                        channel.NotifyOfPropertyChange(() => channel.Photo);
                        //channel.NotifyOfPropertyChange(() => channel.ChatPhoto);
                    }

                    var serviceMessages = Items.OfType<TLMessageService>();
                    foreach (var serviceMessage in serviceMessages)
                    {
                        var editPhoto = serviceMessage.Action as TLMessageActionChatEditPhoto;
                        if (editPhoto != null && editPhoto.Photo == chatPhoto)
                        {
                            editPhoto.NotifyOfPropertyChange(() => editPhoto.Photo);
                            break;
                        }
                    }
                }

                var message = item.Owner as TLMessage;
                if (message != null)
                {
                    var messages = Items.OfType<TLMessage>();
                    foreach (var m in messages)
                    {
                        var media = m.Media as TLMessageMediaVideo;
                        if (media != null && m == item.Owner)
                        {
                            m.Media.IsCanceled = false;
                            m.Media.LastProgress = 0.0;
                            m.Media.DownloadingProgress = 0.0;
                            m.NotifyOfPropertyChange(() => m.Self);
                            m.Media.IsoFileName = item.IsoFileName;
                            break;
                        }

                        var doc = m.Media as TLMessageMediaDocument;
                        if (doc != null && m == item.Owner)
                        {
                            m.Media.IsCanceled = false;
                            m.Media.LastProgress = 0.0;
                            m.Media.DownloadingProgress = 0.0;
                            m.Media.NotifyOfPropertyChange(() => m.Media.Self); // update download icon for documents
                            m.NotifyOfPropertyChange(() => m.Self);
                            m.Media.IsoFileName = item.IsoFileName;
                            break;
                        }

                        var audioMedia = m.Media as TLMessageMediaAudio;
                        if (audioMedia != null && m == item.Owner)
                        {
                            m.Media.IsCanceled = false;
                            m.Media.LastProgress = 0.0;
                            m.Media.DownloadingProgress = 0.0;
                            m.Media.IsoFileName = item.IsoFileName;

                            var a = audioMedia.Audio as TLAudio;
                            if (a != null)
                            {
                                var fileName = a.GetFileName();
                                var wavFileName = Path.GetFileNameWithoutExtension(fileName) + ".wav";
#if WP8
                                var component = new WindowsPhoneRuntimeComponent();
                                var result = component.InitPlayer(ApplicationData.Current.LocalFolder.Path + "\\" + fileName);
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

                            break;
                        }

                    }
                    return;
                }

                var photo = item.Owner as TLPhoto;
                if (photo != null)
                {
                    var isUpdated = false;
                    var messages = Items.OfType<TLMessage>();
                    foreach (var m in messages)
                    {
                        var mediaPhoto = m.Media as TLMessageMediaPhoto;
                        if (mediaPhoto != null && mediaPhoto.Photo == photo)
                        {
                            mediaPhoto.NotifyOfPropertyChange(() => mediaPhoto.Photo);
                            mediaPhoto.NotifyOfPropertyChange(() => mediaPhoto.Self);
                            isUpdated = true;
                            break;
                        }

                        var mediaWebPage = m.Media as TLMessageMediaWebPage;
                        if (mediaWebPage != null && mediaWebPage.Photo == photo)
                        {
                            mediaWebPage.NotifyOfPropertyChange(() => mediaWebPage.Photo);
                            mediaWebPage.NotifyOfPropertyChange(() => mediaWebPage.Self);
                            isUpdated = false;
                            break;
                        }
                    }

                    if (isUpdated) return;

                    var serviceMessages = Items.OfType<TLMessageService>();
                    foreach (var serviceMessage in serviceMessages)
                    {
                        var editPhoto = serviceMessage.Action as TLMessageActionChatEditPhoto;
                        if (editPhoto != null && editPhoto.Photo == photo)
                        {
                            editPhoto.NotifyOfPropertyChange(() => editPhoto.Photo);
                            isUpdated = true;
                            break;
                        }
                    }
                }

                var document = item.Owner as TLDocument;
                if (document != null)
                {
                    var messages = Items.OfType<TLMessage>();
                    foreach (var m in messages)
                    {
                        var media = m.Media as TLMessageMediaDocument;
                        if (media != null && TLDocumentBase.DocumentEquals(media.Document, document))
                        {
                            media.NotifyOfPropertyChange(() => media.Document);
                            break;
                        }
                    }
                }

                var video = item.Owner as TLVideo;
                if (video != null)
                {
                    var messages = Items.OfType<TLMessage>();
                    foreach (var m in messages)
                    {
                        var media = m.Media as TLMessageMediaVideo;
                        if (media != null && media.Video == video)
                        {
                            media.NotifyOfPropertyChange(() => media.Video);
                            break;
                        }
                    }
                }

                var audio = item.Owner as TLAudio;
                if (audio != null)
                {
                    var messages = Items.OfType<TLMessage>();
                    foreach (var m in messages)
                    {
                        var media = m.Media as TLMessageMediaAudio;
                        if (media != null && media.Audio == audio)
                        {
                            media.NotifyOfPropertyChange(() => media.Audio);
                            break;
                        }
                    }
                }

                var webPage = item.Owner as TLWebPage;
                if (webPage != null)
                {
                    var messages = Items.OfType<TLMessage>();
                    foreach (var m in messages)
                    {
                        var media = m.Media as TLMessageMediaWebPage;
                        if (media != null && media.WebPage == webPage)
                        {
                            media.NotifyOfPropertyChange(() => media.Photo);
                            media.NotifyOfPropertyChange(() => media.Self);
                            break;
                        }
                    }
                }
            });
        }

        public void Handle(ProgressChangedEventArgs args)
        {
            var message = args.Item.Owner as TLMessage;
            if (message != null)
            {
                var video = message.Media as TLMessageMediaVideo;
                if (video != null && !video.IsCanceled)
                {
                    var delta = args.Progress - video.DownloadingProgress;

                    if (delta > 0.0)
                    {
                        video.DownloadingProgress = args.Progress;

                    }
                    return;
                }

                var audio = message.Media as TLMessageMediaAudio;
                if (audio != null && !audio.IsCanceled)
                {
                    var delta = args.Progress - audio.DownloadingProgress;

                    if (delta > 0.0)
                    {
                        audio.DownloadingProgress = args.Progress;
                    }
                    return;
                }

                var document = message.Media as TLMessageMediaDocument;
                if (document != null && !document.IsCanceled)
                {
                    var delta = args.Progress - document.DownloadingProgress;

                    if (delta > 0.0)
                    {
                        document.DownloadingProgress = args.Progress;
                    }
                    return;
                }
            }
        }

        public void Handle(UploadProgressChangedEventArgs args)
        {
            var message = args.Item.Owner as TLMessage;
            if (message != null)
            {
                var photo = message.Media as TLMessageMediaPhoto;
                if (photo != null)
                {
                    var delta = args.Progress - photo.UploadingProgress;

                    if (delta > 0.0)
                    {
                        photo.UploadingProgress = args.Progress;
                    }

                    UploadTypingManager.SetTyping(UploadTypingKind.Photo);

                    return;
                }

                var document = message.Media as TLMessageMediaDocument;
                if (document != null)
                {
                    var delta = args.Progress - document.UploadingProgress;

                    if (delta > 0.0)
                    {
                        document.UploadingProgress = args.Progress;
                    }

                    UploadTypingManager.SetTyping(UploadTypingKind.Document);

                    return;
                }

                var video = message.Media as TLMessageMediaVideo;
                if (video != null)
                {
                    var delta = args.Progress - video.UploadingProgress;

                    if (delta > 0.0)
                    {
                        video.UploadingProgress = args.Progress;
                    }

                    UploadTypingManager.SetTyping(UploadTypingKind.Video);

                    return;
                }
            }
        }

        public void Handle(UploadingCanceledEventArgs args)
        {
            var message = args.Item.Owner as TLMessage;
            if (message != null)
            {
                var photo = message.Media as TLMessageMediaPhoto;
                if (photo != null)
                {
                    message.Media.UploadingProgress = 0.0;
                    message.Status = MessageStatus.Failed;
                }

                var document = message.Media as TLMessageMediaDocument;
                if (document != null)
                {
                    message.Media.UploadingProgress = 0.0;
                    message.Status = MessageStatus.Failed;
                }

                var video = message.Media as TLMessageMediaVideo;
                if (video != null)
                {
                    message.Media.UploadingProgress = 0.0;
                    message.Status = MessageStatus.Failed;
                }

                UploadTypingManager.CancelTyping();
            }
        }

        public void Handle(TopMessageUpdatedEventArgs args)
        {
#if WP8
            if (!_isFirstSliceLoaded)
            {
                Execute.ShowDebugMessage("DialogDetailsViewModel.Handle(TLMessageCommon) _isFirstSliceLoaded=false");
                return;
            }
#endif

            var serviceMessage = args.Message as TLMessageService;
            if (serviceMessage == null) return;
            Handle(serviceMessage);
        }

        public void Handle(UpdateCompletedEventArgs args)
        {
            if (Peer == null) return; // tombstoning 
            if (!_isUpdated) return;

            CacheService.GetHistoryAsync(
                new TLInt(StateService.CurrentUserId),
                TLUtils.InputPeerToPeer(Peer, StateService.CurrentUserId),
                messages =>
                {
                    ProcessRepliesAndAudio(messages);

                    Execute.BeginOnUIThread(() =>
                    {
                        var messageIndexes = new Dictionary<int, int>();
                        for (var i = 0; i < Items.Count; i++)
                        {
                            messageIndexes[Items[i].Index] = Items[i].Index;
                        }
                        var topMessage = Items.FirstOrDefault(x => x.Index != 0);
                        var lastMessage = Items.LastOrDefault(x => x.Index != 0);


                        var newMessages = new List<TLMessageBase>();
                        var newMessagesAtMiddle = new List<TLMessageBase>();
                        foreach (var message in messages)
                        {

                            if (message.Index != 0)  //возможно, это новое сообщение
                            {
                                if (topMessage == null && lastMessage == null)  // в имеющемся списке нет сообщений с индексом
                                {
                                    newMessages.Add(message);
                                }
                                else
                                {
                                    if (topMessage != null && message.Index > topMessage.Index)  // до первого сообщения с индексом в списке 
                                    {
                                        newMessages.Add(message);
                                    }
                                    else if (lastMessage != null
                                        && !messageIndexes.ContainsKey(message.Index)
                                        && message.Index < lastMessage.Index)  // в середине списка до последнего сообщения с индексом
                                    {
                                        Execute.ShowDebugMessage("Catch middle message: " + message);
                                        newMessagesAtMiddle.Add(message);
                                    }
                                    else
                                    {
                                        break;
                                    }
                                }

                            }
                        }

                        if (newMessages.Count > 0)
                        {
                            Subtitle = GetSubtitle();

                            for (var i = newMessagesAtMiddle.Count; i > 0; i--)
                            {
                                TLDialog.InsertMessageInOrder(Items, newMessagesAtMiddle[i]);
                            }

                            AddUnreadHistory(newMessages);
                        }
                    });
                    
                }, int.MaxValue);
        }

        private static bool UseSeparator(IList<TLMessageBase> messages)
        {
            return messages.Count > 1;
        }

        private void AddUnreadHistory(IList<TLMessageBase> newMessages)
        {
            var useSeparator = UseSeparator(newMessages);

            AddSeparator(useSeparator);

            const int firstSliceCount = 1;
            var count = 0;
            var secondSlice = new List<TLMessageBase>();
            for (var i = newMessages.Count; i > 0; i--)
            {
                if (count < firstSliceCount || !useSeparator)
                {
                    count++;
                    Items.Insert(0, newMessages[i - 1]);
                }
                else
                {
                    secondSlice.Add(newMessages[i - 1]);
                }
            }

            if (secondSlice.Count == 0)
            {
                ContinueAddUnreadHistory();
            }
            else
            {
                InsertAndHoldPosition(secondSlice, () =>
                {
                    ContinueAddUnreadHistory();

                    BeginOnUIThread(() => { ScrollToBottomVisibility = Visibility.Visible; });
                });
            }
        }

        private void InsertAndHoldPosition(IEnumerable<TLMessageBase> items, System.Action callback)
        {
            BeginOnUIThread(() =>
            {
                HoldScrollingPosition = true;
                BeginOnUIThread(() =>
                {
                    foreach (var message in items)
                    {
                        Items.Insert(0, message);
                    }
                    HoldScrollingPosition = false;

                    callback.SafeInvoke();
                });
            });
        }

        private void ContinueAddUnreadHistory()
        {
            UpdateReplyMarkup(Items);

            if (_isActive)
            {
                ReadHistoryAsync();
            }
        }

        private void AddSeparator(bool useSeparator)
        {
            for (var i = 0; i < Items.Count; i++)
            {
                var serviceMessage = Items[i] as TLMessageService;
                if (serviceMessage != null && serviceMessage.Action is TLMessageActionUnreadMessages)
                {
                    Items.RemoveAt(i--);
                }
            }

            if (useSeparator)
            {
                var separator = new TLMessageService17
                {
                    FromId = new TLInt(StateService.CurrentUserId),
                    ToId = TLUtils.InputPeerToPeer(Peer, StateService.CurrentUserId),
                    Status = With is TLBroadcastChat
                        ? MessageStatus.Broadcast
                        : MessageStatus.Sending,
                    Out = new TLBool { Value = true },
                    Unread = new TLBool(true),
                    Date = TLUtils.DateToUniversalTimeTLInt(MTProtoService.ClientTicksDelta, DateTime.Now),
                    Action = new TLMessageActionUnreadMessages(),
                    //IsAnimated = true,
                    RandomId = TLLong.Random()
                };
                Items.Insert(0, separator);
            }
        }

        private void UpdateReplyMarkup(IList<TLMessageBase> items)
        {
            for (var i = 0; i < items.Count; i++)
            {
                var message31 = items[i] as TLMessage31;
                if (message31 != null && !message31.Out.Value)
                {
                    if (message31.ReplyMarkup != null)
                    {
                        var fromId = message31.FromId;
                        var user = CacheService.GetUser(fromId) as TLUser;
                        if (user != null && user.IsBot)
                        {
                            SetReplyMarkup(message31);
                            break;
                        }
                    }
                }
            }
        }

        public void Handle(TLUpdatePrivacy privacy)
        {
            GetFullInfo();
        }

        public void Handle(DeleteMessagesEventArgs args)
        {
            if (With == args.Owner)
            {
                BeginOnUIThread(() =>
                {
                    for (var j = 0; j < args.Messages.Count; j++)
                    {
                        for (var i = 0; i < Items.Count; i++)
                        {
                            if (Items[i].Index == args.Messages[j].Index)
                            {
                                Items.RemoveAt(i);
                                break;
                            }
                        }
                    }
                });
            }
        }

        public void Handle(TLUpdateNotifySettings updateNotifySettings)
        {
            // threadpool
            var notifyPeer = updateNotifySettings.Peer as TLNotifyPeer;
            if (notifyPeer != null)
            {
                var peer = notifyPeer.Peer;
                var chat = With as TLChatBase;
                var user = With as TLUserBase;
                var channel = With as TLChannel;

                if (peer is TLPeerChat
                    && chat != null
                    && peer.Id.Value == chat.Index)
                {
                    chat.NotifySettings = updateNotifySettings.NotifySettings;
                    With.NotifyOfPropertyChange(() => chat.NotifySettings);
                }
                else if (peer is TLPeerUser
                    && user != null
                    && peer.Id.Value == user.Index)
                {
                    user.NotifySettings = updateNotifySettings.NotifySettings;
                    With.NotifyOfPropertyChange(() => chat.NotifySettings);
                }
                else if (peer is TLPeerChannel
                    && channel != null
                    && peer.Id.Value == channel.Index)
                {
                    channel.NotifySettings = updateNotifySettings.NotifySettings;
                    With.NotifyOfPropertyChange(() => channel.NotifySettings);
                    NotifyOfPropertyChange(() => AppBarCommandString);
                }
            }
        }

        public void Handle(TLUpdateWebPage updateWebPage)
        {
            Execute.BeginOnUIThread(() =>
            {
                var webPageBase = updateWebPage.WebPage;

                foreach (var webPageKeyValue in _webPagesCache)
                {
                    var mediaBase = webPageKeyValue.Value;
                    var webPageMessageMedia = mediaBase as TLMessageMediaWebPage;
                    if (webPageMessageMedia != null)
                    {
                        var webPage = webPageMessageMedia.WebPage;
                        if (webPage != null)
                        {
                            if (webPage.Id.Value == webPageBase.Id.Value)
                            {
                                webPageMessageMedia.WebPage = webPageBase;

                                if (string.Equals(Text, webPageKeyValue.Key))
                                {
                                    if (webPageBase is TLWebPage || webPageBase is TLWebPagePending)
                                    {
                                        SaveReply();

                                        Reply = new TLMessagesContainter {WebPageMedia = webPageMessageMedia};
                                    }
                                    else
                                    {
                                        RestoreReply();
                                    }
                                }

                                break;
                            }
                        }
                    }
                }
            });
        }

        public void Handle(TLUpdateUserBlocked updateUserBlocked)
        {
            var user = With as TLUserBase;
            if (user != null && user.Id.Value == updateUserBlocked.UserId.Value)
            {
                NotifyOfPropertyChange(() => With);
            }
        }

        public void Handle(TLAllStickersBase allStickersBase)
        {
            var allStickers = allStickersBase as TLAllStickers;
            if (allStickers != null)
            {
                Stickers = allStickers;
            }
        }
    }


    public static class Wav
	{
		public static MemoryStream GetWavAsMemoryStream(this byte[] data, int sampleRate, int audioChannels = 1, int bitsPerSample = 16)
		{
			MemoryStream memoryStream = new MemoryStream();
			Wav.WriteHeader(memoryStream, sampleRate, audioChannels, bitsPerSample);
			Wav.SeekPastHeader(memoryStream);
			memoryStream.Write(data, 0, data.Length);
			Wav.UpdateHeader(memoryStream);
			return memoryStream;
		}
		public static MemoryStream GetWavAsMemoryStream(this Stream data, int sampleRate, int audioChannels = 1, int bitsPerSample = 16)
		{
			MemoryStream memoryStream = new MemoryStream();
			Wav.WriteHeader(memoryStream, sampleRate, audioChannels, bitsPerSample);
			Wav.SeekPastHeader(memoryStream);
			data.Position = 0L;
			data.CopyTo(memoryStream);
			Wav.UpdateHeader(memoryStream);
			return memoryStream;
		}
		public static byte[] GetWavAsByteArray(this byte[] data, int sampleRate, int audioChannels = 1, int bitsPerSample = 16)
		{
			return data.GetWavAsMemoryStream(sampleRate, audioChannels, bitsPerSample).ToArray();
		}
		public static byte[] GetWavAsByteArray(this Stream data, int sampleRate, int audioChannels = 1, int bitsPerSample = 16)
		{
			return data.GetWavAsMemoryStream(sampleRate, audioChannels, bitsPerSample).ToArray();
		}
		public static void WriteHeader(Stream stream, int sampleRate, int audioChannels = 1, int bitsPerSample = 16)
		{
			int num = bitsPerSample / 8;
			Encoding uTF = Encoding.UTF8;
			long position = stream.Position;
			stream.Seek(0L, 0);
			stream.Write(uTF.GetBytes("RIFF"), 0, 4);
			stream.Write(BitConverter.GetBytes(0), 0, 4);
			stream.Write(uTF.GetBytes("WAVE"), 0, 4);
			stream.Write(uTF.GetBytes("fmt "), 0, 4);
			stream.Write(BitConverter.GetBytes(16), 0, 4);
			stream.Write(BitConverter.GetBytes(1), 0, 2);
			stream.Write(BitConverter.GetBytes((short)audioChannels), 0, 2);
			stream.Write(BitConverter.GetBytes(sampleRate), 0, 4);
			stream.Write(BitConverter.GetBytes(sampleRate * num * audioChannels), 0, 4);
			stream.Write(BitConverter.GetBytes((short)num), 0, 2);
			stream.Write(BitConverter.GetBytes((short)bitsPerSample), 0, 2);
			stream.Write(uTF.GetBytes("data"), 0, 4);
			stream.Write(BitConverter.GetBytes(0), 0, 4);
			Wav.UpdateHeader(stream);
			stream.Seek(position, 0);
		}
		public static void SeekPastHeader(Stream stream)
		{
			if (!stream.CanSeek)
			{
				throw new Exception("Can't seek stream to update wav header");
			}
			stream.Seek(44L, 0);
		}
		public static void UpdateHeader(Stream stream)
		{
			if (!stream.CanSeek)
			{
				throw new Exception("Can't seek stream to update wav header");
			}
			long position = stream.Position;
			stream.Seek(4L, 0);
			stream.Write(BitConverter.GetBytes((int)stream.Length - 8), 0, 4);
			stream.Seek(40L, 0);
			stream.Write(BitConverter.GetBytes((int)stream.Length - 44), 0, 4);
			stream.Seek(position, 0);
		}
	}
}
