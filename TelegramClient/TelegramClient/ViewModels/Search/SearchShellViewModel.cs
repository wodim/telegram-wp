using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using Caliburn.Micro;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using TelegramClient.Resources;
using TelegramClient.Services;
using TelegramClient.ViewModels.Dialogs;

namespace TelegramClient.ViewModels.Search
{
    public class SearchShellViewModel : Conductor<ISearch>.Collection.OneActive
    {
        private string _text;

        public string Text
        {
            get { return _text; }
            set
            {
                if (value != _text)
                {
                    _text = value;
                    NotifyOfPropertyChange(() => ShowRecent);
                    Search();
                }
            }
        }

        public string SearchHint { get; set; }

        public SearchDialogsViewModel SearchDialogs { get; protected set; }

        public SearchMessagesViewModel SearchMessages { get; protected set; }

        private readonly IStateService _stateService;

        private readonly INavigationService _navigationService;

        private readonly string _hashtag;

        private static TLVector<TLResultInfo> _recentResults; 

        public ObservableCollection<TLObject> Recent { get; set; }

        public bool ShowRecent
        {
            get { return Recent.Count > 0 && string.IsNullOrEmpty(Text); }
        }

        private readonly ICacheService _cacheService;

        private readonly object _recentSyncRoot = new object();

        private readonly TLUserBase _bot;

        private readonly List<TLMessageBase> _forwardMessages;

        private readonly TLUserBase _sharedContact;

        private readonly string _accessToken;

        private readonly string _logFileName;

        private readonly Uri _webLink;

        private readonly string _url;

        private readonly string _urlText;

        public SearchShellViewModel(ICacheService cacheService, IStateService stateService, INavigationService navigationService, SearchDialogsViewModel searchDialogs, SearchMessagesViewModel searchMessages)
        {
            _cacheService = cacheService;

            Recent = new ObservableCollection<TLObject>();

            var forwardMessages = stateService.ForwardMessages;
            var sharedContact = stateService.SharedContact;
            var logFileName = stateService.LogFileName;
            var accessToken = stateService.AccessToken;
            var bot = stateService.Bot;
            var webLink = stateService.WebLink;
            var url = stateService.Url;
            var urlText = stateService.UrlText;
            var loadedDialogs = stateService.LoadedDialogs;
            stateService.ForwardMessages = null;
            stateService.LogFileName = null;
            stateService.SharedContact = null;
            stateService.AccessToken = null;
            stateService.Bot = null;
            stateService.WebLink = null;
            stateService.Url = null;
            stateService.UrlText = null;
            stateService.LoadedDialogs = null;

            _forwardMessages = forwardMessages;
            _sharedContact = sharedContact;
            _logFileName = logFileName;
            _accessToken = accessToken;
            _bot = bot;
            _webLink = webLink;
            _url = url;
            _urlText = urlText;

            SearchDialogs = searchDialogs;
            SearchDialogs.ChatsOnly = bot != null;
            SearchDialogs.LoadedDilaogs = loadedDialogs;
            SearchDialogs.OpenDialogDetailsAction = OpenDialogDetails;

            SearchMessages = searchMessages;
            SearchMessages.ForwardMessages = forwardMessages;

            _stateService = stateService;
            _navigationService = navigationService;

            if (_stateService.Hashtag != null)
            {
                _hashtag = _stateService.Hashtag;
                _stateService.Hashtag = null;
            }

            SearchHint = GetSearchHint();
        }

