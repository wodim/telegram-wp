using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using Caliburn.Micro;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.Cache.EventArgs;
using Telegram.Api.Services.FileManager;
using Telegram.Api.Services.Updates;
using Telegram.Api.TL;
using TelegramClient.Controls;
using TelegramClient.Converters;
using TelegramClient.Resources;
using TelegramClient.Services;
using TelegramClient.Utils;
using TelegramClient.ViewModels.Contacts;
using TelegramClient.Views;
using Execute = Telegram.Api.Helpers.Execute;
using TypingTuple = Telegram.Api.WindowsPhone.Tuple<Telegram.Api.TL.TLDialog, TelegramClient.ViewModels.Dialogs.InputTypingManager>;
using TypingUser = Telegram.Api.WindowsPhone.Tuple<int, Telegram.Api.TL.TLSendMessageActionBase>;

namespace TelegramClient.ViewModels.Dialogs
{
    public partial class DialogsViewModel : ItemsViewModelBase<TLDialogBase>, Telegram.Api.Aggregator.IHandle<TopMessageUpdatedEventArgs>, Telegram.Api.Aggregator.IHandle<DialogAddedEventArgs>, Telegram.Api.Aggregator.IHandle<DialogRemovedEventArgs>, Telegram.Api.Aggregator.IHandle<DownloadableItem>, Telegram.Api.Aggregator.IHandle<UploadableItem>, Telegram.Api.Aggregator.IHandle<string>, Telegram.Api.Aggregator.IHandle<TLEncryptedChatBase>, Telegram.Api.Aggregator.IHandle<TLUpdateUserName>, Telegram.Api.Aggregator.IHandle<UpdateCompletedEventArgs>, Telegram.Api.Aggregator.IHandle<TLUpdateNotifySettings>, Telegram.Api.Aggregator.IHandle<TLUpdateNewAuthorization>, Telegram.Api.Aggregator.IHandle<TLUpdateServiceNotification>, Telegram.Api.Aggregator.IHandle<TLUpdateUserTyping>, Telegram.Api.Aggregator.IHandle<TLUpdateChatUserTyping>
    {
        public bool FirstRun { get; set; }

        public DialogsViewModel(ICacheService cacheService, ICommonErrorHandler errorHandler, IStateService stateService, INavigationService navigationService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator)
            : base(cacheService, errorHandler, stateService, navigationService, mtProtoService, eventAggregator)
        {
            Items = new ObservableCollection<TLDialogBase>();
            EventAggregator.Subscribe(this);
            DisplayName = (string)new LowercaseConverter().Convert(AppResources.Dialogs, null, null, null);
            Status = Items.Count == 0 && LazyItems.Count == 0 ? AppResources.Loading : string.Empty;

            BeginOnThreadPool(() =>
            {
                var isAuthorized = SettingsHelper.GetValue<bool>(Constants.IsAuthorizedKey);
                if (isAuthorized)
                {
                    var dialogs = CacheService.GetDialogs();

                    var dialogsCache = new Dictionary<int, TLDialogBase>();
                    var clearedDialogs = new List<TLDialogBase>();
                    foreach (var dialog in dialogs)
                    {
                        if (!dialogsCache.ContainsKey(dialog.Index))
                        {
                            clearedDialogs.Add(dialog);
                            dialogsCache[dialog.Index] = dialog;
                        }
                        else
                        {
                            var cachedDialog = dialogsCache[dialog.Index];
                            if (cachedDialog.Peer is TLPeerUser && dialog.Peer is TLPeerUser)
                            {
                                CacheService.DeleteDialog(dialog);
                                continue;
                            }
                            if (cachedDialog.Peer is TLPeerChat && dialog.Peer is TLPeerChat)
                            {
                                CacheService.DeleteDialog(dialog);
                                continue;
                            }
                        }
                    }

                    // load cache
                    BeginOnUIThread(() =>
                    {
                        Status = dialogs.Count == 0 ? AppResources.Loading : string.Empty;
                        Items.Clear();

                        const int maxDialogSlice = 8;
                        for (var i = 0; i < clearedDialogs.Count && i < maxDialogSlice; i++)
                        {
                            Items.Add(clearedDialogs[i]);
                        }

                        if (maxDialogSlice < clearedDialogs.Count)
                        {
                            BeginOnUIThread(() =>
                            {
                                for (var i = maxDialogSlice; i < clearedDialogs.Count; i++)
                                {
                                    Items.Add(clearedDialogs[i]);
                                }

                                UpdateItemsAsync(Telegram.Api.Constants.CachedDialogsCount);
                            });
                        }
                        else
                        {
                            UpdateItemsAsync(Telegram.Api.Constants.CachedDialogsCount);
                        }
                    });
                }
            });
        }

