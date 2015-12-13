using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using TelegramClient.Helpers;
using TelegramClient.ViewModels.Dialogs;

namespace TelegramClient.Views.Dialogs
{
    public partial class DialogSearchMessagesView
    {
        public DialogSearchMessagesViewModel ViewModel
        {
            get { return DataContext as DialogSearchMessagesViewModel; }
        }

        private bool _once;

        public DialogSearchMessagesView()
        {
            InitializeComponent();

            SearchLabel.Visibility = Visibility.Collapsed;

            OpenStoryboard.Completed += (sender, args) =>
            {
                Text.Focus();
            };

            CloseStoryboard.Completed += (sender, args) =>
            {
                Text.Text = string.Empty;
            };

            Loaded += (sender, args) =>
            {
                ViewModel.PropertyChanged += OnViewModelPropertyChanged;

                if (!_once)
                {
                    _once = true;
                    if (ViewModel.IsOpen)
                    {
                        OpenStoryboard.Begin();
                    }
                }
            };

            Unloaded += (sender, args) =>
            {
                ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            };
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (Property.NameEquals(e.PropertyName, () => ViewModel.IsOpen))
            {
                if (ViewModel.IsOpen)
                {
                    OpenStoryboard.Begin();
                }
                else
                {
                    CloseStoryboard.Begin();
                }
            }
        }

        private void ButtonBase_OnClick(object sender, GestureEventArgs e)
        {
            ViewModel.Close();
        }

        private void ButtonUp_OnClick(object sender, GestureEventArgs gestureEventArgs)
        {
            ViewModel.Up();
        }

        private void ButtonDown_OnClick(object sender, GestureEventArgs gestureEventArgs)
        {
            ViewModel.Down();
        }
    }
}
