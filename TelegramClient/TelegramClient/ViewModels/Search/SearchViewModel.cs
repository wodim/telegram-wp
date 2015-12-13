using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Caliburn.Micro;
using Telegram.Api.Aggregator;
using Telegram.Api.Extensions;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.FileManager;
using Telegram.Api.TL;
using Telegram.Controls.Utils;
using TelegramClient.Resources;
using TelegramClient.Services;
using TelegramClient.ViewModels.Dialogs;
using Execute = Telegram.Api.Helpers.Execute;

namespace TelegramClient.ViewModels.Search
{
    public class SearchViewModel : ItemsViewModelBase<TLObject>, Telegram.Api.Aggregator.IHandle<DownloadableItem>
    {
        public IList<TLDialogBase> LoadedDilaogs { get; set; }

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

        private IList<TLUserBase> _usersSource;

        private IList<TLChatBase> _chatsSource;

        private SearchRequest _lastRequest;

        private readonly Dictionary<string, SearchRequest> _searchResultsCache = new Dictionary<string, SearchRequest>(); 

        public SearchViewModel(ICacheService cacheService, ICommonErrorHandler errorHandler, IStateService stateService, INavigationService navigationService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator) 
            : base(cacheService, errorHandler, stateService, navigationService, mtProtoService, eventAggregator)
        {
            Recent = new ObservableCollection<TLObject>();

            LoadedDilaogs = stateService.LoadedDialogs;
            stateService.LoadedDialogs = null;

            Status = AppResources.SearchAmongYourDialogsAndMessages;

            EventAggregator.Subscribe(this);
        }

        public void OpenDialogDetails(TLObject with)
        {
            if (with == null) return;

            var dialog = with as TLDialog;
            if (dialog != null)
            {
                with = dialog.With;
                StateService.Message = dialog.TopMessage;
            }

            StateService.RemoveBackEntry = true;
            StateService.With = with;
            StateService.AnimateTitle = true;
            NavigationService.UriFor<DialogDetailsViewModel>().Navigate();

            SaveRecent(with);
        }

