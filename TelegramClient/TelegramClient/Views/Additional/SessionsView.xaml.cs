using System.Windows;
using Microsoft.Phone.Controls;
using GestureEventArgs = System.Windows.Input.GestureEventArgs;

namespace TelegramClient.Views.Additional
{
    public partial class SessionsView
    {
        public SessionsView()
        {
            InitializeComponent();
        }

        private void MainItemGrid_OnTap(object sender, GestureEventArgs e)
        {
            ContextMenuService.GetContextMenu((DependencyObject)sender).IsOpen = true;       
        }
    }
}