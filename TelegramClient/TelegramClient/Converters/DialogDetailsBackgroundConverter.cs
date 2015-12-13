using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using TelegramClient.ViewModels.Additional;

namespace TelegramClient.Converters
{
    public class DialogDetailsBackgroundConverter : DependencyObject, IValueConverter
    {
        public static readonly DependencyProperty ImageTemplateProperty = DependencyProperty.Register(
            "ImageTemplate", typeof (DataTemplate), typeof (DialogDetailsBackgroundConverter), new PropertyMetadata(default(DataTemplate)));

        public DataTemplate ImageTemplate
        {
            get { return (DataTemplate) GetValue(ImageTemplateProperty); }
            set { SetValue(ImageTemplateProperty, value); }
        }

        public static readonly DependencyProperty AnimatedTemplateProperty = DependencyProperty.Register(
            "AnimatedTemplate", typeof (DataTemplate), typeof (DialogDetailsBackgroundConverter), new PropertyMetadata(default(DataTemplate)));

        public DataTemplate AnimatedTemplate
        {
            get { return (DataTemplate) GetValue(AnimatedTemplateProperty); }
            set { SetValue(AnimatedTemplateProperty, value); }
        }

        public static readonly DependencyProperty EmptyTemplateProperty = DependencyProperty.Register(
            "EmptyTemplate", typeof (DataTemplate), typeof (DialogDetailsBackgroundConverter), new PropertyMetadata(default(DataTemplate)));

        public DataTemplate EmptyTemplate
        {
            get { return (DataTemplate) GetValue(EmptyTemplateProperty); }
            set { SetValue(EmptyTemplateProperty, value); }
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var background = value as BackgroundItem;
            if (background == null)
            {
                return EmptyTemplate;
            }

            if (background.Name == Constants.AnimatedBackground1String)
            {
                return AnimatedTemplate;
            }

            return ImageTemplate;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
