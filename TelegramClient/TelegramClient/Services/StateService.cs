using System;
using System.Collections.Generic;
using System.Device.Location;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Telegram.Api.TL.Interfaces;
using TelegramClient.ViewModels.Contacts;
#if WP81
using Windows.Storage;
#endif
using Caliburn.Micro;
using ImageTools;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.TL;
using TelegramClient.Models;
using TelegramClient.ViewModels.Additional;
using TelegramClient.ViewModels.Media;

namespace TelegramClient.Services
{
    public class StateService : PropertyChangedBase, IStateService
    {
        public string Url { get; set; }
        public string UrlText { get; set; }

        public TLChannelParticipantRoleBase CurrentRole { get; set; }
        public TLChannel NewChannel { get; set; }

        public TLMessageBase Message { get; set; }
        public Uri WebLink { get; set; }
        public string AccessToken { get; set; }
        public TLUserBase Bot { get; set; }
        public int SelectedAutolockTimeout { get; set; }
        public string LogFileName { get; set; }

        public GeoCoordinate GeoCoordinate { get; set; }

        public bool AnimateTitle { get; set; }
        public string ShareCaption { get; set; }
        public string ShareLink { get; set; }
        public string ShareMessage { get; set; }

        public bool IsInviteVisible { get; set; }

        public TLPasswordBase Password { get; set; }
        public TLPasswordInputSettings NewPasswordSettings { get; set; }

        public string Hashtag { get; set; }

        public TLDialogBase Dialog { get; set; }

        public bool FirstRun { get; set; }
        public TLString PhoneNumber { get; set; }
        public string PhoneNumberString { get; set; }
        public TLString PhoneCode { get; set; }
        public TLString PhoneCodeHash { get; set; }
        public TLInt SendCallTimeout { get; set; }
        public TLBool PhoneRegistered { get; set; }
        public bool ClearNavigationStack { get; set; }
        public TLUserBase CurrentContact { get; set; }
        public TLString CurrentContactPhone { get; set; }

        public int CurrentUserId
        {
            get
            {
                return SettingsHelper.GetValue<int>(Constants.CurrentUserKey);
            }
            set
            {
                var mtProtoService = IoC.Get<IMTProtoService>();
                mtProtoService.CurrentUserId = new TLInt(value);

                SettingsHelper.CrossThreadAccess(
                    settings =>
                    {
                        settings[Constants.CurrentUserKey] = value;
                        settings.Save();
                    });
            }
        }

        private Color? _currentBackgroundColor;

        public Color CurrentBackgroundColor
        {
            get
            {
                if (_currentBackgroundColor.HasValue)
                {
                    return _currentBackgroundColor.Value;
                }

                var background = CurrentBackground;

                var color = Colors.Black;
                color.A = 153;  //99000000

                var blackTransparent = Colors.Black;
                blackTransparent.A = 0;

                _currentBackgroundColor = background != null && background.Name != "Empty" ? color : blackTransparent;

                return _currentBackgroundColor.Value;
            }
        }

        private Brush _currentForegroundBrush;

        public Brush CurrentForegroundBrush
        {
            get
            {
                if (_currentForegroundBrush != null)
                {
                    return _currentForegroundBrush;
                }

                var background = CurrentBackground;

                var brush = new SolidColorBrush(Colors.White);
                var defaultBrush = (Brush)Application.Current.Resources["PhoneForegroundBrush"];

                _currentForegroundBrush = background != null && background.Name != "Empty" ? brush : defaultBrush;

                return _currentForegroundBrush;
            }
        }

        private Brush _currentForegroundSubtleBrush;

        public Brush CurrentForegroundSubtleBrush
        {
            get
            {
                if (_currentForegroundSubtleBrush != null)
                {
                    return _currentForegroundSubtleBrush;
                }

                var background = CurrentBackground;

                var brush = new SolidColorBrush(Colors.White){Opacity = 0.7};
               
                var defaultBrush = (Brush)Application.Current.Resources["PhoneSubtleBrush"];

                _currentForegroundSubtleBrush = background != null && background.Name != "Empty" ? brush : defaultBrush;

                return _currentForegroundSubtleBrush;
            }
        }

        private BackgroundItem _currentBackground;

        public BackgroundItem CurrentBackground
        {
            get
            {
                if (_currentBackground == null)
                {
                    _currentBackground = SettingsHelper.GetValue<BackgroundItem>(Constants.CurrentBackgroundKey);
                }

                return _currentBackground;
            }
            set
            {
                _currentBackground = value;

                SettingsHelper.CrossThreadAccess(
                    settings =>
                    {
                        settings[Constants.CurrentBackgroundKey] = value;
                        settings.Save();

                        _currentBackgroundColor = null;
                        _currentForegroundBrush = null;
                        _currentForegroundSubtleBrush = null;

                        NotifyOfPropertyChange(() => CurrentBackground);
                    });
            }
        }