        public void OpenDialogDetails(TLObject with)
        {
            if (with == null) return;


            if (_forwardMessages != null)
            {
                var channel = with as TLChannel;
                if (channel != null && !channel.Creator && !channel.IsEditor)
                {
                    MessageBox.Show(AppResources.PostToChannelError, AppResources.Error, MessageBoxButton.OK);

                    return;
                }
            }

            var result = MessageBoxResult.OK;
            //if (ForwardMessage != null || ForwardMessages != null)
            //{
            //    result = MessageBox.Show(AppResources.ForwardMessagesToThisChat, AppResources.Confirm, MessageBoxButton.OKCancel);
            //}

            if (_bot != null)
            {
                var chat = with as TLChat;
                var userName = _bot as IUserName;
                if (chat == null)
                {
                    return;
                }

                var botName = userName != null ? userName.UserName : _bot.FirstName;
                botName = TLString.IsNullOrEmpty(botName) ? _bot.LastName : botName;
                var chatName = chat.FullName;
                result = MessageBox.Show(string.Format(AppResources.AddUserToTheGroup, botName, chatName), AppResources.Confirm, MessageBoxButton.OKCancel);
            }

            if (result != MessageBoxResult.OK)
            {
                return;
            }

            _stateService.RemoveBackEntry = true;
            _stateService.With = with;
            _stateService.ForwardMessages = _forwardMessages;
            if (_forwardMessages != null || _logFileName != null || _sharedContact != null)
            {
                _stateService.RemoveBackEntries = true;
            }
            _stateService.LogFileName = _logFileName;
            _stateService.SharedContact = _sharedContact;
            _stateService.AccessToken = _accessToken;
            _stateService.Bot = _bot;
            _stateService.WebLink = _webLink;
            _stateService.Url = _url;
            _stateService.UrlText = _urlText;
            _stateService.AnimateTitle = true;
            _navigationService.UriFor<DialogDetailsViewModel>().Navigate();

            SaveRecent(with);
        }

        public void ClearRecent()
        {
            Recent.Clear();
            NotifyOfPropertyChange(() => ShowRecent);

            Telegram.Api.Helpers.Execute.BeginOnThreadPool(() =>
            {
                var recentResults = new TLVector<TLResultInfo>();
                _recentResults = recentResults;
                TLUtils.SaveObjectToMTProtoFile(_recentSyncRoot, Constants.RecentSearchResultsFileName, recentResults);
            });
        }

        public void SaveRecent(TLObject with)
        {
            Telegram.Api.Helpers.Execute.BeginOnThreadPool(() =>
            {
                var recentResults = _recentResults ?? new TLVector<TLResultInfo>();

                var id = GetId(with);
                var type = GetType(with);
                if (id == null || type == null) return;

                var isAdded = false;
                for (var i = 0; i < recentResults.Count; i++)
                {
                    var recentResult = recentResults[i];

                    if (recentResults[i].Id.Value == id.Value
                        && recentResults[i].Type.ToString() == type.ToString())
                    {
                        recentResults[i].Count = new TLLong(recentResults[i].Count.Value + 1);

                        var newPosition = i;
                        for (var j = i - 1; j >= 0; j--)
                        {
                            if (recentResults[j].Count.Value <= recentResults[i].Count.Value)
                            {
                                newPosition = j;
                            }
                        }

                        if (i != newPosition)
                        {
                            recentResults.RemoveAt(i);
                            recentResults.Insert(newPosition, recentResult);
                        }
                        isAdded = true;
                        break;
                    }
                }

                if (!isAdded)
                {
                    var recentResult = new TLResultInfo
                    {
                        Id = id,
                        Type = type,
                        Count = new TLLong(1)
                    };

                    for (var i = 0; i < recentResults.Count; i++)
                    {
                        if (recentResults[i].Count.Value <= 1)
                        {
                            recentResults.Insert(i, recentResult);
                            isAdded = true;
                            break;
                        }
                    }

                    if (!isAdded)
                    {
                        recentResults.Add(recentResult);
                    }
                }

                _recentResults = recentResults;

                TLUtils.SaveObjectToMTProtoFile(_recentSyncRoot, Constants.RecentSearchResultsFileName, recentResults);
            });
        }

        private static TLString GetType(TLObject with)
        {
            return with is TLUserBase ? new TLString("user") : new TLString("chat");
        }

