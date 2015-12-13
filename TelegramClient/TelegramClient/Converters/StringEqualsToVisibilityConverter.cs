using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Telegram.Api.TL;

namespace TelegramClient.Converters
{
    public class StringEqualsToVisibilityConverter : IValueConverter
    {
        public bool IsInvert { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var visibility = false;

            if (value != null)
            {
                var values = ((string) parameter).Split(' ');

                foreach (var s in values)
                {
                    if (string.Equals(value.ToString(), s, StringComparison.OrdinalIgnoreCase))
                    {
                        visibility = true;
                        break;
                    }
                }
                
                if (IsInvert)
                {
                    visibility = !visibility;
                }
            }

            return visibility ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class DialogStatusEqualsToVisibilityConverter : IValueConverter
    {
        public bool IsInvert { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var visibility = false;

            if (value != null)
            {
                var dialog = value as TLDialog;
                var broadcastDialog = value as TLBroadcastDialog;
                var encryptedDialog = value as TLEncryptedDialog;
                if (dialog != null)
                {
                    var topMessage = dialog.TopMessage;
                    if (topMessage != null)
                    {
                        value = topMessage.Status;
                    }
                }
                else if (broadcastDialog != null)
                {
                    var topMessage = broadcastDialog.TopMessage;
                    if (topMessage != null)
                    {
                        value = topMessage.Status;
                    }
                }
                else if (encryptedDialog != null)
                {
                    var topMessage = encryptedDialog.TopMessage;
                    if (topMessage != null
                        && TLUtils.IsDisplayedDecryptedMessage(topMessage))
                    {
                        value = topMessage.Status;
                    }
                    else
                    {
                        for (var i = 0; i < encryptedDialog.Messages.Count; i++)
                        {
                            if (TLUtils.IsDisplayedDecryptedMessage(encryptedDialog.Messages[i]))
                            {
                                value = encryptedDialog.Messages[i].Status;
                                break;
                            }
                        }
                    }
                }

                var values = ((string)parameter).Split(' ');

                foreach (var s in values)
                {
                    if (string.Equals(value.ToString(), s, StringComparison.OrdinalIgnoreCase))
                    {
                        visibility = true;
                        break;
                    }
                }

                if (IsInvert)
                {
                    visibility = !visibility;
                }
            }

            return visibility ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
