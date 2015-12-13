using System;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Caliburn.Micro;
using Microsoft.Phone.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.Updates;
using Telegram.Api.TL;
using TelegramClient.Helpers;
using TelegramClient.Models;
using TelegramClient.Resources;
using TelegramClient.Services;
using TelegramClient.ViewModels.Additional;
using ErrorType = Telegram.Api.TL.ErrorType;

namespace TelegramClient.ViewModels.Auth
{
    public class ConfirmViewModel : ViewModelBase, Telegram.Api.Aggregator.IHandle<string>
    {
        private DateTime _startTime;

        private readonly DispatcherTimer _callTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(0.5) };

        private int _timeCounter = Constants.SendCallDefaultTimeout;

        public int TimeCounter
        {
            get { return _timeCounter; }
            set
            {
                SetField(ref _timeCounter, value, () => TimeCounter);
            }
        }

        private string _timeCounterString = " ";

        public string TimeCounterString
        {
            get { return _timeCounterString; }
            set { SetField(ref _timeCounterString, value, () => TimeCounterString); }
        }

        private string _code;

        public string Code
        {
            get { return _code; }
            set { SetField(ref _code, value, () => Code); }
        }

        private Visibility _helpVisibility = Visibility.Collapsed;

        public Visibility HelpVisibility
        {
            get { return _helpVisibility; }
            set { SetField(ref _helpVisibility, value, () => HelpVisibility); }
        }

        public string Subtitle { get; set; }

        public DebugViewModel Debug { get; private set; }

        public int SendCallTimeout { get; set; }

        private bool _changePhoneNumber;

        private IExtendedDeviceInfoService _extendedDeviceInfoService;

        public ConfirmViewModel(IExtendedDeviceInfoService extendedDeviceInfoService, DebugViewModel debug, ICacheService cacheService, ICommonErrorHandler errorHandler, IStateService stateService, INavigationService navigationService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator) 
            : base(cacheService, errorHandler, stateService, navigationService, mtProtoService, eventAggregator)
        {
            _extendedDeviceInfoService = extendedDeviceInfoService;
#if DEBUG
            HelpVisibility = Visibility.Visible;
#endif

            BeginOnThreadPool(() =>
            {
                Subtitle = StateService.PhoneNumberString;
                BeginOnUIThread(() => NotifyOfPropertyChange(() => Subtitle));
            });

            SendCallTimeout = stateService.SendCallTimeout != null ? StateService.SendCallTimeout.Value : Constants.SendCallDefaultTimeout;

            EventAggregator.Subscribe(this);
            SuppressUpdateStatus = true;

            Debug = debug;

            if (StateService.ChangePhoneNumber)
            {
                _changePhoneNumber = true;
                StateService.ChangePhoneNumber = false;
            }

            //_updatesService = updatesService;

            PropertyChanged += (sender, args) =>
            {
                if (Property.NameEquals(args.PropertyName, () => Code))
                {
                    NotifyOfPropertyChange(() => CanConfirm);

                    if (!string.IsNullOrEmpty(Code) && Code.Length == Constants.PhoneCodeLength)
                    {
                        Confirm();
                    }
                }
            };

            _callTimer.Tick += (sender, args) =>
            {
                _timeCounter = (int)(SendCallTimeout - (DateTime.Now - _startTime).TotalSeconds);
                TimeCounterString = string.Format(AppResources.WeWillCallYou, TimeSpan.FromSeconds(TimeCounter).ToString(@"m\:ss"));
                if (_timeCounter <= 0)    
                {
                    _timeCounter = 0;
                    TimeCounterString = AppResources.TelegramDialedYourNumber;
                    HelpVisibility = Visibility.Visible;
                    _callTimer.Stop();
                    MTProtoService.SendCallAsync(
                        StateService.PhoneNumber, StateService.PhoneCodeHash,
                        result => BeginOnUIThread(() =>
                        {

                        }),
                        error => BeginOnUIThread(() =>
                        {
#if DEBUG
                            MessageBox.Show(error.ToString());
#endif
                        }));
                }
                NotifyOfPropertyChange(() => TimeCounter);
            };
        }

