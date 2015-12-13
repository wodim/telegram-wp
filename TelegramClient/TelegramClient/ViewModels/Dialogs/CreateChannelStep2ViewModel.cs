using System;
using System.Linq;
using System.Text;
using System.Windows;
using Caliburn.Micro;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using TelegramClient.Helpers;
using TelegramClient.Resources;
using TelegramClient.Services;
using Execute = Telegram.Api.Helpers.Execute;

namespace TelegramClient.ViewModels.Dialogs
{
    public class CreateChannelStep2ViewModel : CreateDialogViewModel
    {
        private bool _isPublic = true;

        public bool IsPublic
        {
            get { return _isPublic; }
            set
            {
                SetField(ref _isPublic, value, () => IsPublic);
                NotifyOfPropertyChange(() => ChannelTypeDescription);
                NotifyOfPropertyChange(() => ChannelLinkDescription);
            }
        }

        public string ChannelTypeDescription
        {
            get { return IsPublic ? AppResources.PublicChannelDescription : AppResources.PrivateChannelDescription; }
        }

        private string _userName;

        public string UserName
        {
            get { return _userName; }
            set { SetField(ref _userName, value, () => UserName); }
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

        public string ChannelLinkDescription
        {
            get { return IsPublic ? AppResources.PublicLinkDescription : AppResources.PrivateLinkDescription; }
        }

        public TLExportedChatInvite Invite { get; set; }


        private string _inviteLink;

        public string InviteLink
        {
            get { return _inviteLink; }
            set { SetField(ref _inviteLink, value, () => InviteLink); }
        }

        private readonly TLChannel _newChannel;

        public CreateChannelStep2ViewModel(ICacheService cacheService, ICommonErrorHandler errorHandler, IStateService stateService, INavigationService navigationService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator)
            : base(cacheService, errorHandler, stateService, navigationService, mtProtoService, eventAggregator)
        {
            InviteLink = AppResources.Loading;

            _newChannel = StateService.NewChannel;
            StateService.NewChannel = null;

            PropertyChanged += (sender, args) =>
            {
                if (Property.NameEquals(args.PropertyName, () => IsPublic))
                {
                    if (!IsPublic && Invite == null)
                    {
                        ExportInvite();
                    }
                }
            };
        }

        protected override void OnActivate()
        {
            base.OnActivate();

            if (StateService.RemoveBackEntry)
            {
                StateService.RemoveBackEntry = false;
                NavigationService.RemoveBackEntry();
            }
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
            var username = UserName;
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

            MTProtoService.CheckUsernameAsync(_newChannel.ToInputChannel(), new TLString(username),
                result => Execute.BeginOnUIThread(() =>
                {
                    HasError = !result.Value;
                    if (HasError)
                    {
                        Error = AppResources.UsernameOccupied;
                    }
                }),
                error => Execute.BeginOnUIThread(() =>
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
                        messageBuilder.AppendLine("Method: channels.checkUsername");
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
                        else if (TLRPCError.TypeEquals(error, ErrorType.CHANNELS_ADMIN_PUBLIC_TOO_MUCH))
                        {
                            Error = AppResources.ChannelsAdminPublicTooMuchShort;
                            MessageBox.Show(AppResources.ChannelsAdminPublicTooMuch, AppResources.Error, MessageBoxButton.OK);
                        }
                        else 
                        {
                            Error = error.ToString();
                        }
                    }
                    else
                    {
                        Error = string.Empty;
                        Execute.ShowDebugMessage("account.checkUsername error " + error);
                    }
                }));
        }

        private void ExportInvite()
        {
            if (IsWorking) return;
            if (Invite != null) return;

            IsWorking = true;
            MTProtoService.ExportInviteAsync(_newChannel.ToInputChannel(),
                result => Execute.BeginOnUIThread(() =>
                {
                    IsWorking = false;
                    Invite = result;
                    var inviteExported = Invite as TLChatInviteExported;
                    if (inviteExported != null)
                    {
                        if (!TLString.IsNullOrEmpty(inviteExported.Link))
                        {
                            InviteLink = inviteExported.Link.ToString();
                        }
                    }
                }),
                error => Execute.BeginOnUIThread(() =>
                {
                    IsWorking = false;
                    Execute.ShowDebugMessage("channels.exportInvite error " + error);
                }));
        }

