using System;
using System.Security.Cryptography;
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
    public class EnterPasswordViewModel : ViewModelBase
    {
        private string _password;

        public string Password
        {
            get { return _password; }
            set
            {
                SetField(ref _password, value, () => Password);
                NotifyOfPropertyChange(() => CanDone);
            }
        }

        public string PasswordHint { get; set; }

        private readonly TLPassword _passwordBase;

        private TLString _email;

        public EnterPasswordViewModel(ICacheService cacheService, ICommonErrorHandler errorHandler, IStateService stateService, INavigationService navigationService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator)
            : base(cacheService, errorHandler, stateService, navigationService, mtProtoService, eventAggregator)
        {
            _passwordBase = StateService.Password as TLPassword;

            if (_passwordBase != null)
            {
                PasswordHint = _passwordBase.Hint.ToString();
            }
        }

        public bool CanDone
        {
            get { return !string.IsNullOrEmpty(Password); }
        }

        public void Done()
        {
            if (IsWorking) return;

            var currentSalt = _passwordBase.CurrentSalt;

            var sha = new SHA256Managed();
            var passwordHashData = sha.ComputeHash(TLUtils.Combine(currentSalt.Data, new TLString(_password).Data, currentSalt.Data));
            var passwordHash = TLString.FromBigEndianData(passwordHashData);

            IsWorking = true;
            MTProtoService.GetPasswordSettingsAsync(passwordHash,
                result => BeginOnUIThread(() =>
                {
                    IsWorking = false;

                    _passwordBase.CurrentPasswordHash = passwordHash;
                    _passwordBase.Settings = result;

                    StateService.RemoveBackEntry = true;
                    StateService.Password = _passwordBase;
                    NavigationService.UriFor<PasswordViewModel>().Navigate();
                }),
                error => BeginOnUIThread(() =>
                {
                    IsWorking = false;
                    if (error.TypeEquals(ErrorType.PASSWORD_HASH_INVALID))
                    {
                        MessageBox.Show(AppResources.PasswordInvalidString, AppResources.Error, MessageBoxButton.OK);
                    }
                    else if (error.CodeEquals(ErrorCode.FLOOD))
                    {
                        MessageBox.Show(AppResources.FloodWaitString + Environment.NewLine + "(" + error.Message + ")", AppResources.Error, MessageBoxButton.OK);
                    }
                    else
                    {
                        Execute.ShowDebugMessage("account.checkPassword error " + error);
                    }
                }));
        }

        public void ForgotPassword()
        {
            if (_passwordBase == null) return;

            if (_passwordBase.HasRecovery.Value)
            {
                IsWorking = true;
                MTProtoService.RequestPasswordRecoveryAsync(
                    result => BeginOnUIThread(() =>
                    {
                        IsWorking = false;
                        _passwordBase.EmailUnconfirmedPattern = result.EmailPattern;

                        MessageBox.Show(string.Format(AppResources.SentRecoveryCodeMessage, result.EmailPattern), AppResources.AppName, MessageBoxButton.OK);

                        StateService.Password = _passwordBase;
                        StateService.RemoveBackEntry = true;
                        NavigationService.UriFor<PasswordRecoveryViewModel>().Navigate();
                    }),
                    error => BeginOnUIThread(() =>
                    {
                        IsWorking = false;

                        var messageBuilder = new StringBuilder();
                        messageBuilder.AppendLine(AppResources.Error);
                        messageBuilder.AppendLine();
                        messageBuilder.AppendLine("Method: account.requestPasswordRecovery");
                        messageBuilder.AppendLine("Result: " + error);

                        if (TLRPCError.CodeEquals(error, ErrorCode.FLOOD))
                        {
                            Execute.BeginOnUIThread(() => MessageBox.Show(AppResources.FloodWaitString + Environment.NewLine + "(" + error.Message + ")", AppResources.Error, MessageBoxButton.OK));
                        }
                        else if (TLRPCError.CodeEquals(error, ErrorCode.INTERNAL))
                        {
                            Execute.BeginOnUIThread(() => MessageBox.Show(messageBuilder.ToString(), AppResources.ServerError, MessageBoxButton.OK));
                        }
                        else if (TLRPCError.CodeEquals(error, ErrorCode.BAD_REQUEST))
                        {
                            if (TLRPCError.TypeEquals(error, ErrorType.PASSWORD_EMPTY))
                            {
                                Execute.BeginOnUIThread(() => MessageBox.Show(messageBuilder.ToString(), AppResources.Error, MessageBoxButton.OK));
                            }
                            else if (TLRPCError.TypeEquals(error, ErrorType.PASSWORD_RECOVERY_NA))
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
                            Execute.ShowDebugMessage("account.requestPasswordRecovery error " + error);
                        }
                    }));
            }
            else
            {
                MessageBox.Show(AppResources.NoRecoveryEmailMessage, AppResources.Sorry, MessageBoxButton.OK);
            }
        }

        public void Cancel()
        {
            NavigationService.GoBack();
        }
    }
}
