using System.Linq;
using System.Windows;
using System.Windows.Input;
using TelegramClient.ViewModels.Contacts;

namespace TelegramClient.Views.Contacts
{
    public partial class ContactDetailsView
    {
        public ContactDetailsViewModel ViewModel
        {
            get { return DataContext as ContactDetailsViewModel; }
        }

        public ContactDetailsView()
        {
            InitializeComponent();
        }
#if DEBUG
        //~ContactDetailsView()
        //{
        //    TLUtils.WritePerformance("++ContactDetailsV destr");
        //}
#endif

        private void UIElement_OnTap(object sender, GestureEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element == null) return;

            var span = element.DataContext as TimerSpan;
            if (span == null) return;

            ViewModel.SelectSpan(span);
        }

        private void ToggleSwitch_OnChecked(object sender, RoutedEventArgs e)
        {
            ViewModel.SelectSpan(ViewModel.Spans.First());
        }

        private void ToggleSwitch_OnUnchecked(object sender, RoutedEventArgs e)
        {
            ViewModel.SelectSpan(ViewModel.Spans.Last());
        }
    }
}