        public bool IsEmptyBackground
        {
            get { return CurrentBackground == null || CurrentBackground.Name == "Empty"; }
        }

        public bool SendByEnter
        {
            get
            {
                return SettingsHelper.GetValue<bool>(Constants.SendByEnterKey);
            }
            set
            {
                SettingsHelper.CrossThreadAccess(
                    settings =>
                    {
                        settings[Constants.SendByEnterKey] = value;
                        settings.Save();
                        NotifyOfPropertyChange(() => SendByEnter);
                    });
            }
        }

        public void ResetPasscode()
        {
            PasscodeUtils.Reset();
        }

        public DateTime CloseTime
        {
            get
            {
                return SettingsHelper.GetValue<DateTime>(Constants.AppCloseTimeKey);
            }
            set
            {
                SettingsHelper.CrossThreadAccess(
                    settings =>
                    {
                        settings[Constants.AppCloseTimeKey] = value;
                        settings.Save();
                    });

                NotifyOfPropertyChange(() => CloseTime);
            }
        }

        public string Passcode
        {
            get
            {
                return SettingsHelper.GetValue<string>(Constants.PasscodeKey);
            }
            set
            {
                SettingsHelper.CrossThreadAccess(
                    settings =>
                    {
                        settings[Constants.PasscodeKey] = value;
                        settings.Save();
                    });

                NotifyOfPropertyChange(() => Passcode);
            }
        }

        public bool IsSimplePasscode
        {
            get
            {
                return SettingsHelper.GetValue<bool>(Constants.IsSimplePasscodeKey);
            }
            set
            {
                SettingsHelper.CrossThreadAccess(
                    settings =>
                    {
                        settings[Constants.IsSimplePasscodeKey] = value;
                        settings.Save();
                    });

                NotifyOfPropertyChange(() => IsSimplePasscode);
            }
        }

        public bool Locked
        {
            get
            {
                return SettingsHelper.GetValue<bool>(Constants.IsPasscodeEnabledKey);
            }
            set
            {
                SettingsHelper.CrossThreadAccess(
                    settings =>
                    {
                        settings[Constants.IsPasscodeEnabledKey] = value;
                        settings.Save();
                    });

                NotifyOfPropertyChange(() => Locked);
            }
        }

        public int AutolockTimeout
        {
            get
            {
                return SettingsHelper.GetValue<int>(Constants.PasscodeAutolockKey);
            }
            set
            {
                SettingsHelper.CrossThreadAccess(
                    settings =>
                    {
                        settings[Constants.PasscodeAutolockKey] = value;
                        settings.Save();
                    });

                NotifyOfPropertyChange(() => AutolockTimeout);
            }
        }

        public WriteableBitmap ActiveBitmap { get; set; }

        public bool CreateSecretChat { get; set; }
        public TLEncryptedChatBase CurrentEncryptedChat { get; set; }
        public TLString CurrentKey { get; set; }
        public TLLong CurrentKeyFingerprint { get; set; }
        public bool MediaTab { get; set; }

        public TLObject With { get; set; }
        public IList<TLMessageBase> DialogMessages { get; set; }
        public IList<TLDialogBase> LoadedDialogs { get; set; }
        public bool RemoveBackEntry { get; set; }
        public bool RemoveBackEntries { get; set; }
        public TLGeoPoint GeoPoint { get; set; }
        public TLMessageMediaVenue Venue { get; set; }
        public TLMessage MediaMessage { get; set; }
        public TLDecryptedMessage DecryptedMediaMessage { get; set; }

        public Photo Photo { get; set; }
        public string FileId { get; set; }
        public Photo Document { get; set; }
        public byte[] ProfilePhotoBytes { get; set; }
        public TLChatBase CurrentChat { get; set; }
        public IInputPeer CurrentInputPeer { get; set; }

        private readonly List<string> _sounds = new List<string> { "Default", "Sound1", "Sound2", "Sound3", "Sound4", "Sound5", "Sound6" }; 

        public List<string> Sounds
        {
            get { return _sounds; }
        }

        public TLUserBase Participant { get; set; }

        public TLMessage CurrentPhotoMessage { get; set; }
        public TLDecryptedMessage CurrentDecryptedPhotoMessage { get; set; }
        public int CurrentMediaMessageId { get; set; }
        public IList<TLMessage> CurrentMediaMessages { get; set; }
        public IList<TLDecryptedMessage> CurrentDecryptedMediaMessages { get; set; } 

        public TLPhotoBase CurrentPhoto { get; set; }


       // public bool InAppVibration { get; set; }
       // public bool InAppSound { get; set; }
       // public bool InAppMessagePreview { get; set; }


