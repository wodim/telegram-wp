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
    public class DialogToBriefInfoConverter : IValueConverter
    {
        public static string Convert(TLDecryptedMessageBase value, bool showContent)
        {
            var serviceMessage = value as TLDecryptedMessageService;
            if (serviceMessage != null)
            {
                if (serviceMessage.Action is TLDecryptedMessageActionEmpty)
                {
                    return AppResources.SecretChatCreated;
                }

                return DecryptedServiceMessageToTextConverter.Convert(serviceMessage);
            }

            var message = value as TLDecryptedMessage;
            if (message != null)
            {
                var canSendString = string.Empty;

                if (message.Status == MessageStatus.Failed)
                {
                    canSendString = string.Format("{0}: ", AppResources.SendingFailed);
                }

                if (message.Media != null)
                {
                    if (message.Media is TLDecryptedMessageMediaDocument)
                    {
                        return canSendString + AppResources.Document;
                    }

                    if (message.Media is TLDecryptedMessageMediaContact)
                    {
                        return canSendString + AppResources.Contact;
                    }

                    if (message.Media is TLDecryptedMessageMediaGeoPoint)
                    {
                        return canSendString + AppResources.GeoPoint;
                    }

                    if (message.Media is TLDecryptedMessageMediaPhoto)
                    {
                        return canSendString + AppResources.Photo;
                    }

                    if (message.Media is TLDecryptedMessageMediaVideo)
                    {
                        return canSendString + AppResources.Video;
                    }

                    if (message.Media is TLDecryptedMessageMediaAudio)
                    {
                        return canSendString + AppResources.Audio;
                    }

                    if (message.Media is TLDecryptedMessageMediaExternalDocument)
                    {
                        if (message.IsSticker())
                        {
                            return canSendString + AppResources.Sticker;
                        }

                        return canSendString + AppResources.Document;
                    }
                }

                if (message.Message != null)
                {
                    if (showContent)
                    {

                        return canSendString + message.Message;
                    }

                    return canSendString + AppResources.Message;
                }
            }

            return null;
        }

        public static string Convert(TLMessageBase value, bool showContent)
        {
            var emptyMessage = value as TLMessageEmpty;
            if (emptyMessage != null)
            {
                return AppResources.EmptyMessage;
            }

            //var forwardedMessage = value as TLMessageForwarded;
            //if (forwardedMessage != null)
            //{
            //    return AppResources.ForwardedMessage;
            //}

            var serviceMessage = value as TLMessageService;
            if (serviceMessage != null)
            {
                return ServiceMessageToTextConverter.Convert(serviceMessage);

                //return AppResources.ServiceMessage;
            }
               
            var message = value as TLMessage;
            if (message != null)
            {
                var canSendString = string.Empty;

                if (message.Status == MessageStatus.Failed)
                {
                    canSendString = string.Format("{0}: ", AppResources.SendingFailed);
                }

                if (message.Media != null)
                {
                    if (message.Media is TLMessageMediaDocument)
                    {
                        if (message.IsSticker())
                        {
                            return canSendString + AppResources.Sticker;
                        }

                        return canSendString + AppResources.Document;
                    }

                    if (message.Media is TLMessageMediaContact)
                    {
                        return canSendString + AppResources.Contact;
                    }

                    if (message.Media is TLMessageMediaGeo)
                    {
                        return canSendString + AppResources.GeoPoint;
                    }

                    if (message.Media is TLMessageMediaPhoto)
                    {
                        return canSendString + AppResources.Photo;
                    }

                    if (message.Media is TLMessageMediaVideo)
                    {
                        return canSendString + AppResources.Video;
                    }

                    if (message.Media is TLMessageMediaAudio)
                    {
                        return canSendString + AppResources.Audio;
                    }

                    if (message.Media is TLMessageMediaUnsupported)
                    {
                        return canSendString + AppResources.UnsupportedMedia;
                    }
                }

                if (message.Message != null)
                {
                    if (showContent)
                    {

                        return canSendString + message.Message;                     
                    }

                    return canSendString + AppResources.Message;
                }
            }

            return null;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var dialog = value as TLDialog;
            var broadcast = value as TLBroadcastDialog;
            if (dialog != null)
            {
                //if (!string.IsNullOrEmpty(dialog.TypingString))
                //{
                //    return dialog.TypingString;
                //}

                var message = dialog.TopMessage;
                if (message != null)
                {
                    return Convert(message, true);
                }
            }
            else if (broadcast != null)
            {
                var message = broadcast.TopMessage;
                if (message != null)
                {
                    return Convert(message, true);
                }
            }
            else
            {
                var encryptedDialog = value as TLEncryptedDialog;
                if (encryptedDialog != null)
                {
                    var chatId = encryptedDialog.Peer.Id;
                    var encryptedChat = IoC.Get<ICacheService>().GetEncryptedChat(chatId);
                    var chatWaiting = encryptedChat as TLEncryptedChatWaiting;
                    if (chatWaiting != null)
                    {
                        var participant = IoC.Get<ICacheService>().GetUser(chatWaiting.ParticipantId);
                        return string.Format(AppResources.WaitingForUserToGetOnline, participant.FirstName);
                    }

                    var chatDiscarded = encryptedChat as TLEncryptedChatDiscarded;
                    if (chatDiscarded != null)
                    {
                        return AppResources.SecretChatDiscarded;
                    }

                    var chatEmpty = encryptedChat as TLEncryptedChatEmpty;
                    if (chatEmpty != null)
                    {
                        return AppResources.EmptySecretChat;
                    }

                    var chat = encryptedChat as TLEncryptedChat;
                    if (chat != null)
                    {
                        if (TLUtils.IsDisplayedDecryptedMessage(encryptedDialog.TopMessage))
                        {
                            return Convert(encryptedDialog.TopMessage, true);
                        }

                        for (var i = 0; i < encryptedDialog.Messages.Count; i++)
                        {
                            if (TLUtils.IsDisplayedDecryptedMessage(encryptedDialog.Messages[i]))
                            {
                                return Convert(encryptedDialog.Messages[i], true);
                            }
                        }

                        var currentUserId = IoC.Get<IStateService>().CurrentUserId;
                        if (chat.AdminId.Value == currentUserId)
                        {
                            var cacheService = IoC.Get<ICacheService>();
                            var user = cacheService.GetUser(chat.ParticipantId);
                            if (user != null)
                            {
                                var userName = TLString.IsNullOrEmpty(user.FirstName) ? user.LastName : user.FirstName;
                                return string.Format(AppResources.UserJoinedYourSecretChat, userName);
                            }
                        }
                        else
                        {
                            return AppResources.YouJoinedTheSecretChat;
                        }

                        return AppResources.SecretChatCreated;
                    }

                    var chatRequested = encryptedChat as TLEncryptedChatRequested;
                    if (chatRequested != null)
                    {
                        return AppResources.SecretChatRequested;
                    }
                }

            }
            

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
