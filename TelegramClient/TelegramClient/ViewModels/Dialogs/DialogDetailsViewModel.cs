using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Navigation;
using Telegram.Api;
using Telegram.Api.Aggregator;
using Telegram.Api.Extensions;
using Telegram.Api.Helpers;
using Telegram.Api.Services.Cache.EventArgs;
using Telegram.EmojiPanel;
using TelegramClient.ViewModels.Chats;
using TelegramClient.Views;
#if WP8
using Windows.Storage;
#endif
using Caliburn.Micro;
using Microsoft.Xna.Framework.Media;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.FileManager;
using Telegram.Api.TL;
using Telegram.Api.TL.Interfaces;
using TelegramClient.Converters;
using TelegramClient.Helpers;
using TelegramClient.Resources;
using TelegramClient.Services;
using TelegramClient.Utils;
using TelegramClient.ViewModels.Additional;
using TelegramClient.ViewModels.Media;
using Execute = Telegram.Api.Helpers.Execute;

namespace TelegramClient.ViewModels.Dialogs
{
    public partial class DialogDetailsViewModel : ItemsViewModelBase<TLMessageBase>
    {
        public bool HasBots
        {
            get
            {
                var user = With as TLUser;
                if (user != null && user.IsBot)
                {
                    return true;
                }

                var chat = With as TLChatBase;
                if (chat != null && chat.BotInfo != null)
                {
                    return chat.BotInfo.Count > 0;
                }

                return false;
            }
        }

        public bool IsSingleBot
        {
            get
            {
                var user = With as TLUser;
                if (user != null && user.IsBot)
                {
                    return true;
                }

                var chat = With as TLChatBase;
                if (chat != null && chat.BotInfo != null)
                {
                    return chat.BotInfo.Count == 1;
                }

                return false;
            }
        }

        public bool IsAppBarCommandVisible
        {
            get
            {
                var channel = With as TLChannel;
                if (channel != null)// && channel.IsBroadcast)
                {
                    if (channel.IsMegaGroup)
                    {
                        return false;
                    }

                    if (!channel.Creator && !channel.IsEditor && !channel.IsModerator)
                    {
                        return true;
                    }
                }

                if (IsChannelForbidden)
                {
                    return true;
                }

                if (IsChatForbidden)
                {
                    return true;
                }

                if (IsChatDeactivated)
                {
                    return true;
                }

                var userBase = With as TLUserBase;
                if (userBase != null && userBase.Blocked != null && userBase.Blocked.Value)
                {
                    return true;
                }

                var user = With as TLUser;
                var bot = _bot as TLUser;
                if (user != null && bot != null && bot.IsBot && !string.IsNullOrEmpty(bot.AccessToken))
                {
                    return true;
                }

                if (user != null && user.IsBot && Items.Count == 0 && LazyItems.Count == 0)
                {
                    return true;
                }

                return false;
            }
        }

        public string AppBarCommandString
        {
            get
            {
                if (IsChannel)
                {
                    var channel = (TLChannel) With;

                    if (channel.Left.Value && channel.IsPublic)
                    {
                        return AppResources.Join.ToLowerInvariant();
                    }

                    var notifySettings = channel.NotifySettings as TLPeerNotifySettings;
                    if (notifySettings != null)
                    {
                        var muteUntil = notifySettings.MuteUntil.Value > 0;
                        return muteUntil ? AppResources.Unmute.ToLowerInvariant() : AppResources.Mute.ToLowerInvariant();
                    }

                    return AppResources.Mute.ToLowerInvariant();
                }

                if (IsChannelForbidden)
                {
                    return AppResources.Delete.ToLowerInvariant();
                }

                if (IsChatDeactivated)
                {
                    return AppResources.Delete.ToLowerInvariant();
                }

                if (IsBotStarting)
                {
                    return AppResources.Start.ToLowerInvariant();
                }

                if (IsUserBlocked)
                {
                    if (IsBot)
                    {
                        return AppResources.Restart.ToLowerInvariant();
                    }

                    return AppResources.UnblockContact.ToLowerInvariant();
                }

                return string.Empty;
            }
        }

        public bool IsGroupActionEnabled
        {
            get { return Items.Any(x => x.IsSelected); }
        }

        public string ScrollButtonImageSource
        {
            get
            {
                var isLightTheme = (Visibility)Application.Current.Resources["PhoneLightThemeVisibility"] == Visibility.Visible;

                if (isLightTheme)
                {
                    return "/Images/ApplicationBar/appbar.next.light.png";
                }

                return "/Images/ApplicationBar/appbar.next.png";
            }
        }

        public string ChannelShareImageSource
        {
            get
            {
                var isLightTheme = (Visibility)Application.Current.Resources["PhoneLightThemeVisibility"] == Visibility.Visible;

                if (isLightTheme)
                {
                    return "/Images/Messages/channel.share.black.png";
                }

                return "/Images/Messages/channel.share.white.png";
            }
        }

        public Brush ReplyBackgroundBrush
        {
            get
            {
                var isLightTheme = (Visibility)Application.Current.Resources["PhoneLightThemeVisibility"] == Visibility.Visible;

                if (!isLightTheme)
                {
                    if (StateService.IsEmptyBackground)
                    {
                        return (Brush)Application.Current.Resources["PhoneChromeBrush"];
                    }
                } 
                var color = Colors.Black;
                color.A = 102;
                return new SolidColorBrush(color);
            }
        }

        public Brush WatermarkForeground
        {
            get
            {
                var isLightTheme = (Visibility)Application.Current.Resources["PhoneLightThemeVisibility"] == Visibility.Visible;

                if (isLightTheme)
                {
                    if (StateService.IsEmptyBackground)
                    {
                        var color = Colors.Black;
                        color.A = 153;
                        return new SolidColorBrush(color);
                    }
                }

                return (Brush)Application.Current.Resources["PhoneContrastForegroundBrush"];
            }
        }

        public TLAllStickers Stickers { get; protected set; }

        public TLStickerPack GetStickerPack(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;

            if (Stickers == null) return null;
            if (Stickers.Packs == null) return null;

            for (var i = 0; i < Stickers.Packs.Count; i++)
            {
                if (Stickers.Packs[i].Emoticon != null
                    && Stickers.Packs[i].Emoticon.ToString() == text)
                {
                    return Stickers.Packs[i];
                }
            }

            return null;
        }

        public string EmptyDialogImageSource
        {
            get
            {
                var isLightTheme = (Visibility)Application.Current.Resources["PhoneLightThemeVisibility"] == Visibility.Visible;

                if (isLightTheme)
                {
                    return "/Images/Messages/chat.nomessages-white-WXGA.png";
                }

                return "/Images/Messages/chat.nomessages-WXGA.png";
            }
        }

        private bool _isSelectionEnabled;

        public bool IsSelectionEnabled
        {
            get { return _isSelectionEnabled; }
            set
            {
                SetField(ref _isSelectionEnabled, value, () => IsSelectionEnabled);

                if (!value)
                {
                    foreach (var item in Items)
                    {
                        item.IsSelected = false;
                    }
                }
            }
        }

        public void ChangeSelection(TLMessageBase message)
        {
            if (message == null) return;

            message.IsSelected = !message.IsSelected;
            NotifyOfPropertyChange(() => IsGroupActionEnabled);
        }

        private string _text;

        public string Text
        {
            get { return _text; }
            set
            {
                SetField(ref _text, value, () => Text);
                SaveUnsendedTextAsync(_text);
                NotifyOfPropertyChange(() => CanSend);
            }
        }

        private readonly object _unsendedTextRoot = new object();

        private void SaveUnsendedTextAsync(string text)
        {
            BeginOnThreadPool(() =>
            {
                var inputPeer = With as IInputPeer;
                if (inputPeer == null) return;

                if (string.IsNullOrEmpty(text))
                {
                    FileUtils.Delete(_unsendedTextRoot, inputPeer.GetUnsendedTextFileName());
                    return;
                }

                TLUtils.SaveObjectToMTProtoFile(_unsendedTextRoot, inputPeer.GetUnsendedTextFileName(), new TLString(text));
            });
        }

        private void LoadUnsendedTextAsync(Action<string> callback)
        {
            BeginOnThreadPool(() =>
            {
                var inputPeer = With as IInputPeer;
                if (inputPeer == null) return;

                var result = TLUtils.OpenObjectFromMTProtoFile<TLString>(_unsendedTextRoot, inputPeer.GetUnsendedTextFileName());
                callback.SafeInvoke(result != null ? result.ToString() : null);
            });
        }

        private bool _isEmptyDialog;

        public bool IsEmptyDialog
        {
            get { return _isEmptyDialog; }
            set { SetField(ref _isEmptyDialog, value, () => IsEmptyDialog); }
        }

        private string _subtitle;

        public string Subtitle
        {
            get { return _subtitle; }
            set { SetField(ref _subtitle, value, () => Subtitle); }
        }

        private TLInputPeerBase _peer;

        public TLInputPeerBase Peer
        {
            get { return _peer; }
            set { SetField(ref _peer, value, () => Peer); }
        }

        public TLObject With { get; set; }

        private IUploadFileManager _uploadFileManager;

        private IUploadFileManager UploadFileManager
        {
            get { return _uploadFileManager ?? (_uploadFileManager = IoC.Get<IUploadFileManager>()); }
        }

        private IUploadDocumentFileManager _uploadDocumentFileManager;

        private IUploadDocumentFileManager UploadDocumentFileManager
        {
            get { return _uploadDocumentFileManager ?? (_uploadDocumentFileManager = IoC.Get<IUploadDocumentFileManager>()); }
        }

        private IUploadVideoFileManager _uploadVideoFileManager;

        public IUploadVideoFileManager UploadVideoFileManager
        {
            get { return _uploadVideoFileManager ?? (_uploadVideoFileManager = IoC.Get<IUploadVideoFileManager>()); }
        }

        private IUploadAudioFileManager _uploadAudioFileManager;

        public IUploadAudioFileManager UploadAudioFileManager
        {
            get { return _uploadAudioFileManager ?? (_uploadAudioFileManager = IoC.Get<IUploadAudioFileManager>()); }
        }

        private IFileManager _downloadFileManager;

        private IFileManager DownloadFileManager
        {
            get { return _downloadFileManager ?? (_downloadFileManager = IoC.Get<IFileManager>()); }
        }

        private IVideoFileManager _downloadVideoFileManager;

        private IVideoFileManager DownloadVideoFileManager
        {
            get { return _downloadVideoFileManager ?? (_downloadVideoFileManager = IoC.Get<IVideoFileManager>()); }
        }

        private IAudioFileManager _downloadAudioFileManager;

        private IAudioFileManager DownloadAudioFileManager
        {
            get { return _downloadAudioFileManager ?? (_downloadAudioFileManager = IoC.Get<IAudioFileManager>()); }
        }

