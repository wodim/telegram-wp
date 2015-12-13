using System.Windows.Input;
using TelegramClient.ViewModels.Additional;

namespace TelegramClient.Views.Additional
{
    public partial class EnterPasswordView
    {
        public EnterPasswordViewModel ViewModel
        {
            get { return DataContext as EnterPasswordViewModel; }
        }

        public EnterPasswordView()
        {
            InitializeComponent();

            Loaded += (sender, args) => PasscodeBox.Focus();
        }

        private void Passcode_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ViewModel.Done();
            }
        }
    }
}