        public bool IsMainViewOpened { get; set; }
        public bool IsDialogOpened { get; set; }
        public TLObject ActiveDialog { get; set; }

        public TLUserBase SharedContact { get; set; }
        public string IsoFileName { get; set; }
        public Country SelectedCountry { get; set; }
        public bool FocusOnInputMessage { get; set; }
        public string VideoIsoFileName { get; set; }
        public long Duration { get; set; }
        public RecordedVideo RecordedVideo { get; set; }
        public IList<TLUserBase> RemovedUsers { get; set; }
        public List<TLMessageBase> ForwardMessages { get; set; }

        public bool SuppressNotifications { get; set; }

        public bool Tombstoning { get; set; }

        public string UserId { get; set; }
        public string ChatId { get; set; }
        public string BroadcastId { get; set; }

        public int ForwardingMessagesCount { get; set; }
        public bool RequestForwardingCount { get; set; }

        public ExtendedImage ExtendedImage { get; set; }

#if WP81
        public StorageFile VideoFile { get; set; }
        public CompressingVideoFile CompressingVideoFile { get; set; }
#endif
        public int AccountDaysTTL { get; set; }
        public TLPrivacyRules PrivacyRules { get; set; }
        public IPrivacyValueUsersRule UsersRule { get; set; }


        public IList<TLUserBase> SelectedUsers { get; set; }
        public IList<TLInt> SelectedUserIds { get; set; }
        public bool NavigateToDialogDetails { get; set; }
        public bool NavigateToSecretChat { get; set; }
        public string Domain { get; set; }
        public bool ChangePhoneNumber { get; set; }
        public TimerSpan SelectedTimerSpan { get; set; }
        public TLDCOption DCOption { get; set; }
        public TLDHConfig DHConfig { get; set; }
        public List<TLMessageBase> Source { get; set; }
        //public bool SearchDialogs { get; set; }

        #region Settings

        private readonly object _settingsRoot = new object();

        private Settings _settings;

        public void GetNotifySettingsAsync(Action<Settings> callback)
        {
            if (_settings != null)
            {
                callback(_settings);
                return;
            }

            Telegram.Api.Helpers.Execute.BeginOnThreadPool(() =>
            {
                _settings = TLUtils.OpenObjectFromFile<Settings>(_settingsRoot, Constants.CommonNotifySettingsFileName) ?? new Settings();
                callback(_settings);
            });
        }

        public void SaveNotifySettingsAsync(Settings settings)
        {
            TLUtils.SaveObjectToFile(_settingsRoot, Constants.CommonNotifySettingsFileName, settings);
        }

        #endregion

        private readonly object _serverFilesRoot = new object();

        private TLVector<TLServerFile> _serverFiles; 

        public void GetServerFilesAsync(Action<TLVector<TLServerFile>> callback)
        {
            if (_serverFiles != null)
            {
                callback(_serverFiles);
                return;
            }

            Telegram.Api.Helpers.Execute.BeginOnThreadPool(() =>
            {
                _serverFiles = TLUtils.OpenObjectFromMTProtoFile<TLVector<TLServerFile>>(_serverFilesRoot, Constants.CachedServerFilesFileName) ?? new TLVector<TLServerFile>();
                callback(_serverFiles);
            });
        }

        public void SaveServerFilesAsync(TLVector<TLServerFile> serverFiles)
        {
            TLUtils.SaveObjectToMTProtoFile(_serverFilesRoot, Constants.CachedServerFilesFileName, serverFiles);
        }

        private readonly object _allStickersRoot = new object();

        private TLAllStickers _allStickers; 

        public void GetAllStickersAsync(Action<TLAllStickers> callback)
        {
            if (_allStickers != null)
            {
                callback(_allStickers);
                return;
            }

            Telegram.Api.Helpers.Execute.BeginOnThreadPool(() =>
            {
                _allStickers = TLUtils.OpenObjectFromMTProtoFile<TLAllStickers>(_allStickersRoot, Constants.AllStickersFileName);
                callback(_allStickers);
            });
        }

        public void SaveAllStickersAsync(TLAllStickers allStickers)
        {
            Telegram.Api.Helpers.Execute.BeginOnThreadPool(() =>
            {
                _allStickers = allStickers;
                if (allStickers != null)
                {
                    var allStickers29 = allStickers as TLAllStickers29;
                    if (allStickers29 != null && allStickers29.RecentlyUsed != null)
                    {
                        allStickers29.RecentlyUsed = new TLVector<TLRecentlyUsedSticker>(allStickers29.RecentlyUsed.Take(20).ToList());
                    }
                }

                TLUtils.SaveObjectToMTProtoFile(_allStickersRoot, Constants.AllStickersFileName, allStickers);
            });
        }
    }
}
