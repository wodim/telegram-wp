using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Data;
using Microsoft.Phone.Info;
using Telegram.Api.TL;

namespace TelegramClient.Converters
{
    public class MessageToFontFamilyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var message = value as TLMessage;
            if (message != null)
            {
                var emptyMedia = message.Media as TLMessageMediaEmpty;
                if (emptyMedia != null)
                {
                    return Application.Current.Resources["PhoneFontFamilyNormal"];
                }

                return Application.Current.Resources["PhoneFontFamilySemiBold"];
            }

            return Application.Current.Resources["PhoneFontFamilyNormal"];
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
