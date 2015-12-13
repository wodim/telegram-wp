using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Coding4Fun.Toolkit.Controls;
using Microsoft.Phone.Controls;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using Telegram.Controls.VirtualizedView;
using Telegram.EmojiPanel.Controls.Emoji;
using Telegram.EmojiPanel.Controls.Utilites;
using TelegramClient.Converters;
using TelegramClient.Resources;
using TelegramClient.Views.Dialogs;
using GestureEventArgs = System.Windows.Input.GestureEventArgs;

namespace TelegramClient.Views.Additional
{
    public partial class StickersView
    {
        public StickersView()
        {
            InitializeComponent();
        }

        private void StickerSet_OnTap(object sender, GestureEventArgs e)
        {
            OpenStickerSet(sender, e);
        }

        private void OpenStickerSet(object sender, GestureEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element == null) return;

            var stickerSet = element.DataContext as TLStickerSet;
            if (stickerSet == null) return;

            var scrollViewer = new ScrollViewer();
            scrollViewer.Height = 400.0;
            var panel = new MyVirtualizingPanel{ VerticalAlignment = VerticalAlignment.Top };
            scrollViewer.Content = panel;
            panel.InitializeWithScrollViewer(scrollViewer);
            var sprites = CreateStickerSetSprites(stickerSet);
            const int firstSliceLength = 4;

            var messagePrompt = new MessagePrompt();
            messagePrompt.Title = stickerSet.Title.ToString();
            messagePrompt.VerticalAlignment = VerticalAlignment.Center;
            messagePrompt.Message = (string)new StickerSetToCountStringConverter().Convert(stickerSet, null, null, null);
            messagePrompt.Body = new TextBlock
            {
                Height = scrollViewer.Height,
                Text = AppResources.Loading,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Style = (Style) Application.Current.Resources["PhoneTextGroupHeaderStyle"]
            };
            messagePrompt.IsCancelVisible = false;
            messagePrompt.IsAppBarVisible = true;
            messagePrompt.Opened += (o, args) =>
            {
                Execute.BeginOnUIThread(TimeSpan.FromSeconds(0.1), () =>
                {
                    messagePrompt.Body = scrollViewer;
                    panel.AddItems(sprites.Take(firstSliceLength).Cast<VListItemBase>());
                    Execute.BeginOnUIThread(() =>
                    {
                        panel.AddItems(sprites.Skip(firstSliceLength).Cast<VListItemBase>());
                    });
                });
            };
            messagePrompt.Show();
        }

        private static List<StickerSpriteItem> CreateStickerSetSprites(TLStickerSet stickerSet)
        {
            if (stickerSet == null) return null;

            const int stickersPerRow = 4;
            var sprites = new List<StickerSpriteItem>();
            var stickers = new List<TLStickerItem>();
            for (var i = 1; i <= stickerSet.Stickers.Count; i++)
            {
                stickers.Add((TLStickerItem)stickerSet.Stickers[i - 1]);

                if (i % stickersPerRow == 0 || i == stickerSet.Stickers.Count)
                {
                    var item = new StickerSpriteItem(stickersPerRow, stickers, 96.0, 438.0, true);
                    sprites.Add(item);
                    stickers.Clear();
                }
            }

            return sprites;
        }

        private void Remove_OnLoaded(object sender, RoutedEventArgs e)
        {
            var menuItem = (MenuItem) sender;
            if (menuItem != null)
            {
                var stickerSet = menuItem.DataContext as TLStickerSet32;
                if (stickerSet != null)
                {
                    menuItem.Visibility = stickerSet.IsOfficial()
                        ? Visibility.Collapsed
                        : Visibility.Visible;
                }
            }
        }
    }
}