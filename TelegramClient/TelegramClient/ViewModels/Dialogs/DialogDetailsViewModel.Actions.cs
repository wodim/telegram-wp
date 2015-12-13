using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Text;
using System.Windows;
using Telegram.Api;
using Telegram.Api.Services.Updates;
using TelegramClient.Services;
using TelegramClient.ViewModels.Additional;
#if WP8
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using Windows.Storage;
#endif
using Caliburn.Micro;
using Telegram.Api.Extensions;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.Cache.EventArgs;
using Telegram.Api.TL;
using TelegramClient.Resources;
using TelegramClient.ViewModels.Chats;
using TelegramClient.ViewModels.Contacts;
using TelegramClient.ViewModels.Media;
using Execute = Telegram.Api.Helpers.Execute;

namespace TelegramClient.ViewModels.Dialogs
{
    public partial class DialogDetailsViewModel
    {
        public void OpenCropedMessage(TLMessage message)
        {
            if (message == null) return;

            StateService.MediaMessage = message;
            NavigationService.UriFor<MessageViewerViewModel>().Navigate();
        }

#if WP81
        private static StorageFile _fromFile;
        
        public static async void SaveFile(StorageFile toFile)
        {
            if (_fromFile == null) return;
            if (toFile == null) return;

            try
            {
                using (var streamSource = await _fromFile.OpenStreamForReadAsync())
                {
                    using (var streamDest = await toFile.OpenStreamForWriteAsync())
                    {
                        await streamSource.CopyToAsync(streamDest, 1024);
                    }
                }
            }
            catch (Exception ex)
            {
                Execute.ShowDebugMessage("SaveFile exception \n" + ex);
            }
            
            return;
        }
#endif



#if WP81

        public void SaveMedia(TLMessage message)
        {
            if (message == null) return;

            SaveMediaCommon(message);
        }
        public static async void SaveMediaCommon(TLMessage message)
        {
            var storageFile = await GetStorageFile(message.Media);
            if (storageFile == null)
            {
                return;
            }

            _fromFile = storageFile;

            var fileSavePicker = new FileSavePicker();
            fileSavePicker.SuggestedSaveFile = storageFile;
            fileSavePicker.SuggestedFileName = storageFile.Name;
            fileSavePicker.FileTypeChoices.Add(storageFile.FileType ?? "file", new[] { storageFile.FileType });
            fileSavePicker.ContinuationData.Add("From", "DialogDetailsView");
            fileSavePicker.PickSaveFileAndContinue();
        }

        public string GetMediaFileName(TLMessage message)
        {
            var mediaDocument = message.Media as TLMessageMediaDocument;
            if (mediaDocument != null)
            {
                var file = mediaDocument.File;

                if (file == null)
                {
                    var document = mediaDocument.Document as TLDocument;
                    if (document != null)
                    {
                        var localFileName = document.GetFileName();
                        var globalFileName = mediaDocument.IsoFileName;
                        var store = IsolatedStorageFile.GetUserStoreForApplication();
                        if (store.FileExists(localFileName))
                        {
                            return Path.GetFileName(localFileName);
                        }

                        if (store.FileExists(globalFileName))
                        {
                            return Path.GetFileName(globalFileName);
                        }
        
                        if (File.Exists(globalFileName))
                        {
                            return Path.GetFileName(globalFileName);
                        }

                    }
                }

                return Path.GetFileName(mediaDocument.File.Name);
            }

            var mediaVideo = message.Media as TLMessageMediaVideo;
            if (mediaVideo != null)
            {
                var file = mediaVideo.File;

                if (file == null)
                {
                    var video = mediaVideo.Video as TLVideo;
                    if (video != null)
                    {
                        var localFileName = video.GetFileName();
                        var globalFileName = mediaVideo.IsoFileName;
                        var store = IsolatedStorageFile.GetUserStoreForApplication();
                        if (store.FileExists(localFileName))
                        {
                            return Path.GetFileName(localFileName);
                        }

                        if (store.FileExists(globalFileName))
                        {
                            return Path.GetFileName(globalFileName);
                        }

                        if (File.Exists(globalFileName))
                        {
                            return Path.GetFileName(globalFileName);
                        }
                    }
                }

                return Path.GetFileName(mediaVideo.File.Name);
            }

            return null;
        }
#endif

        public UserActionViewModel UserAction { get; protected set; }

        public void ChangeUserAction()
        {
            if (UserActionViewModel.IsRequired(With))
            {
                if (UserAction == null)
                {
                    UserAction = new UserActionViewModel((TLUserBase)With);
                    UserAction.InvokeUserAction += (sender, args) => InvokeUserAction();
                    UserAction.InvokeUserAction2 += (sender, args) => InvokeUserAction2();
                    NotifyOfPropertyChange(() => UserAction);
                }
                else
                {
                    UserAction.SetUser((TLUserBase)With);
                }
            }
            else
            {
                if (UserAction != null)
                {
                    UserAction = null;
                    NotifyOfPropertyChange(() => UserAction);
                }
            }
        }

        public void InvokeUserAction()
        {
            var userRequest = With as TLUserRequest;
            if (userRequest != null)
            {
                AddToContacts(userRequest);
                return;
            }

            var userForeign = With as TLUserForeign;
            if (userForeign != null)
            {
                ShareMyContactInfo();
                return;
            }
        }

        public void InvokeUserAction2()
        {
            ReportSpam();
        }

        private void ShareMyContactInfo()
        {
            var currentUser = CacheService.GetUser(new TLInt(StateService.CurrentUserId));
            if (currentUser == null) return;

            SendContact(currentUser);
        }

        public void AddToContacts(TLUserRequest userRequest)
        {
            if (userRequest == null) return;

            var phone = userRequest.Phone;

            IsWorking = true;
            ContactDetailsViewModel.ImportContactAsync(
                userRequest, phone, MTProtoService,
                result =>
                {
                    IsWorking = false;
                    if (result.Users.Count > 0)
                    {
                        EventAggregator.Publish(result.Users[0]);
                    }

                    var contact = result.Users[0] as TLUserContact;
                    if (contact != null)
                    {
                        ContactsHelper.CreateContactAsync(DownloadFileManager, StateService, contact);
                    }

                    if (UserAction != null)
                    {
                        UserAction.Remove();
                    }
                },
                error =>
                {
                    IsWorking = false;
                });
        }

