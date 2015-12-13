using System;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Caliburn.Micro;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using TelegramClient.Helpers;
using TelegramClient.Resources;
using TelegramClient.Services;
using Execute = Telegram.Api.Helpers.Execute;

namespace TelegramClient.ViewModels.Additional
{
    public class PasswordViewModel : ViewModelBase
    {
        public Visibility ChangePasswordVisibility
        {
            get
            {
                if (_password is TLPassword)
                {
                    return Visibility.Visible;
                }

                if (_password is TLNoPassword && TLString.IsNullOrEmpty(_password.EmailUnconfirmedPattern))
                {
                    return Visibility.Visible;
                }

                return Visibility.Collapsed;
            }
        }

        public Visibility CompletePasswordVisibility
        {
            get
            {
                if (_password is TLPassword)
                {
                    return Visibility.Collapsed;
                }

                if (_password is TLNoPassword && TLString.IsNullOrEmpty(_password.EmailUnconfirmedPattern))
                {
                    return Visibility.Collapsed;
                }

                return Visibility.Visible;
            }
        }

        private TLPasswordBase _password;

        private bool _passwordEnabled;

        public bool PasswordEnabled
        {
            get { return _passwordEnabled; }
            set { SetField(ref _passwordEnabled, value, () => PasswordEnabled); }
        }

        private Visibility _recoveryEmailUnconfirmedVisibility;

        public Visibility RecoveryEmailUnconfirmedVisibility
        {
            get { return _recoveryEmailUnconfirmedVisibility; }
            set
            {
                SetField(ref _recoveryEmailUnconfirmedVisibility, value, () => RecoveryEmailUnconfirmedVisibility);
                NotifyOfPropertyChange(() => RecoveryEmailUnconfirmedHint);
            }
        }

        public string RecoveryEmailUnconfirmedHint
        {
            get
            {
                if (!TLString.IsNullOrEmpty(_password.EmailUnconfirmedPattern))
                {
                    return string.Format(AppResources.RecoveryEmailPending, _password.EmailUnconfirmedPattern);
                }

                var password = _password as TLPassword;
                if (password != null && password.HasRecovery.Value)
                {
                    return AppResources.RecoveryEmailComplete;
                }

                return string.Empty;
            }
        }

        public string CompletePasswordLabel
        {
            get
            {
                if (!TLString.IsNullOrEmpty(_password.EmailUnconfirmedPattern))
                {
                    return string.Format(AppResources.StepsToCompleteToTwoStepsVerificationSetup, _password.EmailUnconfirmedPattern);
                }

                return AppResources.StepsToCompleteToTwoStepsVerificationSetup;
            }
        }

        public string RecoveryEmailLabel
        {
            get
            {
                if (_password != null)
                {
                    if (!TLString.IsNullOrEmpty(_password.EmailUnconfirmedPattern))
                    {
                        return AppResources.ChangeRecoveryEmail;
                    }

                    var password = _password as TLPassword;
                    if (password != null && password.HasRecovery.Value)
                    {
                        return AppResources.ChangeRecoveryEmail;
                    }
                }
                return AppResources.SetRecoveryEmail;
            }
        }

        private bool _suppressPasswordEnabled;

        private readonly DispatcherTimer _checkPasswordSettingsTimer = new DispatcherTimer();

        private void StartTimer()
        {
            if (!TLString.IsNullOrEmpty(_password.EmailUnconfirmedPattern))
            {
                _checkPasswordSettingsTimer.Start();
            }
        }

        private void StopTimer()
        {
            _checkPasswordSettingsTimer.Stop();
        }

        public PasswordViewModel(ICacheService cacheService, ICommonErrorHandler errorHandler, IStateService stateService, INavigationService navigationService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator) 
            : base(cacheService, errorHandler, stateService, navigationService, mtProtoService, eventAggregator)
        {
            _checkPasswordSettingsTimer.Tick += OnCheckPasswordSettings;
            _checkPasswordSettingsTimer.Interval = TimeSpan.FromSeconds(5.0);

            _password = StateService.Password;
            StateService.Password = null;
            if (_password != null)
            {
                PasswordEnabled = _password.IsAvailable;
                RecoveryEmailUnconfirmedVisibility = !TLString.IsNullOrEmpty(_password.EmailUnconfirmedPattern) || (_password is TLPassword && ((TLPassword)_password).HasRecovery.Value)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }

            PropertyChanged += (o, e) =>
            {
                if (Property.NameEquals(e.PropertyName, () => PasswordEnabled) && !_suppressPasswordEnabled)
                {
                    if (PasswordEnabled)
                    {
                        ChangePassword();
                    }
                    else
                    {
                        ClearPassword();
                    }
                }
            };
        }

