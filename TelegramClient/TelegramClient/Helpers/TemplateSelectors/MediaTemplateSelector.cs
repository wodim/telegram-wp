using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Telegram.Api;
using Telegram.Api.TL;

namespace TelegramClient.Helpers.TemplateSelectors
{
    public class DecryptedMediaTemplateSelector : IValueConverter
    {
        public DataTemplate EmptyTemplate { get; set; }

        public DataTemplate ContactTemplate { get; set; }

        public DataTemplate PhotoTemplate { get; set; }

        public DataTemplate SecretPhotoTemplate { get; set; }

        public DataTemplate VideoTemplate { get; set; }

        public DataTemplate GeoPointTemplate { get; set; }

        public DataTemplate DocumentTemplate { get; set; }

        public DataTemplate AudioTemplate { get; set; }

        public DataTemplate UnsupportedTemplate { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            TLInt ttl = null;
            var useSecretPhotos = false;
            var decryptedMessage = value as TLDecryptedMessage;
            if (decryptedMessage != null)
            {
                ttl = decryptedMessage.TTL;
                useSecretPhotos = decryptedMessage is TLDecryptedMessage17;
                value = decryptedMessage.Media;
            }

            var emptyMedia = value as TLDecryptedMessageMediaEmpty;
            if (emptyMedia != null)
            {
                return EmptyTemplate;
            }

            var contactMedia = value as TLDecryptedMessageMediaContact;
            if (contactMedia != null)
            {
                return ContactTemplate;
            }

            var photoMedia = value as TLDecryptedMessageMediaPhoto;
            if (photoMedia != null)
            {
                if (ttl != null && ttl.Value > 0 && ttl.Value <= 60.0 && useSecretPhotos)
                {
                    return SecretPhotoTemplate;
                }

                return PhotoTemplate;
            }

            var documentMedia = value as TLDecryptedMessageMediaDocument;
            if (documentMedia != null)
            {
                return DocumentTemplate;
            }

            var videoMedia = value as TLDecryptedMessageMediaVideo;
            if (videoMedia != null)
            {
                return VideoTemplate;
            }

            var geoPointMedia = value as TLDecryptedMessageMediaGeoPoint;
            if (geoPointMedia != null)
            {
                return GeoPointTemplate;
            }

            var audioMedia = value as TLDecryptedMessageMediaAudio;
            if (audioMedia != null)
            {
                return AudioTemplate;
            }

            return UnsupportedTemplate;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class MediaTemplateSelector : IValueConverter
    {
        public DataTemplate VenueTemplate { get; set; }

        public DataTemplate WebPageTemplate { get; set; }

        public DataTemplate WebPageSmallPhotoTemplate { get; set; }

        public DataTemplate WebPagePhotoTemplate { get; set; }

        public DataTemplate WebPagePendingTemplate { get; set; }

        public DataTemplate EmptyTemplate { get; set; }

        public DataTemplate ContactTemplate { get; set; }

        public DataTemplate PhotoTemplate { get; set; }

        public DataTemplate VideoTemplate { get; set; }

        public DataTemplate GeoPointTemplate { get; set; }

        public DataTemplate DocumentTemplate { get; set; }

        public DataTemplate AudioTemplate { get; set; }

        public DataTemplate UnsupportedTemplate { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var emptyMedia = value as TLMessageMediaEmpty;
            if (emptyMedia != null)
            {
                return EmptyTemplate;
            }

            var contactMedia = value as TLMessageMediaContact;
            if (contactMedia != null)
            {
                return ContactTemplate;
            }

            var photoMedia = value as TLMessageMediaPhoto;
            if (photoMedia != null)
            {
                return PhotoTemplate;
            }

            var documentMedia = value as TLMessageMediaDocument;
            if (documentMedia != null)
            {
                return DocumentTemplate;
            }

            var videoMedia = value as TLMessageMediaVideo;
            if (videoMedia != null)
            {
                return VideoTemplate;
            }

            var venueMedia = value as TLMessageMediaVenue;
            if (venueMedia != null)
            {
                return VenueTemplate;
            }

            var geoPointMedia = value as TLMessageMediaGeo;
            if (geoPointMedia != null)
            {
                return GeoPointTemplate;
            }

            var audioMedia = value as TLMessageMediaAudio;
            if (audioMedia != null)
            {
                return AudioTemplate;
            }

            var webPageMedia = value as TLMessageMediaWebPage;
            if (webPageMedia != null)
            {
                var webPageEmpty = webPageMedia.WebPage as TLWebPageEmpty;
                if (webPageEmpty != null)
                {
                    return EmptyTemplate;
                }

                var webPagePending = webPageMedia.WebPage as TLWebPagePending;
                if (webPagePending != null)
                {
                    return EmptyTemplate;
                }

                var webPage = webPageMedia.WebPage as TLWebPage;
                if (webPage != null)
                {
                    if (webPage.Photo != null
                        && webPage.Type != null)
                    {
                        if (webPage.Type != null)
                        {
                            var type = webPage.Type.ToString();
                            if (string.Equals(type, "photo", StringComparison.OrdinalIgnoreCase)
                                || string.Equals(type, "video", StringComparison.OrdinalIgnoreCase)
                                || (webPage.SiteName != null && string.Equals(webPage.SiteName.ToString(), "twitter", StringComparison.OrdinalIgnoreCase)))
                            {
                                return WebPagePhotoTemplate;
                            }
                        }

                        return WebPageSmallPhotoTemplate;
                    }
                }

                return WebPageTemplate;
            }

            return UnsupportedTemplate;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class MediaGridTemplateSelector : IValueConverter
    {
        public DataTemplate EmptyTemplate { get; set; }

        public DataTemplate PhotoTemplate { get; set; }

        public DataTemplate VideoTemplate { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var message = value as TLMessage;
            if (message == null)
            {
                return EmptyTemplate;
            }

            value = message.Media;

            var emptyMedia = value as TLMessageMediaEmpty;
            if (emptyMedia != null)
            {
                return EmptyTemplate;
            }

            var photoMedia = value as TLMessageMediaPhoto;
            if (photoMedia != null)
            {
                return PhotoTemplate;
            }

            var videoMedia = value as TLMessageMediaVideo;
            if (videoMedia != null)
            {
                return VideoTemplate;
            }

            return EmptyTemplate;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ReplyTemplateSelector : IValueConverter
    {
        public DataTemplate WebPageEmptyTemplate { get; set; }

        public DataTemplate WebPagePendingTemplate { get; set; }

        public DataTemplate WebPageTemplate { get; set; }

        public DataTemplate ForwardedMessagesTemplate { get; set; }

        public DataTemplate ForwardEmptyTemplate { get; set; }

        public DataTemplate ForwardTextTemplate { get; set; }

        public DataTemplate ForwardContactTemplate { get; set; }

        public DataTemplate ForwardPhotoTemplate { get; set; }

        public DataTemplate ForwardVideoTemplate { get; set; }

        public DataTemplate ForwardGeoPointTemplate { get; set; }

        public DataTemplate ForwardDocumentTemplate { get; set; }

        public DataTemplate ForwardStickerTemplate { get; set; }

        public DataTemplate ForwardAudioTemplate { get; set; }

        public DataTemplate ForwardUnsupportedTemplate { get; set; }


        public DataTemplate ReplyEmptyTemplate { get; set; }

        public DataTemplate ReplyLoadingTemplate { get; set; }

        public DataTemplate ReplyTextTemplate { get; set; }

        public DataTemplate ReplyContactTemplate { get; set; }

        public DataTemplate ReplyPhotoTemplate { get; set; }

        public DataTemplate ReplyVideoTemplate { get; set; }

        public DataTemplate ReplyGeoPointTemplate { get; set; }

        public DataTemplate ReplyDocumentTemplate { get; set; }

        public DataTemplate ReplyStickerTemplate { get; set; }

        public DataTemplate ReplyAudioTemplate { get; set; }

        public DataTemplate ReplyUnsupportedTemplate { get; set; }

        public DataTemplate ReplyServiceTextTemplate { get; set; }

        public DataTemplate ReplyServicePhotoTemplate { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return null;

            var info = value as ReplyInfo;
            if (info == null)
            {
                return ReplyUnsupportedTemplate;
            }

            if (info.Reply == null)
            {
                return ReplyLoadingTemplate;
            }

            var messagesContainter = info.Reply as TLMessagesContainter;
            if (messagesContainter != null)
            {
                return GetMessagesContainerTemplate(messagesContainter);
            }

            if (info.ReplyToMsgId == null || info.ReplyToMsgId.Value == 0)
            {
                return ReplyUnsupportedTemplate;
            }

            var messageService = info.Reply as TLMessageService;
            if (messageService != null)
            {
                value = messageService.Action;

                if (value is TLMessageActionChatAddUser)
                {
                    return ReplyServiceTextTemplate;
                }

                if (value is TLMessageActionChatCreate)
                {
                    return ReplyServiceTextTemplate;
                }

                if (value is TLMessageActionChatDeletePhoto)
                {
                    return ReplyServiceTextTemplate;
                }

                if (value is TLMessageActionChatDeleteUser)
                {
                    return ReplyServiceTextTemplate;
                }

                if (value is TLMessageActionChatEditPhoto)
                {
                    return ReplyServicePhotoTemplate;
                }

                if (value is TLMessageActionChatEditTitle)
                {
                    return ReplyServiceTextTemplate;
                }
            }

            var message = info.Reply as TLMessage;
            if (message != null)
            {
                if (!TLString.IsNullOrEmpty(message.Message) 
                    && (message.Media is TLMessageMediaEmpty || message.Media is TLMessageMediaWebPage))
                {
                    return ReplyTextTemplate;
                }

                value = message.Media;

                if (value is TLMessageMediaEmpty)
                {
                    return ReplyUnsupportedTemplate;
                }

                if (value is TLMessageMediaContact)
                {
                    return ReplyContactTemplate;
                }

                if (value is TLMessageMediaPhoto)
                {
                    return ReplyPhotoTemplate;
                }

                if (value is TLMessageMediaDocument)
                {
                    if (message.IsSticker())
                    {
                        return ReplyStickerTemplate;
                    }

                    return ReplyDocumentTemplate;
                }

                if (value is TLMessageMediaVideo)
                {
                    return ReplyVideoTemplate;
                }

                if (value is TLMessageMediaGeo)
                {
                    return ReplyGeoPointTemplate;
                }

                if (value is TLMessageMediaAudio)
                {
                    return ReplyAudioTemplate;
                }
            }

            var emptyMessage = info.Reply as TLMessageEmpty;
            if (emptyMessage != null)
            {
                return ReplyEmptyTemplate;
            }

            return ReplyUnsupportedTemplate;
        }

        private DataTemplate GetMessagesContainerTemplate(TLMessagesContainter container)
        {
            if (container.WebPageMedia != null)
            {
                var webPageMedia = container.WebPageMedia as TLMessageMediaWebPage;
                if (webPageMedia != null)
                {
                    var webPagePending = webPageMedia.WebPage as TLWebPagePending;
                    if (webPagePending != null)
                    {
                        return WebPagePendingTemplate;
                    }

                    var webPage = webPageMedia.WebPage as TLWebPage;
                    if (webPage != null)
                    {
                        return WebPageTemplate;
                    }

                    var webPageEmpty = webPageMedia.WebPage as TLWebPageEmpty;
                    if (webPageEmpty != null)
                    {
                        return WebPageEmptyTemplate;
                    }
                }
            }

            if (container.FwdMessages != null)
            {
                if (container.FwdMessages.Count == 1)
                {
                    var message = container.FwdMessages[0];
                    if (message != null)
                    {
                        var text = container.FwdMessages[0].Message;

                        if (!string.IsNullOrEmpty(text.ToString()))
                        {
                            return ForwardTextTemplate;
                        }

                        var media = container.FwdMessages[0].Media;
                        if (media != null)
                        {
                            var mediaPhoto = media as TLMessageMediaPhoto;
                            if (mediaPhoto != null)
                            {
                                return ForwardPhotoTemplate;
                            }

                            var mediaAudio = media as TLMessageMediaAudio;
                            if (mediaAudio != null)
                            {
                                return ForwardAudioTemplate;
                            }

                            var mediaDocument = media as TLMessageMediaDocument;
                            if (mediaDocument != null)
                            {
                                if (message.IsSticker())
                                {
                                    return ForwardStickerTemplate;
                                }

                                return ForwardDocumentTemplate;
                            }

                            var mediaVideo = media as TLMessageMediaVideo;
                            if (mediaVideo != null)
                            {
                                return ForwardVideoTemplate;
                            }

                            var mediaGeo = media as TLMessageMediaGeo;
                            if (mediaGeo != null)
                            {
                                return ForwardGeoPointTemplate;
                            }

                            var mediaContact = media as TLMessageMediaContact;
                            if (mediaContact != null)
                            {
                                return ForwardContactTemplate;
                            }

                            var mediaEmpty = media as TLMessageMediaEmpty;
                            if (mediaEmpty != null)
                            {
                                return ForwardEmptyTemplate;
                            }
                        }
                    }
                }

                return ForwardedMessagesTemplate;
            }

            return ReplyUnsupportedTemplate;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