        private volatile bool _isUpdated;

        private void UpdateItemsAsync(int limit)
        {
            IsWorking = true;

            MTProtoService.GetDialogsAsync(
#if LAYER_40
                new TLInt(0), new TLInt(limit),
#else
                new TLInt(0), new TLInt(0), new TLInt(limit),
#endif
                result =>
                {
                    UpdateChannels();

                    // сортируем, т.к. при синхронизации, если есть отправляющиеся сообщений, то TopMessage будет замещен на них
                    // и начальная сортировка сломается
                    var orderedDialogs = new TLVector<TLDialogBase>(result.Dialogs.Count);
                    foreach (var orderedDialog in result.Dialogs.OrderByDescending(x => x.GetDateIndex()))
                    {
                        orderedDialogs.Add(orderedDialog);
                    }
                    result.Dialogs = orderedDialogs;

                    BeginOnUIThread(() =>
                    {
                        IsWorking = false;
                        IsLastSliceLoaded = result.Dialogs.Count < limit;

                        _offset = limit;

                        var needUpdate = false;
                        var itemsCount = Items.Count;
                        int i = 0, j = 0;
                        for (; i < result.Dialogs.Count && j < Items.Count; i++, j++)
                        {
                            if (itemsCount - 1 < i || result.Dialogs[i] != Items[j])
                            {
                                // skip "User joined Telegram!" message
                                var dialog = Items[j] as TLDialog;
                                if (dialog != null)
                                {
                                    var messageService = dialog.TopMessage as TLMessageService;
                                    if (messageService != null && messageService.Action is TLMessageActionContactRegistered)
                                    {
                                        i--;
                                        continue;
                                    }

                                    if (dialog.Peer is TLPeerChannel)
                                    {
                                        i--;
                                        continue;
                                    }
                                }
                                
                                var encryptedDialog = Items[j] as TLEncryptedDialog;
                                if (encryptedDialog != null)
                                {
                                    i--;
                                    continue;
                                }


                                needUpdate = true;
                                break;
                            }
                        }

                        if (i < j)
                        {
                            for (var k = i; k < j; k++)
                            {
                                if (k < result.Dialogs.Count)
                                {
                                    Items.Add(result.Dialogs[k]);
                                }
                            }
                        }

                        // load updated cache
                        Status = Items.Count == 0 && result.Dialogs.Count == 0 ? string.Format("{0}", AppResources.NoDialogsHere) : string.Empty;

                        if (needUpdate)
                        {
                            var encryptedDialogs = Items.OfType<TLEncryptedDialog>();
                            var startIndex = 0;
                            foreach (var encryptedDialog in encryptedDialogs)
                            {
                                for (var k = startIndex; k < result.Dialogs.Count; k++)
                                {
                                    if (encryptedDialog.GetDateIndex() > result.Dialogs[k].GetDateIndex())
                                    {
                                        result.Dialogs.Insert(k, encryptedDialog);
                                        startIndex = k;
                                        break;
                                    }
                                }
                            }

                            var broadcasts = Items.OfType<TLBroadcastDialog>();
                            startIndex = 0;
                            foreach (var broadcast in broadcasts)
                            {
                                for (var k = startIndex; k < result.Dialogs.Count; k++)
                                {
                                    if (broadcast.GetDateIndex() > result.Dialogs[k].GetDateIndex())
                                    {
                                        result.Dialogs.Insert(k, broadcast);
                                        startIndex = k;
                                        break;
                                    }
                                }
                            }

                            var channels = Items.Where(x => x.Peer is TLPeerChannel);
                            startIndex = 0;
                            foreach (var channel in channels)
                            {
                                for (var k = startIndex; k < result.Dialogs.Count; k++)
                                {
                                    if (channel.GetDateIndex() > result.Dialogs[k].GetDateIndex())
                                    {
                                        result.Dialogs.Insert(k, channel);
                                        startIndex = k;
                                        break;
                                    }
                                }
                            }

                            Items.Clear();
                            foreach (var dialog in result.Dialogs)
                            {
                                Items.Add(dialog);
                            }

                            IsLastSliceLoaded = false;
                            _isUpdated = true;
                        }
                        else
                        {
                            _isUpdated = true;
                        }
                    });
                },
                error => BeginOnUIThread(() =>
                {
                    _isUpdated = true;
                    Status = string.Empty;
                    IsWorking = false;
                }));
        }




