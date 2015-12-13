using System;
using Caliburn.Micro;
using Telegram.Api.Extensions;
using Telegram.Api.TL;

namespace TelegramClient.ViewModels.Media
{
    public class ImageEditorViewModel : PropertyChangedBase
    {
        public Action<TLMessage25> ContinueAction { get; set; }

        public string Caption
        {
            get
            {
                var message = _currentItem as TLMessage;
                if (message != null)
                {
                    var media = message.Media as TLMessageMediaPhoto28;
                    if (media != null)
                    {
                        return media.Caption.ToString();
                    }
                }

                return null;
            }
            set
            {
                var message = _currentItem as TLMessage;
                if (message != null)
                {
                    var media = message.Media as TLMessageMediaPhoto28;
                    if (media != null)
                    {
                        media.Caption = new TLString(value);
                    }
                }
            }
        }

        private TLMessage25 _currentItem;

        public TLMessage25 CurrentItem
        {
            get { return _currentItem; }
            set
            {
                if (_currentItem != value)
                {
                    _currentItem = value;
                    NotifyOfPropertyChange(() => CurrentItem);
                    NotifyOfPropertyChange(() => Caption);
                }
            }
        }

        private bool _isOpen;

        public bool IsOpen { get { return _isOpen; } }

        public void Done()
        {
            _isOpen = false;
            NotifyOfPropertyChange(() => IsOpen);

            ContinueAction.SafeInvoke(_currentItem);
        }

        public void Cancel()
        {
            CloseEditor();    
        }

        public void OpenEditor()
        {
            _isOpen = CurrentItem != null;
            NotifyOfPropertyChange(() => IsOpen);
        }

        public void CloseEditor()
        {
            _isOpen = false;
            NotifyOfPropertyChange(() => IsOpen);

            _currentItem = null;
        }
    }
}