        public void AppBarCommand()
        {
            if (IsChannel)
            {
                var channel = With as TLChannel;
                if (channel != null)
                {
                    if (channel.Left.Value)
                    {
                        IsWorking = true;
                        MTProtoService.JoinChannelAsync(channel,
                            result => Execute.BeginOnUIThread(() =>
                            {
                                IsWorking = false;

                                Subtitle = GetSubtitle();
                                NotifyOfPropertyChange(() => With);
                                NotifyOfPropertyChange(() => IsAppBarCommandVisible);
                                NotifyOfPropertyChange(() => AppBarCommandString);

                                var message = Items.FirstOrDefault();
                                if (message != null)
                                {
                                    CacheService.DeleteChannelMessages(channel.Id, new TLVector<TLInt>{message.Id});
                                    CacheService.SyncMessage(message, new TLPeerChannel{ Id = channel.Id },
                                        m =>
                                        {
                                            
                                        });
                                }
                            }),
                            error => Execute.BeginOnUIThread(() =>
                            {
                                IsWorking = false;
                                Execute.ShowDebugMessage("channels.joinChannel error " + error);
                            }));


                        return;
                    }

                    var notifySettings = channel.NotifySettings as TLPeerNotifySettings;
                    if (notifySettings != null)
                    {
                        var muteUntil = notifySettings.MuteUntil.Value == 0 ? int.MaxValue : 0;

                        var inputSettings = new TLInputPeerNotifySettings
                        {
                            EventsMask = notifySettings.EventsMask,
                            MuteUntil = new TLInt(muteUntil),
                            ShowPreviews = notifySettings.ShowPreviews,
                            Sound = notifySettings.Sound
                        };

                        IsWorking = true;
                        MTProtoService.UpdateNotifySettingsAsync(new TLInputNotifyPeer{Peer = channel.ToInputPeer()}, inputSettings,
                            result => Execute.BeginOnUIThread(() =>
                            {
                                IsWorking = false;

                                notifySettings.MuteUntil = new TLInt(muteUntil);
                                NotifyOfPropertyChange(() => AppBarCommandString);
                                channel.NotifyOfPropertyChange(() => channel.NotifySettings);

                                var dialog = CacheService.GetDialog(new TLPeerChannel { Id = channel.Id });
                                if (dialog != null)
                                {
                                    dialog.NotifySettings = channel.NotifySettings;
                                    dialog.NotifyOfPropertyChange(() => dialog.NotifySettings);
                                    var settings = dialog.With as INotifySettings;
                                    if (settings != null)
                                    {
                                        settings.NotifySettings = channel.NotifySettings;
                                    }
                                }

                                CacheService.Commit();
                            }),
                            error => Execute.BeginOnUIThread(() =>
                            {
                                IsWorking = false;
                                Execute.ShowDebugMessage("account.updateNotifySettings error " + error);
                            }));
                    }
                }
            }
            else if (IsChannelForbidden)
            {
                var channelForbidden = With as TLChannelForbidden;
                if (channelForbidden != null)
                {
                    DeleteChannelInternal(channelForbidden.Id);
                }
            }
            else if (IsChatForbidden || IsChatDeactivated)
            {
                var chat = With as TLChatBase;
                if (chat != null)
                {
                    DialogsViewModel.DeleteAndExitDialogCommon((TLChatBase)With, MTProtoService, () =>
                    {
                        var dialog = CacheService.GetDialog(new TLPeerChat { Id = chat.Id });
                        if (dialog != null)
                        {
                            EventAggregator.Publish(new DialogRemovedEventArgs(dialog));
                            CacheService.DeleteDialog(dialog);
                            DialogsViewModel.UnpinFromStart(dialog);
                        }
                        BeginOnUIThread(() =>
                        {
                            if (NavigationService.CanGoBack)
                            {
                                NavigationService.GoBack();
                            }
                            else
                            {
                                NavigateToShellViewModel();
                            }
                        });
                    },
                        error =>
                        {
                            Execute.ShowDebugMessage("DeleteAndExitDialogCommon error " + error);
                        });
                }
            }
            else if (IsBotStarting)
            {
                if (_bot == null || string.IsNullOrEmpty(_bot.AccessToken))
                {
                    _text = "/start";
                    Execute.BeginOnUIThread(() => SendInternal(false, false));
                }
                else
                {
                    var accessToken = new TLString(_bot.AccessToken);
                    _bot.AccessToken = string.Empty;

                    BeginOnUIThread(() =>
                    {
                        var text = With is TLUser
                            ? new TLString("/start")
                            : new TLString("/start@" + ((IUserName)_bot).UserName);

                        var message = GetMessage(text, new TLMessageMediaEmpty());
                        var previousMessage = InsertSendingMessage(message);
                        IsEmptyDialog = Items.Count == 0 && LazyItems.Count == 0;
                        NotifyOfPropertyChange(() => With);

                        BeginOnThreadPool(() =>
                            CacheService.SyncSendingMessage(
                                message, previousMessage,
                                TLUtils.InputPeerToPeer(Peer, StateService.CurrentUserId),
                                result => StartBotInternal(result, accessToken)));
                    });
                }

                return;
            }
            else if (IsUserBlocked)
            {
                var user = With as TLUserBase;
                if (user != null)
                {
                    var confirmation = IsBot
                        ? MessageBoxResult.OK 
                        : MessageBox.Show(AppResources.UnblockContactConfirmation, AppResources.AppName, MessageBoxButton.OKCancel);

                    if (confirmation == MessageBoxResult.OK)
                    {
                        IsWorking = true;
                        MTProtoService.UnblockAsync(user.ToInputUser(),
                            result => BeginOnUIThread(() =>
                            {
                                IsWorking = false;
                                user.Blocked = TLBool.False;
                                CacheService.Commit();
                                Handle(new TLUpdateUserBlocked{ UserId = user.Id, Blocked = TLBool.False });

                                if (IsBot)
                                {
                                    _text = "/start";
                                    Execute.BeginOnUIThread(() => SendInternal(false, false));
                                }
                            }),
                            error => BeginOnUIThread(() =>
                            {
                                IsWorking = false;
                                Execute.ShowDebugMessage("contacts.Unblock error " + error);
                            }));
                    }
                }

                return;
            }
        }

        private void StartBotInternal(TLMessageBase message, TLString accessToken)
        {
            var message31 = message as TLMessage31;
            if (message31 == null) return;

            var user = _bot as TLUser;
            if (user == null) return;

            MTProtoService.StartBotAsync(user.ToInputUser(), accessToken, message31,
                result =>
                {

                },
                error => Execute.BeginOnUIThread(() =>
                {
                    if (error.TypeEquals(ErrorType.PEER_FLOOD))
                    {
                        MessageBox.Show(AppResources.PeerFloodSendMessage, AppResources.Error, MessageBoxButton.OK);
                    }
                    else
                    {
                        Execute.ShowDebugMessage("messages.startBot error " + error);
                    }
                }));
        }

        public bool IsChatForbidden
        {
            get { return With is TLChatForbidden || (With is TLChat && ((TLChat)With).Left.Value); }           
        }

        public bool IsChatDeactivated
        {
            get { return With is TLChat40 && (((TLChat40)With).Deactivated); }
        }

        public bool IsChannelForbidden
        {
            get { return With is TLChannelForbidden; }
        }

        public bool IsBot
        {
            get
            {
                var bot = _bot as TLUser;
                if (bot != null && bot.IsBot)
                {
                    return true;
                }

                var user = With as TLUser;
                if (user != null && user.IsBot)
                {
                    return true;
                }

                return false;
            }
        }

        public bool IsBotStarting
        {
            get
            {
                var bot = _bot as TLUser;
                if (bot != null && bot.IsBot && !string.IsNullOrEmpty(bot.AccessToken))
                {
                    return true;
                }

                var user = With as TLUser;
                if (user != null && user.IsBot && Items.Count == 0 && LazyItems.Count == 0)
                {
                    return true;
                }

                return false;
            }
        }

        public bool IsUserBlocked
        {
            get
            {
                var user = With as TLUserBase;
                if (user != null && user.Blocked != null && user.Blocked.Value)
                {
                    return true;
                }

                return false;
            }
        }

        public bool IsBroadcast
        {
            get { return With is TLBroadcastChat; }
        }

