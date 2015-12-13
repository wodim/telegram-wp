using System.Security.Cryptography;
using Caliburn.Micro;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using TelegramClient.Services;

namespace TelegramClient.ViewModels.Additional
{
    public class ChangePasswordViewModel : ViewModelBase
    {
        private string _password;

        public string Password
        {
            get { return _password; }
            set
            {
                SetField(ref _password, value, () => Password);
                NotifyOfPropertyChange(() => CanChangePassword);
            }
        }

        private string _confirmPassword;

        public string ConfirmPassword
        {
            get { return _confirmPassword; }
            set
            {
                SetField(ref _confirmPassword, value, () => ConfirmPassword);
                NotifyOfPropertyChange(() => CanChangePassword);
            }
        }

        private readonly TLPasswordBase _passwordBase;

        public ChangePasswordViewModel(ICacheService cacheService, ICommonErrorHandler errorHandler, IStateService stateService, INavigationService navigationService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator) 
            : base(cacheService, errorHandler, stateService, navigationService, mtProtoService, eventAggregator)
        {
            _passwordBase = StateService.Password;
            StateService.Password = null;
        }

        public bool CanChangePassword
        {
            get { return !string.IsNullOrEmpty(_password) && string.Equals(_password, _confirmPassword); }
        }

        public void ChangePassword()
        {
            if (!CanChangePassword) return;
            if (_passwordBase == null) return;

            var newSaltData = TLUtils.Combine(_passwordBase.NewSalt.Data, TLLong.Random().ToBytes());
            var newSalt = TLString.FromBigEndianData(newSaltData);

            var sha = new SHA256Managed();
            var newPasswordHashData = sha.ComputeHash(TLUtils.Combine(newSalt.Data, new TLString(_password).Data, newSalt.Data));
            var newPasswordHash = TLString.FromBigEndianData(newPasswordHashData);

            var newSettings = new TLPasswordInputSettings
            {
                NewSalt = newSalt,
                NewPasswordHash = newPasswordHash,
                Hint = TLString.Empty
            };

            _passwordBase.TempNewPassword = Password;

            StateService.Password = _passwordBase;
            StateService.NewPasswordSettings = newSettings;
            StateService.RemoveBackEntry = true;
            NavigationService.UriFor<ChangePasswordHintViewModel>().Navigate();

            return;
        }
    }
}
