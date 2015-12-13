using System;
using System.Windows;
using Caliburn.Micro;
using Microsoft.Phone.Shell;
using TelegramClient.Resources;
using TelegramClient.ViewModels.Additional;

namespace TelegramClient.Views.Additional
{
    public partial class EditCurrentUserView
    {
        public static readonly DependencyProperty AppBarStateProperty =
            DependencyProperty.Register("AppBarState", typeof(string), typeof(EditCurrentUserView), new PropertyMetadata(OnAppBarStateChanged));

        private static void OnAppBarStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var state = (string)e.NewValue;

            var view = (EditCurrentUserView)d;

            if (state == "Working")
            {
                view._doneButton.IsEnabled = false;
                view._cancelButton.IsEnabled = false;
            }
            else
            {
                view._doneButton.IsEnabled = true;
                view._cancelButton.IsEnabled = true;
            }
        }

        public string AppBarState
        {
            get { return (string)GetValue(AppBarStateProperty); }
            set { SetValue(AppBarStateProperty, value); }
        }

        public EditCurrentUserViewModel ViewModel
        {
            get { return DataContext as EditCurrentUserViewModel; }
        }

        private readonly AppBarButton _doneButton = new AppBarButton
        {
            Text = AppResources.Done,
            IconUri = new Uri("/Images/ApplicationBar/appbar.check.png", UriKind.Relative)
        };

        private readonly AppBarButton _cancelButton = new AppBarButton
        {
            Text = AppResources.Cancel,
            IconUri = new Uri("/Images/ApplicationBar/appbar.cancel.rest.png", UriKind.Relative)
        };

        public EditCurrentUserView()
        {
            InitializeComponent();

            _doneButton.Click += (sender, args) => ViewModel.Done();
            _cancelButton.Click += (sender, args) => ViewModel.Cancel();

            Loaded += (sender, args) => BuildLocalizedAppBar();
        }

        private void BuildLocalizedAppBar()
        {
            if (ApplicationBar == null)
            {
                ApplicationBar = new ApplicationBar();

                ApplicationBar.Buttons.Add(_doneButton);
                ApplicationBar.Buttons.Add(_cancelButton);
            }
        }
    }
}