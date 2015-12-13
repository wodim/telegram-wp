using System;
using System.Globalization;
using System.Windows.Data;
//#if WP8
//using PhoneNumbers;
//#endif
using Telegram.Api.TL;

namespace TelegramClient.Converters
{
    public class SimplePhoneNumberConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var phone = value as TLString;
            if (phone == null) return value;

            var phoneString = phone.ToString();

            return phoneString.StartsWith("+") ? phoneString : "+" + phoneString ;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class PhoneNumberConverter : IValueConverter
    {
        public static string Convert(TLString phone)
        {
//#if WP8
//            var phoneUtil = PhoneNumberUtil.GetInstance();
//            try
//            {
//                return phoneUtil.Format(phoneUtil.Parse("+" + phone.Value, ""), PhoneNumberFormat.INTERNATIONAL).Replace('-', ' ');
//            }
//            catch (Exception e)
//            {
//                return "+" + phone.Value;
//            }

//#endif
            return "+" + phone.Value;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var phone = value as TLString;
            if (phone == null) return value;

            return Convert(phone);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
