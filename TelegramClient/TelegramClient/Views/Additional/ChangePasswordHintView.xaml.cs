using System.Windows.Input;
using TelegramClient.ViewModels.Additional;

namespace TelegramClient.Views.Additional
{
    public partial class ChangePasswordHintView 
    {
        public ChangePasswordHintViewModel ViewModel
        {
            get { return DataContext as ChangePasswordHintViewModel; }
        }

        public ChangePasswordHintView()
        {
            InitializeComponent();

            Loaded += (o, e) =>
            {
                PasswordHint.Focus();
                PasswordHint.SelectAll();
            };
        }

        private void Text_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ViewModel.ChangePasswordHint();
            }
        }
    }
}