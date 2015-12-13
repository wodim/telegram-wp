using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Caliburn.Micro;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using TelegramClient.Resources;
using TelegramClient.Services;
using TelegramClient.ViewModels.Search;
using Execute = Telegram.Api.Helpers.Execute;

namespace TelegramClient.ViewModels.Dialogs
{
    public class ChooseDialogViewModel : ItemsViewModelBase<TLDialogBase>
    {
        public TLUserBase SharedContact { get; set; }

        public List<TLMessageBase> ForwardedMessages { get; set; }

        private string LogFileName { get; set; }

        private const int FirstSliceLength = 7;

        private readonly string _accessToken;

        private readonly TLUserBase _bot;

        private readonly Uri _webLink;

        private readonly string _url;

        private readonly string _text;

        public ChooseDialogViewModel(ICacheService cacheService, ICommonErrorHandler errorHandler, IStateService stateService, INavigationService navigationService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator) 
            : base(cacheService, errorHandler, stateService, navigationService, mtProtoService, eventAggregator)
        {
            EventAggregator.Subscribe(this);

            LogFileName = StateService.LogFileName;
            StateService.LogFileName = null;

            ForwardedMessages = StateService.ForwardMessages;
            StateService.ForwardMessages = null;

            SharedContact = StateService.SharedContact;
            StateService.SharedContact = null;

            _accessToken = StateService.AccessToken;
            StateService.AccessToken = null;

            _bot = StateService.Bot;
            StateService.Bot = null;

            _webLink = StateService.WebLink;
            StateService.WebLink = null;

            _url = StateService.Url;
            StateService.Url = null;

            _text = StateService.UrlText;
            StateService.UrlText = null;

            Status = AppResources.Loading;

            BeginOnThreadPool(() =>
            {
                var isAuthorized = SettingsHelper.GetValue<bool>(Constants.IsAuthorizedKey);
                if (isAuthorized)
                {
                    var dialogs = CacheService.GetDialogs();

                    var dialogsCache = new Dictionary<int, TLDialogBase>();
                    var clearedDialogs = new List<TLDialogBase>();
                    foreach (var dialog in dialogs)
                    {
                        if (!dialogsCache.ContainsKey(dialog.Index))
                        {
                            if (dialog is TLDialog || dialog is TLBroadcastDialog)
                            {
                                if (!SkipDialog(_bot, dialog))
                                {
                                    clearedDialogs.Add(dialog);
                                }
                                dialogsCache[dialog.Index] = dialog;
                            }
                        }
                        else
                        {
                            var cachedDialog = dialogsCache[dialog.Index];
                            if (cachedDialog.Peer is TLPeerUser && dialog.Peer is TLPeerUser)
                            {
                                CacheService.DeleteDialog(dialog);
                                continue;
                            }
                            if (cachedDialog.Peer is TLPeerChat && dialog.Peer is TLPeerChat)
                            {
                                CacheService.DeleteDialog(dialog);
                                continue;
                            }
                        }
                    }

                    BeginOnUIThread(() =>
                    {
                        foreach (var clearedDialog in clearedDialogs)
                        {
                            LazyItems.Add(clearedDialog);
                        }

                        var lastDialog = clearedDialogs.LastOrDefault(x => x.TopMessageId != null);
                        _maxId = lastDialog != null ? lastDialog.TopMessageId.Value : 0;

                        Status = LazyItems.Count == 0 ? AppResources.Loading : string.Empty;

                        for (var i = 0; i < LazyItems.Count && i < FirstSliceLength; i++)
                        {
                            Items.Add(LazyItems[i]);
                        }

                        BeginOnUIThread(TimeSpan.FromSeconds(0.5), () =>
                        {
                            for (var i = FirstSliceLength; i < LazyItems.Count; i++)
                            {
                                Items.Add(LazyItems[i]);
                            }
                            LazyItems.Clear();

                            LoadNextSlice();
                        });
                    });
                }
                
            });
        }

