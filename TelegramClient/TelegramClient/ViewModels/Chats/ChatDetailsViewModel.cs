using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using Caliburn.Micro;
using Telegram.Api.Aggregator;
using Telegram.Api.Extensions;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.Cache.EventArgs;
using Telegram.Api.Services.FileManager;
using Telegram.Api.TL;
using TelegramClient.Converters;
using TelegramClient.Helpers;
using TelegramClient.Resources;
using TelegramClient.Services;
using TelegramClient.Utils;
using System.Linq;
using TelegramClient.ViewModels.Additional;
using TelegramClient.ViewModels.Contacts;
using TelegramClient.ViewModels.Dialogs;
using TelegramClient.ViewModels.Media;
using Action = System.Action;
using Execute = Telegram.Api.Helpers.Execute;

namespace TelegramClient.ViewModels.Chats
{
    public class ChatDetailsViewModel : ItemsViewModelBase<TLUserBase>, 
        Telegram.Api.Aggregator.IHandle<TLMessageBase>, 
        Telegram.Api.Aggregator.IHandle<TLUserBase>, 
        Telegram.Api.Aggregator.IHandle<UploadableItem>, 
        Telegram.Api.Aggregator.IHandle<TLUpdateNotifySettings>, 
        Telegram.Api.Aggregator.IHandle<TLUpdateChannel>,
        Telegram.Api.Aggregator.IHandle<TLUpdateChatParticipants>,
        Telegram.Api.Aggregator.IHandle<TLUpdateChatAdmins>,
        Telegram.Api.Aggregator.IHandle<TLUpdateChatParticipantAdmin>

