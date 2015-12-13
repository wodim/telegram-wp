using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Caliburn.Micro;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using TelegramClient.Services;

namespace TelegramClient.ViewModels.Media
{
    public class VideoPlayerViewModel : ViewModelBase
    {
        public string IsoFileName { get; set; }

        public VideoPlayerViewModel(ICacheService cacheService, ICommonErrorHandler errorHandler, IStateService stateService, INavigationService navigationService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator) : base(cacheService, errorHandler, stateService, navigationService, mtProtoService, eventAggregator)
        {
            IsoFileName = StateService.IsoFileName;
            StateService.IsoFileName = null;
        }
    }
}
