using System.Windows;
using Caliburn.Micro;
using Microsoft.Phone.Controls;
using Telegram.Api.TL;
using TelegramClient.Services;
using TelegramClient.ViewModels.Chats;
using GestureEventArgs = System.Windows.Input.GestureEventArgs;

namespace TelegramClient.Views.Chats
{
    public partial class ChannelMembersView
    {
        public ChannelMembersViewModel ViewModel
        {
            get { return DataContext as ChannelMembersViewModel; }
        }

        public ChannelMembersView()
        {
            InitializeComponent();
        }

        private void MainItemGrid_OnTap(object sender, GestureEventArgs e)
        {
            
        }

        private void TelegramNavigationTransition_OnEndTransition(object sender, RoutedEventArgs e)
        {
            ViewModel.ForwardInAnimationComplete();
        }

        private void ContextMenu_OnLoaded(object sender, RoutedEventArgs e)
        {
            var contextMenu = sender as ContextMenu;
            if (contextMenu == null) return;

            var user = contextMenu.DataContext as TLUserBase;
            if (user == null) return;

            contextMenu.Visibility = user.Index == IoC.Get<IStateService>().CurrentUserId
                ? Visibility.Collapsed
                : Visibility.Visible;
        }
    }
}