        private static TLDialogsBase _channels;

        public static TLDialogBase GetChannel(TLInt id)
        {
            if (_channels != null)
            {
                foreach (var dialog in _channels.Dialogs)
                {
                    var peerChannel = dialog.Peer as TLPeerChannel;
                    if (peerChannel != null && peerChannel.Id.Value == id.Value)
                    {
                        return dialog;
                    }
                }
            }

            return null;
        }

        private void UpdateChannels()
        {

            MTProtoService.GetChannelDialogsAsync(new TLInt(0), new TLInt(100),
                results => Execute.BeginOnUIThread(() =>
                {
                    AddChannels(results);
                }),
                error => Execute.BeginOnUIThread(() =>
                {
                    Execute.ShowDebugMessage("channels.getDialogs error " + error);
                }));
        }

        private void AddChannels(TLDialogsBase channels)
        {
            //return;
            if (channels == null) return;

            _channels = channels;

            var removeChannels = new Context<TLInt>();
            foreach (var d in Items)
            {
                if (d.Peer is TLPeerChannel)
                {
                    removeChannels[d.Peer.Id.Value] = d.Peer.Id;
                }
            }

            var item = Items.OfType<TLDialog>().LastOrDefault(x => x.TopMessage != null);
            if (item != null)
            {
                var minDate = item.GetDateIndex();
                var newChannels = new List<TLDialogBase>();

                foreach (var dialogBase in channels.Dialogs)
                {
                    var dialog = dialogBase as TLDialog;
                    if (dialog != null && !removeChannels.ContainsKey(dialog.Peer.Id.Value))
                    {
                        var dialogDate = dialog.GetDateIndex();
                        if (dialogDate > minDate)
                        {
                            newChannels.Add(dialog);
                        }
                    }
                }
                var startIndex = 0;
                foreach (var channel in newChannels.OrderByDescending(x => x.GetDateIndex()))
                    // by default channels are ordered by join date
                {
                    for (var i = startIndex; i < Items.Count; i++)
                    {
                        if (channel.GetDateIndex() > Items[i].GetDateIndex())
                        {
                            Items.Insert(i, channel);
                            startIndex = i;
                            break;
                        }
                    }
                }
            }
            else
            {
            }
        }

        protected override void OnActivate()
        {
            base.OnActivate();

            if (FirstRun)
            {
                OnInitialize();
            }
        }

