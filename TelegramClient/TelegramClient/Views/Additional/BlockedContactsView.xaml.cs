using System;
using System.Windows;
using Caliburn.Micro;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using TelegramClient.Resources;
using TelegramClient.ViewModels.Additional;
using GestureEventArgs = System.Windows.Input.GestureEventArgs;

namespace TelegramClient.Views.Additional
{
    public partial class BlockedContactsView
    {

        private readonly AppBarButton _addContactButton = new AppBarButton
        {
            Text = AppResources.Add,
            IconUri = new Uri("/Images/ApplicationBar/appbar.add.rest.png", UriKind.Relative)
        };

        public BlockedContactsView()
        {
            InitializeComponent();

            //AnimationContext = LayoutRoot;

            _addContactButton.Click += (sender, args) => ((BlockedContactsViewModel) DataContext).AddContact();
            Loaded += (sender, args) =>
            {
                BuildLocalizedAppBar();
            };
        }

        private void BuildLocalizedAppBar()
        {
            if (ApplicationBar != null) return;

            ApplicationBar = new ApplicationBar();
            ApplicationBar.Buttons.Add(_addContactButton);
        }

        private void MainItemGrid_OnTap(object sender, GestureEventArgs e)
        {
            //ContextMenuService.GetContextMenu((DependencyObject)sender).IsOpen = true;       
        }
    }
}