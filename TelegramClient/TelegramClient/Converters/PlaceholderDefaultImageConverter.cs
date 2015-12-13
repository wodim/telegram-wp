using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;
using Telegram.Api;
using Telegram.Api.TL;

namespace TelegramClient.Converters
{
    public class PlaceholderDefaultImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return null;

            var user = value as TLUserBase;
            if (user != null && user.Index == 333000)
            {
#if WP81
                return new Uri("/ApplicationIcon106.png", UriKind.Relative);
#elif WP8
                return new Uri("/ApplicationIcon210.png", UriKind.Relative);
#endif

                return new Uri("/ApplicationIcon99.png", UriKind.Relative);
            }

            if (value is TLBroadcastChat)
            {
                return new Uri("/Images/Placeholder/placeholder.broadcast.png", UriKind.Relative);
            }

            return value is TLChatBase
                ? new Uri("/Images/Placeholder/placeholder.group.transparent-WXGA.png", UriKind.Relative)
                : new Uri("/Images/Placeholder/placeholder.user.transparent-WXGA.png", UriKind.Relative);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class PlaceholderDefaultTextConverter : IValueConverter
    {
        public static string GetText(TLObject value)
        {
            if (value == null) return null;

            var word1 = string.Empty;
            var word2 = string.Empty;

            var user = value as TLUserBase;
            if (user != null)
            {
                word1 = user.FirstName.ToString();
                word2 = user.LastName.ToString();
            }

            var broadcast = value as TLBroadcastChat;
            if (broadcast != null)
            {
                var words = broadcast.FullName.Trim().Split(' ');

                if (words.Length > 0)
                {
                    if (words.Length == 1)
                    {
                        var si = StringInfo.GetTextElementEnumerator(broadcast.FullName);

                        word1 = si.MoveNext() ? si.GetTextElement() : string.Empty;
                        word2 = si.MoveNext() ? si.GetTextElement() : string.Empty;
                    }
                    else
                    {
                        word1 = words[0];
                        word2 = words[words.Length - 1];
                    }
                }
            }

            var chat = value as TLChatBase;
            if (chat != null)
            {
                var words = chat.FullName.Trim().Split(' ');

                if (words.Length > 0)
                {
                    if (words.Length == 1)
                    {
                        var si = StringInfo.GetTextElementEnumerator(chat.FullName);

                        word1 = si.MoveNext() ? si.GetTextElement() : string.Empty;
                        word2 = si.MoveNext() ? si.GetTextElement() : string.Empty;
                    }
                    else
                    {
                        word1 = words[0];
                        word2 = words[words.Length - 1];
                    }
                }
            }

            var si1 = StringInfo.GetTextElementEnumerator(word1);
            var si2 = StringInfo.GetTextElementEnumerator(word2);

            word1 = si1.MoveNext() ? si1.GetTextElement() : string.Empty;
            word2 = si2.MoveNext() ? si2.GetTextElement() : string.Empty;

            return string.Format("{0}{1}", word1, word2).Trim().ToUpperInvariant();
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return GetText(value as TLObject);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class LinkDefaultTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return null;

            var firstLetter = string.Empty;

            var message = value as TLMessage;
            if (message == null) return null;

            var links = message.Links;
            if (links != null && links.Count > 0)
            {
                firstLetter = GetFirstLetter(links[0]);
            }
            else
            {
                var mediaWebPage = message.Media as TLMessageMediaWebPage;
                if (mediaWebPage != null)
                {
                    var webPage = mediaWebPage.WebPage as TLWebPage;
                    if (webPage != null)
                    {
                        if (!TLString.IsNullOrEmpty(webPage.DisplayUrl))
                        {
                            firstLetter = GetFirstLetter(webPage.DisplayUrl.ToString());
                        }
                    }
                }
            }

            return firstLetter;
        }

        private static string GetFirstLetter(string url)
        {
            url = url.Replace("http://", string.Empty);
            url = url.Replace("https://", string.Empty);
            url = url.Replace("www.", string.Empty);

            var si = StringInfo.GetTextElementEnumerator(url);
            var word1 = si.MoveNext() ? si.GetTextElement() : string.Empty;

            return word1.Trim().ToUpperInvariant();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
