using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Org.BouncyCastle.Security;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using TelegramClient.Converters;
#if WP8
using System.Threading.Tasks;
using Windows.Storage;
#endif
using Caliburn.Micro;
using Telegram.Api.Extensions;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.Cache.EventArgs;
using Telegram.Api.Services.FileManager;
using Telegram.Api.TL;
using Telegram.Api.TL.Interfaces;
using TelegramClient.Extensions;
using TelegramClient.Helpers;
using TelegramClient.Resources;
using TelegramClient.Services;
using TelegramClient.ViewModels.Additional;
using TelegramClient.ViewModels.Contacts;
using TelegramClient.ViewModels.Media;
using Execute = Telegram.Api.Helpers.Execute;

namespace TelegramClient.ViewModels.Dialogs
{
    public partial class SecretDialogDetailsViewModel : 
        ItemsViewModelBase<TLDecryptedMessageBase>
    {
        public bool IsGroupActionEnabled
        {
            get { return Items.Any(x => x.IsSelected); }
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

        public Uri EncryptedImageSource
        {
            get
            {
                var isLightTheme = (Visibility)Application.Current.Resources["PhoneLightThemeVisibility"] == Visibility.Visible;

                return isLightTheme ?
                    new Uri("/Images/Dialogs/secretchat-white-WXGA.png", UriKind.Relative) :
                    new Uri("/Images/Dialogs/secretchat-black-WXGA.png", UriKind.Relative);
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
                    FileUtils.Delete(_unsendedTextRoot, "ec" + Chat.Id + ".dat");
                    return;
                }

                TLUtils.SaveObjectToMTProtoFile(_unsendedTextRoot, "ec" + Chat.Id + ".dat", new TLString(text));
            });
        }

        private void LoadUnsendedTextAsync(Action<string> callback)
        {
            BeginOnThreadPool(() =>
            {
                var inputPeer = With as IInputPeer;
                if (inputPeer == null) return;

                var result = TLUtils.OpenObjectFromMTProtoFile<TLString>(_unsendedTextRoot, "ec" + Chat.Id + ".dat");
                callback.SafeInvoke(result != null ? result.ToString() : null);
            });
        }

        public TLUserBase With { get; protected set; }

        public TLEncryptedChatBase Chat { get; protected set; }

        public string AppBarStatus { get; protected set; }

        public string Subtitle { get; protected set; }

        public Visibility InputVisibility
        {
            get { return Chat is TLEncryptedChat ? Visibility.Visible : Visibility.Collapsed; }
        }

        public Visibility WaitingBarVisibility
        {
            get { return  Chat is TLEncryptedChatWaiting ? Visibility.Visible : Visibility.Collapsed; }
        }

        public Visibility DeleteButtonVisibility
        {
            get { return Chat is TLEncryptedChatDiscarded ? Visibility.Visible : Visibility.Collapsed; }
        }

        public bool IsApplicationBarVisible
        {
            get { return Chat is TLEncryptedChat; }
        }

        public Visibility DescriptionVisibility
        {
            get
            {
                var isEmtpyDialog = Items == null || Items.Count == 0;
                var hasEmptyServiceMessage = false;
                if (!isEmtpyDialog && Items.Count == 1)
                {
                    var serviceMessage = Items[0] as TLDecryptedMessageService;
                    if (serviceMessage != null)
                    {
                        var serviceAction = serviceMessage.Action as TLDecryptedMessageActionEmpty;
                        if (serviceAction != null)
                        {
                            hasEmptyServiceMessage = true;
                        }
                    }
                }

                return (isEmtpyDialog || hasEmptyServiceMessage) && LazyItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed; 
            }
        }

        public ChooseAttachmentViewModel ChooseAttachment { get; set; }

        public bool IsChooseAttachmentOpen { get { return ChooseAttachment != null && ChooseAttachment.IsOpen; } }

        public DecryptedImageViewerViewModel ImageViewer { get; protected set; }

        public IUploadFileManager UploadFileManager
        {
            get { return IoC.Get<IUploadFileManager>(); }
        }

