using System.Windows;
using TelegramClient.ViewModels.Debug;

namespace TelegramClient.Views.Debug
{
    public partial class LongPollView
    {
        public LongPollView()
        {
            InitializeComponent();
        }

        private void ButtonClear_OnClick(object sender, RoutedEventArgs e)
        {
            //((LongPollViewModel)DataContext).Clear();
        }

        //private void ButtonUp_OnClick(object sender, RoutedEventArgs e)
        //{
            
        //}

        private void ButtonDown_OnClick(object sender, RoutedEventArgs e)
        {
            //ScrollViewer.ScrollToVerticalOffset(ScrollViewer.ScrollableHeight);
        }
    }
}
