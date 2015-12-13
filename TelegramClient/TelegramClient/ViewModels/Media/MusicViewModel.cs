using System;
using System.Linq;
using Caliburn.Micro;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Telegram.Api.TL.Interfaces;
using TelegramClient.Converters;
using TelegramClient.Resources;
using TelegramClient.Services;

namespace TelegramClient.ViewModels.Media
{

    public class MusicViewModel<T> : FilesViewModelBase<T> where T : IInputPeer
    {
        public override TLInputMessagesFilterBase InputMessageFilter
        {
            get { return new TLInputMessagesFilterAudioDocuments(); }
        }

        public override string EmptyListImageSource
        {
            get { return "/Images/Messages/nomusic.png"; }
        }

        public MusicViewModel(ICacheService cacheService, ICommonErrorHandler errorHandler, IStateService stateService, INavigationService navigationService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator)
            : base(cacheService, errorHandler, stateService, navigationService, mtProtoService, eventAggregator)
        {
            DisplayName = LowercaseConverter.Convert(AppResources.SharedMusic);
        }

        protected override bool SkipMessage(TLMessageBase messageBase)
        {
            var message = messageBase as TLMessage;
            if (message == null)
            {
                return true;
            }

            var mediaDocument = message.Media as TLMessageMediaDocument;
            if (mediaDocument == null)
            {
                return true;
            }

            var document = mediaDocument.Document as TLDocument22;
            if (document == null)
            {
                return true;
            }

            var audioAttribute = document.Attributes.FirstOrDefault(x => x is TLDocumentAttributeAudio);
            if (audioAttribute == null)
            {
                return true;
            }

            if (message.IsSticker()
                || document.FileName.ToString().EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }
    }
}