        protected override void OnInitialize()
        {
            BeginOnThreadPool(() =>
            {
                var isAuthorized = SettingsHelper.GetValue<bool>(Constants.IsAuthorizedKey);
                if (!FirstRun)
                {
                    return;
                }
                if (!isAuthorized)
                {
                    return;
                }

                FirstRun = false;

                Status = Items.Count == 0 && LazyItems.Count == 0 ? AppResources.Loading : string.Empty;
                var limit = Constants.DialogsSlice;
                MTProtoService.GetDialogsAsync(
#if LAYER_40
                    new TLInt(0), new TLInt(limit),
#else
                    new TLInt(0), new TLInt(0), new TLInt(limit),
#endif
                    dialogs => Execute.BeginOnUIThread(() =>
                    {
                        IsLastSliceLoaded = dialogs.Dialogs.Count < limit;
                        _offset = Constants.DialogsSlice;

                        _isUpdated = true;
                        const int firstSliceCount = 8;
                        var count = 0;
                        for (var i = 0; i < dialogs.Dialogs.Count; i++)
                        {
                            if (count < firstSliceCount)
                            {
                                Items.Add(dialogs.Dialogs[i]);
                                count++;
                            }
                            else
                            {
                                LazyItems.Add(dialogs.Dialogs[i]);
                            }
                        }

                        Status = Items.Count == 0 && LazyItems.Count == 0 ? string.Format("{0}", AppResources.NoDialogsHere) : string.Empty;

                        if (LazyItems.Count > 0)
                        {
                            BeginOnUIThread(() => 
                            {
                                for (var i = 0; i < LazyItems.Count; i++)
                                {
                                    Items.Add(LazyItems[i]);
                                }
                                LazyItems.Clear();
                                
                                InvokeImportContacts();
                                UpdateChannels();
                            });
                        }
                        else
                        {
                            InvokeImportContacts();
                            UpdateChannels();
                        }
                    }),
                    error => BeginOnUIThread(() =>
                    {
                        InvokeImportContacts();
                        UpdateChannels();

                        Execute.ShowDebugMessage("messages.getDialogs error " + error);
                        _isUpdated = true;
                        Status = string.Empty;
                    }));
            });

            base.OnInitialize();
        }

        private void InvokeImportContacts()
        {
            var contacts = IoC.Get<ContactsViewModel>();
            contacts.Handle(new InvokeImportContacts());
        }

        #region Actions

        

        public FrameworkElement OpenDialogElement;

        public void SetOpenDialogElement(object element)
        {
            OpenDialogElement = element as FrameworkElement;
        }



        public override void RefreshItems()
        {
            UpdateItemsAsync(Constants.DialogsSlice);
        }

        #endregion

        public void Handle(TopMessageUpdatedEventArgs eventArgs)
        {
            eventArgs.Dialog.NotifyOfPropertyChange(() => eventArgs.Dialog.With);
            OnTopMessageUpdated(this, eventArgs);
        }

        public void Handle(DialogAddedEventArgs eventArgs)
        {
            OnDialogAdded(this, eventArgs);
        }

        private void OnTopMessageUpdated(object sender, TopMessageUpdatedEventArgs e)
        {
            BeginOnUIThread(() =>
            {
                e.Dialog.TypingString = null;

                var currentPosition = Items.IndexOf(e.Dialog);

                var newPosition = currentPosition;
                for (var i = 0; i < Items.Count; i++)
                {
                    if (// мигает диалог, если просто обновляется последнее сообщение, то номер становится на 1 больше
                        // и сначала удаляем, а потом вставляем на туже позицию
                        i != currentPosition
                        && Items[i].GetDateIndex() <= e.Dialog.GetDateIndex())
                    {
                        newPosition = i;
                        break;
                    }
                }

                if (currentPosition != newPosition)
                {
                    if (currentPosition >= 0
                        && currentPosition < newPosition)
                    {
                        // т.к. будем сначала удалять диалог а потом вставлять, то
                        // curPos + 1 = newPos - это вставка на тоже место и не имеет смысла
                        // Update: имеет, т.к. обновляется инфа о последнем сообщении
                        if (currentPosition + 1 == newPosition)
                        {
                            Items[currentPosition].NotifyOfPropertyChange(() => Items[currentPosition].Self);
                            Items[currentPosition].NotifyOfPropertyChange(() => Items[currentPosition].UnreadCount);
                            return;
                        }
                        Items.Remove(e.Dialog);
                        Items.Insert(newPosition - 1, e.Dialog);
                    }
                    else
                    {
                        Items.Remove(e.Dialog);
                        Items.Insert(newPosition, e.Dialog);
                    }
                }
                else
                {
                    // удалили сообщение и диалог должен переместиться ниже загруженной части списка
                    if (!IsLastSliceLoaded
                        && Items.Count > 0
                        && Items[Items.Count - 1].GetDateIndex() > e.Dialog.GetDateIndex())
                    {
                        Items.Remove(e.Dialog);
                    }

                    Items[currentPosition].NotifyOfPropertyChange(() => Items[currentPosition].Self);
                    Items[currentPosition].NotifyOfPropertyChange(() => Items[currentPosition].UnreadCount);
                }
            });
        }

