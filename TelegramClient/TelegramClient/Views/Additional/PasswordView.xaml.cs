using TelegramClient.ViewModels.Additional;

namespace TelegramClient.Views.Additional
{
    public partial class PasswordView
    {
        public PasswordViewModel ViewModel
        {
            get { return DataContext as PasswordViewModel; }
        }

        public PasswordView()
        {
            InitializeComponent();
        }
    }
}