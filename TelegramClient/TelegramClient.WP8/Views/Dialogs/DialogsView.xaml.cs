using System.Windows;
using System.Windows.Controls;
using Microsoft.Phone.Controls;
using Telegram.Api.TL;
using Telegram.Controls.Extensions;
using TelegramClient.ViewModels.Dialogs;
using GestureEventArgs = System.Windows.Input.GestureEventArgs;

namespace TelegramClient.Views.Dialogs
{
    public partial class DialogsView
    {
        public DialogsViewModel ViewModel
        {
            get { return (DialogsViewModel) DataContext; }
        }

        public DialogsView()
        {
            InitializeComponent();
        }

        private void Items_OnCloseToEnd(object sender, System.EventArgs e)
        {
            ((DialogsViewModel)DataContext).LoadNextSlice();
        }

        public FrameworkElement TapedItem;

        private void MainItemGrid_OnTap(object sender, GestureEventArgs e)
        {
            TapedItem = (FrameworkElement) sender;

            var tapedItemContainer = TapedItem.FindParentOfType<ListBoxItem>();

            var result = ViewModel.OpenDialogDetails(TapedItem.DataContext as TLDialogBase);
            if (result)
            {
                ShellView.StartContinuumForwardOutAnimation(TapedItem, tapedItemContainer);
            }
        }

        private void DeleteAndStop_OnLoaded(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            if (menuItem == null) return;

            var dialog = menuItem.DataContext as TLDialogBase;
            if (dialog == null) return;

            var user = dialog.With as TLUser;

            menuItem.Visibility = user != null && user.IsBot && (user.Blocked == null || !user.Blocked.Value)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void ClearHistory_OnLoaded(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            if (menuItem == null) return;

            var dialog = menuItem.DataContext as TLDialogBase;
            if (dialog == null) return;

            var channel = dialog.Peer as TLPeerChannel;

            menuItem.Visibility = channel != null
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void PinToStart_OnLoaded(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            if (menuItem == null) return;

            var dialog = menuItem.DataContext as TLDialogBase;
            if (dialog == null) return;

            var channel = dialog.Peer as TLPeerChannel;

            menuItem.Visibility = channel != null
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void ContextMenu_OnLoaded(object sender, RoutedEventArgs e)
        {
            var contextMenu = sender as ContextMenu;
            if (contextMenu == null) return;

            var dialog = contextMenu.DataContext as TLDialogBase;
            if (dialog == null) return;

            var channel = dialog.Peer as TLPeerChannel;

            contextMenu.Visibility = channel != null
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void DeleteDialog_OnLoaded(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            if (menuItem == null) return;

            menuItem.Visibility = Visibility.Collapsed;

            var dialog = menuItem.DataContext as TLDialogBase;
            if (dialog == null) return;

            var peerChannel = dialog.Peer as TLPeerChannel;
            if (peerChannel != null)
            {
                var channel = dialog.With as TLChannel;
                if (channel != null && channel.Creator)
                {
                    menuItem.Visibility = Visibility.Visible;
                }
                return;
            }

            var peerUser = dialog.Peer as TLPeerUser;
            if (peerUser != null)
            {
                menuItem.Visibility = Visibility.Visible;
                return;
            }

            var peerChat = dialog.Peer as TLPeerChat;
            if (peerChat != null)
            {
                var isVisible = dialog.With is TLChatForbidden || dialog.With is TLChatEmpty;

                menuItem.Visibility = isVisible? Visibility.Visible : Visibility.Collapsed;
                return;
            }
        }

        private void DeleteAndExit_OnLoaded(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            if (menuItem == null) return;

            menuItem.Visibility = Visibility.Collapsed;

            var dialog = menuItem.DataContext as TLDialogBase;
            if (dialog == null) return;

            var peerChat = dialog.Peer as TLPeerChat;
            if (peerChat != null)
            {
                menuItem.Visibility = Visibility.Visible;
                return;
            }

            var peerEncryptedChat = dialog.Peer as TLPeerEncryptedChat;
            if (peerEncryptedChat != null)
            {
                menuItem.Visibility = Visibility.Visible;
                return;
            }

            var peerBroadcast = dialog.Peer as TLPeerBroadcast;
            if (peerBroadcast != null)
            {
                menuItem.Visibility = Visibility.Visible;
                return;
            }
        }
    }
}