        public void OpenPeerDetails()
        {
            if (With is TLChatBase)
            {
                if (IsChatForbidden)
                {
                    return;
                }

                StateService.CurrentChat = (TLChatBase)With;
                NavigationService.UriFor<ChatViewModel>().Navigate();
            }
            else
            {
                StateService.CurrentContact = (TLUserBase)With; 
                NavigationService.UriFor<ContactViewModel>().Navigate();
                //NavigationService.UriFor<ContactViewModel>().Navigate();
                //NavigationService.UriFor<ContactDetailsViewModel>().WithParam(x => x.PreviousUserId, ((TLUserBase)With).Index).Navigate();
            }
        }

        public void ForwardMessage(TLMessageBase message)
        {
            if (message == null) return;
            if (message.Index <= 0) return;

            ForwardMessagesCommon(new List<TLMessageBase>{ message }, StateService, NavigationService);
        }

        public void ForwardMessages(List<TLMessageBase> selectedItems)
        {
            if (selectedItems.Count == 0) return;

            ForwardMessagesCommon(selectedItems, StateService, NavigationService);
        }

        public static void ForwardMessagesCommon(List<TLMessageBase> messages, IStateService stateService, INavigationService navigationService)
        {
            stateService.ForwardMessages = messages;
            stateService.ForwardMessages.Reverse();

            Execute.BeginOnUIThread(() => navigationService.UriFor<ChooseDialogViewModel>().Navigate());
        }

        public void CopyMessage(TLMessage message)
        {
            if (message == null) return;

            Clipboard.SetText(message.Message.ToString());
        }

        public void DeleteMessage(TLMessageBase message)
        {
            if (message == null) return;

            var messages = new List<TLMessageBase> { message };

            if (With is TLChannel)
            {
                var messageCommon = message as TLMessageCommon;
                if (messageCommon != null)
                {
                    if (messageCommon.ToId is TLPeerChat)
                    {
                        DeleteMessages(MTProtoService, null, null, messages, null, DeleteMessagesInternal);
                        return;
                    }
                }

                DeleteChannelMessages(MTProtoService, (TLChannel)With, null, null, messages, null, DeleteMessagesInternal);
                return;
            }

            if (With is TLBroadcastChat)
            {
                DeleteMessagesInternal(null, messages);
                return;
            }

            if (message.Index == 0 && message.RandomIndex != 0)
            {
                DeleteMessagesInternal(null, messages);
                return;
            }

            DeleteMessages(MTProtoService, null, null, messages, null, DeleteMessagesInternal);
        }

        public void DeleteUploadingMessage(TLMessageBase messageBase)
        {
            var message = messageBase as TLMessage;
            if (message == null) return;

            var media = message.Media;
            if (media == null || media.UploadingProgress == 1.0) return;

            message.Status = MessageStatus.Failed;
            Items.Remove(message);

            MergeGroupMessages(new List<TLMessageBase>{message});

            IsEmptyDialog = Items.Count == 0 && LazyItems.Count == 0;
            NotifyOfPropertyChange(() => With);

            BeginOnThreadPool(() =>
            {
                CacheService.DeleteMessages(new TLVector<TLLong> { message.RandomId });
                CancelUplaoding(message);
            });
        }

        private void DeleteMessagesInternal(TLMessageBase lastMessage, IList<TLMessageBase> messages)
        {
            var channel = With as TLChannel;
            TLPeerBase toPeer = null;
            var localIds = new TLVector<TLLong>();
            var remoteIds = new TLVector<TLInt>();
            var remoteChatIds = new TLVector<TLInt>();
            for (int i = 0; i < messages.Count; i++)
            {
                if (messages[i].RandomIndex != 0)
                {
                    localIds.Add(messages[i].RandomId);
                }

                if (messages[i].Index > 0)
                {
                    var messageCommon = messages[i] as TLMessageCommon;
                    if (channel != null && messageCommon != null && messageCommon.ToId is TLPeerChat)
                    {
                        remoteChatIds.Add(messageCommon.Id);
                        toPeer = messageCommon.ToId;
                    }
                    else
                    {
                        remoteIds.Add(messages[i].Id);
                    }
                }
            }

            if (toPeer != null) CacheService.DeleteMessages(toPeer, lastMessage, remoteChatIds);
            CacheService.DeleteMessages(TLUtils.InputPeerToPeer(Peer, StateService.CurrentUserId), lastMessage, remoteIds);
            CacheService.DeleteMessages(localIds);

            BeginOnUIThread(() =>
            {
                for (var i = 0; i < messages.Count; i++)
                {
                    if (messages[i].Status == MessageStatus.Sending)
                    {
                        messages[i].Status = MessageStatus.Failed;
                        CancelUplaoding(messages[i]);
                    }
                    Items.Remove(messages[i]);
                }

                MergeGroupMessages(messages);

                IsEmptyDialog = Items.Count == 0 && LazyItems.Count == 0;
                NotifyOfPropertyChange(() => With);
            });
        }

        private void CancelUplaoding(TLMessageBase messageBase)
        {
            var message = messageBase as TLMessage;
            if (message == null) return;

            var media = message.Media;

            var mediaPhoto = media as TLMessageMediaPhoto;
            if (mediaPhoto != null && mediaPhoto.FileId != null)
            {
                UploadFileManager.CancelUploadFile(media.FileId);
            }

            var mediaVideo = media as TLMessageMediaVideo;
            if (mediaVideo != null && mediaVideo.FileId != null)
            {
                UploadVideoFileManager.CancelUploadFile(media.FileId);
            }

            var mediaDocument = media as TLMessageMediaDocument;
            if (mediaDocument != null && mediaDocument.FileId != null)
            {
                UploadDocumentFileManager.CancelUploadFile(media.FileId);
            }
        }

        private void DeleteFromItems(IList<TLMessageBase> messages)
        {
            for (var i = 0; i < messages.Count; i++)
            {
                Items.Remove(messages[i]);
            }

            MergeGroupMessages(messages);

            IsEmptyDialog = Items.Count == 0 && LazyItems.Count == 0;
            NotifyOfPropertyChange(() => With);
        }

        public void DeleteMessages()
        {
            TLMessageBase lastItem = null;
            var randomItems = new List<TLMessageBase>();
            var items = new List<TLMessageBase>();
            for (var i = 0; i < Items.Count; i++)
            {
                var message = Items[i];
                if (message.IsSelected)
                {
                    if (message.Index == 0 && message.RandomIndex != 0)
                    {
                        randomItems.Add(message);
                        lastItem = null;
                    }
                    else if (message.Index != 0)
                    {
                        items.Add(message);
                        lastItem = null;
                    }
                }
                else
                {
                    if (lastItem == null)
                    {
                        lastItem = message;
                    }   
                }
            }

            if (randomItems.Count > 0 || items.Count > 0)
            {
                IsSelectionEnabled = false;
            }

            var channel = With as TLChannel;
            if (channel != null)
            {
                var chatMessages = new List<TLMessageBase>();
                var channelMessages = new List<TLMessageBase>();
                if (channel.MigratedFromChatId != null)
                {
                    foreach (var item in items)
                    {
                        var message = item as TLMessageCommon;
                        if (message != null && message.ToId is TLPeerChat)
                        {
                            chatMessages.Add(message);
                        }
                        else
                        {
                            channelMessages.Add(message);
                        }
                    }
                }

                if (chatMessages.Count > 0)
                {
                    DeleteChannelMessages(MTProtoService, (TLChannel)With, lastItem, null, channelMessages, null, DeleteMessagesInternal);
                    DeleteMessages(MTProtoService, lastItem, null, chatMessages, null, DeleteMessagesInternal);

                    return;
                }
                //var messageCommon = message as TLMessageCommon;
                //if (messageCommon != null)
                //{
                //    if (messageCommon.ToId is TLPeerChat)
                //    {
                //        DeleteMessages(MTProtoService, null, null, messages, null, DeleteMessagesInternal);
                //        return;
                //    }
                //}



                DeleteChannelMessages(MTProtoService, (TLChannel)With, lastItem, randomItems, items, DeleteMessagesInternal, DeleteMessagesInternal);

                return;
            }

            if (With is TLBroadcastChat)
            {
                DeleteMessagesInternal(lastItem, randomItems);
                DeleteMessagesInternal(lastItem, items);
                return;
            }

            DeleteMessages(MTProtoService, lastItem, randomItems, items, DeleteMessagesInternal, DeleteMessagesInternal);
        }

