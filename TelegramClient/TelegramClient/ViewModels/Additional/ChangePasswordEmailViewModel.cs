using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using Caliburn.Micro;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using TelegramClient.Resources;
using TelegramClient.Services;
using Execute = Telegram.Api.Helpers.Execute;

namespace TelegramClient.ViewModels.Additional
{
    public class ChangePasswordEmailViewModel : ViewModelBase
    {
        public Visibility SkipRecoveryEmailVisibility
        {
            get { return _newPasswordSettings != null ? Visibility.Visible : Visibility.Collapsed; }
        }

        private string _recoveryEmail;

        public string RecoveryEmail
        {
            get { return _recoveryEmail; }
            set
            {
                SetField(ref _recoveryEmail, value, () => RecoveryEmail);
                NotifyOfPropertyChange(() => CanChangeRecoveryEmail);
            }
        }

        private TLPasswordInputSettings _newPasswordSettings;

        private readonly TLPasswordBase _passwordBase;

        private bool _hasError;

        public bool HasError
        {
            get { return _hasError; }
            set { SetField(ref _hasError, value, () => HasError); }
        }

        private string _error = " ";

        public string Error
        {
            get { return _error; }
            set { SetField(ref _error, value, () => Error); }
        }

        public ChangePasswordEmailViewModel(ICacheService cacheService, ICommonErrorHandler errorHandler, IStateService stateService, INavigationService navigationService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator) 
            : base(cacheService, errorHandler, stateService, navigationService, mtProtoService, eventAggregator)
        {
            _passwordBase = StateService.Password;
            StateService.Password = null;

            _newPasswordSettings = StateService.NewPasswordSettings;
            StateService.NewPasswordSettings = null;
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

        private bool IsValidEmail(string email)
        {
            if (string.IsNullOrEmpty(email)) return false;

            var regex = new Regex(@"^([\w\.\-]+)@([\w\-]+)((\.(\w){2,6})+)$");
            var match = regex.Match(email);

            return match.Success;
        }

        public bool CanChangeRecoveryEmail
        {
            get { return IsValidEmail(_recoveryEmail); }
        }

        public void ChangeRecoveryEmail()
        {
            if (IsWorking) return;
            if (!CanChangeRecoveryEmail) return;
            if (_passwordBase == null) return;

            var currentPasswordHash = _passwordBase is TLPassword ? ((TLPassword)_passwordBase).CurrentPasswordHash : TLString.Empty;

            TLPasswordInputSettings newSettings;
            if (_newPasswordSettings != null)
            {
                newSettings = _newPasswordSettings;
                newSettings.Email = new TLString(RecoveryEmail);
            }
            else
            {
                newSettings = new TLPasswordInputSettings();
                newSettings.Email = new TLString(RecoveryEmail);
            }

           UpdatePasswordSettings(currentPasswordHash, newSettings);
        }

        public void SkipRecoveryEmail()
        {
            if (IsWorking) return;
            if (_passwordBase == null) return;
            if (_newPasswordSettings == null) return;

            var result = MessageBox.Show(AppResources.SkipRecoveryEmailHint, AppResources.Warning, MessageBoxButton.OKCancel);
            if (result == MessageBoxResult.OK)
            {
                var currentPasswordHash = _passwordBase is TLPassword ? ((TLPassword)_passwordBase).CurrentPasswordHash : TLString.Empty;
                UpdatePasswordSettings(currentPasswordHash, _newPasswordSettings);
            }
        }

        private void UpdatePasswordSettings(TLString currentPasswordHash, TLPasswordInputSettings newSettings)
        {
            IsWorking = true;
            MTProtoService.UpdatePasswordSettingsAsync(currentPasswordHash, newSettings,
               result =>
               {
                   IsWorking = false;
                   MTProtoService.GetPasswordAsync(
                       passwordBase => BeginOnUIThread(() =>
                       {
                           var password = passwordBase as TLPassword;
                           if (password != null)
                           {
                               password.CurrentPasswordHash = newSettings.NewPasswordHash; //EMAIL_CONFIRMED, news settings are active already
                           }

                           MessageBox.Show(AppResources.PasswordActive, AppResources.Success, MessageBoxButton.OK);

                           StateService.Password = passwordBase;
                           NavigationService.GoBack();
                       }),
                       error => Execute.BeginOnUIThread(() =>
                       {
                           Execute.ShowDebugMessage("account.getPassword error " + error);
                       }));
               },
               error =>
               {
                   IsWorking = false;
                   var messageBuilder = new StringBuilder();
                   //messageBuilder.AppendLine(AppResources.Error);
                   //messageBuilder.AppendLine();
                   messageBuilder.AppendLine("Method: account.updatePasswordSettings");
                   messageBuilder.AppendLine("Result: " + error);

                   if (TLRPCError.CodeEquals(error, ErrorCode.FLOOD))
                   {
                       HasError = true;
                       Error = AppResources.FloodWaitString;
                       Execute.BeginOnUIThread(() => MessageBox.Show(AppResources.FloodWaitString, AppResources.Error, MessageBoxButton.OK));
                   }
                   else if (TLRPCError.CodeEquals(error, ErrorCode.INTERNAL))
                   {
                       HasError = true;
                       Error = AppResources.ServerError;
                       Execute.BeginOnUIThread(() => MessageBox.Show(messageBuilder.ToString(), AppResources.ServerError, MessageBoxButton.OK));
                   }
                   else if (TLRPCError.CodeEquals(error, ErrorCode.BAD_REQUEST))
                   {
                       if (TLRPCError.TypeEquals(error, ErrorType.PASSWORD_HASH_INVALID))
                       {
                           HasError = true;
                           Error = string.Format("{0} {1}", error.Code, error.Message);
                           Execute.BeginOnUIThread(() => MessageBox.Show(messageBuilder.ToString(), AppResources.Error, MessageBoxButton.OK));
                       }
                       else if (TLRPCError.TypeEquals(error, ErrorType.NEW_PASSWORD_BAD))
                       {
                           HasError = true;
                           Error = string.Format("{0} {1}", error.Code, error.Message);
                           Execute.BeginOnUIThread(() => MessageBox.Show(messageBuilder.ToString(), AppResources.Error, MessageBoxButton.OK));
                       }
                       else if (TLRPCError.TypeEquals(error, ErrorType.NEW_SALT_INVALID))
                       {
                           HasError = true;
                           Error = string.Format("{0} {1}", error.Code, error.Message);
                           Execute.BeginOnUIThread(() => MessageBox.Show(messageBuilder.ToString(), AppResources.Error, MessageBoxButton.OK));
                       }
                       else if (TLRPCError.TypeEquals(error, ErrorType.EMAIL_INVALID))
                       {
                           HasError = true;
                           Error = AppResources.EmailInvalid;
                           Execute.BeginOnUIThread(() => MessageBox.Show(AppResources.EmailInvalid, AppResources.Error, MessageBoxButton.OK));
                       }
                       else if (TLRPCError.TypeEquals(error, ErrorType.EMAIL_UNCONFIRMED))
                       {
                           HasError = false;
                           Error = " ";
                           Execute.BeginOnUIThread(() =>
                           {
                               MTProtoService.GetPasswordAsync(
                                   passwordBase => BeginOnUIThread(() =>
                                   {
                                       IsWorking = false;
                                       var password = passwordBase as TLPassword;
                                       if (password != null)
                                       {
                                           password.CurrentPasswordHash = currentPasswordHash; //EMAIL_UNCONFIRMED - new settings are not active yet
                                       }

                                       MessageBox.Show(AppResources.CompletePasswordHint, AppResources.AlmostThere, MessageBoxButton.OK);

                                       StateService.Password = passwordBase;
                                       NavigationService.GoBack();
                                   }),
                                   error2 => Execute.BeginOnUIThread(() =>
                                   {
                                       IsWorking = false;
                                       Execute.ShowDebugMessage("account.getPassword error " + error2);
                                   }));
                           });
                       }
                       else
                       {
                           HasError = true;
                           Error = string.Format("{0} {1}", error.Code, error.Message);
                           Execute.BeginOnUIThread(() => MessageBox.Show(messageBuilder.ToString(), AppResources.Error, MessageBoxButton.OK));
                       }
                   }
                   else
                   {
                       HasError = true;
                       Error = string.Empty;
                       Execute.ShowDebugMessage("account.updatePasswordSettings error " + error);
                   }
               });
        }
    }
}
