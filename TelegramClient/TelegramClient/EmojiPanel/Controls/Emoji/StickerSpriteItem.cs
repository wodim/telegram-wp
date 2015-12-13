using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Telegram.Api.TL;
using Telegram.Controls.VirtualizedView;
using TelegramClient.Converters;
using TelegramClient.Views.Dialogs;

namespace Telegram.EmojiPanel.Controls.Emoji
{
    class StickerSpriteItem : VListItemBase
    {
//#if DEBUG
//        ~StickerSpriteItem()
//        {
//            Api.Helpers.Execute.BeginOnUIThread(() => MessageBox.Show("Dispose"));
//        }
//#endif

        public override double FixedHeight
        {
            get { return 120.0; }
            set { }
        }

        private readonly double _stickerHeight = 96.0;

        public double StickerHeight
        {
            get { return _stickerHeight; }
        }

        public StickerSpriteItem(int columns, IList<TLStickerItem> stickers, double stickerHeight, double panelWidth, bool showEmoji = false)
        {
            _stickerHeight = stickerHeight;

            var panelMargin = new Thickness(4.0, 0.0, 4.0, 0.0);
            var panelActualWidth = panelWidth - panelMargin.Left - panelMargin.Right;
            //472, 438
            var stackPanel = new Grid{ Width = panelActualWidth, Margin = panelMargin, Background = new SolidColorBrush(Colors.Transparent) };
            for (var i = 0; i < columns; i++)
            {
                stackPanel.ColumnDefinitions.Add(new ColumnDefinition());
            }

            for (var i = 0; i < stickers.Count; i++)
            {
                var binding = new Binding
                {
                    Mode = BindingMode.OneWay,
                    Path = new PropertyPath("Self"),
                    Converter = new DefaultPhotoConverter(),
                    ConverterParameter = StickerHeight
                };

                var stickerImage = new Image
                {
                    Height = StickerHeight,
                    Margin = new Thickness(0, 12, 0, 12),
                    VerticalAlignment = VerticalAlignment.Top,
                };
                stickerImage.SetBinding(Image.SourceProperty, binding);

                var grid = new Grid();
                grid.Children.Add(stickerImage);

                if (showEmoji)
                {
                    var document22 = stickers[i].Document as TLDocument22;
                    if (document22 != null)
                    {
                        var bytes = Encoding.BigEndianUnicode.GetBytes(document22.Emoticon);
                        var bytesStr = BrowserNavigationService.ConvertToHexString(bytes);

                        var emojiImage = new Image
                        {
                            Height = 32,
                            Width = 32,
                            Margin = new Thickness(12, 12, 12, 12),
                            HorizontalAlignment = HorizontalAlignment.Right,
                            VerticalAlignment = VerticalAlignment.Bottom,
                            Source = new BitmapImage(new Uri(string.Format("/Assets/Emoji/Separated/{0}.png", bytesStr), UriKind.RelativeOrAbsolute))
                        };
                        grid.Children.Add(emojiImage);
                    }
                }

                var listBoxItem = new ListBoxItem {Content = grid, DataContext = stickers[i]};
                Microsoft.Phone.Controls.TiltEffect.SetIsTiltEnabled(listBoxItem, true);
                listBoxItem.Tap += Sticker_OnTap;

                Grid.SetColumn(listBoxItem, i);
                stackPanel.Children.Add(listBoxItem);
            }

            Children.Add(stackPanel);

            View.Width = panelWidth;
        }

        public event EventHandler<StickerSelectedEventArgs> StickerSelected;

        protected virtual void RaiseStickerSelected(StickerSelectedEventArgs e)
        {
            var handler = StickerSelected;
            if (handler != null) handler(this, e);
        }

        private void Sticker_OnTap(object sender, GestureEventArgs e)
        {
            var sticker = ((FrameworkElement) sender).DataContext as TLStickerItem;
            if (sticker == null) return;

            RaiseStickerSelected(new StickerSelectedEventArgs {Sticker = sticker});
        }
    }

    public class StickerSelectedEventArgs : EventArgs
    {
        public TLStickerItem Sticker { get; set; }
    }
}
