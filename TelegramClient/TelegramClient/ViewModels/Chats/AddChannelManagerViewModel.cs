using Caliburn.Micro;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using TelegramClient.Services;

namespace TelegramClient.ViewModels.Chats
{
    public class AddChannelManagerViewModel : ViewModelBase
    {
        private TLUserBase _manager;

        public AddChannelManagerViewModel(ICacheService cacheService, ICommonErrorHandler errorHandler, IStateService stateService, INavigationService navigationService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator) 
            : base(cacheService, errorHandler, stateService, navigationService, mtProtoService, eventAggregator)
        {
            _manager = StateService.Participant;
            StateService.Participant = null;
        }

        protected override void OnActivate()
        {
            if (StateService.RemoveBackEntry)
            {
                StateService.RemoveBackEntry = false;
                NavigationService.RemoveBackEntry();
            }

            base.OnActivate();
        }

        public void Done()
        {
            if (_manager == null) return;

            StateService.Participant = _manager;
            NavigationService.GoBack();        
        }

        public void Cancel()
        {
            NavigationService.GoBack();
        }
    }
}
