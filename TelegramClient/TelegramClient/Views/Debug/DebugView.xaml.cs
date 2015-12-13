using System.Windows;
using TelegramClient.ViewModels;

namespace TelegramClient.Views
{
    public partial class DebugView
    {
        public DebugView()
        {
            InitializeComponent();
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            ((DebugViewModel)DataContext).Send();
        }

        private void ButtonClear_OnClick(object sender, RoutedEventArgs e)
        {
            ((DebugViewModel)DataContext).Clear();
        }

        private void ButtonDown_OnClick(object sender, RoutedEventArgs e)
        {
            //ScrollViewer.ScrollToVerticalOffset(ScrollViewer.ScrollableHeight);
        }

        private void ButtonUp_OnClick(object sender, RoutedEventArgs e)
        {
           // ScrollViewer.ScrollToVerticalOffset(0.0);
        }
    }
}