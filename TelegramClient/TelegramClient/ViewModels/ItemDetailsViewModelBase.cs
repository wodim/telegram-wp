using Caliburn.Micro;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using TelegramClient.Services;

namespace TelegramClient.ViewModels
{
    public abstract class ItemDetailsViewModelBase : ViewModelBase
    {
        private TLObject _currentItem;

        public TLObject CurrentItem
        {
            get { return _currentItem; }
            set { SetField(ref _currentItem, value, () => CurrentItem); }
        }

        protected ItemDetailsViewModelBase(ICacheService cacheService, ICommonErrorHandler errorHandler, IStateService stateService, INavigationService navigationService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator)
            : base(cacheService, errorHandler, stateService, navigationService, mtProtoService, eventAggregator)
        {
        }
    }
}
