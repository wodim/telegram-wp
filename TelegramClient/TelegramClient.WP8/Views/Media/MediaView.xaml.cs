using System.Windows;
using System.Windows.Input;
using TelegramClient.ViewModels.Media;

namespace TelegramClient.Views.Media
{
    public partial class MediaView
    {
        public MediaView()
        {
            InitializeComponent();

            // FullHD
            OptimizeFullHD();
        }

        private void OptimizeFullHD()
        {
            var isFullHD = Application.Current.Host.Content.ScaleFactor == 225;
            if (!isFullHD) return;

            //BottomAppBarPlaceholder.Height = new GridLength(Constants.FullHDAppBarHeight);
        }

        private void Items_OnCloseToEnd(object sender, System.EventArgs e)
        {
            ((ISliceLoadable)DataContext).LoadNextSlice();
        }

        private void Files_OnManipulationStarted(object sender, ManipulationStartedEventArgs e)
        {
            ((ISliceLoadable)DataContext).LoadNextSlice();
        }
    }
}