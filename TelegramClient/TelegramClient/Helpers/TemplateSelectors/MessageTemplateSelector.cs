using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Telegram.Api.TL;
using Telegram.Controls;

namespace TelegramClient.Helpers.TemplateSelectors
{
    public class DecryptedMessageTemplateSelector : ITemplateSelector, IValueConverter
    {
        public DecryptedMessageTemplateSelector()
        {
            _templatesCache.Add(typeof(TLDecryptedMessageService), m => ServiceMessageTemplate ?? EmptyMessageTemplate);
            _templatesCache.Add(typeof(TLDecryptedMessage), GenerateCommonMessageTemplate); 
            _templatesCache.Add(typeof(TLDecryptedMessageService17), m => ServiceMessageTemplate ?? EmptyMessageTemplate);
            _templatesCache.Add(typeof(TLDecryptedMessage17), GenerateCommonMessageTemplate);
        }

        private DataTemplate GenerateCommonMessageTemplate(TLDecryptedMessageBase message)
        {
            if (message != null)
            {
                if (message.IsSticker())
                {
                    if (message.Out.Value)
                    {
                        return UserStickerTemplate ?? EmptyMessageTemplate;
                    }

                    return FriendStickerTemplate ?? EmptyMessageTemplate;
                }

                if (message.Out.Value)
                {
                    return UserMessageTemplate ?? EmptyMessageTemplate;
                }

                return FriendMessageTemplate ?? EmptyMessageTemplate;
            }

            return EmptyMessageTemplate;
        }

        private readonly Dictionary<Type, Func<TLDecryptedMessageBase, DataTemplate>> _templatesCache = new Dictionary<Type, Func<TLDecryptedMessageBase, DataTemplate>>();

        protected DataTemplate EmptyMessageTemplate = new DataTemplate();

        public DataTemplate UserMessageTemplate { get; set; }

        public DataTemplate UserStickerTemplate { get; set; }

        public DataTemplate FriendMessageTemplate { get; set; }

        public DataTemplate FriendStickerTemplate { get; set; }

        public DataTemplate ServiceMessageTemplate { get; set; }

        public DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            var message = item as TLDecryptedMessageBase;
            if (message != null)
            {
                Func<TLDecryptedMessageBase, DataTemplate> dataTemplateFunc;
                if (_templatesCache.TryGetValue(message.GetType(), out dataTemplateFunc))
                {
                    return dataTemplateFunc.Invoke(message);
                }

                return EmptyMessageTemplate;
            }

            return EmptyMessageTemplate;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return SelectTemplate(value, null);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class MessageTemplateSelector : ITemplateSelector, IValueConverter
    {
        public MessageTemplateSelector()
        {
            _templatesCache.Add(typeof(TLMessageService), GenerateServiceMessageTemplate);
            _templatesCache.Add(typeof(TLMessageEmpty), m => EmptyMessageTemplate);
            _templatesCache.Add(typeof(TLMessage), GenerateCommonMessageTemplate);
            _templatesCache.Add(typeof(TLMessageForwarded), GenerateCommonMessageTemplate);

            _templatesCache.Add(typeof(TLMessageService17), GenerateServiceMessageTemplate);
            _templatesCache.Add(typeof(TLMessageService40), GenerateServiceMessageTemplate);
            _templatesCache.Add(typeof(TLMessage17), GenerateCommonMessageTemplate);
            _templatesCache.Add(typeof(TLMessageForwarded17), GenerateCommonMessageTemplate);

            _templatesCache.Add(typeof(TLMessage25), GenerateCommonMessageTemplate);
            _templatesCache.Add(typeof(TLMessage31), GenerateCommonMessageTemplate);
            _templatesCache.Add(typeof(TLMessage34), GenerateCommonMessageTemplate);
            _templatesCache.Add(typeof(TLMessage36), GenerateCommonMessageTemplate);
            _templatesCache.Add(typeof(TLMessage40), GenerateCommonMessageTemplate);

        }

        private DataTemplate GenerateServiceMessageTemplate(TLMessageBase message)
        {
            var messageService = message as TLMessageService;
            if (messageService != null)
            {
                var unreadMessagesAction = messageService.Action as TLMessageActionUnreadMessages;
                if (unreadMessagesAction != null)
                {
                    return UnreadMessagesTemplate ?? ServiceMessageTemplate ?? EmptyMessageTemplate;
                }

                return ServiceMessageTemplate ?? EmptyMessageTemplate;
            }

            return EmptyMessageTemplate;
        }

        private DataTemplate GenerateCommonMessageTemplate(TLMessageBase m)
        {
            var messageCommon = m as TLMessageCommon;
            if (messageCommon != null)
            {
                var message40 = messageCommon as TLMessage40;
                //if (message40 != null && message40.Views != null && message40.Views.Value > 0)
                if (message40 != null && (message40.FromId == null || message40.FromId.Value < 0))
                {
                    if (message40.IsSticker())
                    {
                        return ChatStickerTemplate;
                    }

                    return ChatMessageTemplate;
                }

                if (messageCommon.IsSticker())
                {
                    if (messageCommon.Out.Value)
                    {
                        return UserStickerTemplate ?? EmptyMessageTemplate;
                    }

                    if (messageCommon.ToId is TLPeerChat || messageCommon.ToId is TLPeerBroadcast)
                    {
                        return ChatFriendStickerTemplate ?? EmptyMessageTemplate;
                    }

                    return FriendStickerTemplate ?? EmptyMessageTemplate;
                }
                else
                {
                    if (messageCommon.Out.Value)
                    {
                        return UserMessageTemplate ?? EmptyMessageTemplate;
                    }

                    if (messageCommon.ToId is TLPeerChat || messageCommon.ToId is TLPeerBroadcast || messageCommon.ToId is TLPeerChannel)
                    {
                        return ChatFriendMessageTemplate ?? EmptyMessageTemplate;
                    }

                    return FriendMessageTemplate ?? EmptyMessageTemplate;
                }

                return EmptyMessageTemplate;
            }

            return EmptyMessageTemplate;
        }

        private readonly Dictionary<Type, Func<TLMessageBase, DataTemplate>> _templatesCache = new Dictionary<Type, Func<TLMessageBase, DataTemplate>>();

        protected DataTemplate EmptyMessageTemplate = new DataTemplate();

        public DataTemplate UserMessageTemplate { get; set; }

        public DataTemplate FriendMessageTemplate { get; set; }

        public DataTemplate ChatFriendMessageTemplate { get; set; }

        public DataTemplate UserStickerTemplate { get; set; }

        public DataTemplate FriendStickerTemplate { get; set; }

        public DataTemplate ChatFriendStickerTemplate { get; set; }

        public DataTemplate ServiceMessageTemplate { get; set; }

        public DataTemplate UnreadMessagesTemplate { get; set; }

        public DataTemplate ChatMessageTemplate { get; set; }

        public DataTemplate ChatStickerTemplate { get; set; }

        public DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            var message = item as TLMessageBase;
            if (message != null)
            {
                Func<TLMessageBase, DataTemplate> dataTemplateFunc;
                if (_templatesCache.TryGetValue(message.GetType(), out dataTemplateFunc))
                {
                    return dataTemplateFunc.Invoke(message);
                }

                return EmptyMessageTemplate;
            }

            return EmptyMessageTemplate;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return SelectTemplate(value, null);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
