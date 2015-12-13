using System.Diagnostics;
using System.Linq;
using System.Windows;
using Microsoft.Phone.Controls;
using TelegramClient.ViewModels.Chats;
using TelegramClient.ViewModels.Contacts;
using GestureEventArgs = System.Windows.Input.GestureEventArgs;

namespace TelegramClient.Views.Chats
{
    public partial class ChatDetailsView
    {
        public ChatDetailsViewModel ViewModel
        {
            get { return DataContext as ChatDetailsViewModel; }
        }

        public static readonly DependencyProperty TimerProperty = DependencyProperty.Register(
            "Timer", typeof (string), typeof (ChatDetailsView), new PropertyMetadata(default(string)));

        public string Timer
        {
            get { return (string) GetValue(TimerProperty); }
            set { SetValue(TimerProperty, value); }
        }

        public ChatDetailsView()
        {
            var timer = Stopwatch.StartNew();
            InitializeComponent();

            Loaded += (sender, args) =>
            {
                Timer = timer.Elapsed.ToString();
            };
        }
        
#if DEBUG
        //~ChatDetailsView()
        //{
        //    TLUtils.WritePerformance("++ChatDetailsV destr");
        //}
#endif

        private void MainItemGrid_OnTap(object sender, GestureEventArgs e)
        {
            
            //ContextMenuService.GetContextMenu((DependencyObject)sender).IsOpen = true;
        }

        private void UIElement_OnTap(object sender, GestureEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element == null) return;

            var span = element.DataContext as TimerSpan;
            if (span == null) return;

            ViewModel.SelectSpan(span);
        }

        private void CopyLink_OnClick(object sender, RoutedEventArgs e)
        {
            ViewModel.CopyLink();
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