        public static void DeleteMessages(IMTProtoService mtProtoService, TLMessageBase lastItem, IList<TLMessageBase> localMessages, IList<TLMessageBase> remoteMessages, Action<TLMessageBase, IList<TLMessageBase>> localCallback = null, Action<TLMessageBase, IList<TLMessageBase>> remoteCallback = null)
        {
            if (localMessages != null && localMessages.Count > 0)
            {
                localCallback.SafeInvoke(lastItem, localMessages);
            }

            if (remoteMessages != null && remoteMessages.Count > 0)
            {
                mtProtoService.DeleteMessagesAsync(new TLVector<TLInt> { Items = remoteMessages.Select(x => x.Id).ToList() },
                    deletedIds =>
                    {
                        remoteCallback.SafeInvoke(lastItem, remoteMessages);
                    },
                    error =>
                    {
                        Execute.ShowDebugMessage("messages.deleteMessages error " + error);
                    });
            }
        }

        public static void DeleteChannelMessages(IMTProtoService mtProtoService, TLChannel channel, TLMessageBase lastItem, IList<TLMessageBase> localMessages, IList<TLMessageBase> remoteMessages, Action<TLMessageBase, IList<TLMessageBase>> localCallback = null, Action<TLMessageBase, IList<TLMessageBase>> remoteCallback = null)
        {
            if (localMessages != null && localMessages.Count > 0)
            {
                localCallback.SafeInvoke(lastItem, localMessages);
            }

            if (remoteMessages != null && remoteMessages.Count > 0)
            {
                mtProtoService.DeleteMessagesAsync(channel.ToInputChannel(), new TLVector<TLInt> { Items = remoteMessages.Select(x => x.Id).ToList() },
                    deletedIds =>
                    {
                        remoteCallback.SafeInvoke(lastItem, remoteMessages);
                    },
                    error =>
                    {
                        Execute.ShowDebugMessage("channels.deleteMessages error " + error);
                    });
            }
        }

        public void DeleteMessageById(TLMessageBase message, System.Action callback)
        {
            if (message == null) return;

            if ((message.Id == null || message.Id.Value == 0)
                && message.RandomIndex != 0)
            {
                CacheService.DeleteMessages(new TLVector<TLLong> { message.RandomId });
                callback.SafeInvoke();
                BeginOnUIThread(() =>
                {
                    for (var i = 0; i < Items.Count; i++)
                    {
                        if (Items[i].RandomIndex == message.RandomIndex)
                        {
                            Items.RemoveAt(i);
                            break;
                        }
                    }
                });
                return;
            }

            MTProtoService.DeleteMessagesAsync(new TLVector<TLInt> { message.Id },
                deletedIds =>
                {
                    // duplicate: deleting performed through updates
                    CacheService.DeleteMessages(new TLVector<TLInt> { message.Id });
                    callback.SafeInvoke();
                    BeginOnUIThread(() =>
                    {
                        for (var i = 0; i < Items.Count; i++)
                        {
                            if (Items[i].Index == message.Index)
                            {
                                Items.RemoveAt(i);
                                break;
                            }
                        }
                    });
                    _isEmptyDialog = Items.Count == 0 && LazyItems.Count == 0;
                },
                error => 
                {
                    Execute.ShowDebugMessage("messages.deleteMessages error " + error);
                });
        }

        public void DeleteFile(TLMessageBase messageBase)
        {
            var message = messageBase as TLMessage;
            if (message != null)
            {
                var mediaVideo = message.Media as TLMessageMediaVideo;
                if (mediaVideo != null)
                {
                    var video = mediaVideo.Video as TLVideo;
                    if (video != null)
                    {
                        var fileName = video.GetFileName();

                        using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                        {
                            if (store.FileExists(fileName))
                            {
                                store.DeleteFile(fileName);
                            }
                        }
                    }
                }

            }
        }

#if WP81
        public async void Resend(TLMessage25 message)
#else
        public void Resend(TLMessage25 message)
#endif
        {
            if (message == null) return;

            if (message.Index != 0)
            {
                var messageInfo = string.Format("Resend delivered message Id={0} RandomId={1} Status={2} Date={3}", message.Index, message.RandomIndex, message.Status, message.Date);
                Execute.ShowDebugMessage(messageInfo);

                message.Status = MessageStatus.Confirmed;
                CacheService.SyncSendingMessage(message, null, message.ToId, result => { });

                return;
            }

            if (message.RandomIndex == 0)
            {
                var messageInfo = string.Format("Resend with missing randomIndex message Id={0} RandomId={1} Status={2} Date={3}", message.Index, message.RandomIndex, message.Status, message.Date);
                Execute.ShowDebugMessage(messageInfo);

                message.RandomId = TLLong.Random();
            }

            message.Status = MessageStatus.Sending;

            if (message.Media is TLMessageMediaEmpty)
            {
                SendInternal(message, MTProtoService, null, () => Status = string.Empty);
            }
            else
            {
                if (message.Media is TLMessageMediaPhoto)
                {
                    if (message.InputMedia != null)
                    {
                        ShellViewModel.SendMediaInternal(message, MTProtoService, StateService, CacheService);
                    }
                    else
                    {
                        SendPhotoInternal(message);
                    }
                }
                else if (message.Media is TLMessageMediaAudio)
                {
#if WP8
                    if (message.InputMedia != null)
                    {
                        ShellViewModel.SendMediaInternal(message, MTProtoService, StateService, CacheService);
                    }
                    else
                    {
                        SendAudioInternal(message);
                    }
#endif
                }
                else if (message.Media is TLMessageMediaVideo)
                {
#if WP81
                    var file = await GetStorageFile(message.Media);

                    if (file != null)
                    {
                        SendCompressedVideoInternal(message, file);
                    }
                    else
                    {
                        MessageBox.Show(AppResources.UnableToAccessDocument, AppResources.Error, MessageBoxButton.OK);
                        DeleteMessage(message);
                    }
#else
                    SendVideoInternal(message, null);
#endif
                }
                else if (message.Media is TLMessageMediaDocument)
                {
#if WP81
                    var file = await GetStorageFile(message.Media);

                    if (file != null)
                    {
                        SendDocumentInternal(message, file);
                    }
                    else
                    {
                        MessageBox.Show(AppResources.UnableToAccessDocument, AppResources.Error, MessageBoxButton.OK);
                        DeleteMessage(message);
                    }
#else
                    SendDocumentInternal(message, null);
#endif
                }
                else if (message.Media is TLMessageMediaVenue)
                {
                    if (message.InputMedia != null)
                    {
                        ShellViewModel.SendMediaInternal(message, MTProtoService, StateService, CacheService);
                    }
                    else
                    {
                        SendVenueInternal(message);
                    }
                }
                else if (message.Media is TLMessageMediaGeo)
                {
                    if (message.InputMedia != null)
                    {
                        ShellViewModel.SendMediaInternal(message, MTProtoService, StateService, CacheService);
                    }
                    else
                    {
                        SendGeoPointInternal(message);
                    }
                }
                else if (message.Media is TLMessageMediaContact)
                {
                    if (message.InputMedia != null)
                    {
                        ShellViewModel.SendMediaInternal(message, MTProtoService, StateService, CacheService);
                    }
                    else
                    {
                        SendContactInternal(message);
                    }
                }
            }
        }

#if WP81
        public static async Task<StorageFile> GetStorageFile(TLMessageMediaBase media)
        {
            if (media == null) return null;
            if (media.File != null)
            {
                if (File.Exists(media.File.Path))
                {
                    return media.File;
                }
            }

            var file = await GetFileFromFolder(media.IsoFileName);           

            if (file == null)
            {
                file = await GetFileFromLocalFolder(media.IsoFileName);
            }

            if (file == null)
            {
                var mediaPhoto = media as TLMessageMediaPhoto;
                if (mediaPhoto != null)
                {
                    var photo = mediaPhoto.Photo as TLPhoto;
                    if (photo != null)
                    {
                        file = await GetFileFromLocalFolder(photo.GetFileName());
                    }
                }
            }

            if (file == null)
            {
                var mediaDocument = media as TLMessageMediaDocument;
                if (mediaDocument != null)
                {
                    var document = mediaDocument.Document as TLDocument;
                    if (document != null)
                    {
                        file = await GetFileFromLocalFolder(document.GetFileName());
                    }
                }
            }

            if (file == null)
            {
                var mediaVideo = media as TLMessageMediaVideo;
                if (mediaVideo != null)
                {
                    var video = mediaVideo.Video as TLVideo;
                    if (video != null)
                    {
                        file = await GetFileFromLocalFolder(video.GetFileName());
                    }
                }
            }

            return file;
        }

