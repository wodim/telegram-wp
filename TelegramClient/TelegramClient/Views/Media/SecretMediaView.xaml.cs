using System.Threading;
using System.Windows;

namespace TelegramClient.Views.Media
{
    public partial class SecretMediaView
    {
        public SecretMediaView()
        {
            InitializeComponent();

            Loaded += (sender, args) =>
            {
                ThreadPool.QueueUserWorkItem(state =>
                {
                    Thread.Sleep(500);
                    Deployment.Current.Dispatcher.BeginInvoke(() =>
                    {

                        Items.Visibility = Visibility.Visible;
                    });
                });
            };
        }
    }
}