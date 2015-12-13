using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using Telegram.Api;
using Telegram.Api.TL;
using TelegramClient.Resources;
using Language = TelegramClient.Utils.Language;

namespace TelegramClient.Converters
{
    public class MessageToWebPageCaptionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var message = value as TLMessage;
            if (message != null)
            {
                if (!string.IsNullOrEmpty(message.WebPageTitle))
                {
                    return message.WebPageTitle;
                }

                var mediaWebPage = message.Media as TLMessageMediaWebPage;
                if (mediaWebPage != null)
                {
                    var webPage = mediaWebPage.WebPage as TLWebPage;

                    if (webPage != null)
                    {
                        var caption = webPage.Title ?? webPage.SiteName;// ?? webPage.DisplayUrl;
                        if (!TLString.IsNullOrEmpty(caption))
                        {
                            return Language.CapitalizeFirstLetter(caption.ToString());
                            
                        }

                        caption = webPage.DisplayUrl;
                        if (!TLString.IsNullOrEmpty(caption))
                        {
                            var parts = caption.ToString().Split('.');
                            if (parts.Length >= 2)
                            {
                                return Language.CapitalizeFirstLetter(parts[parts.Length - 2]);
                            }
                        }
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

    public class WebPageToCaptionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var webPage = value as TLWebPage;

            if (webPage != null)
            {
                return webPage.SiteName?? webPage.Title ?? webPage.DisplayUrl;
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class WebPageToDescriptionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var webPage = value as TLWebPage;

            if (webPage != null)
            {
                return webPage.Description ?? webPage.Title;
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class MessageContainerToFromConverter : IValueConverter
    {
        private static TLString GetFirstName(TLMessage25 message)
        {
            if (message != null)
            {
                var fromUser = message.FwdFrom as TLUserBase;
                if (fromUser != null)
                {
                    return fromUser.FirstName;
                }

                var fromChat = message.FwdFrom as TLChannel;
                if (fromChat != null)
                {
                    return fromChat.Title;
                }

                var fromChatForbidden = message.FwdFrom as TLChannelForbidden;
                if (fromChatForbidden != null)
                {
                    return fromChatForbidden.Title;
                }
            }

            return null;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var messagesContainer = value as TLMessagesContainter;

            if (messagesContainer != null)
            {
                if (messagesContainer.FwdMessages != null)
                {
                    var usersCache = new Dictionary<int, TLMessage25>();
                    var channelsCache = new Dictionary<int, TLMessage25>();

                    for (var i = 0; i < messagesContainer.FwdMessages.Count; i++)
                    {
                        var message = messagesContainer.FwdMessages[i];
                        if (message.FwdFromId != null)
                        {
                            if (!usersCache.ContainsKey(message.FwdFromId.Value))
                            {
                                usersCache[message.FwdFromId.Value] = message;
                            }
                        }

                        var message40 = message as TLMessage40;
                        if (message40 != null)
                        {
                            if (message40.FwdFromPeer != null)
                            {
                                var peerUser = message40.FwdFromPeer as TLPeerUser;
                                if (peerUser != null)
                                {
                                    if (!usersCache.ContainsKey(peerUser.Id.Value))
                                    {
                                        usersCache[peerUser.Id.Value] = message;
                                    }
                                }

                                var peerChannel = message40.FwdFromPeer as TLPeerChannel;
                                if (peerChannel != null)
                                {
                                    if (!channelsCache.ContainsKey(peerChannel.Id.Value))
                                    {
                                        channelsCache[peerChannel.Id.Value] = message;
                                    }
                                }
                            }
                        }
                    }
                    var count = usersCache.Count + channelsCache.Count;
                    if (count == 1)
                    {
                        return GetFirstName(messagesContainer.FwdMessages[0]);
                    } 
                    if (count == 2)
                    {
                        var list = usersCache.Values.Union(channelsCache.Values).ToList();

                        var message1 = list[0];
                        var message2 = list[1];

                        return string.Format("{0}, {1}", GetFirstName(message1), GetFirstName(message2));
                    }
                    if (count > 2)
                    {
                        return string.Format("{0} {1}", GetFirstName(messagesContainer.FwdMessages[0]), string.Format(AppResources.AndOthers, count - 1).ToLowerInvariant());
                    }
                }
            }

            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class MessageContainerToContentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var messagesContainer = value as TLMessagesContainter;

            if (messagesContainer != null)
            {
                if (messagesContainer.FwdMessages != null)
                {
                    var count = messagesContainer.FwdMessages.Count;
                    if (count == 1)
                    {
                        var mediaPhoto = messagesContainer.FwdMessages[0].Media as TLMessageMediaPhoto;
                        if (mediaPhoto != null)
                        {
                            return AppResources.ForwardedPhotoNominativeSingular;
                        }

                        var mediaAudio = messagesContainer.FwdMessages[0].Media as TLMessageMediaAudio;
                        if (mediaAudio != null)
                        {
                            return AppResources.ForwardedAudioNominativeSingular;
                        }

                        var mediaDocument = messagesContainer.FwdMessages[0].Media as TLMessageMediaDocument;
                        if (mediaDocument != null)
                        {
                            if (messagesContainer.FwdMessages[0].IsSticker())
                            {
                                return AppResources.ForwardedStickerNominativeSingular;
                            }

                            return AppResources.ForwardedFileNominativeSingular;
                        }

                        var mediaVideo = messagesContainer.FwdMessages[0].Media as TLMessageMediaVideo;
                        if (mediaVideo != null)
                        {
                            return AppResources.ForwardedVideoNominativeSingular;
                        }

                        var mediaLocation = messagesContainer.FwdMessages[0].Media as TLMessageMediaGeo;
                        if (mediaLocation != null)
                        {
                            return AppResources.ForwardedLocationNominativeSingular;
                        }

                        var mediaContact = messagesContainer.FwdMessages[0].Media as TLMessageMediaContact;
                        if (mediaContact != null)
                        {
                            return AppResources.ForwardedContactNominativeSingular;
                        }

                        return AppResources.ForwardedMessage;
                    }
                    if (count > 1)
                    {
                        var sameMedia = true;
                        var media = messagesContainer.FwdMessages[0].Media;
                        for (var i = 1; i < messagesContainer.FwdMessages.Count; i++)
                        {
                            if (messagesContainer.FwdMessages[i].Media.GetType() != media.GetType())
                            {
                                sameMedia = false;
                                break;
                            }
                        }

                        if (sameMedia)
                        {
                            if (media is TLMessageMediaPhoto)
                            {
                                return Language.Declension(
                                    count,
                                    AppResources.ForwardedPhotoNominativeSingular,
                                    AppResources.ForwardedPhotoNominativePlural,
                                    AppResources.ForwardedPhotoGenitiveSingular,
                                    AppResources.ForwardedPhotoGenitivePlural).ToLower(CultureInfo.CurrentUICulture);
                            }

                            if (media is TLMessageMediaAudio)
                            {
                                return Language.Declension(
                                    count,
                                    AppResources.ForwardedAudioNominativeSingular,
                                    AppResources.ForwardedAudioNominativePlural,
                                    AppResources.ForwardedAudioGenitiveSingular,
                                    AppResources.ForwardedAudioGenitivePlural).ToLower(CultureInfo.CurrentUICulture);
                            }

                            if (media is TLMessageMediaDocument)
                            {
                                if (messagesContainer.FwdMessages[0].IsSticker())
                                {
                                    return Language.Declension(
                                       count,
                                       AppResources.ForwardedStickerNominativeSingular,
                                       AppResources.ForwardedStickerNominativePlural,
                                       AppResources.ForwardedStickerGenitiveSingular,
                                       AppResources.ForwardedStickerGenitivePlural).ToLower(CultureInfo.CurrentUICulture);
                                }

                                return Language.Declension(
                                    count,
                                    AppResources.ForwardedFileNominativeSingular,
                                    AppResources.ForwardedFileNominativePlural,
                                    AppResources.ForwardedFileGenitiveSingular,
                                    AppResources.ForwardedFileGenitivePlural).ToLower(CultureInfo.CurrentUICulture);
                            }

                            if (media is TLMessageMediaVideo)
                            {
                                return Language.Declension(
                                    count,
                                    AppResources.ForwardedVideoNominativeSingular,
                                    AppResources.ForwardedVideoNominativePlural,
                                    AppResources.ForwardedVideoGenitiveSingular,
                                    AppResources.ForwardedVideoGenitivePlural).ToLower(CultureInfo.CurrentUICulture);
                            }

                            if (media is TLMessageMediaGeo)
                            {
                                return Language.Declension(
                                    count,
                                    AppResources.ForwardedLocationNominativeSingular,
                                    AppResources.ForwardedLocationNominativePlural,
                                    AppResources.ForwardedLocationGenitiveSingular,
                                    AppResources.ForwardedLocationGenitivePlural).ToLower(CultureInfo.CurrentUICulture);
                            }

                            if (media is TLMessageMediaContact)
                            {
                                return Language.Declension(
                                    count,
                                    AppResources.ForwardedContactNominativeSingular,
                                    AppResources.ForwardedContactNominativePlural,
                                    AppResources.ForwardedContactGenitiveSingular,
                                    AppResources.ForwardedContactGenitivePlural).ToLower(CultureInfo.CurrentUICulture);
                            }
                        }

                        return Language.Declension(
                            count,
                            AppResources.ForwardedMessageNominativeSingular,
                            AppResources.ForwardedMessageNominativePlural,
                            AppResources.ForwardedMessageGenitiveSingular,
                            AppResources.ForwardedMessageGenitivePlural).ToLower(CultureInfo.CurrentUICulture);
                    }
                }
            }

            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