        private void OnDialogAdded(object sender, DialogAddedEventArgs e)
        {
            var dialog = e.Dialog;
            if (dialog == null) return;

            BeginOnUIThread(() =>
            {
                var index = -1;
                for (var i = 0; i < Items.Count; i++)
                {
                    if (Items[i] == e.Dialog)
                    {
                        return;
                    }

                    if (Items[i].GetDateIndex() < dialog.GetDateIndex())
                    {
                        index = i;
                        break;
                    }
                }

#if LAYER_40
                if (e.Dialog.Peer is TLPeerChannel)
                {
                    for (var i = 0; i < Items.Count; i++)
                    {
                        if (e.Dialog.Peer.GetType() == Items[i].Peer.GetType()
                            && e.Dialog.Peer.Id.Value == Items[i].Peer.Id.Value)
                        {
                            Items.RemoveAt(i);
                            Execute.ShowDebugMessage("OnDialogAdded RemoveAt=" + i);
                            break;
                        }
                    }
                }
#endif


                if (index == -1)
                {
                    Items.Add(dialog);
                }
                else
                {
                    Items.Insert(index, dialog);
                }
                Status = Items.Count == 0 || LazyItems.Count == 0 ? string.Empty : Status;
            });
        }

        public void Handle(DialogRemovedEventArgs args)
        {
            BeginOnUIThread(() =>
            {
#if LAYER_40
                if (args.Dialog.Peer is TLPeerChannel)
                {
                    for (var i = 0; i < Items.Count; i++)
                    {
                        if (args.Dialog.Peer.GetType() == Items[i].Peer.GetType()
                            && args.Dialog.Peer.Id.Value == Items[i].Peer.Id.Value)
                        {
                            Items.RemoveAt(i);
                            break;
                        }
                    }
                    return;
                }
#endif

                var dialog = Items.FirstOrDefault(x => x.Index == args.Dialog.Index);

                if (dialog != null)
                {
                    Items.Remove(dialog);
                }
            });
        }

        public void Handle(DownloadableItem item)
        {
            var photo = item.Owner as TLUserProfilePhoto;
            if (photo != null)
            {
                var user = CacheService.GetUser(photo);
                if (user != null)
                {
                    user.NotifyOfPropertyChange(() => user.Photo);
                }
                return;
            }

            var chatPhoto = item.Owner as TLChatPhoto;
            if (chatPhoto != null)
            {
                var chat = CacheService.GetChat(chatPhoto);
                if (chat != null)
                {
                    chat.NotifyOfPropertyChange(() => chat.Photo);
                    return;
                }

                var channel = CacheService.GetChannel(chatPhoto);
                if (channel != null)
                {
                    channel.NotifyOfPropertyChange(() => channel.Photo);
                    return;
                }
                return;
            }
        }

        public void Handle(string command)
        {
            if (string.Equals(command, Commands.LogOutCommand))
            {
                LazyItems.Clear();
                BeginOnUIThread(() => Items.Clear());
                Status = string.Empty;
                IsWorking = false;
            }
        }

        public void Handle(TLUpdateUserName userName)
        {
            Execute.BeginOnUIThread(() =>
            {
                for (var i = 0; i < Items.Count; i++)
                {
                    if (Items[i].WithId == userName.UserId.Value
                        && Items[i].With is TLUserBase)
                    {
                        var user = (TLUserBase)Items[i].With;
                        user.FirstName = userName.FirstName;
                        user.LastName = userName.LastName;

                        var userWithUserName = user as IUserName;
                        if (userWithUserName != null)
                        {
                            userWithUserName.UserName = userName.UserName;
                        }

                        Items[i].NotifyOfPropertyChange(() => Items[i].With);
                        break;
                    }
                }
            });
        }

