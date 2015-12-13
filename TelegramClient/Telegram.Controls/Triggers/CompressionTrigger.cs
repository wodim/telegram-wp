using System.Windows;
using System.Windows.Interactivity;

namespace Telegram.Controls.Triggers
{
    public class CompressionTrigger : TriggerBase<LazyListBox>
    {

        public static DependencyProperty IsDisabledProperty = DependencyProperty.Register("IsDisabled", typeof(bool), typeof(CompressionTrigger), null);

        public bool IsDisabled
        {
            get { return (bool)GetValue(IsDisabledProperty); }
            set { SetValue(IsDisabledProperty, value); }
        }

        public static DependencyProperty CompressionTypeProperty = DependencyProperty.Register("CompressionType", typeof(CompressionType), typeof(CompressionTrigger), null);

        public CompressionType CompressionType
        {
            get { return (CompressionType)GetValue(CompressionTypeProperty); }
            set { SetValue(CompressionTypeProperty, value); }
        }

        protected override void OnAttached()
        {
            base.OnAttached();

            AssociatedObject.Compression += AssociatedObject_Compression;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.Compression -= AssociatedObject_Compression;

            base.OnDetaching();
        }

        private void AssociatedObject_Compression(object sender, CompressionEventArgs args)
        {
            if (!IsDisabled
                && args.Type == CompressionType)
            {
                InvokeActions(null);
            }
        }
    }
}