        public bool CanConfirm
        {
            get { return !string.IsNullOrEmpty(Code); }
        }

        private TLRPCError _lastError;

        public void Confirm()
        {
            if (_changePhoneNumber)
            {
                ConfirmChangePhoneNumber();
                return;
            }

            IsWorking = true;
            StateService.PhoneCode = new TLString(Code);
#if LOG_REGISTRATION
            TLUtils.WriteLog("auth.signIn"); 
#endif
            MTProtoService.SignInAsync(
                StateService.PhoneNumber, StateService.PhoneCodeHash, StateService.PhoneCode,
                auth => BeginOnUIThread(() =>
                {
#if LOG_REGISTRATION
                    TLUtils.WriteLog("auth.signIn result " + auth); 
                    TLUtils.WriteLog("TLUtils.IsLogEnabled=false");
#endif

                    TLUtils.IsLogEnabled = false;
                    TLUtils.LogItems.Clear();

                    TimeCounterString = string.Empty;
                    HelpVisibility = Visibility.Collapsed;
                    _callTimer.Stop();

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
                    //_updatesService.SetCurrentUser(auth.User);
                    _isProcessing = false;
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
                    TLUtils.WriteLog("auth.signIn error " + error); 
#endif
                    _lastError = error;
                    IsWorking = false;
                    if (error.TypeEquals(ErrorType.PHONE_NUMBER_UNOCCUPIED))
                    {
                        _callTimer.Stop();
                        StateService.ClearNavigationStack = true;
                        NavigationService.UriFor<SignUpViewModel>().Navigate();
                    }
                    else if (error.TypeEquals(ErrorType.PHONE_CODE_INVALID))
                    {
                        MessageBox.Show(AppResources.PhoneCodeInvalidString, AppResources.Error, MessageBoxButton.OK);
                    }
                    else if (error.TypeEquals(ErrorType.PHONE_CODE_EMPTY))
                    {
                        MessageBox.Show(AppResources.PhoneCodeEmpty, AppResources.Error, MessageBoxButton.OK);
                    }
                    else if (error.TypeEquals(ErrorType.PHONE_CODE_EXPIRED))
                    {
                        MessageBox.Show(AppResources.PhoneCodeExpiredString, AppResources.Error, MessageBoxButton.OK);
                        OnBackKeyPress();
                        NavigationService.GoBack();
                    }
                    else if (error.TypeEquals(ErrorType.SESSION_PASSWORD_NEEDED))
                    {
                        IsWorking = true;
                        MTProtoService.GetPasswordAsync(
                            password => BeginOnUIThread(() =>
                            {
                                IsWorking = false;
                                _callTimer.Stop();
                                StateService.Password = password;
                                StateService.RemoveBackEntry = true;
                                NavigationService.UriFor<ConfirmPasswordViewModel>().Navigate();
                            }),
                            error2 =>
                            {
                                IsWorking = false;
                                Telegram.Api.Helpers.Execute.ShowDebugMessage("account.getPassword error " + error);
                            });
                    }
                    else if (error.CodeEquals(ErrorCode.FLOOD))
                    {
                        MessageBox.Show(AppResources.FloodWaitString + Environment.NewLine + "(" + error.Message + ")", AppResources.Error, MessageBoxButton.OK);
                    }
                    else
                    {
                        Telegram.Api.Helpers.Execute.ShowDebugMessage("account.signIn error " + error);
                    }
                }));
        }

