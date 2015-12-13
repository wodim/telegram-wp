using System.Windows;
using System.Windows.Input;
using Microsoft.Phone.Shell;
using Telegram.EmojiPanel.Controls.Emoji;
using TelegramClient.ViewModels.Additional;

namespace TelegramClient.Views.Additional
{
    public partial class ChangePasswordEmailView
    {
        public ChangePasswordEmailViewModel ViewModel
        {
            get { return DataContext as ChangePasswordEmailViewModel; }
        }

        public ChangePasswordEmailView()
        {
            InitializeComponent();

            Loaded += (sender, args) => RecoveryEmail.Focus();
        }

        private void Text_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ViewModel.ChangeRecoveryEmail();
            }
        }

        private ApplicationBar _appBar;

        private void RecoveryEmail_OnGotFocus(object sender, RoutedEventArgs e)
        {
            if (_appBar == null)
            {
                _appBar = new ApplicationBar();
            }

            SkipRecoveryEmailTransform.Y = - (EmojiControl.PortraitOrientationHeight - _appBar.DefaultSize);
        }

        private void RecoveryEmail_OnLostFocus(object sender, RoutedEventArgs e)
        {
            SkipRecoveryEmailTransform.Y = 0;
        }
    }
}