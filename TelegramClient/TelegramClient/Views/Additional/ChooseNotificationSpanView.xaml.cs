using System;
using Caliburn.Micro;
using Microsoft.Phone.Shell;
using TelegramClient.Resources;
using TelegramClient.ViewModels.Additional;

namespace TelegramClient.Views.Additional
{
    public partial class ChooseNotificationSpanView
    {
        private readonly AppBarButton _doneButton = new AppBarButton
            {
                Text = AppResources.Done,
                IconUri = new Uri("/Images/ApplicationBar/appbar.check.png", UriKind.Relative)
            };

            private readonly AppBarButton _cancelButton = new AppBarButton
            {
                Text = AppResources.Cancel,
                IconUri = new Uri("/Images/ApplicationBar/appbar.cancel.rest.png", UriKind.Relative)
            };

            public ChooseNotificationSpanViewModel ViewModel
            {
                get { return DataContext as ChooseNotificationSpanViewModel; }
            }

            public ChooseNotificationSpanView()
            {
                InitializeComponent();

                _doneButton.Click += (sender, args) => ViewModel.Done();
                _cancelButton.Click += (sender, args) => ViewModel.Cancel();

                BuildLocalizedAppBar();
            }

            private void BuildLocalizedAppBar()
            {
                if (ApplicationBar != null) return;

                ApplicationBar = new ApplicationBar();
                ApplicationBar.Buttons.Add(_doneButton);
                ApplicationBar.Buttons.Add(_cancelButton);
            }
    }
}