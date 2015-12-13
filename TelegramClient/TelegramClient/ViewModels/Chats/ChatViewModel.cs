using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using Caliburn.Micro;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.FileManager;
using Telegram.Api.TL;
using TelegramClient.Helpers;
using TelegramClient.Resources;
using TelegramClient.Services;
using TelegramClient.ViewModels.Media;

namespace TelegramClient.ViewModels.Chats
{
    public class ChatViewModel : Conductor<ViewModelBase>.Collection.OneActive, Telegram.Api.Aggregator.IHandle<TLMessageBase>
    {
        public bool IsViewerOpen
        {
            get { return ProfilePhotoViewer != null && ProfilePhotoViewer.IsOpen; }
        }

        public TLChatBase Chat { get; protected set; }

        public ChatDetailsViewModel ChatDetails { get; protected set; }

        private readonly IStateService _stateService;

        private readonly INavigationService _navigationService;

        private readonly ITelegramEventAggregator _eventAggregator;

        private readonly IMTProtoService _mtProtoService;

        private readonly ICacheService _cacheService;

        public ProfilePhotoViewerViewModel ProfilePhotoViewer { get { return ChatDetails.ProfilePhotoViewer; } }

        public ChatViewModel(ChatDetailsViewModel chatDetails, IMTProtoService mtProtoService, ICacheService cacheService, ITelegramEventAggregator eventAggregator, INavigationService navigationService, IStateService stateService)
        {
            //tombstoning
            if (stateService.CurrentChat == null)
            {
                stateService.ClearNavigationStack = true;
                navigationService.UriFor<ShellViewModel>().Navigate();
                return;
            }

            Chat = stateService.CurrentChat;
            stateService.CurrentChat = null;

            ChatDetails = chatDetails;
            ChatDetails.ProfilePhotoViewer = ProfilePhotoViewer;

            _stateService = stateService;
            _navigationService = navigationService;
            _eventAggregator = eventAggregator;
            _mtProtoService = mtProtoService;
            _cacheService = cacheService;

            _eventAggregator.Subscribe(this);
        }

        protected override void OnInitialize()
        {
            if (Chat == null) return;

            ChatDetails.CurrentItem = Chat;
            var notifySettings = Chat.NotifySettings as TLPeerNotifySettings;
            if (notifySettings != null)
            {
                var sound = _stateService.Sounds.FirstOrDefault(x => string.Equals(x, notifySettings.Sound.Value, StringComparison.OrdinalIgnoreCase));
                ChatDetails.SetSelectedSound(sound ?? _stateService.Sounds[0]);
            }
            NotifyOfPropertyChange(() => Chat);

            Items.Add(ChatDetails);
            ActivateItem(ChatDetails);

            base.OnInitialize();
        }

        protected override void OnActivate()
        {
            if (_stateService != null
                && _stateService.Participant != null)
            {
                var participant = _stateService.Participant;
                _stateService.Participant = null;

                var forwardingMessagesCount = _stateService.ForwardingMessagesCount;
                _stateService.ForwardingMessagesCount = 0;

                var broadcastChat = Chat as TLBroadcastChat;
                if (broadcastChat != null)
                {
                    var serviceMessage = new TLMessageService17();
                    serviceMessage.ToId = new TLPeerBroadcast { Id = Chat.Id };
                    serviceMessage.FromId = new TLInt(_stateService.CurrentUserId);
                    serviceMessage.Out = new TLBool(true);
                    serviceMessage.SetUnread(new TLBool(false));
                    serviceMessage.Date = TLUtils.DateToUniversalTimeTLInt(_mtProtoService.ClientTicksDelta, DateTime.Now);
                    serviceMessage.Action = new TLMessageActionChatAddUser{ UserId = participant.Id };

                    broadcastChat.ParticipantIds.Add(participant.Id);

                    _cacheService.SyncBroadcast(broadcastChat, 
                        result =>
                        {
                            _eventAggregator.Publish(serviceMessage);
                        });
                    //ChatDetails.UpdateTitles();
                }
                else
                {
                    _mtProtoService.AddChatUserAsync(Chat.Id, participant.ToInputUser(), new TLInt(forwardingMessagesCount),
                        statedMessage =>
                        {
                            var updates = statedMessage as TLUpdates;
                            if (updates != null)
                            {
                                var updateNewMessage = updates.Updates.FirstOrDefault(x => x is TLUpdateNewMessage) as TLUpdateNewMessage;
                                if (updateNewMessage != null)
                                {
                                    _eventAggregator.Publish(updateNewMessage.Message);
                                }
                            }

                            ChatDetails.UpdateTitles();
                        },
                        error => Execute.BeginOnUIThread(() =>
                        {
                            if (error.TypeEquals(ErrorType.PEER_FLOOD))
                            {
                                MessageBox.Show(AppResources.PeerFloodAddContact, AppResources.Error, MessageBoxButton.OK);
                            }

                            Telegram.Api.Helpers.Execute.ShowDebugMessage("messages.addChatUser error " + error);
                        }));    
                }
            }

            if (_stateService != null
                && _stateService.SelectedTimerSpan != null)
            {
                ChatDetails.SelectedSpan = _stateService.SelectedTimerSpan;
                _stateService.SelectedTimerSpan = null;
            }

            ChatDetails.StartTimer();

            base.OnActivate();
        }

        protected override void OnDeactivate(bool close)
        {
            ChatDetails.StopTimer();

            base.OnDeactivate(close);
        }

        public void Edit()
        {
            _stateService.CurrentChat = Chat;
            _navigationService.UriFor<EditChatViewModel>().Navigate();
        }

        public void AddParticipant()
        {
            ChatDetails.AddParticipant();
        }

        public void Handle(TLMessageBase message)
        {
            var serviceMessage = message as TLMessageService;
            if (serviceMessage != null)
            {
                var editTitleAction = serviceMessage.Action as TLMessageActionChatEditTitle;
                if (editTitleAction != null && serviceMessage.ToId.Id.Value == Chat.Index)
                {
                    NotifyOfPropertyChange(() => Chat);
                }
            }
        }

        public void ForwardInAnimationComplete()
        {
            ChatDetails.ForwardInAnimationComplete();
        }

        public void SetAdmins()
        {
            _stateService.CurrentChat = Chat;
            _navigationService.UriFor<AddAdminsViewModel>().Navigate();
        }
    }
}
