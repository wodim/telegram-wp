using System;
using System.Windows;
using Caliburn.Micro;
using Telegram.Api.Extensions;

namespace TelegramClient.ViewModels.Additional
{
    public class AskQuestionConfirmationViewModel : PropertyChangedBase
    {
        public string TelegramFaq
        {
            get { return Constants.TelegramFaq; }
        }

        public string TelegramTroubleshooting
        {
            get { return Constants.TelegramTroubleshooting; }
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

        private Action<MessageBoxResult> _callback;

        public void Open(Action<MessageBoxResult> callback)
        {
            IsOpen = true;
            _callback = callback;
        }

        public void Close(MessageBoxResult result)
        {
            IsOpen = false;
            _callback.SafeInvoke(result);
        }
    }
}
