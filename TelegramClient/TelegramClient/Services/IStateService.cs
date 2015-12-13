using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Device.Location;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;
using Windows.Storage.FileProperties;
using Caliburn.Micro;
using Telegram.Api.TL.Interfaces;
using TelegramClient.ViewModels.Contacts;
#if WP8
using Windows.Storage;
#endif
using ImageTools;
using Telegram.Api.TL;
using TelegramClient.Models;
using TelegramClient.ViewModels.Additional;
using TelegramClient.ViewModels.Media;

namespace TelegramClient.Services
{
    public class Photo
    {
        public string FileName { get; set; }

        public byte[] Bytes { get; set; }

#if WP8
        public StorageFile File { get; set; }
#endif

        public byte[] PreviewBytes { get; set; }

        public int Width { get; set; }

        public  int Height { get; set; }


    }

    public class PhotoFile : PropertyChangedBase
    {
        public StorageFile File { get; set; }

        public StorageItemThumbnail Thumbnail { get; set; }

        public TLMessage Message { get; set; }

        public PhotoFile Self { get { return this; } }

        public bool IsButton { get; set; }

        private bool _isSelected;

        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                if (value != _isSelected)
                {
                    _isSelected = value;
                    NotifyOfPropertyChange(() => IsSelected);
                }
            }
        }
    }

    public interface IStateService
    {
        string Url { get; set; }
        string UrlText { get; set; }

        TLChannelParticipantRoleBase CurrentRole { get; set; }
        TLChannel NewChannel { get; set; }

        int SelectedAutolockTimeout { get; set; }
        string LogFileName { get; set; }
        GeoCoordinate GeoCoordinate { get; set; }
        bool AnimateTitle { get; set; }

        string ShareCaption { get; set; }
        string ShareLink { get; set; }
        string ShareMessage { get; set; }

        string Hashtag { get; set; }

        bool FirstRun { get; set; }
        TLString PhoneNumber { get; set; }
        TLString PhoneCode { get; set; }
        TLString PhoneCodeHash { get; set; }
        TLInt SendCallTimeout { get; set; }
        TLBool PhoneRegistered { get; set; }
        bool ClearNavigationStack { get; set; }
        TLUserBase CurrentContact { get; set; }
        TLString CurrentContactPhone { get; set; }
        int CurrentUserId { get; set; }
        TLObject With { get; set; }
        bool RemoveBackEntry { get; set; }
        bool RemoveBackEntries { get; set; }
        TLGeoPoint GeoPoint { get; set; }
        TLMessageMediaVenue Venue { get; set; }
        TLMessage MediaMessage { get; set; }
        TLDecryptedMessage DecryptedMediaMessage { get; set; }
        Photo Photo { get; set; }
        string FileId { get; set; }
        Photo Document { get; set; }
        byte[] ProfilePhotoBytes { get; set; }
        TLChatBase CurrentChat { get; set; }
        IInputPeer CurrentInputPeer { get; set; }

        List<string> Sounds { get; }
        TLUserBase Participant { get; set; }
        TLMessage CurrentPhotoMessage { get; set; }
        TLDecryptedMessage CurrentDecryptedPhotoMessage { get; set; }
        int CurrentMediaMessageId { get; set; }
        IList<TLMessage> CurrentMediaMessages { get; set; }
        IList<TLDecryptedMessage> CurrentDecryptedMediaMessages { get; set; }
        TLPhotoBase CurrentPhoto { get; set; }



        void GetNotifySettingsAsync(Action<Settings> callback);
        void SaveNotifySettingsAsync(Settings settings);


        void GetServerFilesAsync(Action<TLVector<TLServerFile>> callback);
        void SaveServerFilesAsync(TLVector<TLServerFile> serverFiles);


        bool IsMainViewOpened { get; set; }
        //bool IsDialogOpened { get; set; }
        TLObject ActiveDialog { get; set; }
        TLUserBase SharedContact { get; set; }
        string IsoFileName { get; set; }
        Country SelectedCountry { get; set; }
        bool FocusOnInputMessage { get; set; }
        string VideoIsoFileName { get; set; }
        long Duration { get; set; }
        RecordedVideo RecordedVideo { get; set; }
        IList<TLUserBase> RemovedUsers { get; set; }
        List<TLMessageBase> ForwardMessages { get; set; }
        bool SuppressNotifications { get; set; }
        string PhoneNumberString { get; set; }
        IList<TLMessageBase> DialogMessages { get; set; }
        IList<TLDialogBase> LoadedDialogs { get; set; } 
        BackgroundItem CurrentBackground { get; set; }
        bool IsEmptyBackground { get; }
        bool SendByEnter { get; set; }

        string Passcode { get; set; }
        bool IsSimplePasscode { get; set; }
        DateTime CloseTime { get; set; }
        bool Locked { get; set; }
        int AutolockTimeout { get; set; }

        void ResetPasscode();

        WriteableBitmap ActiveBitmap { get; set; }
        bool CreateSecretChat { get; set; }
        TLString CurrentKey { get; set; }
        TLLong CurrentKeyFingerprint { get; set; }
        bool MediaTab { get; set; }
        TLEncryptedChatBase CurrentEncryptedChat { get; set; }
        bool Tombstoning { get; set; }
        string UserId { get; set; }
        string ChatId { get; set; }
        string BroadcastId { get; set; }
        int ForwardingMessagesCount { get; set; }
        bool RequestForwardingCount { get; set; }
        ExtendedImage ExtendedImage { get; set; }

#if WP81
        StorageFile VideoFile { get; set; }
        CompressingVideoFile CompressingVideoFile { get; set; }
#endif
        int AccountDaysTTL { get; set; }
        TLPrivacyRules PrivacyRules { get; set; }
        IPrivacyValueUsersRule UsersRule { get; set; }

        IList<TLUserBase> SelectedUsers { get; set; }
        IList<TLInt> SelectedUserIds { get; set; }
        bool NavigateToDialogDetails { get; set; }
        bool NavigateToSecretChat { get; set; }
        string Domain { get; set; }
        bool ChangePhoneNumber { get; set; }
        TimerSpan SelectedTimerSpan { get; set; }
        TLDCOption DCOption { get; set; }
        TLDHConfig DHConfig { get; set; }
        List<TLMessageBase> Source { get; set; }
        TLDialogBase Dialog { get; set; }
        TLPasswordBase Password { get; set; }
        TLPasswordInputSettings NewPasswordSettings { get; set; }
        bool IsInviteVisible { get; set; }
        string AccessToken { get; set; }
        TLUserBase Bot { get; set; }
        Uri WebLink { get; set; }
        TLMessageBase Message { get; set; }
        //bool SearchDialogs { get; set; }
        void GetAllStickersAsync(Action<TLAllStickers> callback);
        void SaveAllStickersAsync(TLAllStickers allStickers);

        event PropertyChangedEventHandler PropertyChanged;
    }
}
