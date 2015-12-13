using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Telegram.Api.TL;
using Telegram.Controls.Extensions;

namespace TelegramClient.Views.Contacts
{
    public partial class ContactsView
    {
        public ContactsView()
        {
            InitializeComponent();
        }

        public FrameworkElement TapedItem;

        private void MainItemGrid_OnTap(object sender, GestureEventArgs e)
        {
            TapedItem = (FrameworkElement)sender;

            if (!(TapedItem.DataContext is TLUserContact)) return;

            if (!(TapedItem.RenderTransform is CompositeTransform))
            {
                TapedItem.RenderTransform = new CompositeTransform();
            }

            var listBoxItem = TapedItem.FindParentOfType<ListBoxItem>();

            ShellView.StartContinuumForwardOutAnimation(TapedItem, listBoxItem);

            Loaded += (o, args) =>
            {

            };
        }
    }
}