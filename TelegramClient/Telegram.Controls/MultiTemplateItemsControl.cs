using System.Windows;
using System.Windows.Controls;

namespace Telegram.Controls
{
    public interface ITemplateSelector
    {
        DataTemplate SelectTemplate(object item, DependencyObject container);
    }

    public class MultiTemplateItemsControl : ItemsControl
    {
        public static readonly DependencyProperty TemplateSelectorProperty =
            DependencyProperty.Register("TemplateSelector",
            typeof(ITemplateSelector), typeof(MultiTemplateItemsControl),
            new PropertyMetadata(OnTemplateChanged));

        public ITemplateSelector TemplateSelector
        {
            get { return (ITemplateSelector)GetValue(TemplateSelectorProperty); }
            set { SetValue(TemplateSelectorProperty, value); }
        }

        private static void OnTemplateChanged(DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {

        }

        protected override void PrepareContainerForItemOverride(
            DependencyObject element, object item)
        {
            base.PrepareContainerForItemOverride(element, item);

            var content = element as ContentPresenter;

            if (content != null)
            {
                content.ContentTemplate = TemplateSelector.SelectTemplate(item, this);
            }
        }
    }
}
