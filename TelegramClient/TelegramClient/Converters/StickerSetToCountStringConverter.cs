using System;
using System.Globalization;
using System.Windows.Data;
using Telegram.Api.TL;
using TelegramClient.Resources;

namespace TelegramClient.Converters
{
    public class StickerSetToCountStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var set = value as TLStickerSet;
            if (set != null)
            {
                var stickers = set.Stickers;
                if (stickers != null)
                {
                    return Utils.Language.Declension(
                        stickers.Count,
                        AppResources.StickerNominativeSingular,
                        AppResources.StickerNominativePlural,
                        AppResources.StickerGenitiveSingular,
                        AppResources.StickerGenitivePlural).ToLower(CultureInfo.CurrentUICulture);
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
