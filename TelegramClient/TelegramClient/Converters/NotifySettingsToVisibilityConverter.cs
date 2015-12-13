using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Caliburn.Micro;
using Telegram.Api.Services;
using Telegram.Api.TL;

namespace TelegramClient.Converters
{
    public class NotifySettingsToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var notifySettings = value as TLPeerNotifySettings;
            if (notifySettings == null)
            {
                return Visibility.Collapsed;
            }

            var clientDelta = IoC.Get<IMTProtoService>().ClientTicksDelta;
            var utc0SecsLong = notifySettings.MuteUntil.Value * 4294967296 - clientDelta;
            var utc0SecsInt = utc0SecsLong / 4294967296.0;

            var muteUntilDateTime = Telegram.Api.Helpers.Utils.UnixTimestampToDateTime(utc0SecsInt);

            return muteUntilDateTime > DateTime.Now ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