        private void OnCheckPasswordSettings(object sender, System.EventArgs e)
        {
            Execute.ShowDebugMessage("account.getPasswordSettings");

            MTProtoService.GetPasswordAsync(
                result => BeginOnUIThread(() =>
                {
                    var password = result as TLPassword;
                    if (password != null && password.HasRecovery.Value)
                    {
                        var currentPassword = _password as TLPassword;
                        if (currentPassword != null)
                        {
                            password.CurrentPasswordHash = currentPassword.CurrentPasswordHash;
                        }

                        _password = password;

                        RecoveryEmailUnconfirmedVisibility = !TLString.IsNullOrEmpty(_password.EmailUnconfirmedPattern) || (_password is TLPassword && ((TLPassword)_password).HasRecovery.Value)
                            ? Visibility.Visible
                            : Visibility.Collapsed;
                        NotifyOfPropertyChange(() => RecoveryEmailLabel);
                        NotifyOfPropertyChange(() => CompletePasswordLabel);
                        NotifyOfPropertyChange(() => ChangePasswordVisibility);
                        NotifyOfPropertyChange(() => CompletePasswordVisibility);

                        StopTimer();
                    }
                }),
                error =>
                {
                    Execute.ShowDebugMessage("account.getPasswordSettings error " + error);
                });
        }

        protected override void OnActivate()
        {
            StartTimer();

            if (StateService.RemoveBackEntry)
            {
                StateService.RemoveBackEntry = false;
                NavigationService.RemoveBackEntry();
            }

            if (StateService.Password == null)
            {
                _suppressPasswordEnabled = true;
                if (_password != null)
                {
                    PasswordEnabled = _password.IsAvailable;
                    RecoveryEmailUnconfirmedVisibility = !TLString.IsNullOrEmpty(_password.EmailUnconfirmedPattern) || (_password is TLPassword && ((TLPassword)_password).HasRecovery.Value)
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                    NotifyOfPropertyChange(() => RecoveryEmailLabel);
                    NotifyOfPropertyChange(() => CompletePasswordLabel);
                    NotifyOfPropertyChange(() => ChangePasswordVisibility);
                    NotifyOfPropertyChange(() => CompletePasswordVisibility);
                }
                _suppressPasswordEnabled = false;
            }
            else
            {
                _password = StateService.Password;
                StateService.Password = null; 

                StartTimer();

                RecoveryEmailUnconfirmedVisibility = !TLString.IsNullOrEmpty(_password.EmailUnconfirmedPattern) || (_password is TLPassword && ((TLPassword)_password).HasRecovery.Value)
                     ? Visibility.Visible
                     : Visibility.Collapsed;
                NotifyOfPropertyChange(() => RecoveryEmailLabel);
                NotifyOfPropertyChange(() => CompletePasswordLabel);
                NotifyOfPropertyChange(() => ChangePasswordVisibility);
                NotifyOfPropertyChange(() => CompletePasswordVisibility);
            }

            base.OnActivate();
        }

        protected override void OnDeactivate(bool close)
        {
            StopTimer();

            base.OnDeactivate(close);
        }

