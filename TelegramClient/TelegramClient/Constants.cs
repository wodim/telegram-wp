namespace TelegramClient
{
    static class Constants
    {
#if PRIVATE_BETA || DEBUG
        public const string LogEmail = 
#else
        public const string LogEmail = 
#endif
        public const int TelegramNotificationsId = 777000;

        public const string TelegramFaq = "https://telegram.org/faq";
        public const string TelegramShare = "https://telegram.org/dl";
        public const string TelegramTroubleshooting = "https://telegram.org/faq#troubleshooting";

        public const int VideoPreviewMaxSize = 90;//320;
        public const int VideoPreviewQuality = 87;

        public const int DocumentPreviewMaxSize = 90;
        public const int DocumentPreviewQuality = 87;

        public const int PhotoPreviewMaxSize = 90;
        public const int PhotoPreviewQuality = 87;

        public const string EmptyBackgroundString = "Empty";
        public const string LibraryBackgroundString = "Library";
        public const string AnimatedBackground1String = "AnimatedBackground1";

        public const int MaximumMessageLength = 4096;

        public const double GlueGeoPointsDistance = 50.0;

        public const double DefaultMessageContentWidth = 323.0;
        public const double MaxStickerDimension = 256.0;
        public const double DefaultMessageContentHeight = 150.0;

        public const int ShowHelpTimeInSeconds = 120;
        public const int PhoneCodeLength = 5;
        public const int DialogsSlice = 20;
        public const int MessagesSlice = 15;
        public const string IsAuthorizedKey = "IsAuthorized";
        public const string ConfigKey = "Config";
        public const string CurrentUserKey = "CurrentUser";
        public const string CurrentBackgroundKey = "CurrentBackground";
        public const string SendByEnterKey = "SendByEnter";


        public const string UnreadCountKey = "UnreadCountKey";
        public const string SettingsKey = "SettingsKey";
        public const string CommonNotifySettingsFileName = "CommonNotifySettings.xml";
        public const string DelayedContactsFileName = "PeopleHub.dat";

        public const string ScheduledAgentTaskName = "TelegramScheduledAgent";
        public const string ToastNotificationChannelName = "TelegramNotificationChannel";

        public const int MaxImportingContactsCount = 1300;
        public static string StaticGoogleMap = "https://maps.googleapis.com/maps/api/staticmap?center={0},{1}&zoom=12&size={2}x{3}&sensor=false&format=jpg";

        public const double SetTypingIntervalInSeconds = 5.0;
        public const int SendCallDefaultTimeout = 90;
        public const int DefaultForwardingMessagesCount = 50;
        public const string ImportedPhonesFileName = "importedPhones.dat";
        public const string CachedServerFilesFileName = "cachedServerFiles.dat";
        public const string AllStickersFileName = "allStickers.dat";

        public const string TelegramFolderName = "Telegram";
        public const int UsernameMinLength = 5;
        public const double GetAllStickersInterval = 60.0*60.0;     // 60 min

        public const int FileSliceLength = 50;
        public const int PhotoVideoSliceLength = 48;    // 4 preview * 12 rows

        // FullHD
        public const double FullHDAppBarHeight = 60.0;
        public const double FullHDAppBarDifference = -12.0;

        //qHD
        public const double QHDAppBarHeight = 67.0;
        public const double QHDAppBarDifference = -5.0;

        public const double NotificationTimerInterval = 10.0;   // seconds
        public const string SnapshotsDirectoryName = "Snapshots";

        public const string FeatureChatCreate = "chat_create";
        public const string FeatureBroadcastCreate = "broadcast_create";
        public const string FeatureChatMessage = "chat_message";
        public const string FeaturePMMessage = "pm_message";
        public const string FeatureBigChatMessage = "bigchat_message";
        public const string FeatureChatUploadPhoto = "chat_upload_photo";
        public const string FeaturePMUploadPhoto = "pm_upload_photo";
        public const string FeatureBigChatUploadPhoto = "bigchat_upload_photo";
        public const string FeatureChatUploadAudio = "chat_upload_audio";
        public const string FeaturePMUploadAudio = "pm_upload_audio";
        public const string FeatureBigChatUploadAudio = "bigchat_upload_audio";
        public const string FeatureChatUploadDocument = "chat_upload_document";
        public const string FeaturePMUploadDocument = "pm_upload_document";
        public const string FeatureBigChatUploadDocument = "bigchat_upload_document";

        //passcode
        public const string AppCloseTimeKey = "AppCloseTime";
        public const string PasscodeKey = "PasscodeEnabled";
        public const string IsSimplePasscodeKey = "IsSimplePasscode";
        public const string IsPasscodeEnabledKey = "IsPasscodeEnabled";
        public const string PasscodeAutolockKey = "PasscodeAutolock";
        public const string PasscodeParamsFileName = "passcode_params.dat";
        public const int PasscodeHashIterations = 1000;

        //password
        public const int PasswordRecoveryCodeLength = 6;
        
        //notifications
        public const int WNSTokenType = 8;
        public const int MPNSTokenType = 3;
        public const int NotificationInterval = 15; // Interval to count notifications (seconds)
        public const int UnmutedCount = 1; // Cont of notifications to show within interval 

        //unread messages
        public const int MinUnreadCountToShowSeparator = 2;

        //venues
        public const string FoursquireSearchEndpointUrl = 
        public const string FoursquareClientId = 
        public const string FoursquareClientSecret = 
        public const string FoursquareVersion = 
        public const string FoursquareVenuesCountLimit = @"25";
        public const string FoursquareLocale = @"en";

        //stickers
        public const string AddStickersLinkPlaceholder = @"https://telegram.me/addstickers/{0}";

        //usernames
        public const string UsernameLinkPlaceholder = @"https://telegram.me/{0}";

        //background tasks
        public const string PushNotificationsBackgroundTaskName = "PushNotificationsTask";
        public const string MessageSchedulerBackgroundTaskName = "SchedulerTask";
        public const string TimerMessageSchedulerBackgroundTaskName = "TimerSchedulerTask";
        public const string BackgroundDifferenceLoaderTaskName = "BackgroundDifferenceLoader";

        //message search
        public const int SearchMessagesSliceLimit = 5;

        //search
        public const string RecentSearchResultsFileName = "search_chats_recent.dat";

        //channels
        public const string ChannelIntroFileName = "channel_intro.dat";
        
        //photo
        public const uint DefaultImageSize = 1600;
    }
}
