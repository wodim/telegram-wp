using System;
using System.Windows;
using Caliburn.Micro;
using Microsoft.Phone.Shell;
using TelegramClient.Resources;
using TelegramClient.ViewModels.Additional;

namespace TelegramClient.Views.Additional
{
    public partial class AllowUsersView
    {
        private readonly AppBarButton _addButton = new AppBarButton
        {
            Text = AppResources.Add,
            IconUri = new Uri("/Images/ApplicationBar/appbar.add.rest.png", UriKind.Relative)
        };

        public AllowUsersViewModel ViewModel
        {
            get { return DataContext as AllowUsersViewModel; }
        }

        public AllowUsersView()
        {
            InitializeComponent();

            _addButton.Click += (sender, args) => ViewModel.SelectUsers();

            Loaded += (sender, args) => BuildLocalizedAppBar();
        }

        private void BuildLocalizedAppBar()
        {
            if (ApplicationBar != null) return;

            ApplicationBar = new ApplicationBar();
            ApplicationBar.Buttons.Add(_addButton);
        }
    }
}