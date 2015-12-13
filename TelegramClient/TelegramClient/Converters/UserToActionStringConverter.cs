using System;
using System.Globalization;
using System.Windows.Data;
using Telegram.Api.TL;
using TelegramClient.Resources;

namespace TelegramClient.Converters
{
    public class UserToActionStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var userRequest = value as TLUserRequest;
            if (userRequest != null)
            {
                return AppResources.AddToContacts.ToUpperInvariant();
            }

            var userForeign = value as TLUserForeign;
            if (userForeign != null)
            {
                return AppResources.ShareMyContactInfo.ToUpperInvariant();
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
