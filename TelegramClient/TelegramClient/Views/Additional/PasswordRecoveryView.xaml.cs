using System.Windows;
using System.Windows.Input;
using TelegramClient.ViewModels.Additional;

namespace TelegramClient.Views.Additional
{
    public partial class PasswordRecoveryView
    {
        public PasswordRecoveryViewModel ViewModel
        {
            get { return DataContext as PasswordRecoveryViewModel; }
        }

        public PasswordRecoveryView()
        {
            InitializeComponent();

            Loaded += (sender, args) => Code.Focus();
        }

        private void Text_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ViewModel.RecoverPassword();
            }
            else if (e.Key >= Key.D0 && e.Key <= Key.D9)
            {
                
            }
            else if (e.Key == Key.Back)
            {

            }
            else
            {
                e.Handled = true;
            }
        }

        private void Hyperlink_OnClick(object sender, RoutedEventArgs e)
        {
            ViewModel.ForgotPassword();
        }
    }
}