using System;
using System.Text;
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
    public class ChangePasswordHintViewModel : ViewModelBase
    {
        private string _passwordHint;

        public string PasswordHint
        {
            get { return _passwordHint; }
            set
            {
                SetField(ref _passwordHint, value, () => PasswordHint);
                NotifyOfPropertyChange(() => CanChangePasswordHint);
            }
        }

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

        private readonly TLPasswordBase _passwordBase;

        private readonly TLPasswordInputSettings _newSettings;

        public ChangePasswordHintViewModel(ICacheService cacheService, ICommonErrorHandler errorHandler, IStateService stateService, INavigationService navigationService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator) 
            : base(cacheService, errorHandler, stateService, navigationService, mtProtoService, eventAggregator)
        {
            _passwordBase = StateService.Password;
            StateService.Password = null;

            _newSettings = StateService.NewPasswordSettings;
            StateService.NewPasswordSettings = null;

            if (_passwordBase != null
                && !string.IsNullOrEmpty(_passwordBase.TempNewPassword) 
                && _passwordBase.TempNewPassword.Length > 2)
            {
                PasswordHint = string.Format("{0}{1}{2}", 
                    _passwordBase.TempNewPassword[0],
                    new String('*', _passwordBase.TempNewPassword.Length - 2),
                    _passwordBase.TempNewPassword[_passwordBase.TempNewPassword.Length - 1]);
            }
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

        public bool CanChangePasswordHint
        {
            get
            {
                return !string.Equals(_passwordBase.TempNewPassword, PasswordHint);
            }
        }

        public void ChangePasswordHint()
        {
            if (_passwordBase == null) return;
            if (_newSettings == null) return;
            if (!CanChangePasswordHint)
            {
                HasError = true;
                Error = AppResources.PasswordHintError;
                return;
            }

            HasError = false;
            Error = " ";

            var newSettings = _newSettings;
            newSettings.Hint = new TLString(PasswordHint);

            var password = _passwordBase as TLPassword;
            if (password != null && password.HasRecovery.Value)
            {
                var currentPasswordHash = password.CurrentPasswordHash;
                UpdatePasswordSettings(currentPasswordHash, newSettings);

                return;
            }


            StateService.Password = _passwordBase;
            StateService.NewPasswordSettings = newSettings;
            StateService.RemoveBackEntry = true;
            NavigationService.UriFor<ChangePasswordEmailViewModel>().Navigate();
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
