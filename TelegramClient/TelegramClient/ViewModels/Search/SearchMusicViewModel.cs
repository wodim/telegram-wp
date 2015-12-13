using Caliburn.Micro;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using TelegramClient.Resources;
using TelegramClient.Services;

namespace TelegramClient.ViewModels.Search
{
    public class SearchMusicViewModel : SearchFilesViewModelBase
    {
        public override TLInputMessagesFilterBase InputMessageFilter
        {
            get { return new TLInputMessagesFilterAudioDocuments(); }
        }

        public SearchMusicViewModel(ICacheService cacheService, ICommonErrorHandler errorHandler, IStateService stateService, INavigationService navigationService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator)
            : base(cacheService, errorHandler, stateService, navigationService, mtProtoService, eventAggregator)
        {
            Status = AppResources.SearchSharedMusic;
        }
    }
}