        public static bool SkipDialog(TLUserBase bot, TLDialogBase dialog)
        {
            if (bot != null && (!(dialog is TLDialog) || !(dialog.With is TLChat)))
            {
                return true;
            }

            return false;
        }

        public bool ChooseDialog(TLDialogBase dialog)
        {
            if (dialog == null) return false;

            if (ForwardedMessages != null)
            {
                var channel = dialog.With as TLChannel;
                if (channel != null && !channel.Creator && !channel.IsEditor)
                {
                    MessageBox.Show(AppResources.PostToChannelError, AppResources.Error, MessageBoxButton.OK);

                    return false;
                }
            }

            var result = MessageBoxResult.OK;
            if (LogFileName != null)
            {
                result = MessageBox.Show(AppResources.ForwardMessagesToThisChat, AppResources.Confirm, MessageBoxButton.OKCancel);
            }

            if (_bot != null)
            {
                var chat = dialog.With as TLChat;
                var userName = _bot as IUserName;
                if (chat == null)
                {
                    return false;
                }

                var botName = userName != null ? userName.UserName : _bot.FirstName;
                botName = TLString.IsNullOrEmpty(botName) ? _bot.LastName : botName;
                var chatName = chat.FullName;
                result = MessageBox.Show(string.Format(AppResources.AddUserToTheGroup, botName, chatName), AppResources.Confirm, MessageBoxButton.OKCancel);
            }
            
            if (result == MessageBoxResult.OK)
            {
                StateService.SharedContact = SharedContact;
                StateService.LogFileName = LogFileName;
                StateService.ForwardMessages = ForwardedMessages;
                StateService.With = dialog.With;
                StateService.RemoveBackEntries = true;
                StateService.AnimateTitle = true;
                StateService.AccessToken = _accessToken;
                StateService.Bot = _bot;
                StateService.WebLink = _webLink;
                StateService.Url = _url;
                StateService.UrlText = _text;
                NavigationService.UriFor<DialogDetailsViewModel>().Navigate();

                return true;
            }

            return false;
        }

        private int _maxId;

        public void LoadNextSlice()
        {
            if (LazyItems.Count > 0 || IsLastSliceLoaded || IsWorking)
            {
                return;
            }

            IsWorking = true;
            var offset = Items.Count;
            var limit = 30;
            MTProtoService.GetDialogsAsync(
#if LAYER_40
                new TLInt(offset), new TLInt(limit),
#else
                new TLInt(0), new TLInt(_maxId), new TLInt(limit),
#endif
                result => Execute.BeginOnUIThread(() =>
                {
                    var lastDialog = result.Dialogs.LastOrDefault(x => x.TopMessageId != null);
                    if (lastDialog != null)
                    {
                        _maxId = lastDialog.TopMessageId.Value;
                    }

                    var itemsAdded = 0;
                    foreach (var dialog in result.Dialogs)
                    {
                        if (!SkipDialog(_bot, dialog))
                        {
                            Items.Add(dialog);
                            itemsAdded++;
                        }
                    }

                    IsWorking = false;
                    IsLastSliceLoaded = result.Dialogs.Count < limit;
                    Status = LazyItems.Count > 0 || Items.Count > 0 ? string.Empty : Status;

                    if (itemsAdded < (Constants.DialogsSlice / 2))
                    {
                        LoadNextSlice();
                    }
                }),
                error => Execute.BeginOnUIThread(() =>
                {
                    IsWorking = false;
                    Status = string.Empty;
                }));
        }

        public void ForwardInAnimationComplete()
        {
            
        }

        public void Search()
        {
            StateService.SharedContact = SharedContact;
            StateService.LogFileName = LogFileName;
            StateService.ForwardMessages = ForwardedMessages;
            StateService.AccessToken = _accessToken;
            StateService.Bot = _bot;
            StateService.WebLink = _webLink;
            StateService.Url = _url;
            StateService.UrlText = _text;
            NavigationService.UriFor<SearchShellViewModel>().Navigate();
        }
    }
}
