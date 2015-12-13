using System.Windows;
using System.Windows.Controls;
using System.Windows.Interactivity;

namespace TelegramClient
{
    public class ScrollToEndOnSizeChangedBehavior : Behavior<FrameworkElement>
    {
        public static readonly DependencyProperty ScrollViewerProperty = DependencyProperty.Register(
            "ScrollViewer", typeof (ScrollViewer), typeof (ScrollToEndOnSizeChangedBehavior), new PropertyMetadata(default(ScrollViewer)));

        public ScrollViewer ScrollViewer
        {
            get { return (ScrollViewer) GetValue(ScrollViewerProperty); }
            set { SetValue(ScrollViewerProperty, value); }
        }

        protected override void OnAttached()
        {
            base.OnAttached();

            AssociatedObject.SizeChanged += OnSizeChanged;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.SizeChanged -= OnSizeChanged;

            base.OnDetaching();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            ScrollViewer.ScrollToVerticalOffset(ScrollViewer.ScrollableHeight);
        }
    }
}