        private void ConfirmChangePhoneNumber()
        {
            IsWorking = true;
            StateService.PhoneCode = new TLString(Code);

            MTProtoService.ChangePhoneAsync(
                StateService.PhoneNumber, StateService.PhoneCodeHash, StateService.PhoneCode,
                auth => BeginOnUIThread(() =>
                {
                    TLUtils.IsLogEnabled = false;
                    TLUtils.LogItems.Clear();

                    TimeCounterString = string.Empty;
                    HelpVisibility = Visibility.Collapsed;
                    _callTimer.Stop();

                    
                    _isProcessing = false;
                    auth.NotifyOfPropertyChange(() => auth.Phone);
                    NavigationService.RemoveBackEntry();
                    NavigationService.GoBack();
                    IsWorking = false;
                }),
                error => BeginOnUIThread(() =>
                {
                    _lastError = error;
                    IsWorking = false;
                    if (error.TypeEquals(ErrorType.PHONE_NUMBER_UNOCCUPIED))
                    {
                        StateService.ClearNavigationStack = true;
                        _callTimer.Stop();
                        NavigationService.UriFor<SignUpViewModel>().Navigate();
                    }
                    else if (error.TypeEquals(ErrorType.PHONE_CODE_INVALID))
                    {
                        MessageBox.Show(AppResources.PhoneCodeInvalidString, AppResources.Error, MessageBoxButton.OK);
                    }
                    else if (error.TypeEquals(ErrorType.PHONE_CODE_EMPTY))
                    {
                        MessageBox.Show(AppResources.PhoneCodeEmpty, AppResources.Error, MessageBoxButton.OK);
                    }
                    else if (error.TypeEquals(ErrorType.PHONE_CODE_EXPIRED))
                    {
                        MessageBox.Show(AppResources.PhoneCodeExpiredString, AppResources.Error, MessageBoxButton.OK);
                        OnBackKeyPress();
                        NavigationService.GoBack();
                    }
                    else if (error.CodeEquals(ErrorCode.FLOOD))
                    {
                        MessageBox.Show(AppResources.FloodWaitString + Environment.NewLine + "(" + error.Message + ")", AppResources.Error, MessageBoxButton.OK);
                    }
                    else
                    {
#if DEBUG
                        MessageBox.Show(error.ToString());
#endif
                    }
                }));
        }

        public void Handle(string command)
        {
            if (string.Equals(command, Commands.LogOutCommand))
            {
                Code = string.Empty;
                IsWorking = false;
                HelpVisibility = Visibility.Collapsed;
            }
        }

        private bool _isProcessing;

        protected override void OnActivate()
        {
            if (_isProcessing) return;

            _isProcessing = true;

            Subtitle = StateService.PhoneNumberString;
            BeginOnUIThread(() => NotifyOfPropertyChange(() => Subtitle));

            TimeCounter = SendCallTimeout;
            _startTime = DateTime.Now;
            _callTimer.Start();

            base.OnActivate();
        }

        public void OnBackKeyPress()
        {
            _isProcessing = false;
            Code = string.Empty;
            _callTimer.Stop();
            TimeCounterString = " ";
#if DEBUG
            HelpVisibility = Visibility.Visible;
#else
            HelpVisibility = Visibility.Collapsed;
#endif
            }

        public string Email
        {
            get { return Constants.LogEmail; }
        }

        public void SendMail()
        {
            var logBuilder = new StringBuilder();
            foreach (var item in TLUtils.LogItems)
            {
                logBuilder.AppendLine(item);
            }

            var body = new StringBuilder();
            body.AppendLine();
            body.AppendLine();
            body.AppendLine("Page: Confirm Code");
            body.AppendLine("Phone: " + "+" + StateService.PhoneNumber);
            body.AppendLine("App version: " + _extendedDeviceInfoService.AppVersion);
            body.AppendLine("OS version: " + _extendedDeviceInfoService.SystemVersion);
            body.AppendLine("Device Name: " + _extendedDeviceInfoService.Model);
            body.AppendLine("Location: " + Telegram.Api.Helpers.Utils.CurrentUICulture());
            body.AppendLine("Wi-Fi: " + _extendedDeviceInfoService.IsWiFiEnabled);
            body.AppendLine("Mobile Network: " + _extendedDeviceInfoService.IsCellularDataEnabled);
            body.AppendLine("Last error: " + ((_lastError != null) ? _lastError.ToString() : null));
            body.AppendLine("Log" + Environment.NewLine + logBuilder);

            var task = new EmailComposeTask();
            task.Body = body.ToString();
            task.To = Constants.LogEmail;
            task.Subject = "WP registration/login issue (" + _extendedDeviceInfoService.AppVersion + ", Code) " + StateService.PhoneNumber;
            task.Show();
        }

        protected override void OnDeactivate(bool close)
        {
            //_callTimer.Stop();
            base.OnDeactivate(close);
        }
    }
}
