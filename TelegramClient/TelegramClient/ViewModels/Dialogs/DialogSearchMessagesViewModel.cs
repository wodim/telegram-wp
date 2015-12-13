using System;
using System.Windows;
using Caliburn.Micro;
using Telegram.Api.Extensions;
using TelegramClient.Helpers;

namespace TelegramClient.ViewModels.Dialogs
{
    public class DialogSearchMessagesViewModel : PropertyChangedBase
    {
        public string NavigationButtonImageSource
        {
            get
            {
                var isLightTheme = (Visibility)Application.Current.Resources["PhoneLightThemeVisibility"] == Visibility.Visible;
                return isLightTheme ? "/Images/ApplicationBar/appbar.next.light.png" : "/Images/ApplicationBar/appbar.next.png";
            }
        }

        private bool _isSearchUpEnabled;

        public bool IsSearchUpEnabled
        {
            get { return _isSearchUpEnabled; }
            set
            {
                if (_isSearchUpEnabled != value)
                {
                    _isSearchUpEnabled = value;
                    NotifyOfPropertyChange(() => IsSearchUpEnabled);
                }
            }
        }

        private bool _isSearchDownEnabled;

        public bool IsSearchDownEnabled
        {
            get { return _isSearchDownEnabled; }
            set
            {
                if (_isSearchDownEnabled != value)
                {
                    _isSearchDownEnabled = value;
                    NotifyOfPropertyChange(() => IsSearchDownEnabled);
                }
            }
        }

        private string _text;

        public string Text
        {
            get { return _text; }
            set
            {
                if (_text != value)
                {
                    _text = value;
                    NotifyOfPropertyChange(() => Text);
                }
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

        private readonly Action<string> _searchAction;

        private readonly System.Action _upAction;

        private readonly System.Action _downAction;

        public void ResultLoaded(int current, int count)
        {
            IsSearchUpEnabled = count > 0 && current < count - 1;
            IsSearchDownEnabled = count > 0 && current > 0;
        } 

        public DialogSearchMessagesViewModel(Action<string> searchAction, System.Action upAction, System.Action downAction)
        {
            _searchAction = searchAction;
            _upAction = upAction;
            _downAction = downAction;

            PropertyChanged += (sender, args) =>
            {
                if (Property.NameEquals(args.PropertyName, () => Text))
                {
                    Search();
                }
            };
        }

        public void Open()
        {
            IsOpen = true;
        }

        public void Close()
        {
            IsOpen = false;
        }

        public void Search()
        {
            _searchAction.SafeInvoke(Text);
        }

        public void Up()
        {
            _upAction.SafeInvoke();
        }

        public void Down()
        {
            _downAction.SafeInvoke();
        }
    }
}