        public IUploadVideoFileManager UploadVideoFileManager
        {
            get { return IoC.Get<IUploadVideoFileManager>(); }
        }

        public IUploadDocumentFileManager UploadDocumentFileManager
        {
            get { return IoC.Get<IUploadDocumentFileManager>(); }
        }

        public string RandomParam { get; set; }

        private readonly DispatcherTimer _selfDestructTimer;
        
        private readonly object _typingUsersSyncRoot = new object();
        
        private readonly Dictionary<int, DateTime> _typingUsersCache = new Dictionary<int, DateTime>();
        
        private readonly Timer _typingUsersTimer;

        public SecretDialogDetailsViewModel(ICacheService cacheService, ICommonErrorHandler errorHandler, IStateService stateService, INavigationService navigationService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator) 
            : base(cacheService, errorHandler, stateService, navigationService, mtProtoService, eventAggregator)
        {

            _typingUsersTimer = new Timer(UpdateTypingUsersCache, null, 0, 5000);

            Items = new ObservableCollection<TLDecryptedMessageBase>();

            Chat = GetChat();
            StateService.With = null;
            if (Chat == null) return;

            With = GetParticipant();
            StateService.Participant = null;
            if (With == null) return;

            eventAggregator.Subscribe(this);

            Status = string.Format(AppResources.SecretChatCaption, With.FirstName);
            Subtitle = GetSubtitle(With);

            _selfDestructTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.0) };
            _selfDestructTimer.Tick += OnSelfDestructTimerTick;

            LoadUnsendedTextAsync(unsendedText =>
            {
                Text = unsendedText;
            });

            BeginOnThreadPool(() =>
            {
                var cachedMessages = CacheService.GetDecryptedHistory(Chat.Index).ToList();

                foreach (var cachedMessage in cachedMessages)
                {
                    var isDisplayedMessage = TLUtils.IsDisplayedDecryptedMessage(cachedMessage);

                    if (isDisplayedMessage)
                    {
                        LazyItems.Add(cachedMessage);
                    }
                }

                if (LazyItems.Count > 0)
                {
                    BeginOnUIThread(() => PopulateItems(() =>
                    {
                        ReadMessages();
                        NotifyOfPropertyChange(() => DescriptionVisibility);
                    }));
                }
                else
                {
                    ReadMessages();
                    NotifyOfPropertyChange(() => DescriptionVisibility);
                }

                NotifyOfPropertyChange(() => DescriptionVisibility);
            });

            PropertyChanged += (sender, args) =>
            {
                if (Property.NameEquals(args.PropertyName, () => Text))
                {
                    if (!_lastTypingTime.HasValue)
                    {
                        SetTypingInternal();
                        return;
                    }
                    var fromLastTypingTimeInSeconds = (DateTime.Now - _lastTypingTime.Value).TotalSeconds;
                    if (fromLastTypingTimeInSeconds > Constants.SetTypingIntervalInSeconds)
                    {
                        SetTypingInternal();
                        return;
                    }
                }
            };
        }

        private TLEncryptedChatBase GetChat()
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
                    var chat = CacheService.GetEncryptedChat(new TLInt(chatId));
                    if (chat != null)
                    {
                        return chat;
                    }

                    NavigateToShellViewModel();
                    return null;
                }

                NavigateToShellViewModel();
                return null;
            }

            return StateService.With as TLEncryptedChatBase;
        }

        private TLUserBase GetParticipant()
        {
            if (StateService.Participant == null)
            {
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

                NavigateToShellViewModel();
                return null;
            }

            return StateService.Participant;
        }

        public void NavigateToShellViewModel()
        {
            StateService.ClearNavigationStack = true;
            NavigationService.UriFor<ShellViewModel>().Navigate();
        }

        private DateTime? _lastTypingTime;

        private void SetTypingInternal()
        {
            var chat = Chat as TLEncryptedChatCommon;
            if (chat == null) return;

            _lastTypingTime = DateTime.Now;
            MTProtoService.SetEncryptedTypingAsync(new TLInputEncryptedChat { AccessHash = chat.AccessHash, ChatId = chat.Id }, new TLBool(true), result => { });
        }

        //~SecretDialogDetailsViewModel()
        //{
            
        //}

        private void StartTimer()
        {
            if (_isActive)
            {
                _selfDestructTimer.Start();

                return;
            }

            //if (Chat != null && Chat.MessageTTL != null && IsActive)
            //{
            //    var seconds = Chat.MessageTTL.Value;

            //    if (seconds > 0)
            //    {
            //        if (seconds < 10)
            //        {
            //            _selfDestructTimer.Interval = TimeSpan.FromSeconds(1.0);
            //        }
            //        else if (seconds < 90)
            //        {
            //            _selfDestructTimer.Interval = TimeSpan.FromSeconds(5.0);
            //        }
            //        else
            //        {
            //            _selfDestructTimer.Interval = TimeSpan.FromSeconds(30.0);
            //        }

            //        _selfDestructTimer.Start();
            //    }
            //}
        }

        private void StopTimer()
        {
            _selfDestructTimer.Stop();
            _previousCheckTime = null;
            _screenshotsCount = null;
        }

        private bool _isActive;

        protected override void OnActivate()
        {
            if (With == null) return;

            BeginOnThreadPool(() =>
            {
                Thread.Sleep(500);
                //ReadMessages();
                BeginOnUIThread(StartTimer);
                _typingUsersTimer.Change(0, 5000);
            });

            base.OnActivate();
        }

        protected override void OnDeactivate(bool close)
        {
            StopTimer();
            _typingUsersTimer.Change(Timeout.Infinite, Timeout.Infinite);   // end timer execution
            base.OnDeactivate(close);
        }

        private DateTime? _previousCheckTime;

        private int? _screenshotsCount;

        private void OnSelfDestructTimerTick(object state, System.EventArgs eventArgs)
        {
            var now = DateTime.Now.Ticks;

            for (var i = 0; i < Items.Count; i++)
            {
                var message = Items[i] as TLDecryptedMessage;
                if (message != null 
                    && message.Status == MessageStatus.Read 
                    && message.DeleteIndex > 0)
                {
                    var diffTicks = now - message.DeleteDate.Value;
                    if (diffTicks > 0)
                    {
                        var message17 = message as TLDecryptedMessage17;
                        if (message17 != null)
                        {
                            var mediaAudio17 = message17.Media as TLDecryptedMessageMediaAudio17;
                            if (mediaAudio17 != null)
                            {
                                if (!message.Out.Value)
                                {
                                    DeleteMessage(message);
                                }
                            }
                        }

                        Items.RemoveAt(i--);
                        CacheService.DeleteDecryptedMessages(new TLVector<TLLong>{message.RandomId});
                        //CacheService.DeleteDecryptedMessages(new );
                    }
                }
            }

#if WP8
            if (!_previousCheckTime.HasValue || DateTime.Now > _previousCheckTime.Value.AddSeconds(5.0))
            {
                BeginOnThreadPool(async () =>
                {
                    try
                    {
                        var screenshotsFolder = await KnownFolders.PicturesLibrary.GetFolderAsync("Screenshots");
                        var screenshotsFiles = await screenshotsFolder.GetFilesAsync();
                        var previousScreenshotsCount = _screenshotsCount;
                        _screenshotsCount = screenshotsFiles.Count;
                        _previousCheckTime = DateTime.Now;
                        if (_screenshotsCount > previousScreenshotsCount)
                        {
                            var chat = Chat as TLEncryptedChat;
                            if (chat == null) return;

                            var screenshotAction = new TLDecryptedMessageActionScreenshotMessages();
                            screenshotAction.RandomIds = new TLVector<TLLong>();

                            var decryptedTuple = GetDecryptedServiceMessageAndObject(screenshotAction, chat,
                                MTProtoService.CurrentUserId, CacheService);

                            Execute.BeginOnUIThread(() =>
                            {
                                Items.Insert(0, decryptedTuple.Item1);
                                NotifyOfPropertyChange(() => DescriptionVisibility);
                            });

                            SendEncryptedService(chat, decryptedTuple.Item2, MTProtoService, CacheService,
                                result =>
                                {

                                });
                        }
                    }
                    catch (FileNotFoundException ex)
                    {
                        // Screenshots folder doesn't exist
                    }
                    catch (Exception ex)
                    {
                        Execute.ShowDebugMessage("OnSelfDestructTimerTick check screenshot ex " + ex);
                    }
                });
            }
#endif
            

            if (Items.Count == 0)
            {
                NotifyOfPropertyChange(() => DescriptionVisibility);
            }
        }

        protected override void OnViewLoaded(object view)
        {
            AppBarStatus = string.Format(AppResources.WaitingForUserToGetOnline, With.FirstName) + "...";
            NotifyOfPropertyChange(() => AppBarStatus);

            base.OnViewLoaded(view);
        }

        public void DeleteMessage(TLObject obj)
        {
            var mediaPhoto = obj as TLDecryptedMessageMediaPhoto;
            if (mediaPhoto != null)
            {
                DeleteMessage(mediaPhoto);
            }

            var message = obj as TLDecryptedMessage;
            if (message != null)
            {
                DeleteMessage(message);
            }
        }

        private void DeleteMessage(TLDecryptedMessageMediaPhoto photo)
        {
            if (photo == null) return;

            var encryptedChat = Chat as TLEncryptedChatCommon;
            if (encryptedChat == null) return;

            TLDecryptedMessage mediaMessage = null;
            for (var i = 0; i < Items.Count; i++)
            {
                var message = Items[i] as TLDecryptedMessage;
                if (message != null && message.Media == photo)
                {
                    mediaMessage = message;
                    break;
                }
            }

            if (mediaMessage == null) return;

            DeleteMessage(mediaMessage);
        }

        private void DeleteMessage(TLDecryptedMessage message)
        {
            if (message == null) return;

            var chat = Chat as TLEncryptedChat;
            if (chat == null) return;

            var messageId = new TLVector<TLLong> { message.RandomId };

            if (message.Status == MessageStatus.Failed)
            {
                Items.Remove(message);
                NotifyOfPropertyChange(() => DescriptionVisibility);
                CacheService.DeleteDecryptedMessages(messageId);
            }
            else
            {
                var deleteMessagesAction = new TLDecryptedMessageActionDeleteMessages { RandomIds = messageId };

                var decryptedTuple = GetDecryptedServiceMessageAndObject(deleteMessagesAction, chat, MTProtoService.CurrentUserId, CacheService);

                SendEncryptedService(chat, decryptedTuple.Item2, MTProtoService, CacheService,
                    result => BeginOnUIThread(() =>
                    {
                        Items.Remove(message);
                        NotifyOfPropertyChange(() => DescriptionVisibility);
                        CacheService.DeleteDecryptedMessages(messageId);
                    }));
            }
        }

        public void CopyMessage(TLDecryptedMessage message)
        {
            if (message == null) return;

            Clipboard.SetText(message.Message.ToString());
        }

        public void OnForwardInAnimationComplete()
        {
            if (ChooseAttachment == null)
            {
                ChooseAttachment = new ChooseAttachmentViewModel(With, CacheService, EventAggregator, NavigationService, StateService, false);
                NotifyOfPropertyChange(() => ChooseAttachment);
            }

            if (StateService.RemoveBackEntry)
            {
                NavigationService.RemoveBackEntry();
                StateService.RemoveBackEntry = false;
            }

            if (StateService.RemoveBackEntries)
            {
                var backEntry = NavigationService.BackStack.FirstOrDefault();
                while (backEntry != null && !backEntry.Source.ToString().Contains("ShellView.xaml"))
                {
                    NavigationService.RemoveBackEntry();
                    backEntry = NavigationService.BackStack.FirstOrDefault();
                }
                
                StateService.RemoveBackEntries = false;
            }

            //if (PrivateBetaIdentityToVisibilityConverter.IsPrivateBeta)
            {
                SecretChatDebug = new SecretChatDebugViewModel(Chat, Rekey);
                NotifyOfPropertyChange(() => SecretChatDebug);
            }
        }

        private void Rekey()
        {
            var chat = Chat as TLEncryptedChat20;
            if (chat == null) return;
            if (chat.PFS_ExchangeId != null) return;
            
            var layer = chat.Layer;
            if (layer.Value < 20) return;

            var aBytes = new byte[256];
            var random = new SecureRandom();
            random.NextBytes(aBytes);
            var p = chat.P;
            var g = chat.G;

            var gaBytes = Telegram.Api.Services.MTProtoService.GetGB(aBytes, g, p);
            var ga = TLString.FromBigEndianData(gaBytes);

            var randomId = TLLong.Random();
            chat.PFS_A = TLString.FromBigEndianData(aBytes);
            chat.PFS_ExchangeId = randomId;
            var actionRequestKey = new TLDecryptedMessageActionRequestKey { ExchangeId = randomId, GA = ga };
            var decryptedTuple = GetDecryptedServiceMessageAndObject(actionRequestKey, chat, MTProtoService.CurrentUserId, CacheService);
            decryptedTuple.Item1.Unread = TLBool.False;
#if DEBUG
            Items.Insert(0, decryptedTuple.Item1);
#endif

            SendEncryptedService(chat, decryptedTuple.Item2, MTProtoService, CacheService,
                result =>
                {

                });
        }

        public void OpenPeerDetails()
        {
            StateService.CurrentContact = With;
            if (Chat != null)
            {
                StateService.CurrentEncryptedChat = Chat;
                StateService.CurrentKey = Chat.Key;
                StateService.CurrentKeyFingerprint = Chat.KeyFingerprint;
                StateService.CurrentDecryptedMediaMessages =
                Items.OfType<TLDecryptedMessage>().Where(x => x.Media is TLDecryptedMessageMediaPhoto || x.Media is TLDecryptedMessageMediaVideo).ToList();
            }
            NavigationService.UriFor<SecretContactViewModel>().Navigate();
        }

        private void ReadMessages()
        {
            BeginOnUIThread(() =>
            {
                var unreadMessages = new List<TLDecryptedMessageBase>();
                for (var i = 0; i < Items.Count; i++)
                {
                    if (!Items[i].Out.Value)
                    {
                        if (Items[i].Unread.Value)
                        {
                            unreadMessages.Add(Items[i]);
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                if (unreadMessages.Count > 0)
                {
                    ReadMessages(unreadMessages.ToArray());
                }
            });
        }

        private void ReadMessages(params TLDecryptedMessageBase[] messages)
        {
            if (!_isActive) return;

            var chat = Chat as TLEncryptedChatCommon;
            if (chat == null) return;

            var maxDate = messages.Max(x => x.Date.Value);

            SetRead(messages);

            MTProtoService.ReadEncryptedHistoryAsync(
                new TLInputEncryptedChat { ChatId = Chat.Id, AccessHash = chat.AccessHash },
                new TLInt(maxDate),
                result =>
                {
                    //SetRead(messages);
                },
                error =>
                {
                    Execute.ShowDebugMessage("messages.readEncryptedHistory error: " + error);
                });
        }

        private void SetRead(params TLDecryptedMessageBase[] messages)
        {
            var dialog = CacheService.GetEncryptedDialog(Chat.Id) as TLEncryptedDialog;

            // input messages, no need to update UI
            messages.ForEach(x =>
            {
                if (x.TTL != null && x.TTL.Value > 0)
                {
                    var decryptedMessage = x as TLDecryptedMessage17;
                    if (decryptedMessage != null)
                    {
                        var decryptedPhoto = decryptedMessage.Media as TLDecryptedMessageMediaPhoto;
                        if (decryptedPhoto != null && x.TTL.Value <= 60.0)
                        {
                            return;
                        }

                        var decryptedVideo17 = decryptedMessage.Media as TLDecryptedMessageMediaVideo17;
                        if (decryptedVideo17 != null && x.TTL.Value <= 60.0)
                        {
                            return;
                        }

                        var decryptedAudio17 = decryptedMessage.Media as TLDecryptedMessageMediaAudio17;
                        if (decryptedAudio17 != null && x.TTL.Value <= 60.0)
                        {
                            return;
                        }
                    }
                    x.DeleteDate = new TLLong(DateTime.Now.Ticks + Chat.MessageTTL.Value * TimeSpan.TicksPerSecond);
                }
                x.Unread = TLBool.False;
                x.Status = MessageStatus.Read;
                CacheService.SyncDecryptedMessage(x, Chat, r => { });
            });

            Execute.BeginOnUIThread(() =>
            {
                if (dialog != null)
                {
                    dialog.UnreadCount = new TLInt(0);

                    dialog.NotifyOfPropertyChange(() => dialog.UnreadCount);
                    dialog.NotifyOfPropertyChange(() => dialog.TopMessage);
                    dialog.NotifyOfPropertyChange(() => dialog.Self);

                    CacheService.Commit();
                }
            });
        }

        public bool SliceLoaded { get; set; }

        public void LoadNextSlice()
        {
            if (LazyItems.Count > 0 || IsLastSliceLoaded) return;

            var lastItem = Items.LastOrDefault();

            long lastRandomId = 0;
            if (lastItem != null)
            {
                lastRandomId = lastItem.RandomIndex;
            }

            var slice = CacheService.GetDecryptedHistory(Chat.Index, lastRandomId);
            
            SliceLoaded = true;

            if (slice.Count == 0)
            {
                IsLastSliceLoaded = true;
            }
            foreach (var message in slice)
            {
                if (TLUtils.IsDisplayedDecryptedMessage(message))
                {
                    Items.Add(message);
                }
            }
        }

        public void OnBackwardInAnimationComplete()
        {
            SendMedia();

            ReadMessages();
        }

        public SecretChatDebugViewModel SecretChatDebug { get; protected set; }

        public void DeleteChat()
        {
            var chatDiscarded = Chat as TLEncryptedChatDiscarded;
            if (chatDiscarded == null) return;

            var dialog = CacheService.GetEncryptedDialog(Chat.Id);
            if (dialog != null)
            {
                EventAggregator.Publish(new DialogRemovedEventArgs(dialog));
                CacheService.DeleteDialog(dialog);
            }
            BeginOnUIThread(() => NavigationService.GoBack());
        }

        public void CancelVideoDownloading(TLDecryptedMessageMediaVideo mediaVideo)
        {
            mediaVideo.DownloadingProgress = 0.0;
            
            var fileManager = IoC.Get<IEncryptedFileManager>();
            fileManager.CancelDownloadFile(mediaVideo);
        }

        public void CancelDocumentDownloading(TLDecryptedMessageMediaDocument mediaDocument)
        {
            mediaDocument.DownloadingProgress = 0.0;

            var fileManager = IoC.Get<IEncryptedFileManager>();
            fileManager.CancelDownloadFile(mediaDocument);
        }

        public void CancelUploading(TLDecryptedMessageMediaBase media)
        {
            var mediaPhoto = media as TLDecryptedMessageMediaPhoto;
            if (mediaPhoto != null && mediaPhoto.File != null)
            {
                var file = mediaPhoto.File as TLEncryptedFile;
                if (file != null && file.Id != null)
                {
                    UploadFileManager.CancelUploadFile(file.Id);
                }
            }

            var mediaDocument = media as TLDecryptedMessageMediaDocument;
            if (mediaDocument != null && mediaDocument.File != null)
            {
                var file = mediaDocument.File as TLEncryptedFile;
                if (file != null && file.Id != null)
                {
                    UploadDocumentFileManager.CancelUploadFile(file.Id);
                }
            }

            var mediaVideo = media as TLDecryptedMessageMediaVideo;
            if (mediaVideo != null && mediaVideo.File != null)
            {
                var file = mediaVideo.File as TLEncryptedFile;
                if (file != null && file.Id != null)
                {
                    UploadVideoFileManager.CancelUploadFile(file.Id);
                }
            }
        }

#if WP81
        public static async Task<StorageFile> GetStorageFile(TLDecryptedMessageMediaBase media)
        {
            if (media == null) return null;
            if (media.StorageFile != null)
            {
                if (File.Exists(media.StorageFile.Path))
                {
                    return media.StorageFile;
                }
            }

            var mediaDocument = media as TLDecryptedMessageMediaDocument;
            if (mediaDocument != null)
            {
                var file = media.File as TLEncryptedFile;
                if (file == null) return null;

                var fileName = String.Format("{0}_{1}_{2}.{3}",
                    file.Id,
                    file.DCId,
                    file.AccessHash,
                    mediaDocument.FileExt);

                return await DialogDetailsViewModel.GetFileFromLocalFolder(fileName);
            }

            var mediaVideo = media as TLDecryptedMessageMediaVideo;
            if (mediaVideo != null)
            {
                var file = media.File as TLEncryptedFile;
                if (file == null) return null;

                var fileName = String.Format("{0}_{1}_{2}.{3}",
                    file.Id,
                    file.DCId,
                    file.AccessHash,
                    "mp4");

                return await DialogDetailsViewModel.GetFileFromLocalFolder(fileName);
            }

            return null;
        }
#endif


#if WP81
        public async void Resend(TLDecryptedMessage message)
#else
        public void Resend(TLDecryptedMessage message)
#endif
        {
            if (message == null) return;

            var chat = Chat as TLEncryptedChat;
            if (chat == null) return;

            TLObject obj = message;
            var decryptedMessage17 = message as TLDecryptedMessage17;
            if (decryptedMessage17 != null)
            {
                var encryptedChat17 = chat as TLEncryptedChat17;
                if (encryptedChat17 == null) return;

                var messageLayer17 = new TLDecryptedMessageLayer17();
                messageLayer17.RandomBytes = TLString.Empty;
                messageLayer17.InSeqNo = decryptedMessage17.InSeqNo;
                messageLayer17.OutSeqNo = decryptedMessage17.OutSeqNo;
                messageLayer17.Layer = encryptedChat17.Layer;
                messageLayer17.Message = decryptedMessage17;

                obj = messageLayer17;
            }

            message.Status = MessageStatus.Sending;

            if (message.Media is TLDecryptedMessageMediaEmpty)
            {
                SendEncrypted(chat, obj, MTProtoService, CacheService);
            }
            else
            {
                message.Media.UploadingProgress = 0.001;
                if (message.Media is TLDecryptedMessageMediaPhoto)
                {
                    SendPhotoInternal(null, obj);
                }
                else if (message.Media is TLDecryptedMessageMediaVideo)
                {
                    SendVideoInternal(null, obj);
                }
                else if (message.Media is TLDecryptedMessageMediaDocument)
                {
#if WP81
                    var file = await GetStorageFile(message.Media);

                    if (file != null)
                    {
                        SendDocumentInternal(file, message);
                    }
                    else
                    {
                        MessageBox.Show(AppResources.UnableToAccessDocument, AppResources.Error, MessageBoxButton.OK);
                        message.Status = MessageStatus.Failed;
                        DeleteMessage(message);
                        return;
                    }
#else
                    SendDocumentInternal(null, message);
#endif
                }
                else if (message.Media is TLDecryptedMessageMediaAudio)
                {
#if WP8
                    SendAudioInternal(obj);
#endif
                }
                else if (message.Media is TLDecryptedMessageMediaContact)
                {

                }
            }

            message.NotifyOfPropertyChange(() => message.Status);
        }

        public void ChangeSelection(TLDecryptedMessageBase message)
        {
            if (message == null) return;

            message.IsSelected = !message.IsSelected;
            NotifyOfPropertyChange(() => IsGroupActionEnabled);
        }

        public void DeleteMessages()
        {
            var deletingMessages = Items.Where(x => x.IsSelected).ToList();
            if (deletingMessages.Count > 0)
            {
                var chat = Chat as TLEncryptedChat;
                if (chat == null) return;

                var messageId = new TLVector<TLLong> { Items = deletingMessages.Select(x => x.RandomId).ToList() };

                var action = new TLDecryptedMessageActionDeleteMessages {RandomIds = messageId};

                var decryptedTuple = GetDecryptedServiceMessageAndObject(action, chat, MTProtoService.CurrentUserId, CacheService);

                SendEncryptedService(chat, decryptedTuple.Item2, MTProtoService, CacheService,
                    result => BeginOnUIThread(() =>
                    {
                        foreach (var deletedMessage in deletingMessages)
                        {
                            Items.Remove(deletedMessage);
                        }
                        NotifyOfPropertyChange(() => DescriptionVisibility);
                        CacheService.DeleteDecryptedMessages(messageId);
                    }));
            }
        }

        private int InsertMessageInOrder(TLDecryptedMessageBase message)
        {
            if (Chat != null && Chat.MessageTTL != null)
            {
                message.TTL = Chat.MessageTTL;
            }

            for (var i = 0; i < Items.Count; i++)
            {
                if (Items[i].DateIndex == message.DateIndex
                    && Items[i].QtsIndex == message.QtsIndex)
                {
                    return -1;
                }

                if (Items[i].DateIndex < message.DateIndex)
                {
                    Items.Insert(i, message);
                    return i;
                }

                if (Items[i].QtsIndex < message.QtsIndex)
                {
                    Items.Insert(i, message);
                    return i;
                }
            }

            Items.Add(message);
            return Items.Count - 1;
        }

        private void AddTypingUser(int userId)
        {
            var typingUsers = new List<int>();
            //lock here
            lock (_typingUsersSyncRoot)
            {
                _typingUsersCache[userId] = DateTime.Now.AddSeconds(5.0);

                foreach (var keyValue in _typingUsersCache)
                {
                    if (keyValue.Value > DateTime.Now)
                    {
                        typingUsers.Add(keyValue.Key);
                    }
                }
            }

            if (typingUsers.Count > 0)
            {
                Subtitle = GetTypingSubtitle(typingUsers);
                NotifyOfPropertyChange(() => Subtitle);
            }
            else
            {
                Subtitle = GetSubtitle(With);
                NotifyOfPropertyChange(() => Subtitle);
            }
        }

        private void RemoveTypingUser(int userId)
        {
            var typingUsers = new List<int>();
            //lock here
            lock (_typingUsersSyncRoot)
            {
                _typingUsersCache.Remove(userId);

                foreach (var keyValue in _typingUsersCache)
                {
                    if (keyValue.Value > DateTime.Now)
                    {
                        typingUsers.Add(keyValue.Key);
                    }
                }
            }

            if (typingUsers.Count > 0)
            {
                Subtitle = GetTypingSubtitle(typingUsers);
                NotifyOfPropertyChange(() => Subtitle);
            }
            else
            {
                Subtitle = GetSubtitle(With);
                NotifyOfPropertyChange(() => Subtitle);
            }
        }

        private void UpdateTypingUsersCache(object state)
        {
            var typingUsers = new List<int>();
            lock (_typingUsersSyncRoot)
            {
                if (_typingUsersCache.Count == 0) return;

                var keys = new List<int>(_typingUsersCache.Keys);
                foreach (var key in keys)
                {
                    if (_typingUsersCache[key] <= DateTime.Now)
                    {
                        _typingUsersCache.Remove(key);
                    }
                    else
                    {
                        typingUsers.Add(key);
                    }
                }
            }

            if (typingUsers.Count > 0)
            {
                Subtitle = GetTypingSubtitle(typingUsers);
                NotifyOfPropertyChange(() => Subtitle);
            }
            else
            {
                Subtitle = GetSubtitle(With);
                NotifyOfPropertyChange(() => Subtitle);
            }
        }

        private static string GetTypingSubtitle(List<int> typingUsers)
        {
            return string.Format("{0}...", AppResources.Typing.ToLower(CultureInfo.InvariantCulture));
        }

        private string GetSubtitle(TLUserBase user)
        {
            return DialogDetailsViewModel.GetUserStatus(user);
        }

        public void OpenMediaContact(TLUserBase user, TLString phone)
        {
            if (user == null) return;

            StateService.CurrentContact = user;
            StateService.CurrentContactPhone = phone;
            NavigationService.UriFor<ContactViewModel>().Navigate();
        }

        public void OnNavigatedTo()
        {
            _isActive = true;
            StateService.ActiveDialog = Chat;
        }

        public void OnNavigatedFrom()
        {
            _isActive = false;
            StateService.ActiveDialog = null;
        }
    }
}