        public static async Task<StorageFile> GetFileFromLocalFolder(string fileName)
        {
            StorageFile file = null;
            try
            {
                var store = IsolatedStorageFile.GetUserStoreForApplication();
                if (!string.IsNullOrEmpty(fileName)
                    && store.FileExists(fileName))
                {
                    file = await ApplicationData.Current.LocalFolder.GetFileAsync(fileName);
                }
            }
            catch (Exception ex)
            {
                Execute.ShowDebugMessage(string.Format("LocalFolder.GetFileAsync({0}) exception ", fileName) + ex);
            }

            return file;
        }

        public static async Task<StorageFile> GetFileFromFolder(string fileName)
        {
            StorageFile file = null;
            try
            {
                if (!string.IsNullOrEmpty(fileName)
                    && File.Exists(fileName))
                {
                    file = await StorageFile.GetFileFromPathAsync(fileName);
                }
            }
            catch (Exception ex)
            {
                Execute.ShowDebugMessage(string.Format("GetFileFromPathAsync({0}) exception ", fileName) + ex);
            }

            return file;
        }
#endif
        public bool CanSend
        {
            get
            {
                var text = GetTrimmedText(Text);

                return !string.IsNullOrEmpty(text) || Reply is TLMessagesContainter;
            }
        }

        public static string GetTrimmedText(string input)
        {
            return input != null ? input.Trim().Replace("\r", "\n").Replace("--", "—") : null;
        }

        public void Send(TLKeyboardButton keyboardButton)
        {
            if (keyboardButton == null) return;

            _text = keyboardButton.Text.ToString();
            var message31 = _replyMarkupMessage;
            if (message31 != null && message31.ReplyMarkup != null)
            {
                message31.ReplyMarkup.HasResponse = true;
            }
            Execute.BeginOnUIThread(() => SendInternal(true, true));
        }

        public void Send(TLString command)
        {
            if (TLString.IsNullOrEmpty(command)) return;

            _text = command.ToString();
            Execute.BeginOnUIThread(() => SendInternal(false, true));
        }

        private void SendInternal(bool useReplyMarkup, bool scrollToBottom)
        {
            _debugNotifyOfPropertyChanged = true;
            var timer = Stopwatch.StartNew();
            var elapsed = new List<TimeSpan>();

            if (!CanSend) return;

            var text = GetTrimmedText(Text) ?? string.Empty;

            if (ProcessSpecialCommands(text)) return;

            //check maximum message length
            if (text.Length > Constants.MaximumMessageLength)
            {
                MessageBox.Show(
                    String.Format(AppResources.MaximumMessageLengthExceeded, Constants.MaximumMessageLength),
                    AppResources.Error, MessageBoxButton.OK);

                return;
            }

            // 0
            elapsed.Add(timer.Elapsed);

            CacheService.CheckDisabledFeature(With, Constants.FeaturePMMessage, Constants.FeatureChatMessage, Constants.FeatureBigChatMessage,
                () =>
                {
                    // 1
                    elapsed.Add(timer.Elapsed);
                    if (string.IsNullOrEmpty(text))
                    {
                        var messagesContainer = Reply as TLMessagesContainter;
                        if (messagesContainer != null)
                        {
                            var fwdMessages25 = messagesContainer.FwdMessages;
                            var fwdMessages = new TLVector<TLMessage>();
                            for (var i = 0; i < fwdMessages25.Count; i++)
                            {
                                fwdMessages.Add(fwdMessages25[i]);
                            }
                            var fwdIds = messagesContainer.FwdIds;

                            if (fwdMessages25.Count > 0 && fwdIds.Count > 0)
                            {
                                SendMessages(fwdMessages, m => SendForwardMessagesInternal(MTProtoService, Peer, fwdIds, fwdMessages25));
                            }
                        }
                    }
                    else
                    {
                        // 2
                        elapsed.Add(timer.Elapsed); 
                        var message = GetMessage(new TLString(text), new TLMessageMediaEmpty());

                        if (Reply != null && IsWebPagePreview(Reply))
                        {
                            message._media = ((TLMessagesContainter)Reply).WebPageMedia;
                            Reply = _previousReply;
                        }
                        else
                        {
                            TLMessageMediaBase media;
                            if (_webPagesCache.TryGetValue(text, out media))
                            {
                                var webPageMessageMedia = media as TLMessageMediaWebPage;
                                if (webPageMessageMedia != null)
                                {
                                    var webPage = webPageMessageMedia.WebPage;
                                    if (webPage != null)
                                    {
                                        message.DisableWebPagePreview = true;
                                    }
                                }
                            }
                        }

                        // 3
                        elapsed.Add(timer.Elapsed);
                        Text = string.Empty;

                        // 4
                        elapsed.Add(timer.Elapsed);
                        var previousMessage = InsertSendingMessage(message, useReplyMarkup);

                        // 5
                        elapsed.Add(timer.Elapsed);
                        IsEmptyDialog = Items.Count == 0 && LazyItems.Count == 0;

                        // 6
                        elapsed.Add(timer.Elapsed);
                        var user = With as TLUser;
                        if (user != null && user.IsBot && Items.Count == 1)
                        {
                            NotifyOfPropertyChange(() => With);
                        }

                        // 7
                        elapsed.Add(timer.Elapsed);
                        if (scrollToBottom)
                        {
                            BeginOnUIThread(ProcessScroll);
                        }

                        // 8
                        elapsed.Add(timer.Elapsed);

                        Execute.BeginOnUIThread(() =>
                        {
                            return;

                            var sb = new StringBuilder();
                            for (var i = 0; i < elapsed.Count; i++)
                            {
                                sb.AppendLine(i + " " + elapsed[i]);
                            }

                            MessageBox.Show(sb.ToString());
                        });

                        _debugNotifyOfPropertyChanged = false;
                        BeginOnThreadPool(() =>
                            CacheService.SyncSendingMessage(
                                message, previousMessage,
                                TLUtils.InputPeerToPeer(Peer, StateService.CurrentUserId),
                                m => SendInternal(message, MTProtoService, null, () => Status = string.Empty)));
                    }

                },
                disabledFeature => Execute.BeginOnUIThread(() => MessageBox.Show(disabledFeature.Description.ToString(), AppResources.AppName, MessageBoxButton.OK)));
        }

