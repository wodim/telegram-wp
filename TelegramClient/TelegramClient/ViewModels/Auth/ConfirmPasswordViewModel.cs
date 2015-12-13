using System;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using Caliburn.Micro;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using TelegramClient.Helpers;
using TelegramClient.Models;
using TelegramClient.Resources;
using TelegramClient.Services;
using TelegramClient.ViewModels.Additional;
using Execute = Telegram.Api.Helpers.Execute;

namespace TelegramClient.ViewModels.Auth
{
    public class ConfirmPasswordViewModel : ViewModelBase
    {
        private Visibility _resetAccountVisibility = Visibility.Collapsed;

        public Visibility ResetAccountVisibility
        {
            get { return _resetAccountVisibility; }
            set { SetField(ref _resetAccountVisibility, value, () => ResetAccountVisibility); }
        }

        private string _code;

        public string Code
        {
            get { return _code; }
            set { SetField(ref _code, value, () => Code); }
        }

        private readonly TLPassword _password;

        public string PasswordHint { get; set; }

        public ConfirmPasswordViewModel(ICacheService cacheService, ICommonErrorHandler errorHandler, IStateService stateService, INavigationService navigationService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator) 
            : base(cacheService, errorHandler, stateService, navigationService, mtProtoService, eventAggregator)
        {
            _password = StateService.Password as TLPassword;
            StateService.Password = null;

            if (_password != null)
            {
                PasswordHint = _password.Hint.ToString();
            }

            PropertyChanged += (sender, args) =>
            {
                if (Property.NameEquals(args.PropertyName, () => Code))
                {
                    NotifyOfPropertyChange(() => CanConfirm);
                }
            };
        }