        public void Handle(UploadableItem item)
        {
            var userSelf = item.Owner as TLUserSelf;
            if (userSelf != null)
            {

                MTProtoService.UploadProfilePhotoAsync(
                    new TLInputFile
                    {
                        Id = item.FileId,
                        MD5Checksum = new TLString(MD5Core.GetHashString(item.Bytes).ToLowerInvariant()),
                        Name = new TLString(Guid.NewGuid() + ".jpg"),
                        Parts = new TLInt(item.Parts.Count)
                    },
                    new TLString(""),
                    new TLInputGeoPointEmpty(),
                    new TLInputPhotoCropAuto(),
                    photosPhoto =>
                    {
                        MTProtoService.GetFullUserAsync(new TLInputUserSelf(), userFull => { }, error => { });
                    },
                    error =>
                    {

                    });
                return;
            }

            var channel = item.Owner as TLChannel;
            if (channel != null)
            {
                if (channel.Id != null)
                {
                    MTProtoService.EditPhotoAsync(
                        channel,
                        new TLInputChatUploadedPhoto
                        {
                            File = new TLInputFile
                            {
                                Id = item.FileId,
                                MD5Checksum = new TLString(MD5Core.GetHashString(item.Bytes).ToLowerInvariant()),
                                Name = new TLString("channelPhoto.jpg"),
                                Parts = new TLInt(item.Parts.Count)
                            },
                            Crop = new TLInputPhotoCropAuto()
                        },
                        statedMessage =>
                        {
                            var updates = statedMessage as TLUpdates;
                            if (updates != null)
                            {
                                var updateNewMessage = updates.Updates.FirstOrDefault(x => x is TLUpdateNewChannelMessage) as TLUpdateNewChannelMessage;
                                if (updateNewMessage != null)
                                {
                                    EventAggregator.Publish(updateNewMessage.Message);
                                }
                            }
                        },
                        error =>
                        {
                            Execute.ShowDebugMessage("messages.editChatPhoto error " + error);
                        });
                }
            }

            var chat = item.Owner as TLChat;
            if (chat != null)
            {
                MTProtoService.EditChatPhotoAsync(
                    chat.Id,
                    new TLInputChatUploadedPhoto
                    {
                        File = new TLInputFile
                        {
                            Id = item.FileId,
                            MD5Checksum = new TLString(MD5Core.GetHashString(item.Bytes).ToLowerInvariant()),
                            Name = new TLString("chatPhoto.jpg"),
                            Parts = new TLInt(item.Parts.Count)
                        },
                        Crop = new TLInputPhotoCropAuto()
                    },
                    statedMessage =>
                    {
                        var updates = statedMessage as TLUpdates;
                        if (updates != null)
                        {
                            var updateNewMessage = updates.Updates.FirstOrDefault(x => x is TLUpdateNewMessage) as TLUpdateNewMessage;
                            if (updateNewMessage != null)
                            {
                                EventAggregator.Publish(updateNewMessage.Message);
                            }
                        }
                    },
                    error =>
                    {
                        Execute.ShowDebugMessage("messages.editChatPhoto error " + error);
                    });
            }
        }

        public void Handle(TLEncryptedChatBase chat)
        {
            Execute.BeginOnUIThread(() =>
            {
                int index = -1;
                TLDialogBase dialog = null;
                for (int i = 0; i < Items.Count; i++)
                {
                    if (Items[i].Peer is TLPeerEncryptedChat
                        && Items[i].Peer.Id.Value == chat.Id.Value)
                    {
                        index = i;
                        dialog = Items[i];
                        break;
                    }
                }

                if (index != -1 && dialog != null)
                {
                    dialog.NotifyOfPropertyChange(() => dialog.Self);
                }
            });
        }

