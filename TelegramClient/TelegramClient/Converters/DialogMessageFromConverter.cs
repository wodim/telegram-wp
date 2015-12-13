using System;
using System.Globalization;
using System.Windows.Data;
using Caliburn.Micro;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using TelegramClient.Resources;
using TelegramClient.Services;

namespace TelegramClient.Converters
{
    public class DialogMessageFromConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var dialog = value as TLDialog;

            if (dialog != null)
            {
                var messageBase = dialog.TopMessage;
                if (messageBase != null && messageBase.ShowFrom)
                {
                    var messageCommon = messageBase as TLMessageCommon;
                    if (messageCommon != null)
                    {
                        var user = messageCommon.From as TLUserBase;
                        if (user != null)
                        {
                            var currentUserId = IoC.Get<IStateService>().CurrentUserId;
                            if (currentUserId == user.Index)
                            {
                                return AppResources.You;
                            }

                            return user.FullName.Trim();
                        }

                        var peerChannel = messageCommon.ToId as TLPeerChannel;
                        if (peerChannel != null
                            && (messageCommon.FromId == null || messageCommon.FromId.Value == -1))
                        {
                            var channel = IoC.Get<ICacheService>().GetChat(peerChannel.Id) as TLChannel;

                            if (channel != null)
                            {
                                return channel.FullName;
                            }

                            var channelForbidden = IoC.Get<ICacheService>().GetChat(peerChannel.Id) as TLChannelForbidden;
                            if (channelForbidden != null)
                            {
                                return channelForbidden.FullName;
                            }
                        }
                    }
                }
            }

            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
