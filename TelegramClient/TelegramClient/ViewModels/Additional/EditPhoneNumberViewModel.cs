using System.Windows;
using Caliburn.Micro;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using TelegramClient.Resources;
using TelegramClient.Services;
using TelegramClient.ViewModels.Auth;

namespace TelegramClient.ViewModels.Additional
{
    public class EditPhoneNumberViewModel : ItemDetailsViewModelBase
    {
        public EditPhoneNumberViewModel(ICacheService cacheService, ICommonErrorHandler errorHandler, IStateService stateService, INavigationService navigationService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator) : base(cacheService, errorHandler, stateService, navigationService, mtProtoService, eventAggregator)
        {
            if (StateService.CurrentContact != null)
            {
                CurrentItem = StateService.CurrentContact;
                StateService.CurrentContact = null;
            }
        }

        public void ChangePhoneNumber()
        {
            BeginOnUIThread(() =>
            {
                var result = MessageBox.Show(AppResources.NewNumberConfirmation, AppResources.Confirm, MessageBoxButton.OKCancel);
                if (result == MessageBoxResult.OK)
                {
                    StateService.RemoveBackEntry = true;
                    StateService.ChangePhoneNumber = true;
                    NavigationService.UriFor<SignInViewModel>().Navigate();
                }
            });
        }
    }
}
