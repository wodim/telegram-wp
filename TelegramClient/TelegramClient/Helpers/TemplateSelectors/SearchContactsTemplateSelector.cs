using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Telegram.Api.TL;
using TelegramClient.ViewModels.Search;

namespace TelegramClient.Helpers.TemplateSelectors
{
    public class SearchTemplateSelector : IValueConverter
    {
        public DataTemplate UserTemplate { get; set; }

        public DataTemplate TextTemplate { get; set; }

        public DataTemplate DialogTemplate { get; set; }

        public DataTemplate MessageTemplate { get; set; }

        public object Convert(object item, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(item is TLObject)) return null;

            var serviceText = item as TLServiceText;
            if (serviceText != null)
            {
                return TextTemplate;
            }

            var userBase = item as TLUserBase;
            if (userBase != null && userBase.IsGlobalResult)
            {
                return UserTemplate;
            }

            var chatBase = item as TLChatBase;
            if (chatBase != null && chatBase.IsGlobalResult)
            {
                return UserTemplate;
            }

            var dialog = item as TLDialog;
            if (dialog != null && dialog.TopMessage != null && dialog.Messages == null)
            {
                return MessageTemplate;
            }

            return DialogTemplate;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class SearchContactsTemplateSelector : IValueConverter
    {
        public DataTemplate ContactTemplate { get; set; }

        public DataTemplate UserTemplate { get; set; }

        public DataTemplate TextTemplate { get; set; }

        public object Convert(object item, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(item is TLObject)) return null;

            var contact = item as TLUserContact;
            if (contact != null)
            {
                return ContactTemplate;
            }

            var serviceText = item as TLServiceText;
            if (serviceText != null)
            {
                return TextTemplate;
            }

            return UserTemplate;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