    {
        public bool IsUpgradeAvailable
        {
            get
            {
                //return false;

                var chat = CurrentItem as TLChat40;
                return chat != null && !chat.Deactivated;
            }
        }

        public bool IsActivateAvailable
        {
            get
            {
                var chat = CurrentItem as TLChat40;
                return chat != null && chat.Creator && chat.Deactivated;
            }
        }

        public string UpgradeDescription
        {
            get { return string.Format(AppResources.UpgradeToSupergroupDescription, 200); }
        }

        public string MembersSubtitle
        {
            get
            {
                var channel = CurrentItem as TLChannel;
                if (channel != null)
                {
                    var count = channel.ParticipantsCount;
                    if (count != null)
                    {
                        return count.Value == 0
                            ? AppResources.NoUsers
                            : Language.Declension(
                                count.Value,
                                AppResources.UserNominativeSingular,
                                AppResources.UserNominativePlural,
                                AppResources.UserGenitiveSingular,
                                AppResources.UserGenitivePlural).ToLower(CultureInfo.CurrentUICulture);
                    }
                }

                return null;
            }
        }

        public string AdministratorsSubtitle
        {
            get
            {
                var channel = CurrentItem as TLChannel;
                if (channel != null)
                {
                    var count = channel.AdminsCount;
                    if (count != null)
                    {
                        return count.Value == 0
                            ? AppResources.NoUsers
                            : Language.Declension(
                                count.Value,
                                AppResources.UserNominativeSingular,
                                AppResources.UserNominativePlural,
                                AppResources.UserGenitiveSingular,
                                AppResources.UserGenitivePlural).ToLower(CultureInfo.CurrentUICulture);
                    }
                }

                return null;
            }
        }

        public string DeleteAndExitGroupString
        {
            get
            {
                if (IsChannel) return AppResources.LeaveChannel.ToLowerInvariant();

                return AppResources.DeleteAndExitGroup.ToLowerInvariant();
            }
        }

        public bool IsDeleteAndExitVisible
        {
            get
            {
                var channel = _currentItem as TLChannel;
                if (channel != null && !channel.Creator && !channel.Left.Value)
                {
                    return true;
                }

                var broadcast = _currentItem as TLBroadcastChat;
                if (broadcast == null)
                {
                    return true;
                }
                
                return false;
            }
        }

        private TimerSpan _selectedSpan;

        public TimerSpan SelectedSpan
        {
            get { return _selectedSpan; }
            set
            {
                _selectedSpan = value;

                if (_selectedSpan != null)
                {
                    if (_selectedSpan.Seconds == 0
                        || _selectedSpan.Seconds == int.MaxValue)
                    {
                        MuteUntil = _selectedSpan.Seconds;
                    }
                    else
                    {
                        var now = DateTime.Now;
                        var muteUntil = now.AddSeconds(_selectedSpan.Seconds);

                        MuteUntil = muteUntil < now ? 0 : TLUtils.DateToUniversalTimeTLInt(MTProtoService.ClientTicksDelta, muteUntil).Value;
                    }
                }
            }
        }

        public IList<TimerSpan> Spans { get; protected set; } 

        private TLChatBase _currentItem;

        public TLChatBase CurrentItem
        {
            get { return _currentItem; }
            set { SetField(ref _currentItem, value, () => CurrentItem); }
        }

        public bool IsChannel
        {
            get { return CurrentItem is TLChannel; }
        }

        public bool IsMegaGroup
        {
            get
            {
                var channel = CurrentItem as TLChannel;
                return (channel != null && channel.IsMegaGroup);
            }
        }

        public bool CanViewParticipants
        {
            get
            {
                return !IsChannel || IsMegaGroup;
            }
        }

        public bool IsChannelAdministrator
        {
            get
            {
                var channel = CurrentItem as TLChannel;
                return channel != null && (channel.Creator || channel.IsEditor);
            }
        }

        public bool IsChannelParticipantsButtonEnabled
        {
            get
            {
                var channel = CurrentItem as TLChannel;
                return channel != null && !channel.IsMegaGroup && (channel.Creator || channel.IsEditor);
            }
        }

        public bool CanEditChat
        {
            get
            {
                var chat = CurrentItem as TLChat40;

                return chat != null && (chat.Creator || !chat.AdminsEnabled.Value || chat.Admin.Value);
            }
        }

        public bool CanEditChannel
        {
            get
            {
                var channel = CurrentItem as TLChannel;
                return channel != null && (channel.Creator || (channel.IsMegaGroup && channel.IsEditor));
            }
        }

        public string Link
        {
            get
            {
                var channel = CurrentItem as TLChannel;
                if (channel != null && !TLString.IsNullOrEmpty(channel.UserName))
                {
                    return "telegram.me/" + channel.UserName;
                }

                return string.Empty;
            }
        }

        private bool _suppressUpdating = true;

        private int _muteUntil;

        public int MuteUntil
        {
            get { return _muteUntil; }
            set { SetField(ref _muteUntil, value, () => MuteUntil); }
        }

        private string _selectedSound;

        public string SelectedSound
        {
            get { return _selectedSound; }
            set { SetField(ref _selectedSound, value, () => SelectedSound); }
        }

        public void SetSelectedSound(string sound)
        {
            _selectedSound = sound;
        }

        public List<string> Sounds { get; protected set; }

        private string _subtitle;

        public string Subtitle
        {
            get { return _subtitle; }
            set { SetField(ref _subtitle, value, () => Subtitle); }
        }

        private string _subtitle2;

        public string Subtitle2
        {
            get { return _subtitle2; }
            set { SetField(ref _subtitle2, value, () => Subtitle2); }
        }

        private string _subtitle3;

        public string Subtitle3
        {
            get { return _subtitle3; }
            set { SetField(ref _subtitle3, value, () => Subtitle3); }
        }

        private readonly IUploadFileManager _uploadManager;

        public ProfilePhotoViewerViewModel ProfilePhotoViewer { get; set; }

        public ChatDetailsViewModel(IUploadFileManager uploadManager, ICacheService cacheService, ICommonErrorHandler errorHandler, IStateService stateService, INavigationService navigationService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator) 
            : base(cacheService, errorHandler, stateService, navigationService, mtProtoService, eventAggregator)
        {
            Spans = new List<TimerSpan>
            {
                new TimerSpan(AppResources.Enabled, string.Empty, 0, AppResources.Enabled),
                new TimerSpan(AppResources.HourNominativeSingular,  "1", (int)TimeSpan.FromHours(1.0).TotalSeconds, string.Format(AppResources.MuteFor, string.Format("{0} {1}", "1", AppResources.HourNominativeSingular).ToLowerInvariant())),
                new TimerSpan(AppResources.HourGenitivePlural, "8", (int)TimeSpan.FromHours(8.0).TotalSeconds, string.Format(AppResources.MuteFor, string.Format("{0} {1}", "8", AppResources.HourGenitivePlural).ToLowerInvariant())),
                new TimerSpan(AppResources.DayNominativePlural, "2", (int)TimeSpan.FromDays(2.0).TotalSeconds, string.Format(AppResources.MuteFor, string.Format("{0} {1}", "2", AppResources.DayNominativePlural).ToLowerInvariant())),
                new TimerSpan(AppResources.Disabled, string.Empty, int.MaxValue, AppResources.Disabled),
            };
            _selectedSpan = Spans[0];

            _notificationTimer = new DispatcherTimer();
            _notificationTimer.Interval = TimeSpan.FromSeconds(Constants.NotificationTimerInterval);
            _notificationTimer.Tick += OnNotificationTimerTick;

            _uploadManager = uploadManager;
            eventAggregator.Subscribe(this);

            DisplayName = LowercaseConverter.Convert(AppResources.Profile);
            
            Items = new ObservableCollection<TLUserBase>();
            
            PropertyChanged += (sender, args) =>
            {
                if (Property.NameEquals(args.PropertyName, () => MuteUntil)
                    && !_suppressUpdating)
                {
                    UpdateNotifySettingsAsync();
                }

                if (Property.NameEquals(args.PropertyName, () => SelectedSound)
                   && !_suppressUpdating)
                {
                    NotificationsViewModel.PlaySound(SelectedSound);

                    UpdateNotifySettingsAsync();
                }
            };
        }

        public void CopyLink()
        {
            if (string.IsNullOrEmpty(Link)) return;

            Clipboard.SetText("https://" + Link);
        }

        private void UpdateUsers(List<TLUserBase> users, Action callback)
        {
            const int firstSliceCount = 3;
            var secondSlice = new List<TLUserBase>();
            for (var i = 0; i < users.Count; i++)
            {
                if (i < firstSliceCount)
                {
                    Items.Add(users[i]);
                }
                else
                {
                    secondSlice.Add(users[i]);
                }
            }

            Execute.BeginOnUIThread(() =>
            {
                foreach (var user in secondSlice)
                {
                    Items.Add(user);
                }
                callback.SafeInvoke();
            });
        }

        private void UpdateNotifySettingsAsync()
        {
            if (CurrentItem == null) return;

            var notifySettings = new TLInputPeerNotifySettings
            {
                EventsMask = new TLInt(1),
                MuteUntil = new TLInt(MuteUntil),
                ShowPreviews = new TLBool(true),
                Sound = string.IsNullOrEmpty(SelectedSound) ? new TLString("default") : new TLString(SelectedSound)
            };

            IsWorking = true;
            MTProtoService.UpdateNotifySettingsAsync(
                CurrentItem.ToInputNotifyPeer(), notifySettings,
                result =>
                {
                    IsWorking = false;
                    CurrentItem.NotifySettings = new TLPeerNotifySettings
                    {
                        EventsMask = new TLInt(1),
                        MuteUntil = new TLInt(MuteUntil),
                        ShowPreviews = notifySettings.ShowPreviews,
                        Sound = notifySettings.Sound
                    };

                    var channel = CurrentItem as TLChannel;
                    var peer = channel != null
                        ? (TLPeerBase) new TLPeerChannel {Id = CurrentItem.Id}
                        : new TLPeerChat {Id = CurrentItem.Id};
                    TLDialogBase dialog = CacheService.GetDialog(peer);
                    if (dialog == null)
                    {
                        if (channel != null)
                        {
                            dialog = DialogsViewModel.GetChannel(channel.Id);
                        }
                    }

                    if (dialog != null)
                    {
                        dialog.NotifySettings = CurrentItem.NotifySettings;
                        dialog.NotifyOfPropertyChange(() => dialog.NotifySettings);
                        var settings = dialog.With as INotifySettings;
                        if (settings != null)
                        {
                            settings.NotifySettings = CurrentItem.NotifySettings;
                        }
                    }

                    CacheService.Commit();
                },
                error =>
                {
                    IsWorking = false;
                    Execute.ShowDebugMessage("account.updateNotifySettings error: " + error);
                });
        }

        private void UpdateChannelItems(TLChannel channel)
        {
            IsWorking = true;
            MTProtoService.GetParticipantsAsync(channel.ToInputChannel(), new TLChannelParticipantsRecent(), new TLInt(0), new TLInt(32),
                result => Execute.BeginOnUIThread(() =>
                {
                    IsWorking = false;
                    Items.Clear();
                    foreach (var user in result.Users)
                    {
                        Items.Add(user);
                    }
                    Status = Items.Count > 0 ? string.Empty : AppResources.NoUsersHere;
                }),
                error => Execute.BeginOnUIThread(() =>
                {
                    IsWorking = false;
                    Status = string.Empty;

                    Execute.ShowDebugMessage("channels.getParticipants error " + error);
                }));
        }

        private void UpdateItems()
        {
            var channel = CurrentItem as TLChannel;
            if (channel != null && channel.IsMegaGroup)
            {
                UpdateChannelItems(channel);
                return;
            }


            if (CurrentItem is TLBroadcastChat)
            {
                return;
            }

            IsWorking = true;
            MTProtoService.GetFullChatAsync(CurrentItem.Id,
                chatFull =>
                {
                    IsWorking = false;

                    var newUsersCache = new Dictionary<int, TLUserBase>();
                    foreach (var user in chatFull.Users)
                    {
                        newUsersCache[user.Index] = user;
                    }

                    var participants = chatFull.FullChat.Participants as IChatParticipants;
                    if (participants != null)
                    {
                        var usersCache = Items.ToDictionary(x => x.Index);

                        var onlineUsers = 0;
                        foreach (var participant in participants.Participants)
                        {
                            var user = newUsersCache[participant.UserId.Value];
                            if (user.Status is TLUserStatusOnline)
                            {
                                onlineUsers++;
                            }

                            if (!usersCache.ContainsKey(user.Index))
                            {
                                BeginOnUIThread(() => InsertInDescOrder(Items, user));
                            }
                        }
                        CurrentItem.UsersOnline = onlineUsers;
                    }

                    var chatFull28 = chatFull.FullChat as TLChatFull28;
                    if (chatFull28 != null)
                    {
                        CurrentItem.ExportedInvite = chatFull28.ExportedInvite;
                    }

                    UpdateNotificationSettings();
                    UpdateTitles();
                },
                error =>
                {
                    IsWorking = false;
                });
        }

        protected override void OnInitialize()
        {
            UpdateNotificationSettings();
            Subtitle = GetChatSubtitle();
            Subtitle2 = GetChatSubtitle2();
            Subtitle3 = GetChatSubtitle3();

            base.OnInitialize();
        }

        private void InsertInDescOrder(IList<TLUserBase> users, TLUserBase user)
        {
            var added = false;
            for (var i = 0; i < users.Count; i++)
            {
                if (users[i].StatusValue <= user.StatusValue)
                {
                    users.Insert(i, user);
                    added = true;
                    break;
                }
            }
            if (!added)
            {
                users.Add(user);
            }
        }

        private string GetChatSubtitle3()
        {
            var channel = CurrentItem as TLChannel;
            if (channel != null)
            {
                if (!TLString.IsNullOrEmpty(channel.About))
                {
                    return channel.About.ToString();
                }
            }

            return string.Empty;
        }

        private string GetChatSubtitle2()
        {
            if (IsChannel)
            {
                return string.Empty;
            }

            var usersCount = Items.Count;
            var onlineUsersCount = Items.Count(x => x.Status is TLUserStatusOnline);

            var currentUser = CacheService.GetUser(new TLInt(StateService.CurrentUserId));
            var isCurrentUserOnline = currentUser != null && currentUser.Status is TLUserStatusOnline;
            if (usersCount == 1 || (onlineUsersCount == 1 && isCurrentUserOnline))
            {
                onlineUsersCount = 0;
            }

            return onlineUsersCount > 0 ? string.Format("{0} {1}", onlineUsersCount, AppResources.Online.ToLowerInvariant()) : string.Empty;
        }

        private string GetChatSubtitle()
        {
            //if (IsChannel)
            //{
            //    return "channel";
            //}

            var chat = CurrentItem as TLChat;
            if (chat != null)
            {
                var participantsCount = chat.ParticipantsCount.Value;
                
                return Language.Declension(
                    participantsCount,
                    AppResources.CompanyNominativeSingular,
                    AppResources.CompanyNominativePlural,
                    AppResources.CompanyGenitiveSingular,
                    AppResources.CompanyGenitivePlural).ToLower(CultureInfo.CurrentUICulture);
            }

            var channel = CurrentItem as TLChannel;
            if (channel != null)
            {
                if (channel.ParticipantsCount != null)
                {
                    var participantsCount = channel.ParticipantsCount.Value;

                    return Language.Declension(
                        participantsCount,
                        AppResources.CompanyNominativeSingular,
                        AppResources.CompanyNominativePlural,
                        AppResources.CompanyGenitiveSingular,
                        AppResources.CompanyGenitivePlural).ToLower(CultureInfo.CurrentUICulture);
                }

                return channel.IsPublic
                    ? AppResources.PublicChannel.ToLowerInvariant()
                    : AppResources.PrivateChannel.ToLowerInvariant();
            }


            var broadcastChat = CurrentItem as TLBroadcastChat;
            if (broadcastChat != null)
            {
                var participantsCount = broadcastChat.ParticipantIds.Count;

                return Language.Declension(
                    participantsCount,
                    AppResources.CompanyNominativeSingular,
                    AppResources.CompanyNominativePlural,
                    AppResources.CompanyGenitiveSingular,
                    AppResources.CompanyGenitivePlural).ToLower(CultureInfo.CurrentUICulture);
            }



            return string.Empty;
        }

        public void UpdateNotificationSettings()
        {

            var chat = CurrentItem;
            if (chat != null && chat.NotifySettings != null)
            {
                var notifySettings = chat.NotifySettings as TLPeerNotifySettings;
                if (notifySettings != null)
                {
                    _suppressUpdating = true;

                    MuteUntil = notifySettings.MuteUntil.Value;

                    var sound = StateService.Sounds.FirstOrDefault(x => string.Equals(x, notifySettings.Sound.Value, StringComparison.OrdinalIgnoreCase));
                    SelectedSound = sound ?? StateService.Sounds[0];

                    _suppressUpdating = false;
                }
            }
        }

        #region Notification timer
        private readonly DispatcherTimer _notificationTimer;

        public void StartTimer()
        {
            _notificationTimer.Start();
        }

        public void StopTimer()
        {
            _notificationTimer.Stop();
        }

        private void OnNotificationTimerTick(object sender, System.EventArgs e)
        {
            if (MuteUntil > 0 && MuteUntil < int.MaxValue)
            {
                NotifyOfPropertyChange(() => MuteUntil);
            }
        }
        #endregion

        #region Actions

        public void SelectSpan(TimerSpan selectedSpan)
        {
            SelectedSpan = selectedSpan;
        }

        public void SelectNotificationSpan()
        {
            //StateService.SelectedTimerSpan = SelectedSpan;
            NavigationService.UriFor<ChooseNotificationSpanViewModel>().Navigate();
        }

        public void OpenLink()
        {
            StateService.ShareLink = "https://" + Link;
            StateService.ShareMessage = "https://" + Link;
            StateService.ShareCaption = AppResources.Share;
            NavigationService.UriFor<ShareViewModel>().Navigate();
        }

        public void AddParticipant()
        {
            if (CurrentItem.IsForbidden) return;

            var participants = CurrentItem.Participants as IChatParticipants;
            var adminParticipant = participants != null? participants.Participants.FirstOrDefault(x => x is TLChatParticipantAdmin) : null;
            StateService.IsInviteVisible = adminParticipant != null && adminParticipant.UserId.Value == StateService.CurrentUserId;
            StateService.CurrentChat = CurrentItem;
            StateService.RemovedUsers = Items;
            StateService.RequestForwardingCount = CurrentItem is TLChat;
            NavigationService.UriFor<AddChatParticipantViewModel>().Navigate();
        }

        public void DeleteParticipant(TLUserBase user)
        {
            if (CurrentItem.IsForbidden) return;
            if (user == null) return;

            var broadcast = CurrentItem as TLBroadcastChat;
            var channel = CurrentItem as TLChannel;
            if (broadcast != null && channel == null)
            {
                var broadcastChat = (TLBroadcastChat) CurrentItem;

                var serviceMessage = new TLMessageService17
                {
                    ToId = new TLPeerBroadcast {Id = broadcastChat.Id},
                    FromId = new TLInt(StateService.CurrentUserId),
                    Out = new TLBool(true),
                    Date = TLUtils.DateToUniversalTimeTLInt(MTProtoService.ClientTicksDelta, DateTime.Now),
                    Action = new TLMessageActionChatDeleteUser {UserId = user.Id}
                };
                serviceMessage.SetUnread(new TLBool(false));

                for (var i = 0; i < broadcastChat.ParticipantIds.Count; i++)
                {
                    if (user.Id.Value == broadcastChat.ParticipantIds[i].Value)
                    {
                        broadcastChat.ParticipantIds.RemoveAt(i);
                        break;
                    }
                }

                broadcastChat.ParticipantIds.Remove(user.Id);

                CacheService.SyncBroadcast(broadcastChat, 
                    result =>
                    {
                        EventAggregator.Publish(serviceMessage);
                        UpdateTitles();            
                    });
            }
            else
            {
                if (user.Index == StateService.CurrentUserId)
                {
                    DeleteAndExitGroup();
                    return;
                }

                IsWorking = true;
                MTProtoService.DeleteChatUserAsync(CurrentItem.Id, user.ToInputUser(),
                    statedMessage =>
                    {
                        IsWorking = false;
                        BeginOnUIThread(() => Items.Remove(user));

                        var updates = statedMessage as TLUpdates;
                        if (updates != null)
                        {
                            var updateNewMessage = updates.Updates.FirstOrDefault(x => x is TLUpdateNewMessage) as TLUpdateNewMessage;
                            if (updateNewMessage != null)
                            {
                                EventAggregator.Publish(updateNewMessage.Message);
                            }
                        }
                        UpdateTitles();
                    },
                    error =>
                    {
                        Execute.ShowDebugMessage("messages.deleteChatUser error " + error);
                        IsWorking = false;
                    });
            }
        }

        public void MessageUser(TLUserBase user)
        {
            if (user == null) return;

            StateService.With = user;
            StateService.RemoveBackEntries = true;
            NavigationService.UriFor<DialogDetailsViewModel>().Navigate();
        }

        public void ViewUser(TLUserBase user)
        {
            if (user == null) return;

            StateService.CurrentContact = user;
            NavigationService.UriFor<ContactViewModel>().Navigate();
        }

        private TLPhotoBase GetPhoto()
        {
            var chat = CurrentItem as TLChat;
            if (chat != null) return chat.Photo;

            var channel = CurrentItem as TLChannel;
            if (channel != null) return channel.Photo;

            return null;
        }

        public void OpenPhoto()
        {
            var chat = CurrentItem as TLChat;
            var channel = CurrentItem as TLChannel;
            if (chat == null && channel == null) return;

            var photoBase = GetPhoto();

            var photo = photoBase as TLChatPhoto;
            if (photo != null)
            {
                StateService.CurrentPhoto = photo;

                if (ProfilePhotoViewer == null)
                {
                    ProfilePhotoViewer = new ProfilePhotoViewerViewModel(StateService, MTProtoService, EventAggregator, NavigationService);
                    NotifyOfPropertyChange(() => ProfilePhotoViewer);
                }

                BeginOnUIThread(() => ProfilePhotoViewer.OpenViewer());
                return;
            }

            var photoEmpty = photoBase as TLChatPhotoEmpty;
            if (photoEmpty != null)
            {
                if ((chat != null && !chat.Left.Value)
                    || (channel != null && channel.Creator))
                {
                    EditChatActions.EditPhoto(result =>
                    {
                        var fileId = TLLong.Random();
                        IsWorking = true;
                        _uploadManager.UploadFile(fileId, CurrentItem, result);
                    });
                }
            }
        }

        public void OpenMedia()
        {
            if (CurrentItem == null) return;

            StateService.CurrentInputPeer = CurrentItem;
            NavigationService.UriFor<FullMediaViewModel>().Navigate();
        }

        public void OpenMembers()
        {
            StateService.CurrentChat = CurrentItem;
            NavigationService.UriFor<ChannelMembersViewModel>().Navigate();
        }

        public void OpenAdministrators()
        {
            StateService.CurrentChat = CurrentItem;
            NavigationService.UriFor<ChannelAdministratorsViewModel>().Navigate();
        }

        public void UpgradeGroup()
        {
            var confirmation = MessageBox.Show(AppResources.UpgradeToSupergroupConfirmation, AppResources.Confirm, MessageBoxButton.OKCancel);
            if (confirmation != MessageBoxResult.OK) return;

            IsWorking = true;
            MTProtoService.MigrateChatAsync(
                CurrentItem.Id,
                result => BeginOnUIThread(() =>
                {
                    IsWorking = false;

                    var updates = result as TLUpdates;
                    if (updates != null)
                    {
                        var channel = updates.Chats.FirstOrDefault(x => x is TLChannel) as TLChannel;
                        if (channel != null)
                        {
                            var migratedFromMaxId = new TLInt(0);
                            var updateNewMessage = updates.Updates.FirstOrDefault(x => x is TLUpdateNewMessage) as TLUpdateNewMessage;
                            if (updateNewMessage != null)
                            {
                                migratedFromMaxId = updateNewMessage.Message.Id;
                            }
                            channel.MigratedFromChatId = CurrentItem.Id;
                            channel.MigratedFromMaxId = migratedFromMaxId;

                            StateService.With = channel;
                            StateService.RemoveBackEntries = true;
                            NavigationService.UriFor<DialogDetailsViewModel>().Navigate();
                        }
                    }
                }),
                error => Execute.BeginOnUIThread(() =>
                {
                    IsWorking = false;
                    Execute.ShowDebugMessage("messages.migrateChat error " + error);
                }));

            return;
        }

        public void DeleteAndExitGroup()
        {
            MessageBoxResult confirmation;

            var channel = CurrentItem as TLChannel;
            if (channel != null)
            {
                confirmation = MessageBox.Show(AppResources.LeaveChannelConfirmation, AppResources.Confirm, MessageBoxButton.OKCancel);
                if (confirmation != MessageBoxResult.OK) return;

                IsWorking = true;
                MTProtoService.LeaveChannelAsync(
                    channel,
                    result => BeginOnUIThread(() =>
                    {
                        IsWorking = false;
                        NavigationService.RemoveBackEntry();

                        var dialog = CacheService.GetDialog(new TLPeerChannel { Id = CurrentItem.Id });
                        if (dialog != null)
                        {
                            CacheService.DeleteDialog(dialog);
                            DialogsViewModel.UnpinFromStart(dialog);
                            EventAggregator.Publish(new DialogRemovedEventArgs(dialog));
                        }

                        NavigationService.GoBack();
                    }),
                    error => Execute.BeginOnUIThread(() =>
                    {
                        IsWorking = false;
                        Execute.ShowDebugMessage("cnannels.leaveChannel error " + error);
                    }));

                return;
            }

            confirmation = MessageBox.Show(string.Format("{0}?", AppResources.DeleteAndExit), AppResources.Confirm, MessageBoxButton.OKCancel);
            if (confirmation != MessageBoxResult.OK) return;

            DialogsViewModel.DeleteAndExitDialogCommon(
                CurrentItem,
                MTProtoService,
                () => BeginOnUIThread(() =>
                {
                    NavigationService.RemoveBackEntry();

                    var dialog = CacheService.GetDialog(new TLPeerChat { Id = CurrentItem.Id });
                    if (dialog != null)
                    {
                        CacheService.DeleteDialog(dialog);
                        DialogsViewModel.UnpinFromStart(dialog);
                        EventAggregator.Publish(new DialogRemovedEventArgs(dialog));
                    }
                    NavigationService.GoBack();
                }),
                error =>
                {
                    Execute.ShowDebugMessage("DeleteAndExitGroupCommon error " + error);
                });
        }

        public void UpdateTitles()
        {
            Subtitle = GetChatSubtitle();
            Subtitle2 = GetChatSubtitle2();
            Subtitle3 = GetChatSubtitle3();

            NotifyOfPropertyChange(() => MembersSubtitle);
            NotifyOfPropertyChange(() => AdministratorsSubtitle);
        }
        #endregion

        public void Handle(TLMessageBase message)
        {
            var serviceMessage = message as TLMessageService;
            if (serviceMessage != null)
            {
                var channel = CurrentItem as TLChannel;
                if (channel != null && channel.Index == serviceMessage.ToId.Id.Value)
                {
                    var chatDeletePhotoAction = serviceMessage.Action as TLMessageActionChatDeletePhoto;
                    if (chatDeletePhotoAction != null)
                    {
                        channel.NotifyOfPropertyChange(() => channel.Photo);
                        return;
                    }
                }

                var chat = CurrentItem as TLChat;
                if (chat != null && chat.Index == serviceMessage.ToId.Id.Value)
                {
                    var chatDeletePhotoAction = serviceMessage.Action as TLMessageActionChatDeletePhoto;
                    if (chatDeletePhotoAction != null)
                    {
                        chat.NotifyOfPropertyChange(() => chat.Photo);
                        return;
                    }

                    var chatDeleteUserAction = serviceMessage.Action as TLMessageActionChatDeleteUser;
                    if (chatDeleteUserAction != null)
                    {
                        HandleDeleteUserAction(chatDeleteUserAction);
                        return;
                    }

                    var chatAddUserAction = serviceMessage.Action as TLMessageActionChatAddUser;
                    if (chatAddUserAction != null)
                    {
                        HandleAddUserAction(chatAddUserAction);
                        return;
                    }
                }


                var broadcastChat = CurrentItem as TLBroadcastChat;
                if (broadcastChat != null && broadcastChat.Index == serviceMessage.ToId.Id.Value)
                {
                    var chatDeleteUserAction = serviceMessage.Action as TLMessageActionChatDeleteUser;
                    if (chatDeleteUserAction != null)
                    {
                        HandleDeleteUserAction(chatDeleteUserAction);
                        return;
                    }

                    var chatAddUserAction = serviceMessage.Action as TLMessageActionChatAddUser;
                    if (chatAddUserAction != null)
                    {
                        HandleAddUserAction(chatAddUserAction);
                        return;
                    }
                }
            }
        }

        private void HandleAddUserAction(TLMessageActionChatAddUser chatAddUserAction)
        {
            var cachedUser = CacheService.GetUser(chatAddUserAction.UserId);

            if (cachedUser != null)
            {
                BeginOnUIThread(() =>
                {
                    InsertInDescOrder(Items, cachedUser);
                    UpdateTitles();
                });
            }
        }

        private void HandleDeleteUserAction(TLMessageActionChatDeleteUser chatDeleteUserAction)
        {
            for (var i = 0; i < Items.Count; i++)
            {
                if (Items[i].Index == chatDeleteUserAction.UserId.Value)
                {
                    BeginOnUIThread(() =>
                    {
                        Items.RemoveAt(i);
                        UpdateTitles();
                    });
                    break;
                }
            }
        }

        public void Handle(TLUserBase user)
        {
            BeginOnUIThread(() =>
            {
                for (var i = 0; i < Items.Count; i++)
                {
                    if (Items[i].Index == user.Index)
                    {
                        UpdateTitles();
                        break;
                    }
                }
            });
        }

        public void Handle(UploadableItem item)
        {
            if (item.Owner == CurrentItem)
            {
                IsWorking = false;
            }
        }

        public void Handle(TLUpdateNotifySettings updateNotifySettings)
        {
            var notifyPeer = updateNotifySettings.Peer as TLNotifyPeer;
            if (notifyPeer != null)
            {
                var peer = notifyPeer.Peer;
                if (peer is TLPeerChat
                    && peer.Id.Value == CurrentItem.Index)
                {
                    Execute.BeginOnUIThread(() =>
                    {
                        CurrentItem.NotifySettings = updateNotifySettings.NotifySettings;
                        var notifySettings = updateNotifySettings.NotifySettings as TLPeerNotifySettings;
                        if (notifySettings != null)
                        {
                            _suppressUpdating = true;
                            MuteUntil = notifySettings.MuteUntil.Value;
                            _suppressUpdating = false;
                        }
                    });
                }
            }
        }

        public void ForwardInAnimationComplete()
        {
            Items.Clear();
            LazyItems.Clear();

            var chat = CurrentItem;
            if (chat != null)
            {
                var participants = chat.Participants as TLChatParticipants40;
                if (participants != null)
                {
                    var users = new List<TLUserBase>(participants.Participants.Count);
                    foreach (var participant in participants.Participants)
                    {
                        var user = CacheService.GetUser(participant.UserId);
                        if (user != null)
                        {
                            var canDeleteUserFromChat = false;

                            var inviter = participant as IInviter;
                            if (inviter != null
                                && inviter.InviterId.Value == StateService.CurrentUserId)
                            {
                                canDeleteUserFromChat = true;
                            }

                            var creator = participant as TLChatParticipantCreator;
                            if (creator != null
                                && creator.UserId.Value == StateService.CurrentUserId)
                            {
                                canDeleteUserFromChat = true;
                            }

                            if (participant.UserId.Value == StateService.CurrentUserId)
                            {
                                canDeleteUserFromChat = true;
                            }

                            user.DeleteActionVisibility = canDeleteUserFromChat
                                ? Visibility.Visible
                                : Visibility.Collapsed;

                            users.Add(user);
                        }
                    }
                    users = users.OrderByDescending(x => x.StatusValue).ToList();

                    UpdateUsers(users, UpdateItems);

                    return;
                }
                else
                {
                    UpdateItems();
                }
            }

            var channel = CurrentItem as TLChannel;
            if (channel != null)
            {

                return;
                
            }

            var broadcastChat = CurrentItem as TLBroadcastChat;
            if (broadcastChat != null)
            {
                var users = new List<TLUserBase>(broadcastChat.ParticipantIds.Count);
                var count = 0;
                foreach (var participantId in broadcastChat.ParticipantIds)
                {
                    var user = CacheService.GetUser(participantId);
                    if (user != null)
                    {
                        user.DeleteActionVisibility = Visibility.Visible;
                        if (count < 4)
                        {
                            Items.Add(user);
                            count++;
                        }
                        else
                        {
                            users.Add(user);
                        }
                    }
                }
                users = users.OrderByDescending(x => x.StatusValue).ToList();

                UpdateUsers(users, UpdateItems);

                return;
            }
        }

        public void Handle(TLUpdateChannel updateChannel)
        {
            var channel = CurrentItem as TLChannel;
            if (channel != null)
            {
                if (channel.Id.Value == updateChannel.ChannelId.Value)
                {
                    NotifyOfPropertyChange(() => IsChannelAdministrator);
                }
            }
        }

        public void Handle(TLUpdateChatParticipants updateChatParticipants)
        {
            NotifyOfPropertyChange(() => CanEditChat);
        }

        public void Handle(TLUpdateChatAdmins updateChatAdmins)
        {
            NotifyOfPropertyChange(() => CanEditChat);
        }

        public void Handle(TLUpdateChatParticipantAdmin updateChatParticipantAdmin)
        {
            NotifyOfPropertyChange(() => CanEditChat);
        }
    }
}
