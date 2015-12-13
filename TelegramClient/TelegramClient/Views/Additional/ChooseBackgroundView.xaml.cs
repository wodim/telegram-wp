using System;
using System.Windows;
using TelegramClient.ViewModels.Additional;

namespace TelegramClient.Views.Additional
{
    public partial class ChooseBackgroundView
    {
        public ChooseBackgroundViewModel ViewModel
        {
            get { return DataContext as ChooseBackgroundViewModel; }
        }

        public ChooseBackgroundView()
        {
            InitializeComponent();
        }

        private void TelegramNavigationTransition_OnEndTransition(object sender, RoutedEventArgs e)
        {
            ViewModel.OnForwardInAnimationComplete();
        }
    }
}