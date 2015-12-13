using System.Threading;
using System.Windows;

namespace TelegramClient.Views.Auth
{
    public partial class SignUpView
    {
        public SignUpView()
        {
            InitializeComponent();

            Loaded += (sender, args) => ThreadPool.QueueUserWorkItem(state =>
            {
                Thread.Sleep(300);
                Deployment.Current.Dispatcher.BeginInvoke(() => FirstName.Focus());
            });
        }
    }
}