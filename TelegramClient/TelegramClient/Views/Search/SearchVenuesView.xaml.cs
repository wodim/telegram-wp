using System;
using System.Windows.Input;
using Telegram.Controls;
using TelegramClient.ViewModels.Search;
using Execute = Telegram.Api.Helpers.Execute;

namespace TelegramClient.Views.Search
{
    public partial class SearchVenuesView
    {
        public SearchVenuesViewModel ViewModel
        {
            get { return DataContext as SearchVenuesViewModel; }
        }

        public SearchVenuesView()
        {
            InitializeComponent();

            Loaded += (o, e) =>
            {
                Execute.BeginOnUIThread(TimeSpan.FromMilliseconds(500), () => SearchBox.Focus());
            };
        }

        private void Items_OnScrollingStateChanged(object sender, ScrollingStateChangedEventArgs e)
        {
            if (e.NewValue)
            {
                var focusElement = FocusManager.GetFocusedElement();
                if (focusElement == SearchBox)
                {
                    Self.Focus();
                }
            }
        }
    }
}