using System.Linq;
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
    public class EditUsernameViewModel : ViewModelBase
    {
        private string _username;

        public string Username
        {
            get { return _username; }
            set { SetField(ref _username, value, () => Username); }
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

        public EditUsernameViewModel(ICacheService cacheService, ICommonErrorHandler errorHandler, IStateService stateService, INavigationService navigationService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator) 
            : base(cacheService, errorHandler, stateService, navigationService, mtProtoService, eventAggregator)
        {
            BeginOnThreadPool(() =>
            {
                var currentUser = CacheService.GetUser(new TLInt(StateService.CurrentUserId)) as IUserName;

                if (currentUser != null
                    && currentUser.UserName != null)
                {
                    Username = currentUser.UserName.ToString();
                }
            });
        }

        public void Done()
        {
            if (IsWorking) return;

            var username = Username;
            if (username != null
                && username.StartsWith("@"))
            {
                username = username.Substring(1, username.Length - 1);
            }

            IsWorking = true;
            MTProtoService.UpdateUsernameAsync(new TLString(username),
                user =>
                {
                    CacheService.SyncUser(user, result => EventAggregator.Publish(new UserNameChangedEventArgs(result)));

                    IsWorking = false;
                    BeginOnUIThread(() => NavigationService.GoBack());
                },
                error =>
                {
                    if (TLRPCError.CodeEquals(error, ErrorCode.FLOOD))
                    {
                        HasError = true;
                        Error = AppResources.FloodWaitString;
                        Execute.BeginOnUIThread(() => MessageBox.Show(AppResources.FloodWaitString, AppResources.Error, MessageBoxButton.OK));
                    }
                    else if (TLRPCError.CodeEquals(error, ErrorCode.INTERNAL))
                    {
                        var messageBuilder = new StringBuilder();
                        messageBuilder.AppendLine(AppResources.ServerErrorMessage);
                        messageBuilder.AppendLine();
                        messageBuilder.AppendLine("Method: account.updateUsername");
                        messageBuilder.AppendLine("Result: " + error);

                        HasError = true;
                        Error = AppResources.ServerError;
                        Execute.BeginOnUIThread(() => MessageBox.Show(messageBuilder.ToString(), AppResources.ServerError, MessageBoxButton.OK));
                    }
                    else if (TLRPCError.CodeEquals(error, ErrorCode.BAD_REQUEST))
                    {
                        if (TLRPCError.TypeEquals(error, ErrorType.USERNAME_INVALID))
                        {
                            HasError = true;
                            Error = AppResources.UsernameInvalid;
                            Execute.BeginOnUIThread(() => MessageBox.Show(AppResources.UsernameInvalid, AppResources.Error, MessageBoxButton.OK));
                        }
                        else if (TLRPCError.TypeEquals(error, ErrorType.USERNAME_OCCUPIED))
                        {
                            HasError = true;
                            Error = AppResources.UsernameOccupied;
                            Execute.BeginOnUIThread(() => MessageBox.Show(AppResources.UsernameOccupied, AppResources.Error, MessageBoxButton.OK));
                        }
                        else
                        {
                            HasError = true;
                            Error = error.ToString();
                            //Execute.BeginOnUIThread(() => NavigationService.GoBack());
                        }
                    }
                    else
                    {
                        HasError = true;
                        Error = string.Empty;
                        Execute.ShowDebugMessage("account.updateUsername error " + error);
                    }

                    IsWorking = false;
                });
        }

        private static bool IsValidSymbol(char symbol)
        {
            if ((symbol >= 'a' && symbol <= 'z')
                || (symbol >= 'A' && symbol <= 'Z')
                || (symbol >= '0' && symbol <= '9')
                || symbol == '_')
            {
                return true;
            }

            return false;
        }

        public void Check()
        {
            var username = Username;
            if (username != null
                && username.StartsWith("@"))
            {
                username = username.Substring(1, username.Length - 1);
            }

            if (string.IsNullOrEmpty(username))
            {
                HasError = false;
                //Error = string.Empty;
                return;
            }

            var isValidSymbols = username.All(IsValidSymbol);
            if (!isValidSymbols)
            {
                HasError = true;
                Error = AppResources.UsernameInvalid;
                return;
            }

            if (username[0] >= '0' && username[0] <= '9')
            {
                HasError = true;
                Error = AppResources.UsernameStartsWithNumber;
                return;
            }

            if (username.Length < Constants.UsernameMinLength)
            {
                HasError = true;
                Error = AppResources.UsernameShort;
                return;
            }

            MTProtoService.CheckUsernameAsync(new TLString(username),
                result =>
                {
                    HasError = !result.Value;
                    if (HasError)
                    {
                        Error = AppResources.UsernameOccupied;
                    }
                },
                error =>
                {
                    HasError = true;
                    if (TLRPCError.CodeEquals(error, ErrorCode.FLOOD))
                    {
                        Error = AppResources.FloodWaitString;
                    }
                    else if (TLRPCError.CodeEquals(error, ErrorCode.INTERNAL))
                    {
                        var messageBuilder = new StringBuilder();
                        messageBuilder.AppendLine(AppResources.ServerErrorMessage);
                        messageBuilder.AppendLine();
                        messageBuilder.AppendLine("Method: account.checkUsername");
                        messageBuilder.AppendLine("Result: " + error);

                        Error = AppResources.ServerError;
                    }
                    else if (TLRPCError.CodeEquals(error, ErrorCode.BAD_REQUEST))
                    {
                        if (TLRPCError.TypeEquals(error, ErrorType.USERNAME_INVALID))
                        {
                            Error = AppResources.UsernameInvalid;
                        }
                        else if (TLRPCError.TypeEquals(error, ErrorType.USERNAME_OCCUPIED))
                        {
                            Error = AppResources.UsernameOccupied;
                        }
                        else
                        {
                            Error = error.ToString();
                            //Execute.BeginOnUIThread(() => NavigationService.GoBack());
                        }
                    }
                    else
                    {
                        Error = string.Empty;
                        Execute.ShowDebugMessage("account.updateUsername error " + error);
                    }
                });
        }

        public void Cancel()
        {
            NavigationService.GoBack();
        }
    }

    public class UserNameChangedEventArgs
    {
        public TLUserBase User { get; set; }

        public UserNameChangedEventArgs(TLUserBase user)
        {
            User = user;
        }
    }
}
