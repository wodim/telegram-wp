using System;
using System.Threading;
using System.Windows;
using Caliburn.Micro;
using Microsoft.Phone.Shell;
using TelegramClient.Resources;
using TelegramClient.ViewModels.Contacts;

namespace TelegramClient.Views.Contacts
{
    public partial class EditContactView
    {
        private readonly AppBarButton _saveButton = new AppBarButton
        {
            Text = AppResources.Save,
            IconUri = new Uri("/Images/ApplicationBar/appbar.save.png", UriKind.Relative)
        };

        private readonly AppBarButton _cancelButton = new AppBarButton
        {
            Text = AppResources.Cancel,
            IconUri = new Uri("/Images/ApplicationBar/appbar.cancel.rest.png", UriKind.Relative)
        };

        public EditContactView()
        {
            InitializeComponent();

            _saveButton.Click += (sender, args) => ((EditContactViewModel) DataContext).Done();
            _cancelButton.Click += (sender, args) => ((EditContactViewModel)DataContext).Cancel();

            Loaded += (sender, args) =>
            {
                BuildLocalizedAppBar();
                ThreadPool.QueueUserWorkItem(state =>
                {
                    Thread.Sleep(300);
                    Deployment.Current.Dispatcher.BeginInvoke(() =>
                    {
                        FirstName.Focus();
                        FirstName.SelectionStart = FirstName.Text.Length;
                        FirstName.SelectionLength = 0;
                    });
                });
            };
        }


        private void BuildLocalizedAppBar()
        {
            if (ApplicationBar != null) return;

            ApplicationBar = new ApplicationBar();
            ApplicationBar.Buttons.Add(_saveButton);
            ApplicationBar.Buttons.Add(_cancelButton);
        }
    }
}