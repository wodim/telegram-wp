using Caliburn.Micro;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using TelegramClient.Services;

namespace TelegramClient.ViewModels.Debug
{
    public class LongPollViewModel : ViewModelBase
    {
        public LongPollViewModel(ICacheService cacheService, ICommonErrorHandler errorHandler, IStateService stateService, INavigationService navigationService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator) 
            : base(cacheService, errorHandler, stateService, navigationService, mtProtoService, eventAggregator)
        {
            DisplayName = "history";

        }

        //public IList<HistoryItem> Items { get { return MTProtoService.History; } }

        //public bool IsLongPollDebugEnabled
        //{
        //    get { return TLUtils.IsLongPollDebugEnabled; }
        //    set { TLUtils.IsLongPollDebugEnabled = value; }
        //}

        //public void Clear()
        //{
        //    TLUtils.LongPollItems.Clear();
        //    NotifyOfPropertyChange(() => Items);
        //}
    }
}
