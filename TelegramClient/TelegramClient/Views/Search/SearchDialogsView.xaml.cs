using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Telegram.Controls;
using Telegram.Controls.Extensions;
using TelegramClient.ViewModels.Search;

namespace TelegramClient.Views.Search
{
    public partial class SearchDialogsView
    {
        public SearchDialogsViewModel ViewModel
        {
            get { return DataContext as SearchDialogsViewModel; }
        }

        public SearchDialogsView()
        {
            InitializeComponent();
        }

        private FrameworkElement _lastTapedItem;

        private void MainItemGrid_OnTap(object sender, GestureEventArgs e)
        {
            _lastTapedItem = sender as FrameworkElement;

            if (_lastTapedItem != null)
            {
                //foreach (var descendant in _lastTapedItem.GetVisualDescendants().OfType<HighlightingTextBlock>())
                //{
                //    if (AnimatedBasePage.GetIsAnimationTarget(descendant))
                //    {
                //        _lastTapedItem = descendant;
                //        break;
                //    }
                //}

                if (!(_lastTapedItem.RenderTransform is CompositeTransform))
                {
                    _lastTapedItem.RenderTransform = new CompositeTransform();
                }

                var tapedItemContainer = _lastTapedItem.FindParentOfType<ListBoxItem>();
                if (tapedItemContainer != null)
                {
                    tapedItemContainer = tapedItemContainer.FindParentOfType<ListBoxItem>();
                }

                SearchShellView.StartContinuumForwardOutAnimation(_lastTapedItem, tapedItemContainer);
            }
        }

        private void Items_OnScrollingStateChanged(object sender, ScrollingStateChangedEventArgs e)
        {
            if (e.NewValue)
            {
                var focusElement = FocusManager.GetFocusedElement();
                if (focusElement != null
                    && focusElement.GetType() == typeof(WatermarkedTextBox))
                {
                    Self.Focus();
                }
            }
        }
    }
}