        private IDocumentFileManager _downloadDocumentFileManager;

        private IDocumentFileManager DownloadDocumentFileManager
        {
            get { return _downloadDocumentFileManager ?? (_downloadDocumentFileManager = IoC.Get<IDocumentFileManager>()); }
        }

        public void BackwardInAnimationComplete()
        {

            BeginOnThreadPool(() =>
            {
                InputTypingManager.Start();

                BeginOnUIThread(() =>
                {
                    NotifyOfPropertyChange(() => With);
                    if (StateService.RemoveBackEntry)
                    {
                        StateService.RemoveBackEntry = false;
                        NavigationService.RemoveBackEntry();
                    }
                });


                SendMedia();
                ReadHistoryAsync();
            });
        }

        private bool _isForwardInAnimationComplete;

        public void ForwardInAnimationComplete()
        {
            _isForwardInAnimationComplete = true;

            if (_isFirstSliceLoaded)
            {
                if (LazyItems.Count == 0)
                {
                    UpdateReplyMarkup(Items);
                }
            }

            SendForwardMessages();
            SendSharedContact();
            SendSharedPhoto();
            try
            {
                SendLogs();
            }
            catch (Exception ex)
            {
                Execute.ShowDebugMessage(ex.ToString());
            }

            if (_delayedMessages != null)
            {
                AddMessagesAndReadHistory(_delayedPosition, _delayedMessages);
                _delayedMessages = null;
            }
            _delayedPosition = -1;

            if (ChooseAttachment == null)
            {
                ChooseAttachment = new ChooseAttachmentViewModel(With, CacheService, EventAggregator, NavigationService, StateService);
                NotifyOfPropertyChange(() => ChooseAttachment);
            }

            //MessageBox.Show("ForwardInAnimationComplete");

            BeginOnThreadPool(() =>
            {
                InputTypingManager.Start();

                BeginOnUIThread(() =>
                {
                    if (StateService.RemoveBackEntry)
                    {
                        StateService.RemoveBackEntry = false;
                        NavigationService.RemoveBackEntry();
                    }

                    if (StateService.RemoveBackEntries)
                    {
                        var backEntry = NavigationService.BackStack.FirstOrDefault();
                        while (backEntry != null 
                            && !backEntry.Source.ToString().EndsWith("ShellView.xaml") 
                            && !IsFirstEntryFromPeopleHub(backEntry, NavigationService.BackStack)
                            && !IsFirstEntryFromTelegramUrl(backEntry, NavigationService.BackStack))
                        {
                            NavigationService.RemoveBackEntry();
                            backEntry = NavigationService.BackStack.FirstOrDefault();
                        }
                        

                        StateService.RemoveBackEntries = false;
                    }
                });

                //ReadHistoryAsync(); Read on LazyItems Population complete
                GetFullInfo();
            });
        }

        private void AddMessagesAndReadHistory(int startPosition, TLVector<TLMessageBase> cachedMessages)
        {
            //if (startPosition > 1)
            //{
            //    ScrollToBottomVisibility = Visibility.Visible;
            //}
            
            BeginOnUIThread(() =>
            {
                for (var i = startPosition; i < cachedMessages.Count; i++)
                {
                    var message = cachedMessages[i];
                    Items.Add(message);
                }

                HoldScrollingPosition = true;
                BeginOnUIThread(() =>
                {
                    for (var i = 0; i < startPosition - 1; i++)
                    {
                        Items.Insert(i, cachedMessages[i]);
                    }
                    HoldScrollingPosition = false;
                });

            });

            ReadHistoryAsync();
        }