        private void ClearPassword()
        {
            TLString currentPasswordHash;
            TLPasswordInputSettings newSettings;
            var password = _password as TLPassword;
            if (password != null)
            {
                currentPasswordHash = password.CurrentPasswordHash;
                newSettings = new TLPasswordInputSettings
                {
                    NewSalt = TLString.Empty,
                    NewPasswordHash = TLString.Empty,
                    Hint = TLString.Empty,
                    Email = TLString.Empty
                };
            }
            else
            {
                currentPasswordHash = TLString.Empty;
                newSettings = new TLPasswordInputSettings
                {
                    Email = TLString.Empty
                };
            }

            IsWorking = true;
            MTProtoService.UpdatePasswordSettingsAsync(currentPasswordHash, newSettings,
                result => BeginOnUIThread(() =>
                {
                    StopTimer();

                    IsWorking = false;
                    _password = new TLNoPassword{ NewSalt = _password.NewSalt, EmailUnconfirmedPattern = TLString.Empty };
                    if (_password != null)
                    {
                        PasswordEnabled = _password.IsAvailable;
                        RecoveryEmailUnconfirmedVisibility = !TLString.IsNullOrEmpty(_password.EmailUnconfirmedPattern) || (_password is TLPassword && ((TLPassword)_password).HasRecovery.Value)
                            ? Visibility.Visible
                            : Visibility.Collapsed;
                        NotifyOfPropertyChange(() => RecoveryEmailLabel);
                        NotifyOfPropertyChange(() => CompletePasswordLabel);
                        NotifyOfPropertyChange(() => ChangePasswordVisibility);
                        NotifyOfPropertyChange(() => CompletePasswordVisibility);
                    }
                }),
                error => BeginOnUIThread(() =>
                {
                    IsWorking = false;
                    var messageBuilder = new StringBuilder();
                    //messageBuilder.AppendLine(AppResources.ServerErrorMessage);
                    //messageBuilder.AppendLine();
                    messageBuilder.AppendLine("Method: account.updatePasswordSettings");
                    messageBuilder.AppendLine("Result: " + error);

                    if (TLRPCError.CodeEquals(error, ErrorCode.FLOOD))
                    {
                        Execute.BeginOnUIThread(() => MessageBox.Show(AppResources.FloodWaitString, AppResources.Error, MessageBoxButton.OK));
                    }
                    else if (TLRPCError.CodeEquals(error, ErrorCode.INTERNAL))
                    {
                        Execute.BeginOnUIThread(() => MessageBox.Show(messageBuilder.ToString(), AppResources.ServerError, MessageBoxButton.OK));
                    }
                    else if (TLRPCError.CodeEquals(error, ErrorCode.BAD_REQUEST))
                    {
                        if (TLRPCError.TypeEquals(error, ErrorType.PASSWORD_HASH_INVALID))
                        {
                            Execute.BeginOnUIThread(() => MessageBox.Show(messageBuilder.ToString(), AppResources.Error, MessageBoxButton.OK));
                        }
                        else if (TLRPCError.TypeEquals(error, ErrorType.NEW_PASSWORD_BAD))
                        {
                            Execute.BeginOnUIThread(() => MessageBox.Show(messageBuilder.ToString(), AppResources.Error, MessageBoxButton.OK));
                        }
                        else if (TLRPCError.TypeEquals(error, ErrorType.NEW_SALT_INVALID))
                        {
                            Execute.BeginOnUIThread(() => MessageBox.Show(messageBuilder.ToString(), AppResources.Error, MessageBoxButton.OK));
                        }
                        else if (TLRPCError.TypeEquals(error, ErrorType.EMAIL_INVALID))
                        {
                            Execute.BeginOnUIThread(() => MessageBox.Show(AppResources.EmailInvalid, AppResources.Error, MessageBoxButton.OK));
                        }
                        else if (TLRPCError.TypeEquals(error, ErrorType.EMAIL_UNCONFIRMED))
                        {
                            Execute.BeginOnUIThread(() => MessageBox.Show(messageBuilder.ToString(), AppResources.Error, MessageBoxButton.OK));
                        }
                        else
                        {
                            Execute.BeginOnUIThread(() => MessageBox.Show(messageBuilder.ToString(), AppResources.Error, MessageBoxButton.OK));
                        }
                    }
                    else
                    {
                        Execute.ShowDebugMessage("account.updatePasswordSettings error " + error);
                    }
                }));
        }

        public void AbortPassword()
        {
            ClearPassword();
        }

        public void ChangePassword()
        {
            StateService.Password = _password;
            NavigationService.UriFor<ChangePasswordViewModel>().Navigate();
        }

        public void ChangeRecoveryEmail()
        {
            StateService.Password = _password;
            NavigationService.UriFor<ChangePasswordEmailViewModel>().Navigate();
        }
    }
}