        public void Handle(UpdateCompletedEventArgs args)
        {
            var dialogs = CacheService.GetDialogs();

            Execute.BeginOnUIThread(() =>
            {

                Items.Clear();
                foreach (var dialog in dialogs)
                {
                    Items.Add(dialog);
                }

                AddChannels(_channels);
            });
        }

        private void HandleTypingCommon(TLUpdateTypingBase updateTyping, Dictionary<int, TypingTuple> typingCache)
        {
            Execute.BeginOnUIThread(() =>
            {
                var frame = Application.Current.RootVisual as TelegramTransitionFrame;
                if (frame != null)
                {
                    var shellView = frame.Content as ShellView;
                    if (shellView == null)
                    {
                        return;
                    }
                }

                var updateChatUserTyping = updateTyping as TLUpdateChatUserTyping;
                var id = updateChatUserTyping != null ? updateChatUserTyping.ChatId : updateTyping.UserId;
                TypingTuple tuple;
                if (!typingCache.TryGetValue(id.Value, out tuple))
                {
                    for (var i = 0; i < Items.Count; i++)
                    {
                        if (updateChatUserTyping == null
                            && Items[i].Peer is TLPeerUser
                            && Items[i].Peer.Id.Value == id.Value
                            || (updateChatUserTyping != null
                                && Items[i].Peer is TLPeerChat
                                && Items[i].Peer.Id.Value == id.Value))
                        {
                            var dialog = Items[i] as TLDialog;
                            if (dialog != null)
                            {
                                tuple = new TypingTuple(dialog, new InputTypingManager(
                                    users => Execute.BeginOnUIThread(() =>
                                    {
                                        dialog.TypingString = GetTypingString(dialog.Peer, users);
                                        dialog.NotifyOfPropertyChange(() => dialog.Self.TypingString);
                                    }),
                                    () => Execute.BeginOnUIThread(() =>
                                    {
                                        dialog.TypingString = null;
                                        dialog.NotifyOfPropertyChange(() => dialog.Self.TypingString);
                                    })));
                                typingCache[id.Value] = tuple;
                            }
                            break;
                        }
                    }
                }

                if (tuple != null)
                {
                    TLSendMessageActionBase action = null;
                    var typingAction = updateTyping as IUserTypingAction;
                    if (typingAction != null)
                    {
                        action = typingAction.Action;
                    }

                    tuple.Item2.AddTypingUser(updateTyping.UserId.Value, action);
                }
            });
        }

        private readonly Dictionary<int, TypingTuple> _userTypingCache = new Dictionary<int, TypingTuple>();

        public void Handle(TLUpdateUserTyping userTyping)
        {
            HandleTypingCommon(userTyping, _userTypingCache);
        }

        private readonly Dictionary<int, TypingTuple> _chatUserTypingCache = new Dictionary<int, TypingTuple>();

        public void Handle(TLUpdateChatUserTyping chatUserTyping)
        {
            HandleTypingCommon(chatUserTyping, _chatUserTypingCache);
        }