        private void SendMessages(IList<TLMessage> messages, Action<IList<TLMessage>> callback)
        {
            var previousMessage = Items.FirstOrDefault();
            foreach (var message in messages)
            {
                CheckChannelMessage(message as TLMessage25);
                Items.Insert(0, message);
            }
            IsEmptyDialog = Items.Count == 0 && LazyItems.Count == 0;
            Reply = null;

            BeginOnThreadPool(() =>
                CacheService.SyncSendingMessages(
                    messages, previousMessage,
                    TLUtils.InputPeerToPeer(Peer, StateService.CurrentUserId),
                    callback.SafeInvoke));
        }

        private bool _debugNotifyOfPropertyChanged;

        public override void NotifyOfPropertyChange(string propertyName)
        {
            if (_debugNotifyOfPropertyChanged)
            {
                //Deployment.Current.Dispatcher.BeginInvoke(() => MessageBox.Show(propertyName));
            }

            base.NotifyOfPropertyChange(propertyName);
        }

        public void Send()
        {
            SendInternal(false, false);
        }

        private TLMessageBase InsertSendingMessage(TLMessage25 message, bool useReplyMarkup = false)
        {
            CheckChannelMessage(message);

            TLMessageBase previousMessage;
#if WP8
            if (_isFirstSliceLoaded)
            {
                if (useReplyMarkup
                    && _replyMarkupMessage != null)
                {
                    var chatBase = With as TLChatBase;
                    if (chatBase != null)
                    {
                        message.ReplyToMsgId = _replyMarkupMessage.Id;
                        message.Reply = _replyMarkupMessage;
                    }

                    BeginOnUIThread(() =>
                    {
                        if (Reply != null)
                        {
                            Reply = null;
                            SetReplyMarkup(null);
                        }
                    });
                }

                var messagesContainer = Reply as TLMessagesContainter;
                if (Reply != null)
                {
                    if (Reply.Index != 0)
                    {
                        message.ReplyToMsgId = Reply.Id;
                        message.Reply = Reply;
                    }
                    else
                    {
                        if (messagesContainer != null)
                        {
                            if (!string.IsNullOrEmpty(message.Message.ToString()))
                            {
                                message.Reply = Reply;
                            }
                        }
                    }

                    var message31 = Reply as TLMessage31;
                    if (message31 != null)
                    {
                        var replyMarkup = message31.ReplyMarkup;
                        if (replyMarkup != null)
                        {
                            replyMarkup.HasResponse = true;
                        }
                    }

                    BeginOnUIThread(() =>
                    {
                        var emptyMedia = message.Media as TLMessageMediaEmpty;
                        if (emptyMedia != null)
                        {
                            Reply = null;
                        }
                    });
                }

                previousMessage = Items.FirstOrDefault();
                Items.Insert(0, message);

                if (messagesContainer != null)
                {
                    if (!string.IsNullOrEmpty(message.Message.ToString()))
                    {
                        foreach (var fwdMessage in messagesContainer.FwdMessages)
                        {
                            CheckChannelMessage(fwdMessage as TLMessage25);
                            Items.Insert(0, fwdMessage);
                        }
                    }
                }

                for (var i = 1; i < Items.Count; i++)
                {
                    var serviceMessage = Items[i] as TLMessageService;
                    if (serviceMessage != null)
                    {
                        var unreadMessagesAction = serviceMessage.Action as TLMessageActionUnreadMessages;
                        if (unreadMessagesAction != null)
                        {
                            Items.RemoveAt(i);
                            break;
                        }
                    }
                }

                Execute.BeginOnUIThread(RaiseScrollToBottom);
            }
            else
            {

                var messagesContainer = Reply as TLMessagesContainter;
                if (Reply != null)
                {
                    if (Reply.Index != 0)
                    {
                        message.ReplyToMsgId = Reply.Id;
                        message.Reply = Reply;
                    }
                    else
                    {
                        if (messagesContainer != null)
                        {
                            if (!string.IsNullOrEmpty(message.Message.ToString()))
                            {
                                message.Reply = Reply;
                            }
                        }
                    }

                    Reply = null;
                }

                Items.Clear();
                Items.Add(message);
                var messages = CacheService.GetHistory(new TLInt(StateService.CurrentUserId), TLUtils.InputPeerToPeer(Peer, StateService.CurrentUserId));
                previousMessage = messages.FirstOrDefault();
                for (var i = 0; i < messages.Count; i++)
                {
                    Items.Add(messages[i]);
                }

                if (messagesContainer != null)
                {
                    if (!string.IsNullOrEmpty(message.Message.ToString()))
                    {
                        foreach (var fwdMessage in messagesContainer.FwdMessages)
                        {
                            Items.Insert(0, fwdMessage);
                        }
                    }
                }

                for (var i = 1; i < Items.Count; i++)
                {
                    var serviceMessage = Items[i] as TLMessageService;
                    if (serviceMessage != null)
                    {
                        var unreadMessagesAction = serviceMessage.Action as TLMessageActionUnreadMessages;
                        if (unreadMessagesAction != null)
                        {
                            Items.RemoveAt(i);
                            break;
                        }
                    }
                }
            }
#else
            var messagesContainer = Reply as TLMessagesContainter;
            if (Reply != null)
            {
                if (Reply.Index != 0)
                {
                    message.ReplyToMsgId = Reply.Id;
                    message.Reply = Reply;
                }
                else
                {
                    if (messagesContainer != null)
                    {
                        if (!string.IsNullOrEmpty(message.Message.ToString()))
                        {
                            message.Reply = Reply;
                        }
                    }
                }

                Reply = null;
            }

            previousMessage = Items.FirstOrDefault();
            Items.Insert(0, message);

            if (messagesContainer != null)
            {
                if (!string.IsNullOrEmpty(message.Message.ToString()))
                {
                    foreach (var fwdMessage in messagesContainer.FwdMessages)
                    {
                        Items.Insert(0, fwdMessage);
                    }
                }
            }

            for (var i = 1; i < Items.Count; i++)
            {
                var serviceMessage = Items[i] as TLMessageService;
                if (serviceMessage != null)
                {
                    var unreadMessagesAction = serviceMessage.Action as TLMessageActionUnreadMessages;
                    if (unreadMessagesAction != null)
                    {
                        Items.RemoveAt(i);
                        break;
                    }
                }
            }

            Execute.BeginOnUIThread(RaiseScrollToBottom);
#endif
            return previousMessage;
        }