        private static TLInt GetId(TLObject with)
        {
            var user = with as TLUserBase;
            if (user != null)
            {
                return user.Id;
            }
            var chat = with as TLChatBase;
            if (chat != null)
            {
                return chat.Id;
            }

            return null;
        }

        private string GetSearchHint()
        {
            if (SearchDialogs != null && (_forwardMessages != null || _webLink != null))
            {
                return AppResources.SearchAmongYourChats;
            }

            if (_hashtag != null)
            {
                return AppResources.SearchMessages;
            }

            return AppResources.SearchAmongYourDialogsAndMessages;
        }

        protected override void OnInitialize()
        {
            //disable dialogs search for hashtags
            if (_hashtag == null)
            {
                Items.Add(SearchDialogs);
            }
            // disable messages search for forwarding and bots (tg://resolve?domain=username&start=access_token, tg://resolve?domain=username&startgroup=access_token)
            if (_forwardMessages == null
                && _bot == null
                && _url == null)
            {
                Items.Add(SearchMessages);
            }

            if (_hashtag != null)
            {
                ActivateItem(SearchMessages);
            }
            else
            {
                ActivateItem(SearchDialogs);
            }

            base.OnInitialize();
        }

        public void Search()
        {
            var text = Text;

            if (string.IsNullOrEmpty(text)) return;

            Telegram.Api.Helpers.Execute.BeginOnUIThread(TimeSpan.FromSeconds(0.5), () =>
            {
                if (!string.Equals(text, Text, StringComparison.OrdinalIgnoreCase)) return;

                SearchDialogs.Text = Text;
                SearchMessages.Text = Text;

                ActiveItem.Search(Text);
            });
        }

        protected override void OnActivate()
        {
            Telegram.Api.Helpers.Execute.BeginOnUIThread(() =>
            {
                if (_stateService.RemoveBackEntry)
                {
                    _stateService.RemoveBackEntry = true;
                    _navigationService.RemoveBackEntry();
                }
            });

            Telegram.Api.Helpers.Execute.BeginOnUIThread(() =>
            {
                Text = _hashtag;
                NotifyOfPropertyChange(() => Text);
            });

            base.OnActivate();
        }

        public void ForwardInAnimationComplete()
        {


            Telegram.Api.Helpers.Execute.BeginOnThreadPool(() =>
            {
                _recentResults = _recentResults ?? TLUtils.OpenObjectFromMTProtoFile<TLVector<TLResultInfo>>(_recentSyncRoot, Constants.RecentSearchResultsFileName) ?? new TLVector<TLResultInfo>();

                var recent = new List<TLObject>();
                foreach (var result in _recentResults)
                {
                    if (result.Type.ToString() == "user")
                    {
                        var user = _cacheService.GetUser(result.Id);
                        if (user != null)
                        {
                            recent.Add(user);
                            if (user.Dialog == null)
                            {
                                user.Dialog = _cacheService.GetDialog(new TLPeerUser { Id = user.Id });
                            }
                        }
                    }

                    if (result.Type.ToString() == "chat")
                    {
                        var chat = _cacheService.GetChat(result.Id);
                        if (chat != null)
                        {
                            recent.Add(chat);
                            if (chat.Dialog == null)
                            {
                                chat.Dialog = _cacheService.GetDialog(new TLPeerChat { Id = chat.Id });
                            }
                        }
                    }
                }

                Telegram.Api.Helpers.Execute.BeginOnUIThread(() =>
                {
                    if (!string.IsNullOrEmpty(Text)) return;

                    Recent.Clear();
                    foreach (var recentItem in recent)
                    {
                        Recent.Add(recentItem);
                    }

                    NotifyOfPropertyChange(() => ShowRecent);
                });
            });
        }

        public void ClearSearchHistory()
        {
            var result = MessageBox.Show(AppResources.ClearSearchHistoryConfirmation, AppResources.Confirm, MessageBoxButton.OKCancel);
            if (result == MessageBoxResult.OK)
            {
                ClearRecent();
            }
        }
    }
}
