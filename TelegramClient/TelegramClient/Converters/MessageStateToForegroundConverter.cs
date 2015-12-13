using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Telegram.Api.TL;

namespace TelegramClient.Converters
{
    public class MessageStateToForegroundConverter : IValueConverter
    {
        #region Implementation of IValueConverter

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var accentColor = new SolidColorBrush((Color)Application.Current.Resources["PhoneAccentColor"]);
            var foregroundColor = new SolidColorBrush((Color)Application.Current.Resources["PhoneSubtleColor"]);

            TLMessageCommon message = null;
            var dialog = value as TLDialog;
            var broadcast = value as TLBroadcastDialog;
            if (dialog != null)
            {
                message = dialog.TopMessage as TLMessageCommon;
            }
            else if (broadcast != null)
            {
                message = broadcast.TopMessage as TLMessageCommon;
            }
            else
            {
                var encryptedDialog = value as TLEncryptedDialog;
                if (encryptedDialog != null)
                {
                    if (TLUtils.IsDisplayedDecryptedMessage(encryptedDialog.TopMessage))
                    {
                        var unreadDecrypted = encryptedDialog.TopMessage.Unread.Value;
                        var outDecrypted = encryptedDialog.TopMessage.Out.Value;

                        return unreadDecrypted && !outDecrypted ? accentColor : foregroundColor;
                    }

                    for (var i = 0; i < encryptedDialog.Messages.Count; i++)
                    {
                        if (TLUtils.IsDisplayedDecryptedMessage(encryptedDialog.Messages[i]))
                        {
                            var unreadDecrypted = encryptedDialog.Messages[i].Unread.Value;
                            var outDecrypted = encryptedDialog.Messages[i].Out.Value;

                            return unreadDecrypted && !outDecrypted ? accentColor : foregroundColor;
                        }
                    }

                    return foregroundColor;
                }

            }

            if (message == null)
            {
                var decryptedMessage = value as TLDecryptedMessageBase;
                if (decryptedMessage != null)
                {
                    var unreadDecrypted = decryptedMessage.Unread.Value;
                    var outDecrypted = decryptedMessage.Out.Value;

                    return unreadDecrypted && !outDecrypted ? accentColor : foregroundColor;
                }
                
                return foregroundColor;
            }

            var unread = message.Unread.Value;
            var outFlag = message.Out.Value;
            var useAccent = unread && !outFlag;
            return useAccent ? accentColor : foregroundColor;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        #endregion
    }

    public class DialogToForegroundConverter : IValueConverter
    {
        #region Implementation of IValueConverter

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var accentColor = new SolidColorBrush((Color)Application.Current.Resources["PhoneAccentColor"]);
            var foregroundColor = new SolidColorBrush((Color)Application.Current.Resources["PhoneForegroundColor"]);

            var encryptedDialog = value as TLEncryptedDialog;
            if (encryptedDialog != null)
            {

                return accentColor;
            }

            return foregroundColor;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