        private bool ProcessSpecialCommands(string text)
        {
            if (string.Equals(text, "/tlg_msgs_err", StringComparison.OrdinalIgnoreCase))
            {
                ShowLastSyncErrors(info =>
                {
                    try
                    {
                        Clipboard.SetText(info);
                    }
                    catch (Exception ex)
                    {

                    }
                });
                Text = string.Empty;
                return true;
            }

            if (text != null
                && text.StartsWith("/tlg_msgs", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var parameters = text.Split(' ');
                    var limit = 15;
                    if (parameters.Length > 1)
                    {
                        limit = Convert.ToInt32(parameters[1]);
                    }

                    ShowMessagesInfo(limit, info =>
                    {
                        try
                        {
                            Clipboard.SetText(info);
                        }
                        catch (Exception ex)
                        {

                        }
                    });
                    Text = string.Empty;
                }
                catch (Exception ex)
                {
                    Execute.BeginOnUIThread(() => MessageBox.Show("Unknown command"));
                }
                return true;
            }

            if (string.Equals(text, "/tlg_cfg", StringComparison.OrdinalIgnoreCase))
            {
                ShowConfigInfo(info =>
                {
                    Execute.BeginOnUIThread(() =>
                    {
                        try
                        {

                            MessageBox.Show(info);
                            Clipboard.SetText(info);
                        }
                        catch (Exception ex)
                        {
                        }

                    });
                });
                Text = string.Empty;
                return true;
            }

            if (string.Equals(text, "/tlg_tr", StringComparison.OrdinalIgnoreCase))
            {
                ShowTransportInfo(info =>
                {
                    Execute.BeginOnUIThread(() =>
                    {
                        try
                        {

                            MessageBox.Show(info);
                            Clipboard.SetText(info);
                        }
                        catch (Exception ex)
                        {
                        }
                    });
                });

                Text = string.Empty;
                return true;
            }

            if (text != null
                && text.StartsWith("/tlg_del_c", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var parameters = text.Split(' ');
                    var id = -1;
                    if (parameters.Length > 1)
                    {
                        id = Convert.ToInt32(parameters[1]);
                    }

                    var chat = CacheService.GetChat(new TLInt(id));
                    if (chat != null)
                    {
                        CacheService.DeleteChat(new TLInt(id));
                        CacheService.Commit();
                    }
                    Execute.BeginOnUIThread(() => MessageBox.Show("Complete"));
                    Text = string.Empty;
                }
                catch (Exception ex)
                {
                    Execute.BeginOnUIThread(() => MessageBox.Show("Unknown command"));
                }

                return true;
            }

            if (text != null
                && text.StartsWith("/tlg_del_u", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var parameters = text.Split(' ');
                    var id = -1;
                    if (parameters.Length > 1)
                    {
                        id = Convert.ToInt32(parameters[1]);
                    }

                    var user = CacheService.GetUser(new TLInt(id));
                    if (user != null)
                    {
                        CacheService.DeleteUser(new TLInt(id));
                        CacheService.Commit();
                    }
                    Execute.BeginOnUIThread(() => MessageBox.Show("Complete"));
                    Text = string.Empty;
                }
                catch (Exception ex)
                {
                    Execute.BeginOnUIThread(() => MessageBox.Show("Unknown command"));
                }
                return true;
            }

            if (text != null
                && text.StartsWith("/tlg_up_tr", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var parameters = text.Split(' ');
                    var dcId = Convert.ToInt32(parameters[1]);
                    var dcIpAddress = parameters[2];
                    var dcPort = Convert.ToInt32(parameters[3]);

                    MTProtoService.UpdateTransportInfoAsync(dcId, dcIpAddress, dcPort,
                        result =>
                        {
                            Execute.BeginOnUIThread(() => MessageBox.Show("Complete /tlg_up_tr"));
                        });

                    Text = string.Empty;

                    //ShowTransportInfo(info =>
                    //{
                    //    Execute.BeginOnUIThread(() =>
                    //    {
                    //        try
                    //        {

                    //            MessageBox.Show(info);
                    //            Clipboard.SetText(info);
                    //        }
                    //        catch (Exception ex)
                    //        {
                    //        }

                    //        Text = string.Empty;
                    //    });
                    //});
                }
                catch (Exception ex)
                {
                    Execute.BeginOnUIThread(() => MessageBox.Show("Unknown command"));
                }
                return true;
            }

            return false;
        }

        public event EventHandler<ScrollToEventArgs> ScrollTo;

        protected virtual void RaiseScrollTo(ScrollToEventArgs args)
        {
            var handler = ScrollTo;
            if (handler != null) handler(this, args);
        }

        public event EventHandler ScrollToBottom;

        protected virtual void RaiseScrollToBottom()
        {
            var handler = ScrollToBottom;
            if (handler != null) handler(this, System.EventArgs.Empty);
        }

        private Visibility _scrollToBottomVisibility = Visibility.Collapsed;

        public Visibility ScrollToBottomVisibility
        {
            get { return _scrollToBottomVisibility; }
            set { SetField(ref _scrollToBottomVisibility, value, () => ScrollToBottomVisibility); }
        }

        public static TLInputPeerBase PeerToInputPeer(TLPeerBase peer)
        {
            if (peer is TLPeerUser)
            {
                var cachedUser = IoC.Get<ICacheService>().GetUser(peer.Id);
                if (cachedUser != null)
                {
                    var userForeign = cachedUser as TLUserForeign;
                    var userRequest = cachedUser as TLUserRequest;
                    var user = cachedUser as TLUser;

                    if (userForeign != null)
                    {
                        return new TLInputPeerForeign { UserId = userForeign.Id, AccessHash = userForeign.AccessHash };
                    }
                    
                    if (userRequest != null)
                    {
                        return new TLInputPeerForeign { UserId = userRequest.Id, AccessHash = userRequest.AccessHash };
                    }

                    if (user != null)
                    {
                        return user.ToInputPeer();
                    }
                    
                    return new TLInputPeerContact { UserId = peer.Id };
                }
                
                return new TLInputPeerContact { UserId = peer.Id };
            }

            if (peer is TLPeerChannel)
            {
                var channel = IoC.Get<ICacheService>().GetChat(peer.Id) as TLChannel;
                if (channel != null)
                {
                    return new TLInputPeerChannel { ChatId = peer.Id, AccessHash = channel.AccessHash };
                }
            }

            if (peer is TLPeerChat)
            {
                return new TLInputPeerChat { ChatId = peer.Id };
            }
            
            return new TLInputPeerBroadcast { ChatId = peer.Id };
        }

