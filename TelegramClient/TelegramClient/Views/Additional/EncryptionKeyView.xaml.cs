using System.Windows;
using TelegramClient.ViewModels.Additional;

namespace TelegramClient.Views.Additional
{
    public partial class EncryptionKeyView
    {
        public EncryptionKeyViewModel ViewModel
        {
            get { return DataContext as EncryptionKeyViewModel; }
        }

        public EncryptionKeyView()
        {
            //var timer = Stopwatch.StartNew();

            InitializeComponent();

            Loaded += (sender, args) =>
            {
                //TimerText.Text = timer.Elapsed.ToString();
            };
        }

        private void NavigationTransition_OnEndTransition(object sender, RoutedEventArgs e)
        {
            ViewModel.AnimationComplete();
        }
    }
}