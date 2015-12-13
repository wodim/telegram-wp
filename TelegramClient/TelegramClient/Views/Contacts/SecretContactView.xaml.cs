using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using Microsoft.Phone.Shell;
using TelegramClient.Helpers;
using TelegramClient.ViewModels.Contacts;
using TelegramClient.Views.Media;

namespace TelegramClient.Views.Contacts
{
    public partial class SecretContactView
    {
        private IApplicationBar _prevAppBar;

        public SecretContactViewModel ViewModel
        {
            get { return DataContext as SecretContactViewModel; }
        }

        public SecretContactView()
        {
            var timer = Stopwatch.StartNew();

            InitializeComponent();

            Loaded += (sender, args) =>
            {
                TimerString.Text = timer.Elapsed.ToString();

                ViewModel.PropertyChanged += OnViewModelPropertyChanged;
            };
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (Property.NameEquals(e.PropertyName, () => ViewModel.ImageViewer)
                && ViewModel.ImageViewer != null)
            {
                ViewModel.ImageViewer.PropertyChanged += OnImageViewerPropertyChanged;
            }
        }

        private void OnImageViewerPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (Property.NameEquals(e.PropertyName, () => ViewModel.ImageViewer.IsOpen))
            {
                Items.IsHitTestVisible = !ViewModel.ImageViewer.IsOpen;
            }
        }

        protected override void OnBackKeyPress(CancelEventArgs e)
        {
            if (ViewModel.ImageViewer != null && ViewModel.ImageViewer.IsOpen)
            {
                ViewModel.ImageViewer.CloseViewer();
                e.Cancel = true;
                return;
            }

            base.OnBackKeyPress(e);
        }

        private void NavigationTransition_OnEndTransition(object sender, RoutedEventArgs e)
        {
            
        }
    }
}