        public void CopyInvite()
        {
            var inviteExported = Invite as TLChatInviteExported;
            if (inviteExported != null)
            {
                if (!TLString.IsNullOrEmpty(inviteExported.Link))
                {
                    Clipboard.SetText(inviteExported.Link.ToString());
                }
            }
        }

        public event EventHandler EmptyUserName;

        protected virtual void RaiseEmptyUserName()
        {
            var handler = EmptyUserName;
            if (handler != null) handler(this, System.EventArgs.Empty);
        }

        public void Done()
        {
            if (IsWorking) return;

            var username = UserName;
            if (username != null
                && username.StartsWith("@"))
            {
                username = username.Substring(1, username.Length - 1);
            }

            IsWorking = true;
            MTProtoService.UpdateUsernameAsync(_newChannel.ToInputChannel(), new TLString(username),
                user => Execute.BeginOnUIThread(() =>
                {
                    //CacheService.SyncUser(user, result => EventAggregator.Publish(new UserNameChangedEventArgs(result)));

                    IsWorking = false;

                    _newChannel.UserName = new TLString(UserName);
                    StateService.NewChannel = _newChannel;
                    StateService.RemoveBackEntry = true;
                    NavigationService.UriFor<CreateChannelStep3ViewModel>().Navigate();
                }),
                error => Execute.BeginOnUIThread(() =>
                {
                    IsWorking = false;
                    if (TLRPCError.CodeEquals(error, ErrorCode.FLOOD))
                    {
                        HasError = true;
                        Error = AppResources.FloodWaitString;
                        MessageBox.Show(AppResources.FloodWaitString, AppResources.Error, MessageBoxButton.OK);
                    }
                    else if (TLRPCError.CodeEquals(error, ErrorCode.INTERNAL))
                    {
                        var messageBuilder = new StringBuilder();
                        messageBuilder.AppendLine(AppResources.ServerErrorMessage);
                        messageBuilder.AppendLine();
                        messageBuilder.AppendLine("Method: channels.updateUsername");
                        messageBuilder.AppendLine("Result: " + error);

                        HasError = true;
                        Error = AppResources.ServerError;
                        MessageBox.Show(messageBuilder.ToString(), AppResources.ServerError, MessageBoxButton.OK);
                    }
                    else if (TLRPCError.CodeEquals(error, ErrorCode.BAD_REQUEST))
                    {
                        if (TLRPCError.TypeEquals(error, ErrorType.USERNAME_INVALID))
                        {
                            HasError = true;
                            Error = AppResources.UsernameInvalid;
                            MessageBox.Show(AppResources.UsernameInvalid, AppResources.Error, MessageBoxButton.OK);
                        }
                        else if (TLRPCError.TypeEquals(error, ErrorType.USERNAME_OCCUPIED))
                        {
                            HasError = true;
                            Error = AppResources.UsernameOccupied;
                            MessageBox.Show(AppResources.UsernameOccupied, AppResources.Error, MessageBoxButton.OK);
                        }
                        else if (TLRPCError.TypeEquals(error, ErrorType.CHANNELS_ADMIN_PUBLIC_TOO_MUCH))
                        {
                            HasError = true;
                            Error = AppResources.ChannelsAdminPublicTooMuchShort;
                            MessageBox.Show(AppResources.ChannelsAdminPublicTooMuch, AppResources.Error, MessageBoxButton.OK);
                        }
                        else
                        {
                            HasError = true;
                            Error = error.ToString();
                        }
                    }
                    else
                    {
                        HasError = true;
                        Error = string.Empty;
                        Execute.ShowDebugMessage("channels.updateUsername error " + error);
                    }
                }));
        }

        public void Next()
        {
            if (IsPublic)
            {
                if (string.IsNullOrEmpty(UserName))
                {
                    MessageBox.Show(AppResources.ChoosePublicChannelLinkNotification);

                    RaiseEmptyUserName();

                    return;
                }



#if LAYER_40
                Done();
#else
                _newChannel.UserName = new TLString(UserName);
                StateService.NewChannel = _newChannel;
                NavigationService.UriFor<CreateChannelStep3ViewModel>().Navigate();
#endif
                return;
            }

            StateService.NewChannel = _newChannel;
            NavigationService.UriFor<CreateChannelStep3ViewModel>().Navigate();
        }
    }
}
