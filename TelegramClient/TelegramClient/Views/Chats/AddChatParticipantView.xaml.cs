using System;
using Microsoft.Phone.Shell;
using TelegramClient.Resources;
using TelegramClient.ViewModels.Chats;

namespace TelegramClient.Views.Chats
{
    public partial class AddChatParticipantView
    {
        public AddChatParticipantViewModel ViewModel
        {
            get { return DataContext as AddChatParticipantViewModel; }
        }

        private readonly ApplicationBarIconButton _searchButton = new ApplicationBarIconButton
        {
            Text = AppResources.Search,
            IconUri = new Uri("/Images/ApplicationBar/appbar.feature.search.rest.png", UriKind.Relative)
        };

        public AddChatParticipantView()
        {
            InitializeComponent();

            _searchButton.Click += (sender, args) => ViewModel.Search();

            Loaded += (o, e) => BuildLocalizedAppBar();
        }

        private bool _initialized;

        private void BuildLocalizedAppBar()
        {
            if (_initialized) return;

            _initialized = true;

            ApplicationBar = new ApplicationBar();
            ApplicationBar.Buttons.Add(_searchButton);
        }
    }
}