        public static bool IsFirstEntryFromTelegramUrl(JournalEntry backEntry, IEnumerable<JournalEntry> backStack)
        {
            if (backEntry.Source.ToString().StartsWith("/Protocol?encodedLaunchUri"))
            {
                if (backStack != null && backStack.Count() == 1)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsFirstEntryFromPeopleHub(JournalEntry backEntry, IEnumerable<JournalEntry> backStack)
        {
            if (backEntry.Source.ToString().StartsWith("/PeopleExtension?action"))
            {
                if (backStack != null && backStack.Count() == 1)
                {
                    return true;
                }
            }

            return false;
        }

        private void SendSharedContact()
        {
            if (StateService.SharedContact != null)
            {
                //var lastItem = NavigationService.BackStack.LastOrDefault();
                // if (lastItem != null && lastItem.Source.)

                var contact = StateService.SharedContact;
                StateService.SharedContact = null;
                SendContact(contact);
                return;
            }
        }

        private TLDialog _currentDialog;

        public ImageViewerViewModel ImageViewer { get; protected set; }

        public AnimatedImageViewerViewModel AnimatedImageViewer { get; protected set; }

        public ChooseAttachmentViewModel ChooseAttachment { get; protected set; }

        public bool IsChooseAttachmentOpen { get { return ChooseAttachment != null && ChooseAttachment.IsOpen; } }

        public void NavigateToShellViewModel()
        {
            NavigateToShellViewModel(StateService, NavigationService);
        }

        public static void NavigateToShellViewModel(IStateService stateService, INavigationService navigationService)
        {
            stateService.ClearNavigationStack = true;
            navigationService.UriFor<ShellViewModel>().Navigate();
        }

        private readonly TLDialogBase _dialog;

        private readonly TLUserBase _bot;

        public DialogDetailsViewModel(
            ICacheService cacheService, ICommonErrorHandler errorHandler, IStateService stateService, INavigationService navigationService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator)
            : base(cacheService, errorHandler, stateService, navigationService, mtProtoService, eventAggregator)
        {
            Items = new ObservableCollection<TLMessageBase>();

            With = GetParticipant();
            StateService.With = null;
            if (With == null)
            {
                return;
            }

            if (StateService.Url != null)
            {
                With.ClearBitmap();
            }

            if (StateService.Dialog != null)
            {
                _dialog = StateService.Dialog;
                StateService.Dialog = null;
            }

            var accessToken = StateService.AccessToken;
            StateService.AccessToken = null;
            if (StateService.Bot != null)
            {
                _bot = StateService.Bot;
                _bot.AccessToken = accessToken;
                StateService.Bot = null;

                var chat = With as TLChat;
                if (chat != null)
                {
                    MTProtoService.AddChatUserAsync(chat.Id, _bot.ToInputUser(), new TLInt(0),
                        statedMessage =>
                        {
                            var updates = statedMessage as TLUpdates;
                            if (updates != null)
                            {
                                var updateNewMessage = updates.Updates.FirstOrDefault(x => x is TLUpdateNewMessage) as TLUpdateNewMessage;
                                if (updateNewMessage != null)
                                {
                                    Handle(updateNewMessage.Message as TLMessageCommon);
                                }

                                if (!string.IsNullOrEmpty(accessToken))
                                {
                                    Execute.BeginOnUIThread(AppBarCommand);
                                }
                            }
                        },
                        error => BeginOnUIThread(() =>
                        {
                            if (error.TypeEquals(ErrorType.PEER_FLOOD))
                            {
                                MessageBox.Show(AppResources.PeerFloodAddContact, AppResources.Error, MessageBoxButton.OK);
                            }
                            else if (error.CodeEquals(ErrorCode.BAD_REQUEST)
                                && error.TypeEquals(ErrorType.USER_ALREADY_PARTICIPANT))
                            {
                                if (!string.IsNullOrEmpty(accessToken))
                                {
                                    AppBarCommand();
                                }
                            }

                            Execute.ShowDebugMessage("messages.addChatUser error " + error);
                        })); 
                }
            }
            else
            {
                var user = With as TLUser;
                if (user != null)
                {
                    user.AccessToken = accessToken;
                }
            }

            if (UserActionViewModel.IsRequired(With))
            {
                UserAction = new UserActionViewModel((TLUserBase)With);
                UserAction.InvokeUserAction += (sender, args) => InvokeUserAction();
                UserAction.InvokeUserAction2 += (sender, args) => InvokeUserAction2();
            }
            //return;

            

            //подписываем на события только, если смогли восстановиться после tombstoning
            EventAggregator.Subscribe(this);

            _peer = ((IInputPeer)With).ToInputPeer();
            _subtitle = GetSubtitle();
            
            PropertyChanged += (sender, args) =>
            {
                if (Property.NameEquals(args.PropertyName, () => Text))
                {
                    if (!string.IsNullOrEmpty(Text))
                    {
                        GetWebPagePreviewAsync(Text);

                        string searchText;
                        var searchByUsernames = SearchByUsernames(Text, out searchText);
                        if (searchByUsernames)
                        {
                            GetUsernameHints(searchText);
                        }
                        else
                        {
                            ClearUsernameHints();
                        }

                        var searchByCommands = SearchByCommands(Text, out searchText);
                        if (searchByCommands)
                        {
                            GetCommandHints(searchText);
                        }
                        else
                        {
                            ClearCommandHints();
                        }

                        //var searchByHashtags = SearchByHashtags(Text, out searchText);

                        //if (searchByHashtags)
                        //{
                        //    GetHashtagHints(searchText);
                        //}
                        //else
                        //{
                        //    HashtagHints.Clear();
                        //}
                    }
                    else
                    {
                        RestoreReply();

                        ClearUsernameHints();
                        ClearHashtagHints();
                        ClearCommandHints();
                    }

                    TextTypingManager.SetTyping();
                }
                else if (Property.NameEquals(args.PropertyName, () => With))
                {
                    NotifyOfPropertyChange(() => IsAppBarCommandVisible);
                    NotifyOfPropertyChange(() => AppBarCommandString);
                }
            };

            LoadUnsendedTextAsync(unsendedText =>
            {
                if (StateService.Url != null)
                {
                    Text = StateService.Url;
                    StateService.Url = null;

                    if (StateService.UrlText != null)
                    {
                        Text = Text + Environment.NewLine + StateService.UrlText;
                        StateService.UrlText = null;
                    }
                }
                else if (StateService.WebLink != null)
                {
                    Text = StateService.WebLink.ToString();
                    StateService.WebLink = null;
                }
                else
                {
                    Text = unsendedText;
                }
            });

            BeginOnThreadPool(() =>
            {
                CacheService.CheckDisabledFeature(With, Constants.FeaturePMMessage, Constants.FeatureChatMessage, Constants.FeatureBigChatMessage, () => { }, result => { }); // for fast CacheService.CheckDisabledFeature

                GetAllStickersAsync();

                if (StateService.Message != null)
                {
                    var message = StateService.Message;
                    StateService.Message = null;
                    Execute.BeginOnUIThread(() =>
                    {
                        _isUpdated = true;
                        _isFirstSliceLoaded = false;

                        Items.Clear();
                        Items.Add(message);
                        //HighlightMessage(message);
                        LoadResultHistory(message); 
                        ReadHistoryAsync();
                    });

                    return;
                }

#if WP8
                var isLocalServiceMessage = false;
                var dialog = _dialog as TLDialog;
                if (dialog != null)
                {
                    var topMessage = dialog.TopMessage;
                    isLocalServiceMessage = IsLocalServiceMessage(topMessage);  // ContactRegistered update
                }

                if (_dialog != null
                    && StateService.ForwardMessages == null
                    && _dialog.UnreadCount.Value >= Constants.MinUnreadCountToShowSeparator
                    && !isLocalServiceMessage)
                {
                    var unreadCount = _dialog.UnreadCount.Value;

                    if (unreadCount > 0)
                    {
                        if (With != null)
                        {
                            With.Bitmap = null;
                        }

                        var cachedMessages = CacheService.GetHistory(new TLInt(StateService.CurrentUserId), TLUtils.InputPeerToPeer(Peer, StateService.CurrentUserId));

                        ProcessRepliesAndAudio(cachedMessages);

                        if (cachedMessages.Count >= unreadCount && cachedMessages.Count > 10)
                        {
                            BeginOnUIThread(() =>
                            {
                                var startPosition = 0;
                                for (var i = 0; i < cachedMessages.Count; i++)
                                {
                                    var message = cachedMessages[i] as TLMessageCommon;
                                    if (message != null && !message.Unread.Value)
                                    {
                                        break;
                                    }
                                    startPosition++;
                                }
                                startPosition--;

                                var items = new TLVector<TLMessageBase>();
                                foreach (var message in cachedMessages)
                                {
                                    items.Add(message);
                                }

                                IsFirstSliceLoaded = true;
                                if (startPosition >= 0)
                                {
                                    AddUnreadHistory(startPosition, items);
                                }
                                else
                                {
                                    AddHistory(items.Items);
                                    _dialog.UnreadCount = new TLInt(0);
                                }
                            });
                        }
                        else
                        {
                            var unreadSlice = 10;
                            var offset = Math.Max(0, unreadCount - unreadSlice);
                            IsWorking = true;
                            MTProtoService.GetHistoryAsync(".ctor",
                                Peer,
                                TLUtils.InputPeerToPeer(Peer, StateService.CurrentUserId),
                                offset < Constants.MessagesSlice,
                                new TLInt(offset),
                                new TLInt(0),
                                new TLInt(20),
                                result => 
                                {
                                    ProcessRepliesAndAudio(result.Messages);

                                    BeginOnUIThread(() =>
                                    {
                                        IsWorking = false;
                                        var startPosition = offset == 0 ? unreadCount - 1 : unreadSlice - 1;

                                        IsFirstSliceLoaded = false;
                                        AddUnreadHistory(startPosition, result.Messages);
                                    });
                                },
                                error =>
                                {
                                    Execute.ShowDebugMessage("messages.getHistory error " + error);
                                    IsWorking = false;
                                });
                        }

                        return;
                    }
                }
#endif
                var messages = GetHistory();

                ProcessRepliesAndAudio(messages);
                AddHistory(messages);
            });
        }

        public IList<TLMessageBase> GetHistory()
        {
            var messages = CacheService.GetHistory(new TLInt(StateService.CurrentUserId), TLUtils.InputPeerToPeer(Peer, StateService.CurrentUserId));
            if (messages == null || messages.Count == 0)
            {
                var dialog = _dialog as TLDialog;
                if (dialog != null)
                {
                    messages = dialog.Messages;
                }
            }

            var channel = With as TLChannel;
            if (channel != null && channel.MigratedFromChatId != null)
            {
                var lastMessage = messages != null ? messages.LastOrDefault() : null;
                if (lastMessage != null && lastMessage.Index == 1)
                {
                    var chatMessages = CacheService.GetHistory(new TLInt(StateService.CurrentUserId), new TLPeerChat { Id = channel.MigratedFromChatId });
                    foreach (var message in chatMessages)
                    {
                        if (!SkipMessage(message))
                        {
                            messages.Add(message);
                        }
                    }
                }
            }

            return messages;
        }

        private bool SkipMessage(TLMessageBase messageBase)
        {
            var channel = With as TLChannel;
            if (channel != null && channel.MigratedFromChatId != null)
            {
                var serviceMessage = messageBase as TLMessageService;
                if (serviceMessage != null)
                {
                    var chatMigrateTo = serviceMessage.Action as TLMessageActionChatMigrateTo;
                    if (chatMigrateTo != null)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private readonly Dictionary<string, TLMessageMediaBase> _webPagesCache = new Dictionary<string, TLMessageMediaBase>();

        private TLMessageBase _previousReply;

        private void SaveReply()
        {
            if (Reply != null && !IsWebPagePreview(Reply))
            {
                _previousReply = Reply;
            }
        }

        private void RestoreReply()
        {
            if (_previousReply != null)
            {
                Reply = _previousReply;
                _previousReply = null;
            }
            else
            {
                if (IsWebPagePreview(Reply))
                {
                    Reply = null;
                }
            }
        }

        private static bool IsWebPagePreview(TLMessageBase message)
        {
            var messagesContainer = message as TLMessagesContainter;
            if (messagesContainer != null)
            {
                return messagesContainer.WebPageMedia != null;
            }

            return false;
        }

        private void GetWebPagePreviewAsync(string t)
        {
            if (t == null)
            {
                return;
            }

            var text = t.Trim();
            TLMessageMediaBase webPageMedia;
            if (_webPagesCache.TryGetValue(text, out webPageMedia))
            {
                var webPageMessageMedia = webPageMedia as TLMessageMediaWebPage;
                if (webPageMessageMedia != null)
                {
                    var webPage = webPageMessageMedia.WebPage as TLWebPage;
                    if (webPage != null)
                    {
                        SaveReply();

                        Reply = new TLMessagesContainter {WebPageMedia = webPageMedia};
                    }
                    else
                    {
                        RestoreReply();
                    }
                }

                return;
            }
            else
            {
                RestoreReply();
            }

            Execute.BeginOnThreadPool(() =>
            {

                Thread.Sleep(1000);

                Execute.BeginOnUIThread(() =>
                {
                    if (!string.Equals(Text, text))
                    {
                        return;
                    }

                    Uri uri;
                    var uriString = text.Trim().StartsWith("http", StringComparison.OrdinalIgnoreCase)
                        ? text.Trim()
                        : "http://" + text.Trim();
                    if (Uri.TryCreate(uriString, UriKind.Absolute, out uri))
                    {
                        Execute.BeginOnThreadPool(() =>
                        {
                            MTProtoService.GetWebPagePreviewAsync(new TLString(text),
                                result => Execute.BeginOnUIThread(() =>
                                {
                                    _webPagesCache[text] = result;

                                    if (!string.Equals(Text, text))
                                    {
                                        return;
                                    }
                                    var webPageMessageMedia = result as TLMessageMediaWebPage;
                                    if (webPageMessageMedia != null)
                                    {

                                        var webPage = webPageMessageMedia.WebPage;
                                        if (webPage is TLWebPage || webPage is TLWebPagePending)
                                        {
                                            SaveReply();

                                            Reply = new TLMessagesContainter { WebPageMedia = result };
                                        }
                                    }
                                }),
                                error =>
                                {
                                    Execute.ShowDebugMessage("messages.getWebPagePreview error " + error);
                                });
                        });
                    }
                });
            });
        }

        private void AddHistory(IList<TLMessageBase> messages)
        {
            IsEmptyDialog = messages.Count == 0;
            LazyItems.Clear();

            //var items = new List<TLMessageBase>();
            const int firstSliceCount = 8;
            var isAnimated = With.Bitmap == null && messages.Count > 1;
            for (var i = 0; i < messages.Count; i++)
            {
                if (i < firstSliceCount)
                {
                    messages[i]._isAnimated = isAnimated;
                    LazyItems.Add(messages[i]);
                }
                else
                {
                    LazyItems.Add(messages[i]);
                    //items.Add(messages[i]);
                }
            }

            NotifyOfPropertyChange(() => IsAppBarCommandVisible);

            if (LazyItems.Count == 0)
            {
                UpdateItemsAsync(0, 0, Constants.MessagesSlice, false);
                ReadHistoryAsync();
            }
            else
            {
                BeginOnUIThread(() => PopulateItems(() =>
                {
                    //Execute.ShowDebugMessage("PopulateComplete");

                    if (_isFirstSliceLoaded
                        && _isForwardInAnimationComplete)
                    {
                        UpdateReplyMarkup(Items);
                    }

                    ReadHistoryAsync();
                    UpdateItemsAsync(0, 0, Constants.MessagesSlice, false);
                }));
            }
        }

        private void AddUnreadHistory(int startPosition, TLVector<TLMessageBase> messages)
        {
            Items.Clear();
            _isUpdated = true;

            if (startPosition < messages.Count)
            {
                var message = messages[startPosition++];
                Items.Add(message);

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
                Items.Add(separator);
            }


            if (startPosition > 1)
            {
                BeginOnUIThread(() =>
                {
                    ScrollToBottomVisibility = Visibility.Visible;
                });
            }

            var forwardInAnimationComplete = _delayedPosition == -1;
            if (forwardInAnimationComplete)
            {
                AddMessagesAndReadHistory(startPosition, messages);
            }
            else
            {
                _delayedPosition = startPosition;
                _delayedMessages = messages;
            }

            IsEmptyDialog = Items.Count == 0 && messages.Count == 0;
        }

        private TLVector<TLMessageBase> _delayedMessages;
        private int _delayedPosition;

        private TLObject GetParticipant()
        {
            if (StateService.With == null)
            {
                if (!string.IsNullOrEmpty(StateService.ChatId))
                {
                    var chatIdString = StateService.ChatId;
                    StateService.ChatId = null;
                    int chatId;
                    try
                    {
                        chatId = Convert.ToInt32(chatIdString);
                    }
                    catch (Exception e)
                    {
                        NavigateToShellViewModel();
                        return null;
                    }
                    var chat = CacheService.GetChat(new TLInt(chatId));
                    if (chat != null)
                    {
                        return chat;
                    }
                    
                    NavigateToShellViewModel();
                    return null;
                }
                
                if (!string.IsNullOrEmpty(StateService.UserId))
                {
                    var userIdString = StateService.UserId;
                    StateService.UserId = null;

                    int userId;
                    try
                    {
                        userId = Convert.ToInt32(userIdString);
                    }
                    catch (Exception e)
                    {
                        NavigateToShellViewModel();
                        return null;
                    }
                    var user = CacheService.GetUser(new TLInt(userId));
                    if (user != null)
                    {
                        return user;
                    }
                    
                    NavigateToShellViewModel();
                    return null;
                }

                if (!string.IsNullOrEmpty(StateService.BroadcastId))
                {
                    var userIdString = StateService.BroadcastId;
                    StateService.BroadcastId = null;

                    int broadcastId;
                    try
                    {
                        broadcastId = Convert.ToInt32(userIdString);
                    }
                    catch (Exception e)
                    {
                        NavigateToShellViewModel();
                        return null;
                    }
                    var broadcast = CacheService.GetBroadcast(new TLInt(broadcastId));
                    if (broadcast != null)
                    {
                        return broadcast;
                    }

                    NavigateToShellViewModel();
                    return null;
                }
                
                NavigateToShellViewModel();
                return null;
            }
            
            return StateService.With;
        }

        public void GetAllStickersAsync()
        {
            StateService.GetAllStickersAsync(cachedStickers =>
            {
                Stickers = cachedStickers;

                var cachedStickers29 = cachedStickers as TLAllStickers29;
                if (cachedStickers29 != null
                    && cachedStickers29.Date != null
                    && cachedStickers29.Date.Value != 0)
                {
                    var date = TLUtils.ToDateTime(cachedStickers29.Date);
                    if (
                        date < DateTime.Now.AddSeconds(Constants.GetAllStickersInterval))
                    {
                        return;
                    }
                }

                var hash = cachedStickers != null ? cachedStickers.Hash ?? TLString.Empty : TLString.Empty;

                MTProtoService.GetAllStickersAsync(hash,
                    result =>
                    {
                        var allStickers = result as TLAllStickers29;
                        if (allStickers != null)
                        {
                            if (cachedStickers29 != null)
                            {
                                allStickers.IsDefaultSetVisible = cachedStickers29.IsDefaultSetVisible;
                                allStickers.RecentlyUsed = cachedStickers29.RecentlyUsed;
                                allStickers.Date = TLUtils.DateToUniversalTimeTLInt(0, DateTime.Now);
                            }
                            Stickers = allStickers;
                            cachedStickers = allStickers;
                            StateService.SaveAllStickersAsync(cachedStickers);
                        }
                    },
                    error =>
                    {
                        Execute.ShowDebugMessage("messages.getAllStickers error " + error);
                    });
            });
        }

#if DEBUG

        public void SendStatus()
        {
            MTProtoService.UpdateStatusAsync(new TLBool(false),
                result =>
                {
                    BeginOnUIThread(() => MessageBox.Show("SendStatus result: " + result.Value, "Result OK", MessageBoxButton.OK));
                },
                error =>
                {
                    BeginOnUIThread(() => MessageBox.Show("SendStatus error: " + error.Code + " " + error.Message, "Result ERROR", MessageBoxButton.OK));
                });
        }
#endif

        private void UpdateItemsAsync(int offset, int maxId, int count, bool isAnimated)
        {
            if (IsBroadcast && !IsChannel)
            {
                return;
            }

            _isUpdating = true;
            IsWorking = true;
#if WP8
            count = 22;
#endif
            MTProtoService.GetHistoryAsync("UpdateItemsAsync",
                Peer, 
                TLUtils.InputPeerToPeer(Peer, StateService.CurrentUserId), 
                !IsChannel, 
                new TLInt(0), 
                new TLInt(maxId), 
                new TLInt(count),
                result => 
                {
                    ProcessRepliesAndAudio(result.Messages);
                    BeginOnUIThread(() =>
                    {
                        // all history is new and has no messages with Index = 0
                        var lastMessage = result.Messages.LastOrDefault();
                        if (lastMessage != null)
                        {
                            var lastId = lastMessage.Index;

                            var firstMessage = Items.FirstOrDefault(x => x.Index != 0);
                            var hasSendingMessages = Items.FirstOrDefault(x => x.Index == 0) != null;
                            if (firstMessage != null && !hasSendingMessages)
                            {
                                var firstId = firstMessage.Index;
                                if (lastId > firstId)
                                {
                                    Items.Clear();
                                }
                            }
                        }

                        foreach (var message in result.Messages)
                        {
                            message._isAnimated = isAnimated;
                        }

                        IsEmptyDialog = Items.Count == 0 && result.Messages.Items.Count == 0;

                        // remove tail
                        IList<TLMessageBase> removedItems;
                        MergeItems(Items, result.Messages.Items, offset, maxId, count, out removedItems);

                        _isUpdating = false;
                        _isUpdated = true;
                        IsWorking = false;
                    });
                },
                error =>
                {
                    Execute.ShowDebugMessage("messages.getHistory error " + error);
                    _isUpdating = false;
                    _isUpdated = true;
                    IsWorking = false;
                });
        }

        private int MergeItems(IList<TLMessageBase> current, IList<TLMessageBase> updated, int offset, int maxId, int count, out IList<TLMessageBase> removedItems)
        {
            TLInt migratedFromChatId = null;
            var channel = With as TLChannel;
            if (channel != null)
            {
                migratedFromChatId = channel.MigratedFromChatId;
            }

            var lastIndex = TLUtils.MergeItemsDesc(x => x.DateIndex, current, updated, offset, maxId, count, out removedItems, x => x.Index,
                m =>
                {
                    return IsLocalServiceMessage(m) || IsChatHistory(migratedFromChatId, m);
                });


            return lastIndex;
        }

        private static bool IsChatHistory(TLInt migratedFromChatId, TLMessageBase messageBase)
        {
            if (migratedFromChatId == null) return false;

            var message = messageBase as TLMessageCommon;
            if (message != null)
            {
                if (message.ToId is TLPeerChat)
                {
                    if (message.ToId.Id.Value == migratedFromChatId.Value)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsLocalServiceMessage(TLMessageBase messageBase)
        {
            var messageService = messageBase as TLMessageService;
            if (messageService != null)
            {
                var action = messageService.Action;
                if (action is TLMessageActionContactRegistered)
                {
                    return true;
                }
            }

            return false;
        }

        private volatile bool _isUpdating;

        private volatile bool _isUpdated;

        public bool SliceLoaded { get; set; }

        private bool _loadingNextSlice;

        public void LoadNextSlice()
        {
            if (IsBroadcast && !IsChannel)
            {
                return;
            }

            if (_loadingNextSlice)
            {
                return;
            }

            if (
#if WP8
                _isUpdating || 
                !_isUpdated ||
#endif                
                IsWorking || LazyItems.Count > 0) return;

            if (IsLastSliceLoaded)
            {
                LoadNextMigratedHistorySlice(Thread.CurrentThread.ManagedThreadId + " ilsl");
                return;
            }

            var channel = With as TLChannel;
            if (channel != null)
            {
                var lastMessage = Items.LastOrDefault() as TLMessageCommon;
                if (lastMessage != null
                    && lastMessage.ToId is TLPeerChat)
                {
                    LoadNextMigratedHistorySlice(Thread.CurrentThread.ManagedThreadId + " ch");
                    return;
                }
            }


            IsWorking = true;
            var maxMessageId = int.MaxValue;
            for (var i = 0; i < Items.Count; i++)
            {
                if (Items[i].Index != 0
                    && Items[i].Index < maxMessageId)
                {
                    maxMessageId = Items[i].Index;
                }
            }

            if (maxMessageId == int.MaxValue)
            {
                maxMessageId = 0;
            }

            _loadingNextSlice = true;
            //var maxMessageId = maxMessage != null ? maxMessage.Index : 0;
            MTProtoService.GetHistoryAsync(Thread.CurrentThread.ManagedThreadId + " LoadNextSlice " + IsWorking,
                Peer,
                TLUtils.InputPeerToPeer(Peer, StateService.CurrentUserId), 
                Items.Count < 1.5 * Constants.MessagesSlice, 
                new TLInt(0), 
                new TLInt(maxMessageId), 
                new TLInt(Constants.MessagesSlice),
                result =>
                {
#if WP8
                    ProcessRepliesAndAudio(result.Messages);

                    BeginOnUIThread(() =>
                    {
                        _loadingNextSlice = false;
                        IsWorking = false;
                        SliceLoaded = true;

                        foreach (var message in result.Messages)
                        {
                            //message.IsAnimated = false;
                            if (!SkipMessage(message))
                            {
                                Items.Add(message);
                            }
                        }

                        IsEmptyDialog = Items.Count == 0 && LazyItems.Count == 0;

                        if (result.Messages.Count < Constants.MessagesSlice)
                        {
                            IsLastSliceLoaded = true;
                            LoadNextMigratedHistorySlice(Thread.CurrentThread.ManagedThreadId + " gh");
                        }
                    });
#else
                    ProcessReplies(result.Messages);

                    IsWorking = false;
                    SliceLoaded = true;
                    IsEmptyDialog = Items.Count == 0 && LazyItems.Count == 0;

                    foreach (var message in result.Messages)
                    {
                        message._isAnimated = false;
                        LazyItems.Add(message);
                    }

                    BeginOnUIThread(PopulateItems);
#endif
                },
                error => BeginOnUIThread(() =>
                {
                    _loadingNextSlice = false;
                    IsWorking = false;
                    Status = string.Empty;
                    Execute.ShowDebugMessage("messages.getHistory error " + error);
                }));
        }

        private bool _isLastMigratedHistorySliceLoaded;

        private bool _isLoadingNextMigratedHistorySlice;

        private void LoadNextMigratedHistorySlice(string debugInfo)
        {
            var channel = With as TLChannel;
            if (channel == null || channel.MigratedFromChatId == null) return;

            if (_isLastMigratedHistorySliceLoaded) return;

            if (IsWorking || LazyItems.Count > 0) return;

            if (_isLoadingNextMigratedHistorySlice) return;

            var maxMessageId = int.MaxValue;
            for (var i = 0; i < Items.Count; i++)
            {
                var messageCommon = Items[i] as TLMessageCommon;
                if (messageCommon == null) continue;

                var peerChat = messageCommon.ToId as TLPeerChat;
                if (peerChat == null) continue;

                if (Items[i].Index != 0
                    && Items[i].Index < maxMessageId)
                {
                    maxMessageId = Items[i].Index;
                }
            }

            if (maxMessageId == int.MaxValue)
            {
                maxMessageId = channel.MigratedFromMaxId != null? channel.MigratedFromMaxId.Value : 0;
            }

            _isLoadingNextMigratedHistorySlice = true;
            IsWorking = true;
            MTProtoService.GetHistoryAsync(debugInfo + " LoadNextMigratedHistorySlice",
                new TLInputPeerChat{ ChatId = channel.MigratedFromChatId }, 
                new TLPeerChat{ Id = channel.MigratedFromChatId }, 
                false,
                new TLInt(0),
                new TLInt(maxMessageId),
                new TLInt(Constants.MessagesSlice),
                result =>
                {
                    ProcessRepliesAndAudio(result.Messages);

                    BeginOnUIThread(() =>
                    {
                        _isLoadingNextMigratedHistorySlice = false;
                        IsWorking = false;

                        if (result.Messages.Count < Constants.MessagesSlice)
                        {
                            _isLastMigratedHistorySliceLoaded = true;
                        }
                        foreach (var message in result.Messages)
                        {
                            //message.IsAnimated = false;
                            Items.Add(message);
                        }

                        IsEmptyDialog = Items.Count == 0 && LazyItems.Count == 0;
                    });
                },
                error => BeginOnUIThread(() =>
                {
                    _isLoadingNextMigratedHistorySlice = false;
                    IsWorking = false;
                    Status = string.Empty;
                    Execute.ShowDebugMessage("messages.getHistory error " + error);
                }));
        }

        private bool _isPreviousSliceLoading;

        private bool _isFirstSliceLoaded = true;

        public bool IsFirstSliceLoaded
        {
            get { return _isFirstSliceLoaded; }
            set { SetField(ref _isFirstSliceLoaded, value, () => IsFirstSliceLoaded); }
        }

        private bool _holdScrollingPosition;

        public bool HoldScrollingPosition
        {
            get { return _holdScrollingPosition; }
            set { SetField(ref _holdScrollingPosition, value, () => HoldScrollingPosition); }
        }

        public void LoadPreviousSlice()
        {
            if (IsBroadcast && !IsChannel)
            {
                return;
            }

            if (_isPreviousSliceLoading
                || _isFirstSliceLoaded) return;

            _isPreviousSliceLoading = true;
            var maxMessageId = 0;
            var channel = With as TLChannel;
            for (var i = 0; i < Items.Count; i++)
            {
                if (channel != null && channel.MigratedFromChatId != null)
                {
                    var messageCommon = Items[i] as TLMessageCommon;
                    if (messageCommon != null && messageCommon.ToId is TLPeerChat)
                    {
                        continue;
                    }
                }

                if (Items[i].Index != 0
                    && Items[i].Index > maxMessageId)
                {
                    maxMessageId = Items[i].Index;
                }
            }

            IsWorking = true;
            MTProtoService.GetHistoryAsync("LoadPreviousSlice",
                Peer,
                TLUtils.InputPeerToPeer(Peer, StateService.CurrentUserId),
                false,
                new TLInt(-Constants.MessagesSlice),
                new TLInt(maxMessageId),
                new TLInt(Constants.MessagesSlice),
                result =>
                {
                    ProcessRepliesAndAudio(result.Messages);

                    BeginOnUIThread(() =>
                    {
                        IsWorking = false;
                        _isPreviousSliceLoading = false;
                        IsEmptyDialog = Items.Count == 0 && LazyItems.Count == 0;

                        if (result.Messages.Count < Constants.MessagesSlice)
                        {
                            IsFirstSliceLoaded = true;
                        }

                        HoldScrollingPosition = true;
                        for (var i = result.Messages.Count; i > 0; i--)
                        {
                            var message = result.Messages[i - 1];
                            if (message.Index > maxMessageId)
                            {
                                Items.Insert(0, result.Messages[i - 1]);
                            }
                        }
                        HoldScrollingPosition = false;
                    });
                },
                error =>
                {
                    IsWorking = false;
                    _isPreviousSliceLoading = false;
                    Status = string.Empty;
                    Execute.ShowDebugMessage("messages.getHistory error " + error);
                });
        }

        private void ReadHistoryAsync()
        {
            BeginOnThreadPool(() =>
            {
                var haveUnreadMessages = false;

                _currentDialog = _currentDialog ?? CacheService.GetDialog(TLUtils.InputPeerToPeer(Peer, StateService.CurrentUserId));
                if (_currentDialog == null)
                {
                    var inputPeerChannel = Peer as TLInputPeerChannel;
                    if (inputPeerChannel != null)
                    {
                        _currentDialog = DialogsViewModel.GetChannel(inputPeerChannel.ChatId) as TLDialog;
                    }
                }

                if (_currentDialog == null) return;

                var maxId = 0;
                var topMessage = _currentDialog.TopMessage as TLMessageCommon;
                if (topMessage != null
                    && !topMessage.Out.Value
                    && topMessage.Unread.Value)
                    //&& !topMessage.IsAudioVideoMessage())
                {
                    maxId = topMessage.Index;
                    haveUnreadMessages = true;
                }

                if (!haveUnreadMessages)
                {
                    for (var i = 0; i < 10 && i < Items.Count; i++)
                    {
                        var messageCommon = Items[i] as TLMessageCommon;
                        if (messageCommon != null
                            && !messageCommon.Out.Value
                            && messageCommon.Unread.Value)
                            //&& !messageCommon.IsAudioVideoMessage())
                        {
                            maxId = maxId > messageCommon.Index ? maxId : messageCommon.Index;
                            haveUnreadMessages = true;
                            break;
                        }
                    }
                }

                if (!haveUnreadMessages) return;

                StateService.GetNotifySettingsAsync(settings =>
                {
                    if (settings.InvisibleMode) return;

                    SetRead(topMessage, d => new TLInt(0));

                    var channel = With as TLChannel;
                    if (channel != null)
                    {
                        MTProtoService.ReadHistoryAsync(channel, new TLInt(maxId),
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
                        MTProtoService.ReadHistoryAsync(Peer, new TLInt(maxId), new TLInt(0),
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
            });           
        }

        private void ReadMessageContents(TLMessage25 message)
        {
            if (message == null) return;

            if (message.Index != 0 && !message.Out.Value && message.NotListened)
            {
                MTProtoService.ReadMessageContentsAsync(new TLVector<TLInt> { message.Id },
                    result =>
                    {
                        message.SetListened();
                        message.Media.NotListened = false;
                        message.Media.NotifyOfPropertyChange(() => message.Media.NotListened);

                        CacheService.Commit();
                    },
                    error => Execute.ShowDebugMessage("messages.readMessageContents error " + error));
            }
        }

        private void DeleteChannelInternal(TLInt channelId)
        {
            BeginOnUIThread(() =>
            {
                MessageBox.Show(AppResources.ChannelIsNoLongerAccessible, AppResources.Error, MessageBoxButton.OK);

                TLDialogBase dialog = _currentDialog ?? CacheService.GetDialog(new TLPeerChannel { Id = channelId });
                if (dialog == null)
                {
                    dialog = DialogsViewModel.GetChannel(channelId);
                }
                if (dialog != null)
                {
                    EventAggregator.Publish(new DialogRemovedEventArgs(dialog));
                    CacheService.DeleteDialog(dialog);
                    DialogsViewModel.UnpinFromStart(dialog);
                }
                if (NavigationService.CanGoBack)
                {
                    NavigationService.GoBack();
                }
                else
                {
                    NavigateToShellViewModel();
                }
            });
        }


        private void GetFullInfo()
        {
            if (Peer is TLInputPeerChannel)
            {
                var channelForbidden = With as TLChannelForbidden;
                if (channelForbidden != null)
                {
                    DeleteChannelInternal(channelForbidden.Id);
                    return;
                }

                var channel = With as TLChannel;
                if (channel == null)
                {
                    return;
                }

                MTProtoService.GetFullChannelAsync(channel.ToInputChannel(),
                    result =>
                    {
                        channel.ChatPhoto = result.FullChat.ChatPhoto;
                        channel.Participants = result.FullChat.Participants;
                        channel.NotifySettings = result.FullChat.NotifySettings;
                        var channelFull = result.FullChat as TLChannelFull;
                        if (channelFull != null)
                        {
                            channel.ExportedInvite = channelFull.ExportedInvite;
                            channel.About = channelFull.About;
                            channel.ParticipantsCount = channelFull.ParticipantsCount;
                            channel.AdminsCount = channelFull.AdminsCount;
                            channel.KickedCount = channelFull.KickedCount;
                        }

                        var channelFull41 = result.FullChat as TLChannelFull41;
                        if (channelFull41 != null)
                        {
                            channel.MigratedFromChatId = channelFull41.MigratedFromChatId;
                            channel.MigratedFromMaxId = channelFull41.MigratedFromMaxId;
                            channel.BotInfo = channelFull41.BotInfo;
                        }

                        BeginOnUIThread(() =>
                        {
                            NotifyOfPropertyChange(() => HasBots);
                            NotifyOfPropertyChange(() => With);
                            Subtitle = GetSubtitle();

                            if (channel.IsKicked)
                            {
                                DeleteChannelInternal(channel.Id);
                            }
                        });
                    },
                    error => BeginOnUIThread(() =>
                    {
                        if (error.TypeEquals(ErrorType.CHANNEL_PRIVATE))
                        {
                            DeleteChannelInternal(channel.Id);
                        } 
                    }));
            }
            else if (Peer is TLInputPeerBroadcast)
            {
                return;
            }
            else if (Peer is TLInputPeerChat)
            {
                var chat = With as TLChatBase;
                if (chat == null)
                {
                    return;
                }

                MTProtoService.GetFullChatAsync(chat.Id,
                    result =>
                    {
                        var newUsersCache = new Dictionary<int, TLUserBase>();
                        foreach (var user in result.Users)
                        {
                            newUsersCache[user.Index] = user;
                        }

                        chat.ChatPhoto = result.FullChat.ChatPhoto;
                        chat.Participants = result.FullChat.Participants;
                        chat.NotifySettings = result.FullChat.NotifySettings;
                        var chatFull28 = result.FullChat as TLChatFull28;
                        if (chatFull28 != null)
                        {
                            chat.ExportedInvite = chatFull28.ExportedInvite;
                        }
                        var chatFull31 = result.FullChat as TLChatFull31;
                        if (chatFull31 != null)
                        {
                            chat.BotInfo = chatFull31.BotInfo;
                            foreach (var botInfoBase in chatFull31.BotInfo)
                            {
                                var botInfo = botInfoBase as TLBotInfo;
                                if (botInfo != null)
                                {
                                    TLUserBase user;
                                    if (newUsersCache.TryGetValue(botInfo.UserId.Value, out user))
                                    {
                                        user.BotInfo = botInfo;
                                    }
                                }
                            }
                        }

                        var participants = result.FullChat.Participants as IChatParticipants;
                        if (participants != null)
                        {
                            var onlineUsers = 0;
                            foreach (var participant in participants.Participants)
                            {
                                var user = newUsersCache[participant.UserId.Value];
                                if (user.Status is TLUserStatusOnline)
                                {
                                    onlineUsers++;
                                }
                            }
                            chat.UsersOnline = onlineUsers;
                        }

                        BeginOnUIThread(() =>
                        {
                            NotifyOfPropertyChange(() => HasBots);
                            NotifyOfPropertyChange(() => With);
                            Subtitle = GetSubtitle();
                        });
                    });
            }
            else
            {
                var user = With as TLUserBase;
                if (user == null)
                {
                    return;
                }

                MTProtoService.GetFullUserAsync(user.ToInputUser(),
                    userFull =>
                    {
                        user.Link = userFull.Link;
                        user.ProfilePhoto = userFull.ProfilePhoto;
                        user.NotifySettings = userFull.NotifySettings;
                        user.Blocked = userFull.Blocked;
                        var userFull31 = userFull as TLUserFull31;
                        if (userFull31 != null)
                        {
                            user.BotInfo = userFull31.BotInfo;
                        }

                        BeginOnUIThread(() =>
                        {
                            NotifyOfPropertyChange(() => HasBots);
                            NotifyOfPropertyChange(() => With);
                            Subtitle = GetSubtitle();
                        });
                    });
            }
        }

        private void SendLogs()
        {
            if (StateService.LogFileName != null)
            {
                var logFileName = StateService.LogFileName;

                SendDocument(logFileName);

                StateService.LogFileName = null;
            }
        }

        private void SendSharedPhoto()
        {
            if (!string.IsNullOrEmpty(StateService.FileId))
            {
                var fileId = StateService.FileId;
                StateService.FileId = null;

                BeginOnUIThread(() =>
                {
                    var result = MessageBox.Show(AppResources.ForwardeMessageToThisChat, AppResources.Confirm, MessageBoxButton.OKCancel);
                    if (result != MessageBoxResult.OK) return;

                    BeginOnThreadPool(() =>
                    {


                        // Retrieve the photo from the media library using the FileID passed to the app.
                        var library = new MediaLibrary();
                        var photoFromLibrary = library.GetPictureFromToken(fileId);
                        var image = photoFromLibrary.GetImage();
                        var stream = new MemoryStream((int)image.Length);
                        image.CopyTo(stream);
                        var photo = new Photo
                        {
                            FileName = photoFromLibrary.Name,
                            Bytes = stream.ToArray(),
                            Width = photoFromLibrary.Width,
                            Height = photoFromLibrary.Height
                        };

                        SendPhoto(photo);
                    });
                });
            }
        }

        private void SendForwardMessages()
        {
            if (With is TLBroadcastChat && !(With is TLChannel)) return;

            if (StateService.ForwardMessages != null)
            {
                var messages = StateService.ForwardMessages;
                StateService.ForwardMessages = null;

                var fwdMessages25 = new TLVector<TLMessage25>();
                var fwdIds = new TLVector<TLInt>();
                foreach (var messageBase in messages)
                {
                    var message = messageBase as TLMessage;
                    if (message == null) continue;
                    if (message.Index <= 0) continue;

                    var fwdFromId = message.FromId;
                    
                    var messageForwarded = message as TLMessageForwarded;
                    if (messageForwarded != null)
                    {
                        fwdFromId = messageForwarded.FwdFromId;
                    }

                    var message25 = message as TLMessage25;
                    if (message25 != null)
                    {
                        if (message25.FwdFromId != null && message25.FwdFromId.Value != 0)
                        {
                            fwdFromId = message25.FwdFromId;
                        }
                    }

                    var fwdMessage = GetMessage(message.Message, message.Media) as TLMessage40;
                    if (fwdMessage == null) continue;
                    
                    if (fwdFromId != null && fwdFromId.Value <= 0)
                    {
                        fwdMessage.FwdFromPeer = message.ToId;
                    }
                    else if (message.ToId is TLPeerChannel)
                    {
                        fwdMessage.FwdFromPeer = message.ToId;
                    }
                    else
                    {
                        fwdMessage.FwdFromPeer = new TLPeerUser{ Id = fwdFromId };
                    }
                    fwdMessage.FwdDate = message.Date;
                    fwdMessage.SetFwd();

                    var message40 = message as TLMessage40;
                    if (message40 != null)
                    {
                        fwdMessage.Views = message40.Views;
                    }

                    fwdIds.Add(message.Id);
                    fwdMessages25.Add(fwdMessage);
                }

                var container = new TLMessagesContainter
                {
                    FwdMessages = fwdMessages25, 
                    FwdIds = fwdIds
                };

                Reply = container;
            }
        }

        private static void SendForwardedMessages(IMTProtoService mtProtoService, TLInputPeerBase peer, TLMessageBase message)
        {
            var messagesContainer = message.Reply as TLMessagesContainter;
            if (messagesContainer != null)
            {
                SendForwardMessagesInternal(mtProtoService, peer, messagesContainer.FwdIds, messagesContainer.FwdMessages);

                message.Reply = null;
            }
        }

        private void SendForwardMessageInternal(TLInt fwdMessageId, TLMessage25 message)
        {
            MTProtoService.ForwardMessageAsync(
                Peer, fwdMessageId,
                message,
                result =>
                {
                    message.Status = MessageStatus.Confirmed;
                },
                error => BeginOnUIThread(() =>
                {
                    if (error.TypeEquals(ErrorType.PEER_FLOOD))
                    {
                        MessageBox.Show(AppResources.PeerFloodSendMessage, AppResources.Error, MessageBoxButton.OK);
                    }
                    else
                    {
                        Execute.ShowDebugMessage("messages.forward error " + error);
                    }
                    Status = string.Empty;
                    if (message.Status == MessageStatus.Sending)
                    {
                        message.Status = message.Index != 0? MessageStatus.Confirmed : MessageStatus.Failed;
                    }
                }));
        }

        private static void SendForwardMessagesInternal(IMTProtoService mtProtoService, TLInputPeerBase toPeer, TLVector<TLInt> fwdMessageIds, IList<TLMessage25> messages)
        {
            mtProtoService.ForwardMessagesAsync(
                toPeer, fwdMessageIds,
                messages,
                result =>
                {
                    foreach (var message in messages)
                    {
                        message.Status = MessageStatus.Confirmed;
                    }
                },
                error => Execute.BeginOnUIThread(() =>
                {
                    if (error.TypeEquals(ErrorType.PEER_FLOOD))
                    {
                        MessageBox.Show(AppResources.PeerFloodSendMessage, AppResources.Error, MessageBoxButton.OK);
                    }
                    else
                    {
                        Execute.ShowDebugMessage("messages.forwardMessages error " + error);
                    }
                    foreach (var message in messages)
                    {
                        if (message.Status == MessageStatus.Sending)
                        {
                            message.Status = message.Index != 0 ? MessageStatus.Confirmed : MessageStatus.Failed;
                        }
                    }
                }));
        }

        private bool _isOnline;

        public string RandomParam;

        private bool _isActive;

        protected override void OnActivate()
        {
            BrowserNavigationService.TelegramLinkAction += OnTelegramLinkAction;
            BrowserNavigationService.MentionNavigated += OnMentionNavigated;
            BrowserNavigationService.SearchHashtag += OnSearchHashtag;
            BrowserNavigationService.InvokeCommand += OnInvokeCommand;

            base.OnActivate();
        }

        private void OnInvokeCommand(object sender, TelegramCommandEventArgs e)
        {
            var commandIndex = e.Command.LastIndexOf('/');
            if (commandIndex != -1)
            {
                var command = e.Command.Substring(commandIndex);

                if (With is TLChatBase)
                {
                    var message31 = e.Message as TLMessage31;
                    if (message31 != null && !message31.Out.Value)
                    {
                        var user = CacheService.GetUser(message31.FromId) as TLUser;
                        if (user != null && user.IsBot && !command.Contains("@") && !IsSingleBot)
                        {
                            command += string.Format("@{0}", user.UserName);
                        }
                    }
                }

                if (!string.IsNullOrEmpty(command))
                {
                    _text = command;
                    SendInternal(false, true);
                }
            }
        }

        private void OnSearchHashtag(object sender, TelegramHashtagEventArgs e)
        {
            var hashtagIndex = e.Hashtag.IndexOf('#');
            if (hashtagIndex != -1)
            {
                var hashtag = e.Hashtag.Substring(hashtagIndex);

                if (!string.IsNullOrEmpty(hashtag))
                {
                    TelegramViewBase.NavigateToHashtag(hashtag);
                }
            }
        }

        private void OnMentionNavigated(object sender, TelegramMentionEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Mention))
            {
                var usernameStartIndex = e.Mention.LastIndexOf("@", StringComparison.OrdinalIgnoreCase);
                if (usernameStartIndex != -1)
                {
                    var username = e.Mention.Substring(usernameStartIndex).TrimStart('@');

                    if (!string.IsNullOrEmpty(username))
                    {
                        TelegramViewBase.NavigateToUsername(MTProtoService, username, string.Empty, PageKind.Profile);
                    }
                }
            }
            else if (e.UserId > 0)
            {
                var user = CacheService.GetUser(new TLInt(e.UserId));
                if (user != null)
                {
                    TelegramViewBase.NavigateToUser(user, null, PageKind.Profile);
                }
            }
            else if (e.ChatId > 0)
            {
                var chat = CacheService.GetChat(new TLInt(e.ChatId));
                if (chat != null)
                {
                    TelegramViewBase.NavigateToChat(chat);
                }
            }
            else if (e.ChannelId > 0)
            {
                var channel = CacheService.GetChat(new TLInt(e.ChannelId)) as TLChannel;
                if (channel != null)
                {
                    TelegramViewBase.NavigateToChat(channel);
                }
            }
        }

        public static void OnTelegramLinkActionCommon(IMTProtoService mtProtoService, IStateService stateService, TelegramEventArgs e)
        {
            if (e.Uri.Contains("joinchat"))
            {
                var hashStartIndex = e.Uri.TrimEnd('/').LastIndexOf("/", StringComparison.OrdinalIgnoreCase);
                if (hashStartIndex != -1)
                {
                    var hash = e.Uri.Substring(hashStartIndex).Replace("/", string.Empty);

                    if (!string.IsNullOrEmpty(hash))
                    {
                        TelegramViewBase.NavigateToInviteLink(mtProtoService, hash);
                    }
                }

                return;
            }

            if (e.Uri.Contains("addstickers"))
            {
                var shortNameStartIndex = e.Uri.TrimEnd('/').LastIndexOf("/", StringComparison.OrdinalIgnoreCase);
                if (shortNameStartIndex != -1)
                {
                    var shortName = e.Uri.Substring(shortNameStartIndex).Replace("/", string.Empty);

                    if (!string.IsNullOrEmpty(shortName))
                    {
                        var inputStickerSet = new TLInputStickerSetShortName { ShortName = new TLString(shortName) };
                        TelegramViewBase.NavigateToStickers(mtProtoService, stateService, inputStickerSet);
                    }
                }

                return;
            }

            var tempUri = HttpUtility.UrlDecode(e.Uri);

            Dictionary<string, string> uriParams = null;
            try
            {
                uriParams = TelegramUriMapper.ParseQueryString(tempUri);
            }
            catch (Exception ex)
            {
                Execute.ShowDebugMessage("Parse uri exception " + tempUri + ex);
            }
            PageKind pageKind;
            var accessToken = TelegramViewBase.GetAccessToken(uriParams, out pageKind);

            var eqLastIndex = e.Uri.LastIndexOf('?');
            var uri = eqLastIndex != -1 ? e.Uri.Substring(0, eqLastIndex).TrimEnd('/') : e.Uri.TrimEnd('/');

            var usernameStartIndex = uri.LastIndexOf('/');
            if (usernameStartIndex != -1)
            {
                var username = uri.Substring(usernameStartIndex).Replace("/", string.Empty);

                if (!string.IsNullOrEmpty(username))
                {
                    TelegramViewBase.NavigateToUsername(mtProtoService, username, accessToken, pageKind);
                }
            }
        }

        private void OnTelegramLinkAction(object sender, TelegramEventArgs e)
        {
            OnTelegramLinkActionCommon(MTProtoService, StateService, e);
        }

        protected override void OnDeactivate(bool close)
        {
            BrowserNavigationService.TelegramLinkAction -= OnTelegramLinkAction;
            BrowserNavigationService.MentionNavigated -= OnMentionNavigated;
            BrowserNavigationService.SearchHashtag -= OnSearchHashtag;
            BrowserNavigationService.InvokeCommand -= OnInvokeCommand;

            InputTypingManager.Stop();

            base.OnDeactivate(close);
        }

        private string GetTypingSubtitle(IList<Telegram.Api.WindowsPhone.Tuple<int, TLSendMessageActionBase>> typingUsers)
        {
            if (With is TLUserBase)
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
                    if (action is TLSendMessageUploadDocumentAction)
                    {
                        return string.Format("{0}...", AppResources.SendingFile.ToLower(CultureInfo.InvariantCulture));
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
                var user = CacheService.GetUser(new TLInt(typingUsers[0].Item1));
                var userName = TLString.IsNullOrEmpty(user.FirstName) ? user.LastName : user.FirstName;
                var typingUser = typingUsers.FirstOrDefault();
                if (typingUser != null)
                {
                    var action = typingUser.Item2;
                    if (action is TLSendMessageUploadPhotoAction)
                    {
                        return string.Format("{0} {1}...", userName, AppResources.IsSendingPhoto.ToLower(CultureInfo.InvariantCulture));
                    }
                    if (action is TLSendMessageRecordAudioAction)
                    {
                        return string.Format("{0} {1}...", userName, AppResources.IsRecordingAudio.ToLower(CultureInfo.InvariantCulture));
                    }
                    if (action is TLSendMessageUploadDocumentAction)
                    {
                        return string.Format("{0} {1}...", userName, AppResources.IsSendingFile.ToLower(CultureInfo.InvariantCulture));
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
                foreach (var typingUser in typingUsers)
                {
                    var user = CacheService.GetUser(new TLInt(typingUser.Item1));
                    if (user != null)
                    {
                        var userName = TLString.IsNullOrEmpty(user.FirstName) ? user.LastName : user.FirstName;
                        firstNames.Add(userName.ToString());
                    }
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

        private string GetSubtitle()
        {
            var channel = With as TLChannel;
            if (channel != null)
            {
                if (channel.ParticipantsCount != null)
                {
                    return Language.Declension(
                        channel.ParticipantsCount.Value,
                        AppResources.CompanyNominativeSingular,
                        AppResources.CompanyNominativePlural,
                        AppResources.CompanyGenitiveSingular,
                        AppResources.CompanyGenitivePlural).ToLower(CultureInfo.CurrentUICulture);
                }

                return channel.IsPublic? AppResources.PublicChannel.ToLowerInvariant() : AppResources.PrivateChannel.ToLowerInvariant();
            }

            var user = With as TLUserBase;
            if (user != null)
            {
                return GetUserStatus(user);
            }

            var chat = With as TLChat;
            if (chat != null)
            {
                var participantsCount = chat.ParticipantsCount.Value;
                var onlineCount = chat.UsersOnline;
                var onlineString = onlineCount > 0 ? string.Format(", {0} {1}", chat.UsersOnline, AppResources.Online.ToLowerInvariant()) : string.Empty;

                var currentUser = CacheService.GetUser(new TLInt(StateService.CurrentUserId));
                var isCurrentUserOnline = currentUser != null && currentUser.Status is TLUserStatusOnline;
                if (participantsCount == 1 || (onlineCount == 1 && isCurrentUserOnline))
                {
                    onlineString = string.Empty;
                }

                return Language.Declension(
                    participantsCount,
                    AppResources.CompanyNominativeSingular,
                    AppResources.CompanyNominativePlural,
                    AppResources.CompanyGenitiveSingular,
                    AppResources.CompanyGenitivePlural).ToLower(CultureInfo.CurrentUICulture)
                    + onlineString;
            }

            var forbiddenChat = With as TLChatForbidden;
            if (forbiddenChat != null)
            {
                return LowercaseConverter.Convert(AppResources.YouWhereKickedFromTheGroup);
            }

            var broadcastChat = With as TLBroadcastChat;
            if (broadcastChat != null)
            {
                var participantsCount = broadcastChat.ParticipantIds.Count;
                var onlineParticipantsCount = 0;
                foreach (var participantId in broadcastChat.ParticipantIds)
                {
                    var participant = CacheService.GetUser(participantId);
                    if (participant != null && participant.Status is TLUserStatusOnline)
                    {
                        onlineParticipantsCount++;
                    }
                }

                var onlineString = onlineParticipantsCount > 0 ? string.Format(", {0} {1}", onlineParticipantsCount, AppResources.Online.ToLowerInvariant()) : string.Empty;

                return Language.Declension(
                    participantsCount,
                    AppResources.CompanyNominativeSingular,
                    AppResources.CompanyNominativePlural,
                    AppResources.CompanyGenitiveSingular,
                    AppResources.CompanyGenitivePlural).ToLower(CultureInfo.CurrentUICulture)
                    + onlineString;
            } 
            
            return string.Empty;
        }

        public static string GetUserStatus(TLUserBase user)
        {
            if (user.BotInfo is TLBotInfo)
            {
                return AppResources.Bot.ToLowerInvariant();
            }

            return UserStatusToStringConverter.Convert(user.Status);
        }

        public void ShowLastSyncErrors(Action<string> callback = null)
        {
            MTProtoService.GetSyncErrorsAsync((syncMessageError, processDifferenceErrors) =>
            {
                var info = new StringBuilder();

                info.AppendLine("syncMessage last error: ");
                info.AppendLine(syncMessageError == null ? "none" : syncMessageError.ToString());
                info.AppendLine();
                info.AppendLine("syncDifference last error: ");
                if (processDifferenceErrors == null || processDifferenceErrors.Count == 0)
                {
                    info.AppendLine("none");
                }
                else
                {
                    foreach (var processDifferenceError in processDifferenceErrors)
                    {
                        info.AppendLine(processDifferenceError.ToString());
                    }
                }

                var infoString = info.ToString();
                Execute.BeginOnUIThread(() => MessageBox.Show(infoString));

                callback.SafeInvoke(infoString);
            });
        }

        public void ShowMessagesInfo(int limit = 15, Action<string> callback=null)
        {
            MTProtoService.GetSendingQueueInfoAsync(queueInfo =>
            {
                var info = new StringBuilder();

                info.AppendLine("Queue: ");
                info.AppendLine(queueInfo);

                var dialogMessages = Items.Take(limit);
                info.AppendLine("Dialog: ");
                var count = 0;
                foreach (var dialogMessage in dialogMessages)
                {
                    info.AppendLine("  " + count++ + " " + dialogMessage);
                }

                dialogMessages = CacheService.GetHistory(new TLInt(StateService.CurrentUserId), TLUtils.InputPeerToPeer(Peer, StateService.CurrentUserId), limit);
                info.AppendLine();
                info.AppendLine("Database: ");
                count = 0;
                foreach (var dialogMessage in dialogMessages)
                {
                    info.AppendLine("  " + count++ + " " + dialogMessage);
                }
                var infoString = info.ToString();
                Execute.BeginOnUIThread(() => MessageBox.Show(infoString));

                callback.SafeInvoke(infoString);
            });
        }

        private void ShowConfigInfo(Action<string> callback)
        {
            MTProtoService.GetConfigInformationAsync(callback.SafeInvoke);
        }

        private void ShowTransportInfo(Action<string> callback)
        {
            MTProtoService.GetTransportInformationAsync(callback.SafeInvoke);
        }

        public void OnNavigatedTo()
        {
            _isActive = true;
            StateService.ActiveDialog = With;
        }

        public void OnNavigatedFrom()
        {
            _isActive = false;
            StateService.ActiveDialog = null;
        }

        private InputTypingManager _inputTypingManager;

        public InputTypingManager InputTypingManager
        {
            get { return _inputTypingManager = _inputTypingManager ?? new InputTypingManager(users => Subtitle = GetTypingSubtitle(users), () => Subtitle = GetSubtitle()); }
        }

        private OutputTypingManager _textTypingManager;

        public OutputTypingManager TextTypingManager
        {
            get { return _textTypingManager = _textTypingManager ?? new OutputTypingManager(Peer, new TLSendMessageTypingAction(), 5.0, MTProtoService); }
        }

        private OutputTypingManager _audioTypingManager;

        public OutputTypingManager AudioTypingManager
        {
            get { return _audioTypingManager = _audioTypingManager ?? new OutputTypingManager(Peer, new TLSendMessageRecordAudioAction(), 5.0, MTProtoService); }
        }

        private OutputUploadTypingManager _uploadTypingManager;

        public OutputUploadTypingManager UploadTypingManager
        {
            get { return _uploadTypingManager = _uploadTypingManager ?? new OutputUploadTypingManager(Peer, 5.0, MTProtoService); }
        }

        private TLMessage25 GetMessage(TLString text, TLMessageMediaBase media)
        {
            var broadcast = With as TLBroadcastChat;
            var channel = With as TLChannel;
            var toId = channel != null 
                ? new TLPeerChannel { Id = channel.Id } 
                : broadcast != null 
                ? new TLPeerBroadcast { Id = broadcast.Id } 
                : TLUtils.InputPeerToPeer(Peer, StateService.CurrentUserId);

            var date = TLUtils.DateToUniversalTimeTLInt(MTProtoService.ClientTicksDelta, DateTime.Now);

            var message = TLUtils.GetMessage(
                new TLInt(StateService.CurrentUserId),
                toId,
                broadcast != null ? MessageStatus.Broadcast : MessageStatus.Sending,
                TLBool.True,
                TLBool.True,
                date,
                text,
                media,
                TLLong.Random(),
                new TLInt(0)
            );

            return message;
        }

        public void ReportSpam()
        {
            if (Peer is TLInputPeerBroadcast)
            {
                return;
            }

            var spamConfirmation = MessageBox.Show("Are you sure you want to report spam?", AppResources.AppName,
                MessageBoxButton.OKCancel);
            if (spamConfirmation != MessageBoxResult.OK) return;

            IsWorking = true;
            MTProtoService.ReportSpamAsync(Peer, 
                result => BeginOnUIThread(() =>
                {
                    IsWorking = false;

                    var chat = With as TLChatBase;
                    if (chat != null)
                    {
                        var confirmation = MessageBox.Show(AppResources.GroupConversationMarkedAsSpamConfirmation, AppResources.AppName, MessageBoxButton.OKCancel);
                        if (confirmation != MessageBoxResult.OK)
                        {
                            return;
                        }

                        DialogsViewModel.DeleteAndExitDialogCommon(chat, MTProtoService,
                            () => BeginOnUIThread(() =>
                            {
                                var dialog = CacheService.GetDialog(new TLPeerChat { Id = chat.Id });
                                DeleteDialogContinueCommon(dialog, StateService, EventAggregator, CacheService, NavigationService);
                            }),
                            error => BeginOnUIThread(() =>
                            {
                                Execute.ShowDebugMessage("DeleteAndExitDialogCommon error " + error);
                            }));

                        return;
                    }

                    var user = With as TLUserBase;
                    if (user != null)
                    {
                        var confirmation = MessageBox.Show(AppResources.ConversationMarkedAsSpamConfirmation, AppResources.AppName, MessageBoxButton.OKCancel);
                        if (confirmation != MessageBoxResult.OK)
                        {
                            return;
                        }

                        IsWorking = true;
                        MTProtoService.BlockAsync(user.ToInputUser(),
                            blocked => BeginOnUIThread(() =>
                            {
                                IsWorking = false;
                                user.Blocked = TLBool.True;
                                CacheService.Commit();
                                
                                DialogsViewModel.DeleteDialogCommon(user, MTProtoService,
                                    () => BeginOnUIThread(() =>
                                    {
                                        var dialog = CacheService.GetDialog(new TLPeerUser { Id = user.Id });
                                        DeleteDialogContinueCommon(dialog, StateService, EventAggregator, CacheService, NavigationService);
                                    }),
                                    error => BeginOnUIThread(() =>
                                    {
                                        Execute.ShowDebugMessage("DeleteDialogCommon error " + error);
                                    }));
                            }),
                            error => BeginOnUIThread(() =>
                            {
                                IsWorking = false;
                                Execute.ShowDebugMessage("contacts.block error " + error);
                            }));

                        return;
                    }
                }), 
                error => BeginOnUIThread(() =>
                {
                    IsWorking = false;

                }));
        }

        public static void DeleteDialogContinueCommon(TLDialogBase dialog, IStateService stateService, ITelegramEventAggregator eventAggregator, ICacheService cacheService, INavigationService navigationService)
        {
            if (dialog != null)
            {
                eventAggregator.Publish(new DialogRemovedEventArgs(dialog));
                cacheService.DeleteDialog(dialog);
                DialogsViewModel.UnpinFromStart(dialog);
            }

            if (navigationService.CanGoBack)
            {
                navigationService.GoBack();
            }
            else
            {
                NavigateToShellViewModel(stateService, navigationService);
            }
        }
    }

    public class InputTypingManager
    {
        private readonly object _typingUsersSyncRoot = new object();

        private readonly Dictionary<int, Telegram.Api.WindowsPhone.Tuple<DateTime, TLSendMessageActionBase>> _typingUsersCache = new Dictionary<int, Telegram.Api.WindowsPhone.Tuple<DateTime, TLSendMessageActionBase>>();

        private readonly Timer _typingUsersTimer;

        private readonly System.Action<IList<Telegram.Api.WindowsPhone.Tuple<int, TLSendMessageActionBase>>> _typingCallback;

        private readonly System.Action _callback;

        public InputTypingManager(System.Action<IList<Telegram.Api.WindowsPhone.Tuple<int, TLSendMessageActionBase>>> typingCallback, System.Action callback)
        {
            _typingUsersTimer = new Timer(UpdateTypingUsersCache, null, Timeout.Infinite, Timeout.Infinite);
            _typingCallback = typingCallback;
            _callback = callback;
        }


        private void StartTypingTimer(int dueTime)
        {
            if (_typingUsersTimer != null)
            {
                _typingUsersTimer.Change(dueTime, Timeout.Infinite);
                //TLUtils.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + " Start TypingTimer " + dueTime, LogSeverity.Error);
            }
        }

        private void StopTypingTimer()
        {
            if (_typingUsersTimer != null)
            {
                _typingUsersTimer.Change(Timeout.Infinite, Timeout.Infinite);
                //TLUtils.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + " Stop TypingTimer ", LogSeverity.Error);
            }
        }

        private void UpdateTypingUsersCache(object state)
        {
            var now = DateTime.Now;
            var nextTime = DateTime.MaxValue;

            var typingUsers = new List<Telegram.Api.WindowsPhone.Tuple<int, TLSendMessageActionBase>>();
            lock (_typingUsersSyncRoot)
            {
                if (_typingUsersCache.Count == 0) return;

                var keys = new List<int>(_typingUsersCache.Keys);
                foreach (var key in keys)
                {
                    if (_typingUsersCache[key].Item1 <= now)
                    {
                        _typingUsersCache.Remove(key);
                    }
                    else
                    {
                        if (nextTime > _typingUsersCache[key].Item1)
                        {
                            nextTime = _typingUsersCache[key].Item1;
                        }
                        typingUsers.Add(new Telegram.Api.WindowsPhone.Tuple<int, TLSendMessageActionBase>(key, _typingUsersCache[key].Item2));
                    }
                }
            }

            if (typingUsers.Count > 0)
            {
                StartTypingTimer((int)(nextTime - now).TotalMilliseconds);
                _typingCallback.SafeInvoke(typingUsers);
            }
            else
            {
                StopTypingTimer();
                _callback.SafeInvoke();
            }
        }

        public void Start()
        {
            StartTypingTimer(0);
        }

        public void Stop()
        {
            StopTypingTimer();
        }

        public void AddTypingUser(int userId, TLSendMessageActionBase action)
        {
            var now = DateTime.Now;
            var nextTime = DateTime.MaxValue;
            var typingUsers = new List<Telegram.Api.WindowsPhone.Tuple<int, TLSendMessageActionBase>>();
            //lock here
            lock (_typingUsersSyncRoot)
            {
                _typingUsersCache[userId] = new Telegram.Api.WindowsPhone.Tuple<DateTime, TLSendMessageActionBase>(now.AddSeconds(5.0), action);

                foreach (var keyValue in _typingUsersCache)
                {
                    if (keyValue.Value.Item1 > now)
                    {
                        if (nextTime > keyValue.Value.Item1)
                        {
                            nextTime = keyValue.Value.Item1;
                        }
                        typingUsers.Add(new Telegram.Api.WindowsPhone.Tuple<int, TLSendMessageActionBase>(keyValue.Key, keyValue.Value.Item2));
                    }
                }
            }

            if (typingUsers.Count > 0)
            {
                StartTypingTimer((int)(nextTime - now).TotalMilliseconds);
                _typingCallback.SafeInvoke(typingUsers);
            }
            else
            {
                _callback.SafeInvoke();
            }
        }

        public void RemoveTypingUser(int userId)
        {
            var typingUsers = new List<Telegram.Api.WindowsPhone.Tuple<int, TLSendMessageActionBase>>();
            //lock here
            lock (_typingUsersSyncRoot)
            {
                _typingUsersCache.Remove(userId);

                foreach (var keyValue in _typingUsersCache)
                {
                    if (keyValue.Value.Item1 > DateTime.Now)
                    {
                        typingUsers.Add(new Telegram.Api.WindowsPhone.Tuple<int, TLSendMessageActionBase>(keyValue.Key, keyValue.Value.Item2));
                    }
                }
            }

            if (typingUsers.Count > 0)
            {
                _typingCallback.SafeInvoke(typingUsers);
            }
            else
            {
                _callback.SafeInvoke();
            }
        }
    }

    public class OutputTypingManager
    {
        public OutputTypingManager(TLInputPeerBase peer, TLSendMessageActionBase action, double delay, IMTProtoService mtProtoService)
        {
            _peer = peer;
            _action = action;
            _delay = delay;
            _mtProtoService = mtProtoService;
        }

        private readonly TLInputPeerBase _peer;

        private readonly TLSendMessageActionBase _action;

        private readonly double _delay;

        private readonly IMTProtoService _mtProtoService;

        private DateTime? _lastTypingTime;

        public void SetTyping()
        {
            if (_peer is TLInputPeerBroadcast)
            {
                return;
            }

            if (_lastTypingTime.HasValue
                && _lastTypingTime.Value.AddSeconds(_delay) > DateTime.Now)
            {
                return;
            }

            _lastTypingTime = DateTime.Now;
            _mtProtoService.SetTypingAsync(_peer, _action, result => { });
            //TLUtils.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + " " + _action.GetType().Name, LogSeverity.Error);
        }

        public void CancelTyping()
        {
            _lastTypingTime = null;
            _mtProtoService.SetTypingAsync(_peer, new TLSendMessageCancelAction(), result => { });
        }
    }

    public enum UploadTypingKind
    {
        Photo,
        Video,
        Document
    }

    public class OutputUploadTypingManager
    {
        public OutputUploadTypingManager(TLInputPeerBase peer, double delay, IMTProtoService mtProtoService)
        {
            _peer = peer;
            _delay = delay;
            _mtProtoService = mtProtoService;
        }

        private readonly TLInputPeerBase _peer;

        private readonly double _delay;

        private readonly IMTProtoService _mtProtoService;

        private DateTime? _lastTypingTime;

        public void SetTyping(UploadTypingKind kind)
        {
            if (_peer is TLInputPeerBroadcast)
            {
                return;
            }

            if (_lastTypingTime.HasValue
                && _lastTypingTime.Value.AddSeconds(_delay) > DateTime.Now)
            {
                return;
            }

            TLSendMessageActionBase action = new TLSendMessageUploadPhotoAction();
            if (kind == UploadTypingKind.Document)
            {
                action = new TLSendMessageUploadDocumentAction();
            }
            else if (kind == UploadTypingKind.Video)
            {
                action = new TLSendMessageUploadVideoAction();
            }

            _lastTypingTime = DateTime.Now;
            _mtProtoService.SetTypingAsync(_peer, action, result => { });
            //TLUtils.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + " " + action.GetType().Name, LogSeverity.Error);
        }

        public void CancelTyping()
        {
            _lastTypingTime = null;
            _mtProtoService.SetTypingAsync(_peer, new TLSendMessageCancelAction(), result => { });
        }
    }
}
