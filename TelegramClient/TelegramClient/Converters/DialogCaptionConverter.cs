using System;
using System.Globalization;
using System.Windows.Data;
using Caliburn.Micro;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
//#if WP8
//using PhoneNumbers;
//#endif
using Telegram.Api.TL;
using TelegramClient.Resources;

namespace TelegramClient.Converters
{
    public class DialogCaptionConverter : IValueConverter
    {
        public static string Convert(object value)
        {
            var user = value as TLUserBase;
            if (user != null)
            {
                if (user.Index == 333000)
                {
                    return AppResources.AppName;
                }

                if (user.Index == 777000)
                {
                    return AppResources.TelegramNotifications;
                }

                var userRequest = user as TLUserRequest;
                if (userRequest != null)
                {
#if WP8
                    //var phoneUtil = PhoneNumberUtil.GetInstance();
                    //try
                    //{
                    //    return phoneUtil.Format(phoneUtil.Parse("+" + user.Phone.Value, ""), PhoneNumberFormat.INTERNATIONAL);
                    //}
                    //catch (Exception e)
                    {
                        return "+" + user.Phone.Value;
                    }
#else
                    return "+" + user.Phone.Value;
#endif

                }

                if (user is TLUserEmpty
                    || user is TLUserDeleted)
                {
                    
                }

                return user.FullName.Trim();
            }

            var chat = value as TLChatBase;
            if (chat != null)
            {
                return chat.FullName.Trim();
            }

            var encryptedChat = value as TLEncryptedChatCommon;
            if (encryptedChat != null)
            {
                var currentUserId = IoC.Get<IMTProtoService>().CurrentUserId;
                var cache = IoC.Get<ICacheService>();

                if (currentUserId.Value == encryptedChat.AdminId.Value)
                {
                    var cachedParticipant = cache.GetUser(encryptedChat.ParticipantId);
                    return cachedParticipant != null ? cachedParticipant.FullName.Trim() : string.Empty;
                }

                var cachedAdmin = cache.GetUser(encryptedChat.AdminId);
                return cachedAdmin != null ? cachedAdmin.FullName.Trim() : string.Empty;
            }

            return value != null? value.ToString() : string.Empty;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Convert(value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
