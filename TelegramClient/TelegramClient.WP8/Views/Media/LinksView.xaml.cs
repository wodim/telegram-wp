using System.Windows;
using System.Windows.Input;
using Microsoft.Phone.Controls;
using Telegram.Api.TL;
using Telegram.Api.TL.Interfaces;
using TelegramClient.ViewModels.Media;

namespace TelegramClient.Views.Media
{
    public partial class LinksView
    {
        public LinksViewModel<IInputPeer> ViewModel { get { return DataContext as LinksViewModel<IInputPeer>; } }

        public LinksView()
        {
            InitializeComponent();
        }

        private void Items_OnCloseToEnd(object sender, System.EventArgs e)
        {
            ((ISliceLoadable)DataContext).LoadNextSlice();
        }

        private void Files_OnManipulationStarted(object sender, ManipulationStartedEventArgs e)
        {
            ((ISliceLoadable)DataContext).LoadNextSlice();
        }

        private void DeleteMessage_OnLoaded(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            if (menuItem == null) return;

            var channel = ViewModel.CurrentItem as TLChannel;
            menuItem.Visibility = (channel == null || channel.Creator)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }
}