        public void ForwardInAnimationComplete()
        {
            Execute.BeginOnThreadPool(() =>
            {
                _recentResults = _recentResults ?? TLUtils.OpenObjectFromMTProtoFile<TLVector<TLResultInfo>>(_recentSyncRoot, Constants.RecentSearchResultsFileName) ?? new TLVector<TLResultInfo>();

                var recent = new List<TLObject>();
                foreach (var result in _recentResults)
                {
                    if (result.Type.ToString() == "user")
                    {
                        var user = CacheService.GetUser(result.Id);
                        if (user != null)
                        {
                            recent.Add(user);
                            if (user.Dialog == null)
                            {
                                user.Dialog = CacheService.GetDialog(new TLPeerUser { Id = user.Id });
                            }
                        }
                    }

                    if (result.Type.ToString() == "chat")
                    {
                        var chat = CacheService.GetChat(result.Id);
                        if (chat != null)
                        {
                            recent.Add(chat);
                            if (chat.Dialog == null)
                            {
                                TLDialogBase dialog = CacheService.GetDialog(new TLPeerChat { Id = chat.Id });
                                if (dialog == null)
                                {
                                    if (chat is TLChannel)
                                    {
                                        dialog = DialogsViewModel.GetChannel(chat.Id);
                                    }
                                }
                                chat.Dialog = dialog;
                            }
                        }
                    }
                }

                Execute.BeginOnUIThread(() =>
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

        public void Search()
        {
            var text = Text;

            if (string.IsNullOrEmpty(text.Trim()))
            {
                Items.Clear();
                Status = string.IsNullOrEmpty(text.Trim()) ? AppResources.SearchAmongYourDialogsAndMessages : AppResources.NoResults;
                return;
            }

            Execute.BeginOnUIThread(TimeSpan.FromSeconds(0.5), () =>
            {
                if (!string.Equals(text, Text, StringComparison.OrdinalIgnoreCase)) return;

                Search(Text);
            });
        }

        public void Search(string text)
        {
            if (!string.Equals(text, Text, StringComparison.OrdinalIgnoreCase)) return;

            if (_lastRequest != null)
            {
                _lastRequest.Cancel();
            }

            var trimmedText = Text.Trim();
            if (string.IsNullOrEmpty(trimmedText))
            {
                Items.Clear();
                Status = string.IsNullOrEmpty(Text) ? AppResources.SearchAmongYourDialogsAndMessages : AppResources.NoResults;

                return;
            }

            var nextRequest = GetNextRequest(text);

            IsWorking = true; 
            Status = Items.Count == 0 ? AppResources.Loading : string.Empty;
            nextRequest.ProcessAsync(results =>
                {
                    if (nextRequest.IsCanceled) return;
                    if (!string.Equals(Text, nextRequest.Text, StringComparison.OrdinalIgnoreCase)) return;

                    const int firstSliceCount = 6;
                    Items.Clear();
                    var items = new List<TLObject>();
                    for (var i = 0; i < results.Count; i++)
                    {
                        if (i < firstSliceCount)
                        {
                            Items.Add(results[i]);
                        }
                        else
                        {
                            items.Add(results[i]);
                        }
                    }

                    IsWorking = false;
                    Status = Items.Count == 0 && items.Count == 0 ? AppResources.Loading : string.Empty;

                    Execute.BeginOnUIThread(() =>
                    {
                        if (nextRequest.IsCanceled) return;
                        if (!string.Equals(Text, nextRequest.Text, StringComparison.OrdinalIgnoreCase)) return;

                        foreach (var item in items)
                        {
                            Items.Add(item);
                        }

                        ProcessGlobalSearch(nextRequest);
                    });
                });

            _searchResultsCache[nextRequest.Text] = nextRequest;
            _lastRequest = nextRequest;
        }

        private SearchRequest GetNextRequest(string text)
        {
            SearchRequest nextRequest;
            if (!_searchResultsCache.TryGetValue(text, out nextRequest))
            {
                IList<TLUserBase> usersSource;

                if (_lastRequest != null
                    && text.IndexOf(_lastRequest.Text, StringComparison.OrdinalIgnoreCase) != -1)
                {
                    usersSource = _lastRequest.UsersSource;
                }
                else
                {
                    var source = _usersSource;

                    if (source == null)
                    {
                        source = CacheService.GetUsersForSearch(LoadedDilaogs)
                            .Where(x => !(x is TLUserEmpty) && !(x is TLUserDeleted) && x.Index != StateService.CurrentUserId)
                            .ToList();
                    }

                    _usersSource = source;

                    usersSource = _usersSource;
                }

                IList<TLChatBase> chatsSource;

                if (_lastRequest != null
                    && text.IndexOf(_lastRequest.Text, StringComparison.OrdinalIgnoreCase) != -1)
                {
                    chatsSource = _lastRequest.ChatsSource;
                }
                else
                {
                    _chatsSource = _chatsSource ??
                                   CacheService.GetChats()
                                       .OrderBy(x => x.FullName)
                                       .ToList();

                    foreach (var chat in _chatsSource)
                    {
                        chat.FullNameWords = chat.FullName.Split(' ');
                    }

                    chatsSource = _chatsSource;
                }

                nextRequest = new SearchRequest(CacheService, text, usersSource, chatsSource);
            }
            return nextRequest;
        }

        private void ProcessGlobalSearch(SearchRequest request)
        {
            if (request.GlobalResults != null)
            {
                if (!string.Equals(Text, request.Text, StringComparison.OrdinalIgnoreCase)) return;
                if (request.IsCanceled) return;

                if (request.GlobalResults.Count > 0)
                {
                    Items.Add(new TLServiceText { Text = AppResources.GlobalSearch });
                    foreach (var user in request.GlobalResults)
                    {
                        Items.Add(user);
                    }
                }

                Status = Items.Count == 0 ? AppResources.NoResults : string.Empty;

                ProcessMessagesSearch(request);
            }
            else
            {
                if (request.Text.Length < Constants.UsernameMinLength)
                {
                    request.GlobalResults = new List<TLObject>();

                    ProcessMessagesSearch(request);
                    return;
                }

                IsWorking = true;
                MTProtoService.SearchAsync(new TLString(request.Text), new TLInt(100),
                    result => Execute.BeginOnUIThread(() =>
                    {
                        IsWorking = false;

                        request.GlobalResults = new List<TLObject>();
                        foreach (var user in result.Users)
                        {
                            if (request.UserResultsIndex != null && request.UserResultsIndex.ContainsKey(user.Index))
                            {
                                continue;
                            }
                            user.IsGlobalResult = true;
                            request.GlobalResults.Add(user);
                        }
                        var contactsFound40 = result as TLContactsFound40;
                        if (contactsFound40 != null)
                        {
                            foreach (var chat in contactsFound40.Chats)
                            {
                                if (request.ChatResultsIndex != null && request.ChatResultsIndex.ContainsKey(chat.Index))
                                {
                                    continue;
                                }
                                chat.IsGlobalResult = true;
                                request.GlobalResults.Add(chat);
                            }
                        }

                        if (!string.Equals(Text, request.Text, StringComparison.OrdinalIgnoreCase)) return;
                        if (request.IsCanceled) return;

                        var hasResults = request.GlobalResults.Count > 0;
                        if (hasResults)
                        {
                            Items.Add(new TLServiceText { Text = AppResources.GlobalSearch });
                            foreach (var r in request.GlobalResults)
                            {
                                Items.Add(r);
                            }
                        }

                        Status = Items.Count == 0 ? AppResources.NoResults : string.Empty;

                        ProcessMessagesSearch(request);
                    }),
                    error => Execute.BeginOnUIThread(() =>
                    {
                        IsWorking = false;
                        Status = Items.Count == 0 ? AppResources.NoResults : string.Empty;

                        Execute.ShowDebugMessage("contacts.search error " + error);
                    }));
            }
        }

        public void LoadNextSlice()
        {
            if (_lastRequest == null) return;

            ProcessMessagesSearch(_lastRequest, true);
        }

        public void ProcessMessagesSearch(SearchRequest request, bool nextSlice = false)
        {
            if (request.MessageResults != null && !nextSlice)
            {
                if (!string.Equals(Text, request.Text, StringComparison.OrdinalIgnoreCase)) return;
                if (request.IsCanceled) return;

                if (request.MessageResults.Count > 0)
                {
                    Items.Add(new TLServiceText { Text = AppResources.Messages });
                    foreach (var result in request.MessageResults)
                    {
                        Items.Add(result);
                    }
                }

                Status = Items.Count == 0 ? AppResources.NoResults : string.Empty;
            }
            else
            {
                if (IsWorking) return;

                IsWorking = true;
                MTProtoService.SearchAsync(
                    new TLInputPeerEmpty(),
                    new TLString(request.Text),
                    new TLInputMessagesFilterEmpty(),
                    new TLInt(0), new TLInt(0), new TLInt(request.Offset), new TLInt(0), new TLInt(request.Limit),
                        result =>
                        {
                            CacheService.AddChats(result.Chats, results => { });
                            CacheService.AddUsers(result.Users, results => { });
                            
                            var items = new List<TLObject>();
                            var newMessages = result as TLMessages;
                            if (newMessages != null)
                            {
                                var usersCache = new Dictionary<int, TLUserBase>();
                                foreach (var user in newMessages.Users)
                                {
                                    usersCache[user.Index] = user;
                                }

                                var chatsCache = new Dictionary<int, TLChatBase>();
                                foreach (var chat in newMessages.Chats)
                                {
                                    chatsCache[chat.Index] = chat;
                                }

                                foreach (var message in newMessages.Messages.OfType<TLMessageCommon>())
                                {
                                    var dialog = new TLDialog { TopMessage = message };
                                    var peer = TLUtils.GetPeerFromMessage(message);
                                    if (peer is TLPeerUser)
                                    {
                                        TLUserBase user;
                                        if (!usersCache.TryGetValue(peer.Id.Value, out user))
                                        {
                                            continue;
                                        }
                                        dialog.With = user;
                                    }
                                    else if (peer is TLPeerChat
                                        || peer is TLPeerChannel)
                                    {
                                        TLChatBase chat;
                                        if (!chatsCache.TryGetValue(peer.Id.Value, out chat))
                                        {
                                            continue;
                                        }

                                        dialog.With = chat;
                                    }
                                    items.Add(dialog);
                                }
                            }

                            Execute.BeginOnUIThread(() =>
                            {
                                IsWorking = false;

                                if (request.MessageResults == null)
                                {
                                    request.MessageResults = new List<TLObject>();
                                }
                                foreach (var item in items)
                                {
                                    request.MessageResults.Add(item);
                                }
                                request.Offset += request.Limit;

                                if (!string.Equals(Text, request.Text, StringComparison.OrdinalIgnoreCase)) return;
                                if (request.IsCanceled) return;

                                if (request.MessageResults.Count > 0)
                                {
                                    if (request.Offset == request.Limit)
                                    {
                                        Items.Add(new TLServiceText { Text = AppResources.Messages });
                                    }
                                    foreach (var item in request.MessageResults)
                                    {
                                        Items.Add(item);
                                        
                                    }
                                }

                                Status = Items.Count == 0 ? AppResources.NoResults : string.Empty;
                            });
                        },
                        error => Execute.BeginOnUIThread(() =>
                        {
                            IsWorking = false;
                            Status = Items.Count == 0 ? AppResources.NoResults : string.Empty;
                        }));
            }
        }

        #region Recents

        private static readonly object _recentSyncRoot = new object();

        private static TLVector<TLResultInfo> _recentResults;

        public ObservableCollection<TLObject> Recent { get; set; }

        public bool ShowRecent
        {
            get { return Recent.Count > 0 && string.IsNullOrEmpty(Text); }
        }

        public void ClearRecent()
        {
            Recent.Clear();
            NotifyOfPropertyChange(() => ShowRecent);

            DeleteRecentAsync();
        }

        public static void DeleteRecentAsync()
        {
            Execute.BeginOnThreadPool(() =>
            {
                _recentResults = new TLVector<TLResultInfo>();
                FileUtils.Delete(_recentSyncRoot, Constants.RecentSearchResultsFileName);
            });
        }

        public void SaveRecent(TLObject with)
        {
            Execute.BeginOnThreadPool(() =>
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

        public void ClearSearchHistory()
        {
            var result = MessageBox.Show(AppResources.ClearSearchHistoryConfirmation, AppResources.Confirm, MessageBoxButton.OKCancel);
            if (result == MessageBoxResult.OK)
            {
                ClearRecent();
            }
        }
        #endregion

        public void Handle(DownloadableItem item)
        {
            BeginOnUIThread(() =>
            {
                var photo = item.Owner as TLUserProfilePhoto;
                if (photo != null)
                {
                    var user = (TLUserBase)Items.FirstOrDefault(x => x is TLUserBase && ((TLUserBase)x).Photo == photo);
                    if (user != null)
                    {
                        user.NotifyOfPropertyChange(() => user.Photo);
                        return;
                    }

                    var dialog = Items.FirstOrDefault(x => x is TLDialogBase && ((TLDialogBase)x).With is TLUserBase && ((TLUserBase)((TLDialog)x).With).Photo == photo);
                    if (dialog != null)
                    {
                        dialog.NotifyOfPropertyChange(() => ((TLDialogBase)dialog).With);
                        return;
                    }
                }

                var chatPhoto = item.Owner as TLChatPhoto;
                if (chatPhoto != null)
                {
                    var chat = (TLChat)Items.FirstOrDefault(x => x is TLChat && ((TLChat)x).Photo == chatPhoto);
                    if (chat != null)
                    {
                        chat.NotifyOfPropertyChange(() => chat.Photo);
                        return;
                    }

                    var dialog = Items.FirstOrDefault(x => x is TLDialogBase && ((TLDialogBase)x).With is TLChat && ((TLChat)((TLDialog)x).With).Photo == chatPhoto);
                    if (dialog != null)
                    {
                        dialog.NotifyOfPropertyChange(() => ((TLDialogBase)dialog).With);
                        return;
                    }
                }

                var channelPhoto = item.Owner as TLChatPhoto;
                if (channelPhoto != null)
                {

                    var channel = (TLChannel)Items.FirstOrDefault(x => x is TLChannel && ((TLChannel)x).Photo == channelPhoto);
                    if (channel != null)
                    {
                        channel.NotifyOfPropertyChange(() => channel.Photo);
                        return;
                    }

                    var dialog = Items.FirstOrDefault(x => x is TLDialogBase && ((TLDialogBase)x).With is TLChannel && ((TLChannel)((TLDialog)x).With).Photo == channelPhoto);
                    if (dialog != null)
                    {
                        dialog.NotifyOfPropertyChange(() => ((TLDialogBase)dialog).With);
                        return;
                    }
                    return;
                }
            });
        }
    }

    public class SearchRequest
    {
        public bool IsCanceled;

        public string TransliterateText { get; private set; }

        public string Text { get; private set; }

        public IList<TLUserBase> UsersSource { get; private set; }

        public IList<TLChatBase> ChatsSource { get; private set; }

        public IList<TLObject> Results { get; private set; }

        public Dictionary<int, TLUserBase> UserResultsIndex { get; private set; }

        public Dictionary<int, TLChatBase> ChatResultsIndex { get; private set; } 

        public IList<TLObject> GlobalResults { get; set; }

        public IList<TLObject> MessageResults { get; set; }

        public int Offset { get; set; }

        public int Limit { get { return 20; } }

        private readonly ICacheService _cacheService;

        public SearchRequest(ICacheService cacheService, string text, IList<TLUserBase> usersSource, IList<TLChatBase> chatsSource)
        {
            _cacheService = cacheService;
            Text = text;
            TransliterateText = Language.Transliterate(text);
            UsersSource = usersSource;
            ChatsSource = chatsSource;
        }

        private static bool IsUserValid(TLUserBase contact, string text)
        {
            if (string.IsNullOrEmpty(text)) return false;

            return contact.FirstName.ToString().StartsWith(text, StringComparison.OrdinalIgnoreCase)
                || contact.LastName.ToString().StartsWith(text, StringComparison.OrdinalIgnoreCase)
                || contact.FullName.StartsWith(text, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsChatValid(TLChatBase chat, string text, bool useFastSearch)
        {
            if (string.IsNullOrEmpty(text)) return false;

            if (!useFastSearch)
            {
                var fullName = chat.FullName;

                var i = fullName.IndexOf(text, StringComparison.OrdinalIgnoreCase);
                if (i != -1)
                {
                    while (i < fullName.Length && i != -1)
                    {
                        if (i == 0 || (i > 0 && fullName[i - 1] == ' '))
                        {
                            return true;
                        }
                        if (fullName.Length > i + 1)
                        {
                            i = fullName.IndexOf(text, i + 1, StringComparison.OrdinalIgnoreCase);
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }
            else
            {
                if (chat.FullNameWords != null
                    && chat.FullNameWords.Any(x => x.StartsWith(text, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsUsernameValid(IUserName userNameContact, string text)
        {
            if (text.Length >= Constants.UsernameMinLength)
            {
                if (userNameContact != null)
                {
                    var userName = userNameContact.UserName != null ? userNameContact.UserName.ToString() : string.Empty;
                    if (userName.StartsWith(text.TrimStart('@'), StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public void ProcessAsync(Action<IList<TLObject>> callback)
        {
            if (Results != null)
            {
                IsCanceled = false;
                callback.SafeInvoke(Results);
                return;
            }

            var usersSource = UsersSource;
            var chatsSource = ChatsSource;

            Execute.BeginOnThreadPool(() =>
            {
                var useFastSearch = !Text.Contains(" ");

                var userResults = new List<TLUserBase>(usersSource.Count);
                foreach (var contact in usersSource)
                {
                    if (IsUserValid(contact, Text)
                        || IsUserValid(contact, TransliterateText)
                        || IsUsernameValid(contact as IUserName, Text))
                    {
                        userResults.Add(contact);
                    }
                }

                var chatsResults = new List<TLChatBase>(chatsSource.Count);
                foreach (var chat in chatsSource)
                {
                    if (IsChatValid(chat, Text, useFastSearch)
                        || IsChatValid(chat, TransliterateText, useFastSearch)
                        || IsUsernameValid(chat as IUserName, Text))
                    {
                        chatsResults.Add(chat);
                    }
                }

                Results = new List<TLObject>(userResults.Count + chatsResults.Count);
                UserResultsIndex = new Dictionary<int, TLUserBase>();
                ChatResultsIndex = new Dictionary<int, TLChatBase>();
                foreach (var userResult in userResults)
                {
                    Results.Add(userResult);
                    UserResultsIndex[userResult.Index] = userResult;
                    if (userResult.Dialog == null)
                    {
                        userResult.Dialog = _cacheService.GetDialog(new TLPeerUser { Id = userResult.Id });
                    }
                }
                foreach (var chatResult in chatsResults)
                {
                    Results.Add(chatResult);
                    ChatResultsIndex[chatResult.Index] = chatResult;
                    if (chatResult.Dialog == null)
                    {
                        TLDialogBase dialog = _cacheService.GetDialog(new TLPeerChat { Id = chatResult.Id });
                        if (dialog == null)
                        {
                            if (chatResult is TLChannel)
                            {
                                dialog = DialogsViewModel.GetChannel(chatResult.Id);
                            }
                        }
                        chatResult.Dialog = dialog;
                    }
                }

                Execute.BeginOnUIThread(() => callback.SafeInvoke(Results));
            });
        }

        public void Cancel()
        {
            IsCanceled = true;
        }

        public void LoadNextSlice(System.Action callback)
        {
        }
    }
}
