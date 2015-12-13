using System;
using System.Collections.Generic;
using System.Windows;
using Caliburn.Micro;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.FileManager;
using Telegram.Api.TL;
using Telegram.EmojiPanel;
using Telegram.EmojiPanel.Controls.Emoji;
using TelegramClient.Resources;
using TelegramClient.Services;
using TelegramClient.Views;
using TelegramClient.Views.Dialogs;
using Execute = Telegram.Api.Helpers.Execute;

namespace TelegramClient.ViewModels.Additional
{
    public class StickersViewModel : ItemsViewModelBase<TLStickerSetBase>, Telegram.Api.Aggregator.IHandle<DownloadableItem>
    {
        public StickersViewModel(ICacheService cacheService, ICommonErrorHandler errorHandler, IStateService stateService, INavigationService navigationService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator) 
            : base(cacheService, errorHandler, stateService, navigationService, mtProtoService, eventAggregator)
        {
            EventAggregator.Subscribe(this);

            Status = AppResources.Loading;

            UpdateAllStickersAsync();
        }


        protected override void OnActivate()
        {
            BrowserNavigationService.MentionNavigated += OnMentionNavigated;

            base.OnActivate();
        }

        protected override void OnDeactivate(bool close)
        {
            BrowserNavigationService.MentionNavigated -= OnMentionNavigated;

            base.OnDeactivate(close);
        }