        public static void SendInternal(TLMessage25 message, IMTProtoService mtProtoService, System.Action callback = null, System.Action faultCallback = null)
        {
            var cacheService = IoC.Get<ICacheService>();

            var inputPeer = PeerToInputPeer(message.ToId);

            if (inputPeer is TLInputPeerBroadcast && !(inputPeer is TLInputPeerChannel))
            {
                var broadcast = cacheService.GetBroadcast(message.ToId.Id);
                var contacts = new TLVector<TLInputUserBase>();

                foreach (var participantId in broadcast.ParticipantIds)
                {
                    var contact = IoC.Get<ICacheService>().GetUser(participantId);
                    contacts.Add(contact.ToInputUser());
                }

                mtProtoService.SendBroadcastAsync(contacts, new TLInputMediaEmpty(), message,
                    result =>
                    {
                        message.Status = MessageStatus.Confirmed;
                        callback.SafeInvoke();
                    },
                    () => 
                    {
                        message.Status = MessageStatus.Confirmed;
                    },
                    error => 
                    {
                        Execute.ShowDebugMessage("messages.sendBroadcast error: " + error);

                        if (message.Status == MessageStatus.Broadcast)
                        {
                            message.Status = message.Index != 0 ? MessageStatus.Confirmed : MessageStatus.Failed;
                        }
                        
                        faultCallback.SafeInvoke();
                    });
            }
            else
            {
                mtProtoService.SendMessageAsync(
                    (TLMessage36)message,
                    result =>
                    {
                        callback.SafeInvoke();
                    },
                    () => 
                    {
                        message.Status = MessageStatus.Confirmed;
                    },
                    error => Execute.BeginOnUIThread(() =>
                    {
                        if (error.TypeEquals(ErrorType.PEER_FLOOD))
                        {
                            MessageBox.Show(AppResources.PeerFloodSendMessage, AppResources.Error, MessageBoxButton.OK);
                        }
                        else if (error.CodeEquals(ErrorCode.BAD_REQUEST))
                        {
                            MessageBox.Show("messages.sendMessage error: " + error, AppResources.Error, MessageBoxButton.OK);
                        }
                        else
                        {
                            Execute.ShowDebugMessage("messages.sendMessage error: " + error);
                        }

                        if (message.Status == MessageStatus.Sending)
                        {
                            message.Status = message.Index != 0 ? MessageStatus.Confirmed : MessageStatus.Failed;
                        }

                        faultCallback.SafeInvoke();
                    }));

                SendForwardedMessages(mtProtoService, inputPeer, message);
            }
        }

        public void OpenFwdContactDetails(TLObject obj)
        {
            var messageForwarded = obj as TLMessageForwarded;
            if (messageForwarded != null)
            {
                if (messageForwarded.FwdFrom == null) return;

                StateService.CurrentContact = messageForwarded.FwdFrom;
                NavigationService.UriFor<ContactViewModel>().Navigate();
            }

            var message25 = obj as TLMessage25;
            if (message25 != null)
            {
                if (message25.FwdFrom == null) return;

                var fwdFromUser = message25.FwdFrom as TLUserBase;
                if (fwdFromUser != null)
                {
                    StateService.CurrentContact = fwdFromUser;
                    NavigationService.UriFor<ContactViewModel>().Navigate();
                    return;
                }

                var fwdFromChannel = message25.FwdFrom as TLChannel;
                if (fwdFromChannel != null)
                {
                    StateService.With = fwdFromChannel;
                    StateService.RemoveBackEntries = true;
                    NavigationService.Navigate(new Uri("/Views/Dialogs/DialogDetailsView.xaml?rndParam=" + TLInt.Random(), UriKind.Relative));
                    return;
                }

                var fwdFromChat = message25.FwdFrom as TLChatBase;
                if (fwdFromChat != null)
                {
                    StateService.CurrentChat = fwdFromChat;
                    NavigationService.UriFor<ChatViewModel>().Navigate();
                    return;
                }
            }
        }

        public void ShowUserProfile(TLMessage message)
        {
            if (message == null) return;

            StateService.CurrentContact = message.From as TLUserBase;
            NavigationService.UriFor<ContactViewModel>().Navigate();
        }

        public void CancelUploading(TLMessageMediaBase media)
        {
            TLMessage message = null;
            for (var i = 0; i < Items.Count; i++)
            {
                var messageCommon = Items[i] as TLMessage;
                if (messageCommon != null && messageCommon.Media == media)
                {
                    message = messageCommon;
                    break;
                }
            }
            if (message != null)
            {
                DeleteUploadingMessage(message);
            }
        }

        public void CancelVideoDownloading(TLMessageMediaVideo mediaVideo)
        {
            BeginOnThreadPool(() =>
            {
                BeginOnUIThread(() =>
                {
                    var message = Items.OfType<TLMessage>().FirstOrDefault(x => x.Media == mediaVideo);
                    DownloadVideoFileManager.CancelDownloadFileAsync(message);

                    mediaVideo.IsCanceled = true;
                    mediaVideo.LastProgress = mediaVideo.DownloadingProgress;
                    mediaVideo.DownloadingProgress = 0.0;
                });
            });
        }

        public void CancelDocumentDownloading(TLMessageMediaDocument mediaDocument)
        {
            BeginOnThreadPool(() =>
            {
                BeginOnUIThread(() =>
                {
                    var message = Items.OfType<TLMessage>().FirstOrDefault(x => x.Media == mediaDocument);

                    DownloadDocumentFileManager.CancelDownloadFileAsync(message);

                    mediaDocument.IsCanceled = true;
                    mediaDocument.LastProgress = mediaDocument.DownloadingProgress;
                    mediaDocument.DownloadingProgress = 0.0;
                });
            });
        }

        public void CancelDownloading(TLPhotoBase photo)
        {
            BeginOnThreadPool(() =>
            {
                DownloadFileManager.CancelDownloadFile(photo);
            });
        }

        public void OpenChatPhoto()
        {
            var user = With as TLUserBase;
            if (user != null)
            {
                var photo = user.Photo as TLUserProfilePhoto;
                if (photo != null)
                {
                    StateService.CurrentPhoto = photo;
                    NavigationService.UriFor<ProfilePhotoViewerViewModel>().Navigate();
                    return;
                }
            }

            var chat = With as TLChat;
            if (chat != null)
            {
                var photo = chat.Photo as TLChatPhoto;
                if (photo != null)
                {
                    StateService.CurrentPhoto = photo;
                    NavigationService.UriFor<ProfilePhotoViewerViewModel>().Navigate();
                    return;
                }
            }
        }

        public void CancelDownloading()
        {
            BeginOnThreadPool(() =>
            {
                BeginOnUIThread(() =>
                {
                    foreach (var item in Items.OfType<TLMessage>())
                    {
                        var mediaPhoto = item.Media as TLMessageMediaPhoto;
                        if (mediaPhoto != null)
                        {
                            CancelDownloading(mediaPhoto.Photo);
                        }
                    }
                });
            });
        }

        public void PinToStart()
        {
            DialogsViewModel.PinToStartCommon(new TLDialog24{ With = With });

        }

        public void ProcessScroll()
        {
            // replies
            if (_previousScrollPosition != null)
            {
                RaiseScrollTo(new ScrollToEventArgs(_previousScrollPosition));
                _previousScrollPosition = null;
                return;
            }


            // unread separator
            if (!_isFirstSliceLoaded)
            {
                Items.Clear();
                var messages = CacheService.GetHistory(new TLInt(StateService.CurrentUserId), TLUtils.InputPeerToPeer(Peer, StateService.CurrentUserId));
                ProcessRepliesAndAudio(messages);

                const int maxCount = 5;
                for (var i = 0; i < messages.Count && i < maxCount; i++)
                {
                    Items.Add(messages[i]);
                }

                //wait to complete animation for hiding ScrollToBottomButton
                BeginOnUIThread(TimeSpan.FromSeconds(0.35), () =>
                {
                    for (var i = maxCount; i < messages.Count; i++)
                    {
                        Items.Add(messages[i]);
                    }
                    _isFirstSliceLoaded = true;
                });
            }
            else
            {
                RaiseScrollToBottom();
            }
        }

        public void Help()
        {
            _text = "/help";
            Send();
        }
    }

    public class ScrollToEventArgs : System.EventArgs
    {
        public TLMessageBase Message { get; set; }

        public ScrollToEventArgs(TLMessageBase message)
        {
            Message = message;
        }
    }
}