        public string GetTypingString(TLPeerBase peer, IList<TypingUser> typingUsers)
        {
            if (peer is TLPeerUser)
            {
                var typingUser = typingUsers.FirstOrDefault();
                if (typingUser != null)
                {
                    var action = typingUser.Item2;
                    if (action is TLSendMessageUploadPhotoAction)
                    {
                        return string.Format("{0}...", AppResources.SendingPhoto.ToLower(CultureInfo.InvariantCulture));
                    }
                    if (action is TLSendMessageRecordAudioAction)
                    {
                        return string.Format("{0}...", AppResources.RecordingAudio.ToLower(CultureInfo.InvariantCulture));
                    }
                    if (action is TLSendMessageUploadAudioAction)
                    {
                        return string.Format("{0}...", AppResources.RecordingAudio.ToLower(CultureInfo.InvariantCulture));
                    }
                    if (action is TLSendMessageUploadDocumentAction)
                    {
                        return string.Format("{0}...", AppResources.SendingFile.ToLower(CultureInfo.InvariantCulture));
                    }
                    if (action is TLSendMessageRecordVideoAction)
                    {
                        return string.Format("{0}...", AppResources.SendingVideo.ToLower(CultureInfo.InvariantCulture));
                    }
                    if (action is TLSendMessageUploadVideoAction)
                    {
                        return string.Format("{0}...", AppResources.SendingVideo.ToLower(CultureInfo.InvariantCulture));
                    }
                }

                return string.Format("{0}...", AppResources.Typing.ToLower(CultureInfo.InvariantCulture));
            }

            if (typingUsers.Count == 1)
            {
                var userId = new TLInt(typingUsers[0].Item1);
                var user = CacheService.GetUser(userId);
                if (user == null)
                {
                    var peerChat = peer as TLPeerChat;
                    if (peerChat != null)
                    {
                        MTProtoService.GetFullChatAsync(peerChat.Id, result => { }, error => { });
                    }

                    return null;
                }

                var userName = TLString.IsNullOrEmpty(user.FirstName) ? user.LastName : user.FirstName;
                var typingUser = typingUsers.FirstOrDefault(); 
                if (typingUser != null)
                {
                    
                    var action = typingUser.Item2;
                    if (action is TLSendMessageUploadPhotoAction)
                    {
                        return string.Format("{0} {1}...", userName, AppResources.IsSendingPhoto.ToLower(CultureInfo.InvariantCulture));
                    }
                    if (action is TLSendMessageUploadAudioAction)
                    {
                        return string.Format("{0} {1}...", userName, AppResources.IsRecordingAudio.ToLower(CultureInfo.InvariantCulture));
                    }
                    if (action is TLSendMessageRecordAudioAction)
                    {
                        return string.Format("{0} {1}...", userName, AppResources.IsRecordingAudio.ToLower(CultureInfo.InvariantCulture));
                    }
                    if (action is TLSendMessageUploadDocumentAction)
                    {
                        return string.Format("{0} {1}...", userName, AppResources.IsSendingFile.ToLower(CultureInfo.InvariantCulture));
                    }
                    if (action is TLSendMessageRecordVideoAction)
                    {
                        return string.Format("{0} {1}...", userName, AppResources.IsSendingVideo.ToLower(CultureInfo.InvariantCulture));
                    }
                    if (action is TLSendMessageUploadVideoAction)
                    {
                        return string.Format("{0} {1}...", userName, AppResources.IsSendingVideo.ToLower(CultureInfo.InvariantCulture));
                    }
                }

                return string.Format("{0} {1}...", userName, AppResources.IsTyping.ToLower(CultureInfo.InvariantCulture));
            }

            if (typingUsers.Count <= 3)
            {
                var firstNames = new List<string>(typingUsers.Count);
                var missingUsers = new List<TLInt>();
                foreach (var typingUser in typingUsers)
                {
                    var user = CacheService.GetUser(new TLInt(typingUser.Item1));
                    if (user != null)
                    {
                        var userName = TLString.IsNullOrEmpty(user.FirstName) ? user.LastName : user.FirstName;
                        firstNames.Add(userName.ToString());
                    }
                    else
                    {
                        missingUsers.Add(new TLInt(typingUser.Item1));
                    }
                }

                if (missingUsers.Count > 0)
                {
                    var peerChat = peer as TLPeerChat;
                    if (peerChat != null)
                    {
                        MTProtoService.GetFullChatAsync(peerChat.Id, result => { }, error => { });
                    }

                    return null;
                }

                return string.Format("{0} {1}...", string.Join(", ", firstNames),
                    AppResources.AreTyping.ToLower(CultureInfo.InvariantCulture));
            }

            return string.Format("{0} {1}...", Language.Declension(
                    typingUsers.Count,
                    AppResources.CompanyNominativeSingular,
                    AppResources.CompanyNominativePlural,
                    AppResources.CompanyGenitiveSingular,
                    AppResources.CompanyGenitivePlural).ToLower(CultureInfo.CurrentUICulture),
                AppResources.AreTyping.ToLower(CultureInfo.InvariantCulture));
        }
    }
}
