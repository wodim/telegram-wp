using System;
using System.Globalization;
using System.Windows.Data;
using Telegram.Api.TL;
using TelegramClient.Resources;

namespace TelegramClient.Converters
{
    public class EmptyDialogMessageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var userBase = value as TLUserBase;
            if (userBase != null && userBase.Index == 333000)
            {
                return AppResources.GotAQuestionAboutTelegram;
            }

            var user = value as TLUser;
            if (user != null && user.BotInfo != null)
            {
                var botInfo = user.BotInfo as TLBotInfo;
                if (botInfo != null)
                {
                    return botInfo.Description;
                }
            }

            return AppResources.NoMessagesYet;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
