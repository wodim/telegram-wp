using System;
using System.Windows;
using Caliburn.Micro;
using Telegram.Api.Extensions;
using Telegram.Api.TL;
using TelegramClient.Resources;

namespace TelegramClient.ViewModels.Additional
{
    public class AddChatParticipantConfirmationViewModel : PropertyChangedBase
    {
        private int _forwardingMessagesCount = Constants.DefaultForwardingMessagesCount;

        public int ForwardingMessagesCount
        {
            get { return _forwardingMessagesCount; }
            set
            {
                _forwardingMessagesCount = value;
                NotifyOfPropertyChange(() => ForwardingMessagesCount);
            }
        }

        private bool _isOpen;

        public bool IsOpen
        {
            get { return _isOpen; }
            protected set
            {
                if (_isOpen != value)
                {
                    _isOpen = value;
                    NotifyOfPropertyChange(() => IsOpen);
                }
            }
        }

        private Action<AddChatParticipantBoxResult> _callback;

        public string AddUserToTheGroupString { get; set; }

        public void Open(TLUserBase user, TLChatBase chat, Action<AddChatParticipantBoxResult> callback)
        {
            var userName = user.FirstName;
            if (TLString.IsNullOrEmpty(userName))
            {
                userName = user.LastName;
            }

            AddUserToTheGroupString = string.Format(AppResources.AddUserToTheGroup, userName, chat.FullName);
            NotifyOfPropertyChange(() => AddUserToTheGroupString);

            IsOpen = true;
            _callback = callback;
        }

        public void Close(MessageBoxResult result)
        {
            IsOpen = false;
            _callback.SafeInvoke(new AddChatParticipantBoxResult{ ForwardingMessagesCount = ForwardingMessagesCount, Result = result });
        }
    }

    public class AddChatParticipantBoxResult
    {
        public int ForwardingMessagesCount { get; set; }

        public MessageBoxResult Result { get; set; }
    }
}
