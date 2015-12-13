using System;
using System.ComponentModel;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BugSense;
using Microsoft.Phone.Controls;
using Telegram.Api.Aggregator;
using TelegramClient.Controls;
using TelegramClient.Helpers;
using TelegramClient.Views;
using TelegramClient.Views.Additional;
using TelegramClient.Views.Dialogs;
#if WP81
using Windows.Graphics.Imaging;
#endif
#if WP8
using TelegramClient_WebP.LibWebP;
using Windows.Phone.PersonalInformation;
#endif
using Caliburn.Micro;
using Microsoft.Devices;
using Microsoft.Phone.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Telegram.Api.Helpers;
using Telegram.Api.MD5;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.FileManager;
using Telegram.Api.Services.Updates;
using Telegram.Api.TL;
using TelegramClient.Converters;
using TelegramClient.Resources;
using TelegramClient.Services;
using TelegramClient.ViewModels.Additional;
using TelegramClient.ViewModels.Contacts;
using TelegramClient.ViewModels.Debug;
using TelegramClient.ViewModels.Dialogs;
using Execute = Telegram.Api.Helpers.Execute;

namespace TelegramClient.ViewModels
{
    public class ShellViewModel : Conductor<ViewModelBase>.Collection.OneActive, 
        Telegram.Api.Aggregator.IHandle<UploadableItem>, 
        Telegram.Api.Aggregator.IHandle<DownloadableItem>,
        Telegram.Api.Aggregator.IHandle<TLMessageCommon>, 
        Telegram.Api.Aggregator.IHandle<TLDecryptedMessageBase>, 
        Telegram.Api.Aggregator.IHandle<UpdatingEventArgs>,
        Telegram.Api.Aggregator.IHandle<UpdateCompletedEventArgs>, 
        Telegram.Api.Aggregator.IHandle<TLUpdateContactRegistered>,
        Telegram.Api.Aggregator.IHandle<ExceptionInfo>
    {
        public bool IsPasscodeEnabled { get; protected set; }

        public IStateService StateService
        {
            get { return _stateService; }
        }

        public Uri PasscodeImageSource
        {
            get
            {
                return PasscodeUtils.Locked
                    ? new Uri("/Images/Dialogs/passcode.close-WXGA.png", UriKind.Relative)
                    : new Uri("/Images/Dialogs/passcode.open-WXGA.png", UriKind.Relative);
            }
        }

        public Brush PasscodeImageBrush
        {
            get
            {
                return PasscodeUtils.Locked
                    ? (Brush)Application.Current.Resources["PhoneAccentBrush"]
                    : (Brush)Application.Current.Resources["PhoneSubtleBrush"];
            }
        }

        //public Visibility PasscodeImageVisibility
        //{
        //    get
        //    {
        //        return string.IsNullOrEmpty(_stateService.Passcode) ? Visibility.Collapsed : Visibility.Visible;                
        //    }
        //}

        public DialogsViewModel Dialogs { get; protected set; }

        public ContactsViewModel Contacts { get; protected set; }

        private DebugViewModel _debug;

        private LongPollViewModel _longPoll;

        private PerformanceViewModel _performance;

        private IStateService _stateService;

        private ITelegramEventAggregator _eventAggregator;

        private readonly IMTProtoService _mtProtoService;

        public IMTProtoService MTProtoService { get { return _mtProtoService; } }

        private INavigationService _navigationService;

        private ICacheService _cacheService;

        private IPushService _pushService;

        private bool _registerDeviceOnce;

        public void OnAnimationComplete()
        {
            BeginOnThreadPool(() =>
            {
                var isAuthorized = SettingsHelper.GetValue<bool>(Constants.IsAuthorizedKey);
                if (!isAuthorized)
                {
                    Telegram.Logs.Log.Write("StartupViewModel ShellViewModel.OnAnimationComplete IsAuthorized=false");
                    TLUtils.IsLogEnabled = true;
                    Execute.BeginOnUIThread(() =>
                    {
                        _stateService.ClearNavigationStack = true;
                        _navigationService.UriFor<StartupViewModel>().Navigate();
                    });
                }
                else
                {
                    TLUtils.IsLogEnabled = false;

                    if (_registerDeviceOnce) return;

                    _registerDeviceOnce = true;
                    _pushService.RegisterDeviceAsync();
                    _mtProtoService.CurrentUserId = new TLInt(_stateService.CurrentUserId);

                    ContactsHelper.UpdateDelayedContactsAsync(_cacheService, _mtProtoService);

                    UpdatePasscode();
                }
            });
        }

        public void UpdatePasscode()
        {
            IsPasscodeEnabled = PasscodeUtils.IsEnabled;
            NotifyOfPropertyChange(() => IsPasscodeEnabled);
        }

        public ShellViewModel(IPushService pushService, PerformanceViewModel performance, LongPollViewModel longPoll, DialogsViewModel dialogs, ContactsViewModel contacts, DebugViewModel debug, 
            ICacheService cacheService, IStateService stateService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator, INavigationService navigationService)
        {
            _pushService = pushService;

            Dialogs = dialogs;
            Contacts = contacts;
            _debug = debug;
            _longPoll = longPoll;
            _performance = performance;

            _stateService = stateService;
            _eventAggregator = eventAggregator;
            _mtProtoService = mtProtoService;
            _mtProtoService.AuthorizationRequired += OnAuthorizationRequired;
            _mtProtoService.CheckDeviceLocked += OnCheckDeviceLocked;
            _navigationService = navigationService;
            _cacheService = cacheService;

            _eventAggregator.Subscribe(this);
        }

        private void OnCheckDeviceLocked(object sender, System.EventArgs e)
        {
            UpdateDeviceLockedAsync(false);
        }

        private static int _previousPeriod = -2;

        public void UpdateDeviceLockedAsync(bool force = true)
        {
            Execute.BeginOnThreadPool(() =>
            {
                var isAuthorized = SettingsHelper.GetValue<bool>(Constants.IsAuthorizedKey);
                if (isAuthorized)
                {
                    UpdatePasscode();

                    Execute.BeginOnUIThread(() =>
                    {
                        var frame = Application.Current.RootVisual as TelegramTransitionFrame;
                        if (frame != null && frame.IsPasscodeActive)
                        {
                            return;
                        }

                        var period = -1;
                        if (PasscodeUtils.IsEnabled)
                        {
                            period = PasscodeUtils.AutolockTimeout;

                            if (PasscodeUtils.Locked)
                            {
                                period = 0;
                            }
                        }

                        if (!force
                            && _previousPeriod == period
                            && (period == -1 || period == int.MaxValue))
                        {
                            return;
                        }

                        if (!force
                            && (period == TimeSpan.FromHours(1).TotalSeconds || period == TimeSpan.FromHours(5).TotalSeconds))
                        {
                            return;
                        }

                        MTProtoService.UpdateDeviceLockedAsync(new TLInt(period),
                            result =>
                            {
                                Execute.BeginOnUIThread(() =>
                                {
                                    _previousPeriod = period;
                                });

                                //Execute.ShowDebugMessage(string.Format("account.updateDeviceLocked {0} result {1}", period, result.Value));
                            },
                            error =>
                            {
                                Execute.ShowDebugMessage(string.Format("account.updateDeviceLocked {0} error {1}", period, error));
                            });
                    });
                }
            });
        }

        private void OnAuthorizationRequired(object sender, AuthorizationRequiredEventArgs e)
        {
            Telegram.Logs.Log.Write("StartupViewModel ShellViewModel.OnAuthorizationRequired " + e.MethodName + " " + e.Error + " " + e.AuthKeyId);

            var updateService = IoC.Get<IUpdatesService>();

            Execute.BeginOnUIThread(() =>
            {
                SettingsViewModel.LogOutCommon(
                    _eventAggregator,
                    _mtProtoService,
                    updateService,
                    _cacheService,
                    _stateService,
                    _pushService,
                    _navigationService);
            });
        }

        protected override void OnInitialize()
        {
            base.OnInitialize();

            Items.Add(Dialogs);
            Items.Add(Contacts);
#if DEBUG
            Items.Add(_debug);
            Items.Add(_longPoll);
            Items.Add(_performance);
#endif

            ActivateItem(Dialogs);
        }

        protected override void OnActivate()
        {
            _stateService.IsMainViewOpened = true;

            ThreadPool.QueueUserWorkItem(state =>
                _stateService.GetNotifySettingsAsync(settings =>
                {
                    var isAuthorized = SettingsHelper.GetValue<bool>(Constants.IsAuthorizedKey);

                    if (isAuthorized && !settings.InvisibleMode)
                    {
                        _mtProtoService.RaiseSendStatus(new SendStatusEventArgs(new TLBool(false)));
                    }
                }));

            if (_stateService.FirstRun)
            {
                _stateService.FirstRun = false;

                Dialogs.FirstRun = true;
                Contacts.FirstRun = true;

                _pushService.RegisterDeviceAsync();
            }

            if (_stateService.ClearNavigationStack)
            {
                _stateService.ClearNavigationStack = false;
                while (_navigationService.RemoveBackEntry() != null) { }
            }

            NavigateByUserNameAsync();

            base.OnActivate();
        }

        private void NavigateByUserNameAsync()
        {
            Execute.BeginOnThreadPool(() =>
            {
                if (_stateService.Domain != null)
                {
                    var domain = _stateService.Domain;
                    _stateService.Domain = null;

                    MTProtoService.ResolveUsernameAsync(new TLString(domain),
                    result => Execute.BeginOnUIThread(() =>
                    {
                        var peerUser = result.Peer as TLPeerUser;
                        if (peerUser != null)
                        {
                            var user = result.Users.FirstOrDefault();
                            if (user != null)
                            {
                                Contacts.OpenContactDetails(user);
                            }
                        }

                        var peerChannel = result.Peer as TLPeerChannel;
                        var peerChat = result.Peer as TLPeerChat;
                        if (peerChannel != null || peerChat != null)
                        {
                            var channel = result.Chats.FirstOrDefault();
                            if (channel != null)
                            {
                                Dialogs.OpenChatDetails(channel);
                            }
                        }
                    }),
                    error => Execute.BeginOnUIThread(() => 
                    {
                        if (TLRPCError.CodeEquals(error, ErrorCode.BAD_REQUEST)
                            && TLRPCError.TypeEquals(error, ErrorType.QUERY_TOO_SHORT))
                        {
                            Execute.ShowDebugMessage("contacts.resolveUsername error " + error);
                        }
                        else if (TLRPCError.CodeEquals(error, ErrorCode.FLOOD))
                        {
                            Execute.ShowDebugMessage("contacts.resolveUsername error " + error);
                        }

                        Execute.ShowDebugMessage("contacts.resolveUsername error " + error);
                    }));
                }
            });
        }

        protected override void OnDeactivate(bool close)
        {
            _stateService.IsMainViewOpened = false;

            base.OnDeactivate(close);
        }


        public string SendingText;

        public void Send()
        {
            var task = new EmailComposeTask();
            task.Body = SendingText;
            task.To = "johnnypmpu@bk.ru";
            task.Subject = "Debug log";
            task.Show();
        }

        public void OpenSettings()
        {
            var user = _cacheService.GetUser(new TLInt(_stateService.CurrentUserId));

            _stateService.CurrentContact = user;
            _navigationService.UriFor<SettingsViewModel>().Navigate();
        }

        public void ComposeMessage()
        {
            Dialogs.CreateDialog();
        }

        public void AddContact()
        {
            Contacts.AddContact();
        }

        public void RefreshItems()
        {
            var itemsViewModel = ActiveItem as ItemsViewModelBase;
            if (itemsViewModel != null)
            {
                itemsViewModel.RefreshItems();
            }
        }

        public void Search()
        {
            var dialogs = ActiveItem as DialogsViewModel;
            if (dialogs != null)
            {
                dialogs.Search();
                return;
            }

            var contacts = ActiveItem as ContactsViewModel;
            if (contacts != null)
            {
                contacts.Search();
            }
        }

        public void About()
        {
            _navigationService.UriFor<AboutViewModel>().Navigate();
        }

        public void Add()
        {
            var itemsViewModel = ActiveItem as DialogsViewModel;
            if (itemsViewModel != null)
            {
                ComposeMessage();
            }
            else
            {
                AddContact();
            }
        }

        public void BeginOnThreadPool(System.Action action)
        {
            ThreadPool.QueueUserWorkItem(state => action());
        }

        public void Handle(UploadableItem item)
        {
            BeginOnThreadPool(() =>
            {
                var mediaDocument = item.Owner as TLMessageMediaDocument;
                if (mediaDocument != null)
                {

                    var m = _cacheService.GetSendingMessages()
                            .OfType<TLMessage>()
                            .FirstOrDefault(x => x.Media == mediaDocument);

                    if (m == null) return;

                    try
                    {
                        var document = (TLDocument)((TLMessageMediaDocument)m.Media).Document;

                        var caption = document.FileName;
                        var uploadedThumb = new TLInputFile
                        {
                            Id = item.FileId,
                            MD5Checksum = new TLString(""),
                            Name = new TLString(caption + ".jpg"),
                            Parts = new TLInt(item.Parts.Count)
                        };

                        ((TLDocument)((TLMessageMediaDocument)m.Media).Document).ThumbInputFile = uploadedThumb;

                    }
                    catch (Exception e)
                    {
                        
                    }
                }

                var mediaVideo = item.Owner as TLMessageMediaVideo;
                if (mediaVideo != null)
                {

                    var m = _cacheService.GetSendingMessages()
                        .OfType<TLMessage>()
                        .FirstOrDefault(x => x.Media == mediaVideo);

                    if (m == null) return;

                    var caption = ((TLVideo)((TLMessageMediaVideo)m.Media).Video).Caption;
                    var uploadedThumb = new TLInputFile
                    {
                        Id = item.FileId,
                        //MD5Checksum = new TLString(MD5Core.GetHashString(item.Bytes).ToLowerInvariant()),
                        MD5Checksum = new TLString(""),
                        Name = new TLString(caption + ".jpg"),
                        Parts = new TLInt(item.Parts.Count)
                    };

                    ((TLVideo)((TLMessageMediaVideo)m.Media).Video).ThumbInputFile = uploadedThumb;
                }
            });

            var message = item.Owner as TLMessage25;
            if (message != null)
            {
                HandleUploadableItemInternal(item, message);
            }

            var decryptedMessage = item.Owner as TLDecryptedMessage;
            if (decryptedMessage != null)
            {
                HandleUploadableEncryptedItemInternal(item, decryptedMessage);
            }

            var decryptedMessageLayer17 = item.Owner as TLDecryptedMessageLayer17;
            if (decryptedMessageLayer17 != null)
            {
                HandleUploadableEncryptedItemInternal(item, decryptedMessageLayer17);
            }
        }

        private void HandleUploadableEncryptedItemInternal(UploadableItem item, TLObject obj)
        {
            var message = SecretDialogDetailsViewModel.GetDecryptedMessage(obj);
            if (message == null) return;

            var mediaPhoto = message.Media as TLDecryptedMessageMediaPhoto;
            if (mediaPhoto != null)
            {
                mediaPhoto.UploadingProgress = 1.0;

                var fileLocation = mediaPhoto.File as TLEncryptedFile;
                if (fileLocation == null) return;

                message.InputFile = GetInputFile(item.FileId, new TLInt(item.Parts.Count), mediaPhoto.Key, mediaPhoto.IV);

                var chatId = message.ChatId;
                if (chatId == null) return;

                var chat = _cacheService.GetEncryptedChat(chatId) as TLEncryptedChat;
                if (chat == null) return;
                
                SecretDialogDetailsViewModel.SendEncryptedMediaInternal(chat, obj, _mtProtoService, _cacheService);
            }

            var mediaDocument = message.Media as TLDecryptedMessageMediaDocument;
            if (mediaDocument != null)
            {
                mediaDocument.UploadingProgress = 1.0;

                var fileLocation = mediaDocument.File as TLEncryptedFile;
                if (fileLocation == null) return;

                message.InputFile = item.IsSmallFile?
                    GetInputFile(item.FileId, new TLInt(item.Parts.Count), mediaDocument.Key, mediaDocument.IV) :
                    GetInputFileBig(item.FileId, new TLInt(item.Parts.Count), mediaDocument.Key, mediaDocument.IV);

                var chatId = message.ChatId;
                if (chatId == null) return;

                var chat = _cacheService.GetEncryptedChat(chatId) as TLEncryptedChat;
                if (chat == null) return;

                SecretDialogDetailsViewModel.SendEncryptedMediaInternal(chat, obj, _mtProtoService, _cacheService);
            }

            var mediaVideo = message.Media as TLDecryptedMessageMediaVideo;
            if (mediaVideo != null)
            {
                mediaVideo.UploadingProgress = 1.0;

                var fileLocation = mediaVideo.File as TLEncryptedFile;
                if (fileLocation == null) return;

                message.InputFile = GetInputFileBig(item.FileId, new TLInt(item.Parts.Count), mediaVideo.Key, mediaVideo.IV);

                var chatId = message.ChatId;
                if (chatId == null) return;

                var chat = _cacheService.GetEncryptedChat(chatId) as TLEncryptedChat;
                if (chat == null) return;

                SecretDialogDetailsViewModel.SendEncryptedMediaInternal(chat, obj, _mtProtoService, _cacheService);
            }

            var mediaAudio = message.Media as TLDecryptedMessageMediaAudio;
            if (mediaAudio != null)
            {
                mediaAudio.UploadingProgress = 1.0;

                var fileLocation = mediaAudio.File as TLEncryptedFile;
                if (fileLocation == null) return;

                message.InputFile = GetInputFile(item.FileId, new TLInt(item.Parts.Count), mediaAudio.Key, mediaAudio.IV);

                var chatId = message.ChatId;
                if (chatId == null) return;

                var chat = _cacheService.GetEncryptedChat(chatId) as TLEncryptedChat;
                if (chat == null) return;

                SecretDialogDetailsViewModel.SendEncryptedMediaInternal(chat, obj, _mtProtoService, _cacheService);
            }
        }

        private static TLInputEncryptedFileBase GetInputFile(TLLong fileId, TLInt partsCount, TLString key, TLString iv)
        {
            var keyData = key.Data;
            var ivData = iv.Data;
            var digest = Telegram.Api.Helpers.Utils.ComputeMD5(TLUtils.Combine(keyData, ivData));
            var fingerprint = new byte[4];
            var sub1 = digest.SubArray(0, 4);
            var sub2 = digest.SubArray(4, 4);
            for (var i = 0; i < 4; i++)
            {
                fingerprint[i] = (byte)(sub1[i] ^ sub2[i]);
            }

            var uploadedFile = new TLInputEncryptedFileUploaded
            {
                Id = fileId,
                MD5Checksum = new TLString(""),
                KeyFingerprint = new TLInt(BitConverter.ToInt32(fingerprint, 0)),
                Parts = partsCount //new TLInt(item.Parts.Count)
            };

            return uploadedFile;
        }

        private static TLInputEncryptedFileBase GetInputFileBig(TLLong fileId, TLInt partsCount, TLString key, TLString iv)
        {
            var keyData = key.Data;
            var ivData = iv.Data;
            var digest = Telegram.Api.Helpers.Utils.ComputeMD5(TLUtils.Combine(keyData, ivData));
            var fingerprint = new byte[4];
            var sub1 = digest.SubArray(0, 4);
            var sub2 = digest.SubArray(4, 4);
            for (var i = 0; i < 4; i++)
            {
                fingerprint[i] = (byte)(sub1[i] ^ sub2[i]);
            }

            var uploadedFile = new TLInputEncryptedFileBigUploaded
            {
                Id = fileId,
                KeyFingerprint = new TLInt(BitConverter.ToInt32(fingerprint, 0)),
                Parts = partsCount
            };

            return uploadedFile;
        }

        private static string MD5HashFromBytes(string path)
        {
            using (var storage = IsolatedStorageFile.GetUserStoreForApplication())
            {
                using (var stream = storage.OpenFile(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var bytes = new byte[stream.Length];
                    stream.Read(bytes, 0, bytes.Length);
                    return MD5Core.GetHashString(bytes).ToUpperInvariant();
                }
            }
        }


        private static string MD5Hash(string path)
        {
            using (var storage = IsolatedStorageFile.GetUserStoreForApplication())
            {
                using (var stream = storage.OpenFile(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var md5Provider = new MD5CryptoServiceProvider();
                    md5Provider.ComputeHash(stream);
                    return BitConverter.ToString(md5Provider.Hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }

        private void HandleUploadableItemInternal(UploadableItem item, TLMessage25 message)
        {
            Execute.BeginOnUIThread(() =>
            {
                if (item.FileNotFound)
                {
                    message.Media.UploadingProgress = 1.0;
                    message.Status = MessageStatus.Failed;

                    return;
                }

                if (message.Status == MessageStatus.Failed)
                {
                    return;
                }

                message.Media.UploadingProgress = 1.0;

                var audioMedia = message.Media as TLMessageMediaAudio;
                if (audioMedia != null)
                {
                    var audio = audioMedia.Audio as TLAudio;
                    if (audio == null) return;

                    var uploadedAudio = new TLInputMediaUploadedAudio
                    {
                        Duration = audio.Duration,
                        File = new TLInputFile
                        {
                            Id = item.FileId,
                            MD5Checksum = TLString.Empty,
                            Name = new TLString(audioMedia.IsoFileName),
                            Parts = new TLInt(item.Parts.Count)
                        },
                        MimeType = audio.MimeType
                    };

                    message.InputMedia = uploadedAudio;
                    SendMediaInternal(message, _mtProtoService, _stateService, _cacheService);
                    return;
                }

                var videoMedia = message.Media as TLMessageMediaVideo28;
                if (videoMedia != null)
                {
                    var video = videoMedia.Video as TLVideo;
                    if (video == null) return;

                    TLInputFileBase file;
                    if (item.IsSmallFile)
                    {
                        file = new TLInputFile
                        {
                            Id = item.FileId,
                            MD5Checksum = TLString.Empty,
                            Name = video.Caption,
                            Parts = new TLInt(item.Parts.Count)
                        };
                    }
                    else
                    {
                        file = new TLInputFileBig
                        {
                            Id = item.FileId,
                            Name = video.Caption,
                            Parts = new TLInt(item.Parts.Count)
                        };
                    }

                    TLInputMediaBase uploadedVideo;
                    if (video.ThumbInputFile != null)
                    {
                        uploadedVideo = new TLInputMediaUploadedThumbVideo28
                        {
                            Duration = video.Duration,
                            W = video.W,
                            H = video.H,
                            File = file,
                            Thumb = video.ThumbInputFile,
                            MimeType = video.MimeType,
                            Caption = videoMedia.Caption ?? TLString.Empty
                        };
                    }
                    else
                    {
                        uploadedVideo = new TLInputMediaUploadedVideo28
                        {
                            Duration = video.Duration,
                            W = video.W,
                            H = video.H,
                            File = file,
                            MimeType = video.MimeType,
                            Caption = videoMedia.Caption ?? TLString.Empty
                        };
                    }

                    message.InputMedia = uploadedVideo;
                    SendMediaInternal(message, _mtProtoService, _stateService, _cacheService);
                    return;
                }

                var mediaDocument = message.Media as TLMessageMediaDocument;
                if (mediaDocument != null)
                {
                    var document = mediaDocument.Document as TLDocument;
                    if (document == null) return;

                    TLInputFileBase file;
                    if (item.IsSmallFile)
                    {
                        file = new TLInputFile
                        {
                            Id = item.FileId,
                            MD5Checksum = TLString.Empty,
                            Name = document.FileName,
                            Parts = new TLInt(item.Parts.Count)
                        };
                    }
                    else
                    {
                        file = new TLInputFileBig
                        {
                            Id = item.FileId,
                            Name = document.FileName,
                            Parts = new TLInt(item.Parts.Count)
                        };
                    }

                    TLInputMediaBase uploadedDocument;
                    if (document.ThumbInputFile != null)
                    {
                        uploadedDocument = new TLInputMediaUploadedThumbDocument22
                        {
                            MD5Hash = new byte[0],
                            Attributes = new TLVector<TLDocumentAttributeBase> { new TLDocumentAttributeFileName { FileName = document.FileName } },
                            MimeType = document.MimeType,
                            File = file,
                            Thumb = document.ThumbInputFile
                        };
                    }
                    else
                    {
                        uploadedDocument = new TLInputMediaUploadedDocument22
                        {
                            MD5Hash = new byte[] { },
                            Attributes = new TLVector<TLDocumentAttributeBase> { new TLDocumentAttributeFileName { FileName = document.FileName } },
                            MimeType = document.MimeType,
                            File = file
                        };
                    }

                    message.InputMedia = uploadedDocument;
                    SendMediaInternal(message, _mtProtoService, _stateService, _cacheService);
                    return;
                }

                var photoMedia = message.Media as TLMessageMediaPhoto28;
                if (photoMedia != null)
                {
                    var uploadedPhoto = new TLInputMediaUploadedPhoto28
                    {
                        MD5Hash = new byte[] { },
                        File = new TLInputFile
                        {
                            Id = item.FileId,
                            MD5Checksum = TLString.Empty,
                            Name = new TLString("file.jpg"),
                            Parts = new TLInt(item.Parts.Count)
                        },
                        Caption = photoMedia.Caption ?? TLString.Empty
                    };
                    message.InputMedia = uploadedPhoto;
                    SendMediaInternal(message, _mtProtoService, _stateService, _cacheService);
                }
            });
        }

        public static void SendMediaInternal(TLMessage25 message, IMTProtoService mtProtoService, IStateService stateService, ICacheService cacheService)
        {
            var inputPeer = DialogDetailsViewModel.PeerToInputPeer(message.ToId);

            if (inputPeer is TLInputPeerBroadcast && !(inputPeer is TLInputPeerChannel))
            {
                var broadcast = IoC.Get<ICacheService>().GetBroadcast(message.ToId.Id);
                var contacts = new TLVector<TLInputUserBase>();

                foreach (var participantId in broadcast.ParticipantIds)
                {
                    var contact = IoC.Get<ICacheService>().GetUser(participantId);
                    contacts.Add(contact.ToInputUser());
                }

                mtProtoService.SendBroadcastAsync(contacts, message.InputMedia, message,
                    result =>
                    {
                        message.Status = MessageStatus.Confirmed;
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
                    });
            }
            else
            {
                var photoSize = GetPhotoSize(message);

                mtProtoService.SendMediaAsync(
                    inputPeer, message.InputMedia, message,
                    updates =>
                    {
                        ProcessSentPhoto(message, photoSize, stateService);
                        ProcessSentDocument(message, stateService);
                        ProcessSentAudio(message);
                    },
                    error => Execute.BeginOnUIThread(() =>
                    {
                        if (error.TypeEquals(ErrorType.PEER_FLOOD))
                        {
                            MessageBox.Show(AppResources.PeerFloodSendMessage, AppResources.Error, MessageBoxButton.OK);
                        }
                        else
                        {
                            Execute.ShowDebugMessage("messages.sendMedia error: " + error);
                        }
                        if (message.Status == MessageStatus.Sending)
                        {
                            message.Status = message.Index != 0 ? MessageStatus.Confirmed : MessageStatus.Failed;
                        }
                    }));
            }
        }

        private static TLPhotoSize GetPhotoSize(TLMessage25 message)
        {
            var mediaPhoto = message.Media as TLMessageMediaPhoto;
            if (mediaPhoto != null)
            {
                var photo = mediaPhoto.Photo as TLPhoto;
                if (photo != null)
                {
                    return photo.Sizes.FirstOrDefault() as TLPhotoSize;
                }
            }

            return null;
        }

        private static void ProcessSentPhoto(TLMessage m, TLPhotoSize oldPhotoSize, IStateService stateService)
        {
            if (m != null && m.InputMedia != null && m.InputMedia.MD5Hash != null)
            {
                var mediaPhoto = m.Media as TLMessageMediaPhoto;
                if (mediaPhoto != null)
                {
                    var photo = mediaPhoto.Photo as TLPhoto;
                    if (photo != null)
                    {
                        Execute.BeginOnThreadPool(() =>
                        {
                            if (oldPhotoSize != null)
                            {
                                var newPhotoSizeM = photo.Sizes.FirstOrDefault(x => TLString.Equals(x.Type, new TLString("m"), StringComparison.OrdinalIgnoreCase)) as TLPhotoSize;
                                var newPhotoSizeX = photo.Sizes.FirstOrDefault(x => TLString.Equals(x.Type, new TLString("x"), StringComparison.OrdinalIgnoreCase)) as TLPhotoSize;

                                var oldFileLocation = string.Format("{0}_{1}_{2}.jpg", oldPhotoSize.Location.VolumeId, oldPhotoSize.Location.LocalId, oldPhotoSize.Location.Secret);
                                var newFileLocationM = newPhotoSizeM != null? string.Format("{0}_{1}_{2}.jpg", newPhotoSizeM.Location.VolumeId, newPhotoSizeM.Location.LocalId, newPhotoSizeM.Location.Secret) : string.Empty;
                                var newFileLocationX = newPhotoSizeX != null ? string.Format("{0}_{1}_{2}.jpg", newPhotoSizeX.Location.VolumeId, newPhotoSizeX.Location.LocalId, newPhotoSizeX.Location.Secret) : string.Empty;
                                using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                                {
                                    if (store.FileExists(oldFileLocation))
                                    {
                                        if (!string.IsNullOrEmpty(newFileLocationM)) store.CopyFile(oldFileLocation, newFileLocationM);
                                        if (!string.IsNullOrEmpty(newFileLocationX)) store.CopyFile(oldFileLocation, newFileLocationX);
                                        store.DeleteFile(oldFileLocation);
                                    }
                                }
                            }
                        });

                        if (m.InputMedia.MD5Hash.Length > 0)
                        {
                            AddServerFileAsync(stateService, new TLServerFile
                            {
                                MD5Checksum = new TLLong(BitConverter.ToInt64(m.InputMedia.MD5Hash, 0)),
                                Media = new TLInputMediaPhoto { Id = new TLInputPhoto { Id = photo.Id, AccessHash = photo.AccessHash } }
                            });
                        }
                    }
                }
            }
        }

        private static void AddServerFileAsync(IStateService stateService, TLServerFile file)
        {
            stateService.GetServerFilesAsync(
                results =>
                {
                    results.Add(file);
                    stateService.SaveServerFilesAsync(results);
                });
        }

        private static void ProcessSentDocument(TLMessage m, IStateService stateService)
        {
            if (m != null && m.InputMedia != null && m.InputMedia.MD5Hash != null)
            {
                var mediaDocument = m.Media as TLMessageMediaDocument;
                if (mediaDocument != null)
                {
                    var document = mediaDocument.Document as TLDocument;
                    if (document != null)
                    {
                        if (m.InputMedia.MD5Hash.Length >= 8)
                        {
                            AddServerFileAsync(stateService, new TLServerFile
                            {
                                MD5Checksum = new TLLong(BitConverter.ToInt64(m.InputMedia.MD5Hash, 0)),
                                Media = new TLInputMediaDocument { Id = new TLInputDocument { Id = document.Id, AccessHash = document.AccessHash } }
                                //Media = m.InputMedia
                            });
                        }
                    }
                }
            }
        }

        private static void ProcessSentAudio(TLMessage message)
        {
            var mediaAudio = message.Media as TLMessageMediaAudio;
            if (mediaAudio == null) return;

            // иначе в UI остается в качестве DataContext отправляетмый TLAudio с рандомным Id, AccessHash
            message.NotifyOfPropertyChange(() => message.Media);

            // rename local copy of uploaded media
            var sourceFileName = message.Media.IsoFileName;
            if (string.IsNullOrEmpty(sourceFileName)) return;

            var wavSourceFileName = Path.GetFileNameWithoutExtension(sourceFileName) + ".wav";

            using (var store = IsolatedStorageFile.GetUserStoreForApplication())
            {
                if (store.FileExists(sourceFileName))
                {
                    store.DeleteFile(sourceFileName);
                }

                if (store.FileExists(wavSourceFileName))
                {
                    var audio = mediaAudio.Audio as TLAudio;
                    if (audio != null)
                    {
                        var destinationFileName = audio.GetFileName();

                        if (!string.IsNullOrEmpty(destinationFileName))
                        {
                            var wavDestinationFileName = Path.GetFileNameWithoutExtension(destinationFileName) + ".wav";
                            store.MoveFile(wavSourceFileName, wavDestinationFileName);
                        }
                    }
                }
            }
        }

        public void Resend(TLMessage25 message)
        {
            if (message.Media is TLMessageMediaEmpty)
            {
               DialogDetailsViewModel.SendInternal(message, _mtProtoService);
            }
        }

        public void ChangePasscodeState()
        {
            PasscodeUtils.ChangeLocked();
            NotifyOfPropertyChange(() => PasscodeImageSource);
            NotifyOfPropertyChange(() => PasscodeImageBrush);
        }

        public void Handle(TLDecryptedMessageBase message)
        {
            if (_stateService.SuppressNotifications) return;
            if (message.Out.Value) return;

            var from = _cacheService.GetUser(message.FromId);
            if (from == null) return;

            _stateService.GetNotifySettingsAsync(
               s =>
               {
                   try
                   {
                       var activeDialog = _stateService.ActiveDialog;
                       var toId = message.ChatId;
                       var fromId = message.FromId;
                       var suppressNotification = activeDialog is TLEncryptedChatBase && toId.Value == ((TLEncryptedChatBase)activeDialog).Id.Value;
                       if (suppressNotification) return;

                       var isDisplayedMessage = TLUtils.IsDisplayedDecryptedMessageInternal(message);
                       if (!isDisplayedMessage) return;

                       if (s.InAppMessagePreview)
                       {
                           var frame = Application.Current.RootVisual as TelegramTransitionFrame;
                           if (frame != null)
                           {
                               var title = from.FullName;

                               var text = DialogToBriefInfoConverter.Convert(message, true).Replace("\n", " ");

                               Deployment.Current.Dispatcher.BeginInvoke(() =>
                               {

                                   var toast = new Telegram.Controls.Notifications.ToastPrompt
                                   {
                                       DataContext = from,
                                       TextOrientation = Orientation.Horizontal,
                                       Foreground = new SolidColorBrush(Colors.White),
                                       FontSize = (double)Application.Current.Resources["PhoneFontSizeSmall"],
                                       Title = title,
                                       Message = text,
                                       ImageHeight = 48,
                                       ImageWidth = 48,
                                       ImageSource = new BitmapImage(new Uri("/ToastPromptIcon.png", UriKind.Relative))
                                   };

                                   toast.Tap += (sender, args) =>
                                   {
                                       var encryptedChat = _cacheService.GetEncryptedChat(message.ChatId);

                                       _stateService.Participant = from;
                                       _stateService.With = encryptedChat;
                                       _stateService.RemoveBackEntries = true;
                                       _navigationService.UriFor<SecretDialogDetailsViewModel>().WithParam(x => x.RandomParam, Guid.NewGuid().ToString()).Navigate();
                                   };

                                   toast.Show();
                               });
                           }
                       }

                       if (s.InAppVibration)
                       {
                           VibrateController.Default.Start(TimeSpan.FromMilliseconds(300));
                       }

                       if (s.InAppSound)
                       {
                           var sound = "Sounds/Default.wav";
                           //if (toId is TLPeerEncryptedChat && !string.IsNullOrEmpty(s.GroupSound))
                           //{
                           //    sound = "Sounds/" + s.GroupSound + ".wav";
                           //}
                           //else 
                               if (!string.IsNullOrEmpty(s.ContactSound))
                           {
                               sound = "Sounds/" + s.ContactSound + ".wav";
                           }

                           //if (toId is TLPeerChat && chat != null && chat.NotifySettings is TLPeerNotifySettings)
                           //{
                           //    sound = "Sounds/" + ((TLPeerNotifySettings)chat.NotifySettings).Sound.Value + ".wav";
                           //}
                           //else 
                               if (/*toId is TLPeerUser &&*/ from != null && from.NotifySettings is TLPeerNotifySettings)
                           {
                               sound = "Sounds/" + ((TLPeerNotifySettings)from.NotifySettings).Sound.Value + ".wav";
                           }

                           if (!Telegram.Api.Helpers.Utils.XapContentFileExists(sound))
                           {
                               sound = "Sounds/Default.wav";
                           }

                           var stream = TitleContainer.OpenStream(sound);
                           var effect = SoundEffect.FromStream(stream);

                           FrameworkDispatcher.Update();
                           effect.Play();
                       }

                   }
                   catch (Exception e)
                   {
                       TLUtils.WriteLine(e.ToString(), LogSeverity.Error);
                   }

               });
        }

        public void Handle(TLMessageCommon message)
        {
            if (_stateService.SuppressNotifications) return;
            if (message.Out.Value) return;
            if (!message.Unread.Value) return;

            var from = _cacheService.GetUser(message.FromId);
            if (from == null) return;

            _stateService.GetNotifySettingsAsync(
                s =>
                {
                    try
                    {
                        var activeDialog = _stateService.ActiveDialog;
                        var toId = message.ToId;
                        var fromId = message.FromId;
                        var suppressNotification = false;
                        TLDialogBase dialog = null;

                        if (toId is TLPeerChat
                            && activeDialog is TLChat 
                            && toId.Id.Value == ((TLChat) activeDialog).Id.Value)
                        {
                            suppressNotification = true;
                        }
                        else if (toId is TLPeerUser
                            && activeDialog is TLUserBase
                            && fromId.Value == ((TLUserBase) activeDialog).Id.Value)
                        {
                            suppressNotification = true;
                        }

                        if (suppressNotification) return;

                        TLChatBase chat = null;
                        TLUserBase user = null;
                        if (message.ToId is TLPeerChat)
                        {
                            chat = _cacheService.GetChat(message.ToId.Id);
                            dialog = _cacheService.GetDialog(new TLPeerChat {Id = message.ToId.Id});
                        }
                        else
                        {
                            if (message.Out.Value)
                            {
                                user = _cacheService.GetUser(message.ToId.Id);
                                dialog = _cacheService.GetDialog(new TLPeerUser { Id = message.ToId.Id });
                            }
                            else
                            {
                                user = _cacheService.GetUser(message.FromId);
                                dialog = _cacheService.GetDialog(new TLPeerUser { Id = message.FromId });
                            }
                        }

                        if (chat != null && chat.NotifySettings != null)
                        {
                            var notifySettings = chat.NotifySettings as TLPeerNotifySettings;
                            if (notifySettings != null && notifySettings.MuteUntil.Value != 0)
                            {
                                suppressNotification = true;
                            }
                        }

                        if (user != null && user.NotifySettings != null)
                        {
                            var notifySettings = user.NotifySettings as TLPeerNotifySettings;
                            if (notifySettings != null && notifySettings.MuteUntil.Value != 0)
                            {
                                suppressNotification = true;
                            }
                        }

                        if (suppressNotification) return;

                        if (dialog != null)
                        {
                            suppressNotification = CheckLastNotificationTime(dialog);
                        }

                        if (suppressNotification) return;

                        if (s.InAppMessagePreview)
                        {
                            Execute.BeginOnUIThread(() =>
                            {
                                var frame = Application.Current.RootVisual as TelegramTransitionFrame;
                                if (frame != null)
                                {
                                    var shellView = frame.Content as ShellView;
                                    if (shellView == null)
                                    {
                                        var title = message.ToId is TLPeerChat
                                            ? string.Format("{0}@{1}", from.FullName, chat.FullName)
                                            : from.FullName;

                                        var text = DialogToBriefInfoConverter.Convert(message, true).Replace("\n", " ");

                                        var toast = new Telegram.Controls.Notifications.ToastPrompt
                                        {
                                            DataContext = from,
                                            TextOrientation = Orientation.Horizontal,
                                            Foreground = new SolidColorBrush(Colors.White),
                                            FontSize = (double)Application.Current.Resources["PhoneFontSizeSmall"],
                                            Title = title,
                                            Message = text,
                                            ImageHeight = 48,
                                            ImageWidth = 48,
                                            ImageSource = new BitmapImage(new Uri("/ToastPromptIcon.png", UriKind.Relative))
                                        };

                                        toast.Tap += (sender, args) =>
                                        {
                                            _stateService.With = message.ToId is TLPeerChat ? (TLObject)chat : from;
                                            _stateService.RemoveBackEntries = true;
                                            _navigationService.UriFor<DialogDetailsViewModel>().WithParam(x => x.RandomParam, Guid.NewGuid().ToString()).Navigate();
                                        };

                                        toast.Show();
                                    }
                                }
                            });
                        }

                        if (_lastNotificationTime.HasValue)
                        {
                            var fromLastNotification = (DateTime.Now - _lastNotificationTime.Value).TotalSeconds;
                            if (fromLastNotification > 0.0 && fromLastNotification < 2.0)
                            {
                                suppressNotification = true;
                            }
                        }
                        _lastNotificationTime = DateTime.Now;

                        if (suppressNotification) return;


                        if (s.InAppVibration)
                        {
                            VibrateController.Default.Start(TimeSpan.FromMilliseconds(300));
                        }

                        if (s.InAppSound)
                        {
                            var sound = "Sounds/Default.wav";
                            if (toId is TLPeerChat && !string.IsNullOrEmpty(s.GroupSound))
                            {
                                sound = "Sounds/" + s.GroupSound + ".wav";
                            }
                            else if (!string.IsNullOrEmpty(s.ContactSound))
                            {
                                sound = "Sounds/" + s.ContactSound + ".wav";
                            }

                            if (toId is TLPeerChat && chat != null && chat.NotifySettings is TLPeerNotifySettings)
                            {
                                sound = "Sounds/" + ((TLPeerNotifySettings)chat.NotifySettings).Sound.Value + ".wav";
                            }
                            else if (toId is TLPeerUser && user != null && user.NotifySettings is TLPeerNotifySettings)
                            {
                                sound = "Sounds/" + ((TLPeerNotifySettings)user.NotifySettings).Sound.Value + ".wav";
                            }

                            if (!Telegram.Api.Helpers.Utils.XapContentFileExists(sound))
                            {
                                sound = "Sounds/Default.wav";
                            }
                            
                            var stream = TitleContainer.OpenStream(sound);
                            var effect = SoundEffect.FromStream(stream);

                            FrameworkDispatcher.Update();
                            effect.Play();
                        }

                    }
                    catch (Exception e)
                    {
                        TLUtils.WriteLine(e.ToString(), LogSeverity.Error);
                    }

                });
        }

        private bool CheckLastNotificationTime(TLDialogBase dialog)
        {
            if (dialog != null)
            {
                var notifySettings = dialog.NotifySettings as TLPeerNotifySettings;
                if (notifySettings != null && notifySettings.MuteUntil.Value != 0)
                {
                    dialog.LastNotificationTime = null;
                    dialog.UnmutedCount = 0;
                    return true;
                }

                if (dialog.LastNotificationTime == null)
                {
                    dialog.LastNotificationTime = DateTime.Now;
                    dialog.UnmutedCount = 1;
                    return false;
                }
                else
                {
                    var interval = (DateTime.Now - dialog.LastNotificationTime.Value).TotalSeconds;
                    if (interval <= Constants.NotificationInterval)
                    {
                        var unmutedCount = dialog.UnmutedCount;
                        if (unmutedCount < Constants.UnmutedCount)
                        {
                            dialog.UnmutedCount++;
                            return false;
                        }
                        else
                        {
                            dialog.UnmutedCount++;
                            return true;
                        }
                    }
                    else
                    {
                        dialog.LastNotificationTime = DateTime.Now;
                        dialog.UnmutedCount = 1;
                        return false;
                    }
                }
            }

            return false;
        }

        private DateTime? _lastNotificationTime;

        public void Review()
        {
            new MarketplaceReviewTask().Show();
        }

        public void OpenKey()
        {
            _mtProtoService.GetConfigInformationAsync(info =>
            {
                Execute.BeginOnUIThread(() =>
                {
                    MessageBox.Show(info);
                });
            });
        }

        public void GetCurrentPacketInfo()
        {
            var packetInfo = _mtProtoService.GetTransportInfo();

            MessageBox.Show(packetInfo);
        }

        public void PingDelayDisconnect(int disconnectDelay)
        {
            MTProtoService.PingDelayDisconnectAsync(TLLong.Random(), new TLInt(disconnectDelay),
                result => Execute.ShowDebugMessage("pingDelayDisconnect result: pong" + result.PingId.Value),
                error => Execute.ShowDebugMessage("pingDelayDisconnect error: " + error));
        }

        public void Handle(UpdatingEventArgs args)
        {
            if (_mtProtoService != null)
            {
                var timeout = 25.0;
#if DEBUG
                timeout = 25.0;
#endif
                _mtProtoService.SetMessageOnTime(timeout, AppResources.Updating + "...");
            }
        }

        public void Handle(UpdateCompletedEventArgs args)
        {
            if (_mtProtoService != null)
            {
                _mtProtoService.SetMessageOnTime(0.0, string.Empty);
            }
        }

        public void Handle(DownloadableItem item)
        {
            var sticker = item.Owner as TLStickerItem;
            if (sticker != null)
            {
                sticker.NotifyOfPropertyChange(() => sticker.Self);
            }
        }

        public void Handle(TLUpdateContactRegistered contactRegistered)
        {
            var user = _cacheService.GetUser(contactRegistered.UserId);

            if (user == null)
            {
                MTProtoService.GetFullUserAsync(new TLInputUserContact { UserId = contactRegistered.UserId },
                    userFull =>
                    {
                        user = userFull.ToUser();
                        CreateContactRegisteredMessage(user);
                    });
            }
            else
            {
                CreateContactRegisteredMessage(user);
            }
        }

        private void CreateContactRegisteredMessage(TLUserBase user)
        {
            var message = new TLMessageService17
            {
                Flags = new TLInt(0),
                Id = new TLInt(0),
                FromId = user.Id,
                ToId = new TLPeerUser { Id = new TLInt(StateService.CurrentUserId) },
                Status = MessageStatus.Confirmed,
                Out = new TLBool(false),
                Unread = new TLBool(true),
                Date = TLUtils.DateToUniversalTimeTLInt(MTProtoService.ClientTicksDelta, DateTime.Now),
                Action = new TLMessageActionContactRegistered { UserId = user.Id },
                //IsAnimated = true,
                RandomId = TLLong.Random()
            };

            Contacts.Handle(user);

            var dialog = _cacheService.GetDialog(new TLPeerUser {Id = user.Id});
            if (dialog == null)
            {
                _cacheService.SyncMessage(message, new TLPeerUser { Id = user.Id }, m => Handle(message));
            }
        }

        public void Handle(ExceptionInfo info)
        {
            BugSenseHandler.Instance.LogError(info.Exception, info.Caption, new NotificationOptions { Type = enNotificationType.None });
        }
    }
}
