using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Windows;
using Caliburn.Micro;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using TelegramClient.Resources;
using TelegramClient.Services;
using Execute = Telegram.Api.Helpers.Execute;

namespace TelegramClient.ViewModels.Dialogs
{
    public class CreateChannelStep3ViewModel : CreateDialogViewModel
    {
        private readonly TLChannel _newChannel;

        public CreateChannelStep3ViewModel(ICacheService cacheService, ICommonErrorHandler errorHandler, IStateService stateService, INavigationService navigationService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator)
            : base(cacheService, errorHandler, stateService, navigationService, mtProtoService, eventAggregator)
        {
            GroupedUsers = new ObservableCollection<TLUserBase>();

            _newChannel = StateService.NewChannel;
            StateService.NewChannel = null;

            BeginOnThreadPool(() =>
            {
                //Thread.Sleep(300);
                _source = _source ??
                    CacheService.GetContacts()
                    .Where(x => !(x is TLUserEmpty) && x.Index != StateService.CurrentUserId)
                    .OrderBy(x => x.FullName)
                    .ToList();

                Status = string.Empty;
                foreach (var contact in _source)
                {
                    contact._isSelected = false;
                    LazyItems.Add(contact);
                }

                if (_source.Count == 0)
                {
                    Status = AppResources.NoUsersHere;
                }

                BeginOnUIThread(PopulateItems);
                Thread.Sleep(500);
                BeginOnUIThread(() =>
                {
                    foreach (var item in _source)
                    {
                        GroupedUsers.Add(item);
                    }
                });
            });
        }

        protected override void OnActivate()
        {
            base.OnActivate();

            if (StateService.RemoveBackEntry)
            {
                StateService.RemoveBackEntry = false;
                NavigationService.RemoveBackEntry();
            }
        }

        public override void Create()
        {
            if (IsWorking) return;
            if (_newChannel == null) return;

            var participants = new TLVector<TLInputUserBase>();
            foreach (var item in SelectedUsers)
            {
                participants.Add(item.ToInputUser());
            }
            participants.Add(new TLInputUserContact { UserId = new TLInt(StateService.CurrentUserId) });

            if (participants.Count == 0)
            {
                MessageBox.Show(AppResources.PleaseChooseAtLeastOneParticipant, AppResources.Error, MessageBoxButton.OK);
                return;
            }

            _newChannel.ParticipantIds = new TLVector<TLInt> { Items = SelectedUsers.Select(x => x.Id).ToList() };

#if LAYER_40
            IsWorking = true;
            MTProtoService.InviteToChannelAsync(_newChannel.ToInputChannel(), participants,
                result => Execute.BeginOnUIThread(() =>
                {
                    IsWorking = false;

                    StateService.With = _newChannel;
                    StateService.RemoveBackEntries = true;
                    NavigationService.UriFor<DialogDetailsViewModel>().Navigate();
                }),
                error => Execute.BeginOnUIThread(() =>
                {
                    IsWorking = false;
                    Execute.ShowDebugMessage("channels.inviteToChannel error " + error);
                }));
#else
            CacheService.SyncBroadcast(_newChannel, result =>
            {
                var broadcastPeer = new TLPeerBroadcast { Id = _newChannel.Id };
                var serviceMessage = new TLMessageService17
                {
                    FromId = new TLInt(StateService.CurrentUserId),
                    ToId = broadcastPeer,
                    Status = MessageStatus.Confirmed,
                    Out = TLBool.True,
                    Date = TLUtils.DateToUniversalTimeTLInt(MTProtoService.ClientTicksDelta, DateTime.Now),
                    //IsAnimated = true,
                    RandomId = TLLong.Random(),
                    Action = new TLMessageActionChannelCreate
                    {
                        Title = _newChannel.Title,
                    }
                };
                serviceMessage.SetUnread(TLBool.False);

                CacheService.SyncMessage(serviceMessage, broadcastPeer,
                    message =>
                    {
                        if (_newChannel.Photo is TLChatPhoto)
                        {
                            var serviceMessage2 = new TLMessageService17
                            {
                                FromId = new TLInt(StateService.CurrentUserId),
                                ToId = broadcastPeer,
                                Status = MessageStatus.Confirmed,
                                Out = TLBool.True,
                                Date = TLUtils.DateToUniversalTimeTLInt(MTProtoService.ClientTicksDelta, DateTime.Now),
                                //IsAnimated = true,
                                RandomId = TLLong.Random(),
                                Action = new TLMessageActionChatEditPhoto
                                {
                                    Photo = _newChannel.Photo,
                                }
                            };
                            serviceMessage2.SetUnread(TLBool.False);

                            CacheService.SyncMessage(serviceMessage2, broadcastPeer, message2 =>
                            {
                                StateService.With = _newChannel;
                                StateService.RemoveBackEntries = true;
                                NavigationService.UriFor<DialogDetailsViewModel>().Navigate();
                            });
                            return;
                        }
                        StateService.With = _newChannel;
                        StateService.RemoveBackEntries = true;
                        NavigationService.UriFor<DialogDetailsViewModel>().Navigate();
                    });

            });
#endif
        }
    }
}
