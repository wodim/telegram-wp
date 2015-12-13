using System.ComponentModel;
using System.Windows;
using Microsoft.Phone.Controls;
using TelegramClient.Helpers;
using TelegramClient.ViewModels.Additional;
using GestureEventArgs = System.Windows.Input.GestureEventArgs;

namespace TelegramClient.Views.Additional
{
    public partial class ChooseAttachmentView
    {
        public ChooseAttachmentViewModel ViewModel
        {
            get { return DataContext as ChooseAttachmentViewModel; }
        }

        public ChooseAttachmentView()
        {
            InitializeComponent();

            Loaded += (o, e) => ViewModel.PropertyChanged += OnViewModelPropertyChanged;
            Unloaded += (o, e) => ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (Property.NameEquals(e.PropertyName, () => ViewModel.IsOpen))
            {
                var frame = Bootstrapper.PhoneFrame;
                if (frame != null)
                {
                    var currentPage = frame.Content as PhoneApplicationPage;
                    if (currentPage != null && currentPage.ApplicationBar != null)
                    {
                        currentPage.ApplicationBar.IsVisible = !ViewModel.IsOpen;
                    }
                }

                if (ViewModel.IsOpen)
                {
                    OpenContactItem.Visibility = ViewModel.OpenContactVisibility;
                    OpenStoryboard.Begin();
                }
                else
                {
                    CloseStoryboard.Begin();
                }
            }
        }

        private void LayoutRoot_OnTap(object sender, GestureEventArgs e)
        {
            ((ChooseAttachmentViewModel)DataContext).Close();
        }
    }
}