        public bool CanConfirm
        {
            get { return !string.IsNullOrEmpty(Code); }
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

        public void Confirm()
        {
            if (_password == null) return;
            if (!CanConfirm) return;

            var currentSalt = _password.CurrentSalt;
            var sha = new SHA256Managed();
            var passwordHashData = sha.ComputeHash(TLUtils.Combine(currentSalt.Data, new TLString(Code).Data, currentSalt.Data));
            var passwordHash = TLString.FromBigEndianData(passwordHashData);

            IsWorking = true;
#if LOG_REGISTRATION
            TLUtils.WriteLog("auth.checkPassword");
#endif
            MTProtoService.CheckPasswordAsync(passwordHash,
                auth => BeginOnUIThread(() =>
                {
#if LOG_REGISTRATION
                    TLUtils.WriteLog("auth.checkPassword result " + auth);
                    TLUtils.WriteLog("TLUtils.IsLogEnabled=false");
#endif

                    TLUtils.IsLogEnabled = false;
                    TLUtils.LogItems.Clear();

                    var result = MessageBox.Show(
                        AppResources.ConfirmPushMessage,
                        AppResources.ConfirmPushTitle,
                        MessageBoxButton.OKCancel);

                    if (result != MessageBoxResult.OK)
                    {
                        StateService.GetNotifySettingsAsync(settings =>
                        {
                            var s = settings ?? new Settings();
                            s.ContactAlert = false;
                            s.ContactMessagePreview = true;
                            s.ContactSound = StateService.Sounds[0];
                            s.GroupAlert = false;
                            s.GroupMessagePreview = true;
                            s.GroupSound = StateService.Sounds[0];

                            s.InAppMessagePreview = true;
                            s.InAppSound = true;
                            s.InAppVibration = true;

                            StateService.SaveNotifySettingsAsync(s);
                        });

                        MTProtoService.UpdateNotifySettingsAsync(
                            new TLInputNotifyUsers(),
                            new TLInputPeerNotifySettings
                            {
                                EventsMask = new TLInt(1),
                                MuteUntil = new TLInt(int.MaxValue),
                                ShowPreviews = new TLBool(true),
                                Sound = new TLString(StateService.Sounds[0])
                            },
                            r => { });

                        MTProtoService.UpdateNotifySettingsAsync(
                            new TLInputNotifyChats(),
                            new TLInputPeerNotifySettings
                            {
                                EventsMask = new TLInt(1),
                                MuteUntil = new TLInt(int.MaxValue),
                                ShowPreviews = new TLBool(true),
                                Sound = new TLString(StateService.Sounds[0])
                            },
                            r => { });
                    }
                    else
                    {
                        StateService.GetNotifySettingsAsync(settings =>
                        {
                            var s = settings ?? new Settings();
                            s.ContactAlert = true;
                            s.ContactMessagePreview = true;
                            s.ContactSound = StateService.Sounds[0];
                            s.GroupAlert = true;
                            s.GroupMessagePreview = true;
                            s.GroupSound = StateService.Sounds[0];

                            s.InAppMessagePreview = true;
                            s.InAppSound = true;
                            s.InAppVibration = true;

                            StateService.SaveNotifySettingsAsync(s);
                        });

                        MTProtoService.UpdateNotifySettingsAsync(
                            new TLInputNotifyUsers(),
                            new TLInputPeerNotifySettings
                            {
                                EventsMask = new TLInt(1),
                                MuteUntil = new TLInt(0),
                                ShowPreviews = new TLBool(true),
                                Sound = new TLString(StateService.Sounds[0])
                            },
                            r => { });

                        MTProtoService.UpdateNotifySettingsAsync(
                            new TLInputNotifyChats(),
                            new TLInputPeerNotifySettings
                            {
                                EventsMask = new TLInt(1),
                                MuteUntil = new TLInt(0),
                                ShowPreviews = new TLBool(true),
                                Sound = new TLString(StateService.Sounds[0])
                            },
                            r => { });
                    }

                    MTProtoService.SetInitState();
                    StateService.CurrentUserId = auth.User.Index;
                    StateService.ClearNavigationStack = true;
                    StateService.FirstRun = true;
                    SettingsHelper.SetValue(Constants.IsAuthorizedKey, true);
                    NavigationService.UriFor<ShellViewModel>().Navigate();
                    IsWorking = false;
                }),
                error => BeginOnUIThread(() =>
                {
#if LOG_REGISTRATION
                    TLUtils.WriteLog("auth.checkPassword error " + error);
#endif
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

        public void ResetAccount()
        {
            var r = MessageBox.Show(AppResources.ResetMyAccountConfirmation, AppResources.Warning, MessageBoxButton.OKCancel);
            if (r != MessageBoxResult.OK) return;

            IsWorking = true;
            MTProtoService.DeleteAccountAsync(new TLString("Forgot password"),
                result => BeginOnUIThread(() =>
                {
                    IsWorking = false;

                    StateService.RemoveBackEntry = true;
                    NavigationService.UriFor<SignUpViewModel>().Navigate();
                }),
                error =>
                {
                    IsWorking = false;
                    Execute.ShowDebugMessage("account.deleteAccount error " + error);
                });
        }

        public void ForgotPassword()
        {
            if (_password == null) return;

            BeginOnUIThread(() =>
            {
                if (_password.HasRecovery.Value)
                {
                    IsWorking = true;
                    MTProtoService.RequestPasswordRecoveryAsync(
                        result => BeginOnUIThread(() =>
                        {
                            IsWorking = false;
                            _password.EmailUnconfirmedPattern = result.EmailPattern;
                            _password.IsAuthRecovery = true;

                            MessageBox.Show(string.Format(AppResources.SentRecoveryCodeMessage, result.EmailPattern), AppResources.AppName, MessageBoxButton.OK);

                            StateService.Password = _password;
                            StateService.RemoveBackEntry = true;
                            NavigationService.UriFor<PasswordRecoveryViewModel>().Navigate();

                            ResetAccountVisibility = Visibility.Visible;
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

                            ResetAccountVisibility = Visibility.Visible;
                        }));
                }
                else
                {
                    MessageBox.Show(AppResources.NoRecoveryEmailMessage, AppResources.Sorry, MessageBoxButton.OK);
                    ResetAccountVisibility = Visibility.Visible;
                }
            });
        }
    }
}
