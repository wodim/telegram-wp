using System;
using System.Windows.Interop;
using System.Windows.Navigation;
using Caliburn.Micro;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using TelegramClient.Controls;
using TelegramClient.Resources;
using TelegramClient.ViewModels.Additional;
using Execute = Telegram.Api.Helpers.Execute;

namespace TelegramClient.Views.Additional
{
    public partial class SettingsView
    {
        public SettingsViewModel ViewModel
        {
            get { return DataContext as SettingsViewModel; }
        }

        private readonly AppBarButton _editButton = new AppBarButton
        {
            Text = AppResources.Edit,
            IconUri = new Uri("/Images/ApplicationBar/appbar.edit.png", UriKind.Relative)
        };

        public SettingsView()
        {
            InitializeComponent();

            _editButton.Click += (sender, args) => ViewModel.EditProfile();

            Loaded += (sender, args) =>
            {
                RunAnimation();
                BuildLocalizedAppBar();
            };
        }

        private bool _isForwardInAnimation;

        private void RunAnimation()
        {
            if (_isForwardInAnimation)
            {
                _isForwardInAnimation = false;
                var forwardInAnimation = TelegramTurnstileAnimations.GetAnimation(LayoutRoot, TurnstileTransitionMode.ForwardIn);
                Execute.BeginOnUIThread(forwardInAnimation.Begin);
            }
            else
            {
                LayoutRoot.Opacity = 1.0;
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.NavigationMode == NavigationMode.New)
            {
                LayoutRoot.Opacity = 0.0;
                _isForwardInAnimation = true;
            }

            base.OnNavigatedTo(e);
        }

        private void BuildLocalizedAppBar()
        {
            if (ApplicationBar == null)
            {
                ApplicationBar = new ApplicationBar{Opacity = 0.99};
                ApplicationBar.Buttons.Add(_editButton);
            }

        }
    }
}