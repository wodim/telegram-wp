using System.Collections.ObjectModel;
using Caliburn.Micro;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.FileManager;
using Telegram.Api.TL;
using TelegramClient.Resources;
using TelegramClient.Services;
using TelegramClient.ViewModels.Contacts;

namespace TelegramClient.ViewModels.Media
{
    public class SecretMediaViewModel : ItemsViewModelBase<TLDecryptedMessage>, Telegram.Api.Aggregator.IHandle<DownloadableItem>
    {

        public SecretMediaViewModel(ICacheService cacheService, ICommonErrorHandler errorHandler, IStateService stateService, INavigationService navigationService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator) : 
            base(cacheService, errorHandler, stateService, navigationService, mtProtoService, eventAggregator)
        {
            Items = new ObservableCollection<TLDecryptedMessage>();
        }

        public string DisplayName
        {
            get
            {
                return AppResources.Media.ToLowerInvariant();
            }
        }

        public SecretContactViewModel Contact { get; set; }

        protected override void OnActivate()
        {
            var mediaItems = StateService.CurrentDecryptedMediaMessages;
            StateService.CurrentDecryptedMediaMessages = null;
            foreach (var item in mediaItems)
            {
                Items.Add(item);
            }


            if (Items.Count == 0)
            {
                Status = AppResources.NoMediaHere;
            }

            base.OnActivate();
        }


        public void OpenMedia(TLDecryptedMessage message)
        {
            if (message == null) return;
            if (Contact == null) return;

            if (Contact.ImageViewer == null)
            {
                Contact.ImageViewer = new DecryptedImageViewerViewModel(StateService);
                Contact.NotifyOfPropertyChange(() => Contact.ImageViewer);
            }

            //var mediaPhoto = message.Media as TLMessageMediaPhoto;
            //if (mediaPhoto != null)
            {
                StateService.CurrentDecryptedMediaMessages = Items;
                StateService.CurrentDecryptedPhotoMessage = message;

                if (Contact.ImageViewer != null)
                {
                    Contact.ImageViewer.OpenViewer();
                }
                //NavigationService.UriFor<ImageViewerViewModel>().Navigate();
                return;
            }
        }

        public void Handle(DownloadableItem message)
        {
            
        }
    }
}
