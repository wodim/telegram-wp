using Caliburn.Micro;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using TelegramClient.Helpers;
using TelegramClient.Services;

namespace TelegramClient.ViewModels.Additional
{
    public class EnterPasscodeViewModel : ViewModelBase
    {
        private string _passcode;

        public string Passcode
        {
            get { return _passcode; }
            set
            {
                SetField(ref _passcode, value, () => Passcode);
                NotifyOfPropertyChange(() => IsPasscodeValid);
            }
        }

        public bool Simple
        {
            get { return PasscodeUtils.IsSimple; }
        }

        public bool IsPasscodeValid
        {
            get { return PasscodeUtils.Check(Passcode); }
        }

        public EnterPasscodeViewModel(ICacheService cacheService, ICommonErrorHandler errorHandler, IStateService stateService, INavigationService navigationService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator)
            : base(cacheService, errorHandler, stateService, navigationService, mtProtoService, eventAggregator)
        {
            PropertyChanged += (sender, args) =>
            {
                if (Property.NameEquals(args.PropertyName, () => Passcode))
                {
                    if (PasscodeUtils.IsEnabled && PasscodeUtils.IsSimple)
                    {
                        Done();
                    }
                }
            };
        }

        public void Done()
        {
            if (!IsPasscodeValid) return;

            StateService.RemoveBackEntry = true;
            NavigationService.UriFor<PasscodeViewModel>().Navigate();
        }

        public void Cancel()
        {
            NavigationService.GoBack();
        }
    }
}
