using System;
using Caliburn.Micro;
using Microsoft.Phone.Shell;
using TelegramClient.Resources;
using TelegramClient.ViewModels.Additional;

namespace TelegramClient.Views.Additional
{
    public partial class SnapshotsView
    {
        public SnapshotsViewModel ViewModel
        {
            get { return DataContext as SnapshotsViewModel; }
        }

        private readonly AppBarButton _createSnapshot = new AppBarButton
        {
            Text = AppResources.Add,
            IconUri = new Uri("/Images/ApplicationBar/appbar.add.rest.png", UriKind.Relative)
        };

        public SnapshotsView()
        {
            InitializeComponent();

            _createSnapshot.Click += (sender, args) => ViewModel.Create();

            Loaded += (sender, args) =>
            {
                BuildLocalizedAppBar();
            };
        }

        private void BuildLocalizedAppBar()
        {
            if (ApplicationBar != null) return;

            ApplicationBar = new ApplicationBar();
            ApplicationBar.Buttons.Add(_createSnapshot);
        }
    }
}