        private void OnMentionNavigated(object sender, TelegramMentionEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Mention))
            {
                var usernameStartIndex = e.Mention.LastIndexOf("@", StringComparison.OrdinalIgnoreCase);
                if (usernameStartIndex != -1)
                {
                    var username = e.Mention.Substring(usernameStartIndex).TrimStart('@');

                    if (!string.IsNullOrEmpty(username))
                    {
                        TelegramViewBase.NavigateToUsername(MTProtoService, username, string.Empty);
                    }
                }
            }
            else if (e.UserId >= 0)
            {
                var user = CacheService.GetUser(new TLInt(e.UserId));
                if (user != null)
                {
                    TelegramViewBase.NavigateToUser(user, null, PageKind.Profile);
                }
            }
            else if (e.ChatId >= 0)
            {
                var chat = CacheService.GetChat(new TLInt(e.ChatId));
                if (chat != null)
                {
                    TelegramViewBase.NavigateToChat(chat);
                }
            }
            else if (e.ChannelId >= 0)
            {
                var channel = CacheService.GetChat(new TLInt(e.ChatId)) as TLChannel;
                if (channel != null)
                {
                    TelegramViewBase.NavigateToChat(channel);
                }
            }
        }

        private readonly Dictionary<string, TLVector<TLObject>> _stickerSets = new Dictionary<string, TLVector<TLObject>>();

        private readonly Dictionary<long, string> _emoticons = new Dictionary<long, string>(); 


        private void UpdateAllStickersAsync()
        {
            BeginOnThreadPool(() =>
            {
                StateService.GetAllStickersAsync(cachedStickers =>
                {
                    IsWorking = true;
                    MTProtoService.GetAllStickersAsync(TLString.Empty,
                        result => Execute.BeginOnUIThread(() =>
                        {
                            Status = string.Empty;
                            IsWorking = false;

                            Items.Clear();

                            var allStickers = result as TLAllStickers29;
                            if (allStickers != null)
                            {
                                var cachedStickers29 = cachedStickers as TLAllStickers29;
                                if (cachedStickers29 != null)
                                {
                                    allStickers.IsDefaultSetVisible = cachedStickers29.IsDefaultSetVisible;
                                    allStickers.RecentlyUsed = cachedStickers29.RecentlyUsed;
                                    allStickers.Date = TLUtils.DateToUniversalTimeTLInt(0, DateTime.Now);
                                }

                                cachedStickers = allStickers;
                                StateService.SaveAllStickersAsync(cachedStickers);

                                _emoticons.Clear();
                                _stickerSets.Clear();

                                for (var i = 0; i < allStickers.Packs.Count; i++)
                                {
                                    var emoticon = allStickers.Packs[i].Emoticon.ToString();
                                    foreach (var document in allStickers.Packs[i].Documents)
                                    {
                                        _emoticons[document.Value] = emoticon;
                                    }
                                }

                                for (var i = 0; i < allStickers.Documents.Count; i++)
                                {
                                    var document22 = allStickers.Documents[i] as TLDocument22;
                                    if (document22 != null)
                                    {
                                        string emoticon;
                                        if (_emoticons.TryGetValue(document22.Id.Value, out emoticon))
                                        {
                                            document22.Emoticon = emoticon;
                                        }

                                        if (document22.StickerSet != null)
                                        {
                                            var setId = document22.StickerSet.Name;
                                            TLVector<TLObject> stickers;
                                            if (_stickerSets.TryGetValue(setId, out stickers))
                                            {
                                                stickers.Add(new TLStickerItem { Document = document22 });
                                            }
                                            else
                                            {
                                                _stickerSets[setId] = new TLVector<TLObject> { new TLStickerItem { Document = document22 } };
                                            }
                                        }
                                    }
                                }

                                var firstChunkSize = 10;
                                var count = 0;
                                var delayedItems = new List<TLStickerSetBase>();
                                for (var i = 0; i < allStickers.Sets.Count; i++)
                                {
                                    var set = allStickers.Sets[i];
                                    var setName = set.Id.ToString();
                                    TLVector<TLObject> stickers;
                                    if (_stickerSets.TryGetValue(setName, out stickers))
                                    {
                                        set.Stickers = stickers;
                                        if (set.Stickers.Count > 0)
                                        {
                                            if (count < firstChunkSize)
                                            {
                                                Items.Add(set);
                                                count++;
                                            }
                                            else
                                            {
                                                delayedItems.Add(set);
                                            }
                                        }
                                    }
                                }

                                BeginOnUIThread(TimeSpan.FromSeconds(0.5), () =>
                                {
                                    foreach (var set in delayedItems)
                                    {
                                        Items.Add(set);
                                    }
                                });
                            }
                        }),
                        error => BeginOnUIThread(() =>
                        {
                            Status = string.Empty;
                            IsWorking = false;
                            Execute.ShowDebugMessage("messages.getAllStickers error " + error);
                        }));
                });
            });
        }

        public void Remove(TLStickerSet set)
        {
            if (set == null) return;
            var inputSet = new TLInputStickerSetId { Id = set.Id, AccessHash = set.AccessHash };

            IsWorking = true;
            MTProtoService.UninstallStickerSetAsync(inputSet,
                result => Execute.BeginOnUIThread(() =>
                {
                    IsWorking = false;
                    Items.Remove(set);
                    var emojiControl = EmojiControl.GetInstance();
                    emojiControl.RemoveStickerSet(inputSet);
                }),
                error => Execute.BeginOnUIThread(() =>
                {
                    IsWorking = false;
                    Execute.ShowDebugMessage("messages.uninstallStickerSet error " + error);
                }));
        }

        public void CopyLink(TLStickerSet set)
        {
            if (set == null) return;

            var shortName = set.ShortName.ToString();
            if (string.IsNullOrEmpty(shortName)) return;

            var addStickersLink = string.Format(Constants.AddStickersLinkPlaceholder, shortName);

            Clipboard.SetText(addStickersLink);
            MTProtoService.SetMessageOnTime(2.0, AppResources.LinkCopiedToClipboard);
        }

        public void Share(TLStickerSet set)
        {
            if (set == null) return;

            var shortName = set.ShortName.ToString();
            if (string.IsNullOrEmpty(shortName)) return;

            var addStickersLink = string.Format(Constants.AddStickersLinkPlaceholder, shortName);

            StateService.ShareLink = addStickersLink;
            StateService.ShareMessage = addStickersLink;
            StateService.ShareCaption = AppResources.Share;
            NavigationService.UriFor<ShareViewModel>().Navigate();
        }

        public void Handle(DownloadableItem item)
        {
            var sticker = item.Owner as TLStickerItem;
            if (sticker != null)
            {
                sticker.NotifyOfPropertyChange(() => sticker.Self);
            }
        }
    }
}
