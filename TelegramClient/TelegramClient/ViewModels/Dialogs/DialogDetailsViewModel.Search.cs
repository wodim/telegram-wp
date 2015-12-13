using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Telegram.Api.TL;
using Execute = Telegram.Api.Helpers.Execute;

namespace TelegramClient.ViewModels.Dialogs
{
    public partial class DialogDetailsViewModel
    {

        public DialogSearchMessagesViewModel SearchMessages { get; protected set; }

        public void Search()
        {
            if (SearchMessages == null)
            {
                SearchMessages = new DialogSearchMessagesViewModel(Search, SearchUp, SearchDown);
                NotifyOfPropertyChange(() => SearchMessages);
            }

            BeginOnUIThread(() =>
            {
                if (!SearchMessages.IsOpen)
                {
                    _currentResultIndex = default(int);
                    _currentResults = null;

                    SearchMessages.Open();
                }
                else
                {
                    SearchMessages.Close();
                }
            });
        }

        private static readonly Dictionary<string, TLMessagesBase> _searchResults = new Dictionary<string, TLMessagesBase>();

        private int _currentResultIndex;

        private TLMessagesBase _currentResults;

        private void Search(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                SearchMessages.ResultLoaded(0, 0);
                return;
            }

            BeginOnUIThread(TimeSpan.FromSeconds(0.5), () =>
            {
                if (!string.Equals(text, SearchMessages.Text, StringComparison.Ordinal)) return;

                TLMessagesBase cachedResults;
                if (_searchResults.TryGetValue(text, out cachedResults))
                {
                    ContinueSearch(cachedResults);

                    return;
                }

                SearchAsync(new TLString(text), new TLInt(0), new TLInt(Constants.SearchMessagesSliceLimit));
            });
        }

        private void SearchAsync(TLString text, TLInt maxId, TLInt limit)
        {
            IsWorking = true;
            MTProtoService.SearchAsync(Peer, text, new TLInputMessagesFilterEmpty(), new TLInt(0), new TLInt(Int32.MaxValue), new TLInt(0), maxId, limit,
                result => BeginOnUIThread(() =>
                {
                    IsWorking = false;

                    ProcessRepliesAndAudio(result.Messages);

                    var key = text.ToString();
                    TLMessagesBase cachedResult;
                    if (_searchResults.TryGetValue(key, out cachedResult))
                    {
                        var lastId = cachedResult.Messages.Last().Id;
                        if (lastId.Value == maxId.Value)
                        {
                            var cachedUsersIndex = new Dictionary<int, int>();
                            foreach (var cachedUser in cachedResult.Users)
                            {
                                cachedUsersIndex[cachedUser.Index] = cachedUser.Index;
                            }
                            foreach (var user in result.Users)
                            {
                                if (!cachedUsersIndex.ContainsKey(user.Index))
                                {
                                    cachedResult.Users.Add(user);
                                }
                            }
                            var cachedChatsIndex = new Dictionary<int, int>();
                            foreach (var cachedChat in cachedResult.Chats)
                            {
                                cachedChatsIndex[cachedChat.Index] = cachedChat.Index;
                            }
                            foreach (var chat in result.Chats)
                            {
                                if (!cachedChatsIndex.ContainsKey(chat.Index))
                                {
                                    cachedResult.Chats.Add(chat);
                                }
                            }
                            foreach (var message in result.Messages)
                            {
                                cachedResult.Messages.Add(message);
                            }

                            SearchMessages.ResultLoaded(_currentResultIndex, cachedResult.Messages.Count);
                        }
                    }
                    else
                    {
                        _searchResults[key] = result;

                        if (string.Equals(key, SearchMessages.Text, StringComparison.Ordinal))
                        {
                            ContinueSearch(result);
                        }
                    }

                }),
                error => BeginOnUIThread(() =>
                {
                    IsWorking = false;
                    Execute.ShowDebugMessage("messages.search error " + error);
                }));
        }

        private void ContinueSearch(TLMessagesBase result)
        {
            _currentResults = result;

            if (result.Messages.Count > 0)
            {
                _currentResults = result;

                LoadResult(0);
            }
            else
            {
                SearchMessages.ResultLoaded(0, 0);
            }
        }

        private void LoadResult(int resultIndex)
        {
            TLUtils.WriteLine(string.Format("LoadResult index={0}", resultIndex), LogSeverity.Error);

            _currentResultIndex = resultIndex;
            var message = _currentResults.Messages[_currentResultIndex];
            var nextMessage = _currentResults.Messages.Count > _currentResultIndex + 1? _currentResults.Messages[_currentResultIndex + 1] : null;

            Items.Clear();
            Items.Add(message);
            //HighlightMessage(message);
            LoadResultHistory(message);
            if (nextMessage != null)
            {
                PreloadResultHistory(nextMessage);
            }

            SliceLoaded = false;
            _isFirstSliceLoaded = false;
            _isLastMigratedHistorySliceLoaded = false;
            IsLastSliceLoaded = false;

            SearchMessages.ResultLoaded(resultIndex, _currentResults.Messages.Count);

            if (resultIndex >= _currentResults.Messages.Count - 3)
            {
                var messagesSlice = _currentResults as TLMessagesSlice;
                if (messagesSlice != null && messagesSlice.Count.Value > messagesSlice.Messages.Count)
                {
                    var maxId = messagesSlice.Messages.Last().Id;
                    SearchAsync(new TLString(SearchMessages.Text), maxId, new TLInt(Constants.SearchMessagesSliceLimit));
                }
                else
                {
                    var channel = With as TLChannel;
                    if (channel != null && channel.MigratedFromChatId != null)
                    {
                        
                    }
                }
            }

            //if (ScrollToBottomVisibility == Visibility.Collapsed)
            //{
            //    Execute.BeginOnUIThread(() => ScrollToBottomVisibility = Visibility.Visible);
            //}
        }

        private static readonly Dictionary<int, IList<TLMessageBase>> _resultHistoryCache = new Dictionary<int, IList<TLMessageBase>>(); 

        private void LoadResultHistory(TLMessageBase message)
        {
            var maxId = message.Id;
            var offset = new TLInt(-6);
            var limit = new TLInt(14);

            IList<TLMessageBase> resultHistory;
            if (_resultHistoryCache.TryGetValue(maxId.Value, out resultHistory))
            {
                ContinueLoadResultHistory(limit, maxId, resultHistory);

                return;
            }

            IsWorking = true;
            MTProtoService.GetHistoryAsync("LoadResultHistory", Peer, TLUtils.InputPeerToPeer(Peer, StateService.CurrentUserId), false, offset, maxId, limit,
                result => BeginOnUIThread(() =>
                {
                    IsWorking = false;

                    ProcessRepliesAndAudio(result.Messages);

                    _resultHistoryCache[maxId.Value] = result.Messages;

                    if (_currentResults == null
                        || _currentResults.Messages[_currentResultIndex] == message)
                    {
                        ContinueLoadResultHistory(limit, maxId, result.Messages);
                    }
                }),
                error => BeginOnUIThread(() =>
                {
                    IsWorking = false;
                    Execute.ShowDebugMessage("messages.getHistory error " + error);
                }));
        }

        private void PreloadResultHistory(TLMessageBase message)
        {
            var maxId = message.Id;
            var offset = new TLInt(-6);
            var limit = new TLInt(14);

            IList<TLMessageBase> resultHistory;
            if (_resultHistoryCache.TryGetValue(maxId.Value, out resultHistory))
            {
                return;
            }

            IsWorking = true;
            MTProtoService.GetHistoryAsync("PreloadResultHistory", Peer, TLUtils.InputPeerToPeer(Peer, StateService.CurrentUserId), false, offset, maxId, limit,
                result => BeginOnUIThread(() =>
                {
                    IsWorking = false;

                    ProcessRepliesAndAudio(result.Messages);

                    _resultHistoryCache[maxId.Value] = result.Messages;
                }),
                error => BeginOnUIThread(() =>
                {
                    IsWorking = false;
                    Execute.ShowDebugMessage("messages.getHistory error " + error);
                }));
        }

        private void ContinueLoadResultHistory(TLInt limit, TLInt maxId, IList<TLMessageBase> resultMessages)
        {
            var upperSlice = new List<TLMessageBase>();
            var bottomSlice = new List<TLMessageBase>();
            for (var i = 0; i < resultMessages.Count; i++)
            {
                var resultMessage = resultMessages[i];

                if (resultMessage.Index < maxId.Value)
                {
                    upperSlice.Add(resultMessage);
                }
                else if (resultMessage.Index > maxId.Value)
                {
                    bottomSlice.Add(resultMessage);
                }
            }

            if (Items.Count == 1)
            {
                foreach (var upperMessage in upperSlice)
                {
                    upperMessage._isAnimated = false;//isAnimated;
                    if (!SkipMessage(upperMessage))
                    {
                        Items.Add(upperMessage);
                    }
                }
            }

            if (limit.Value > upperSlice.Count + bottomSlice.Count)
            {
                var channel = With as TLChannel;
                if (channel != null && channel.MigratedFromChatId != null)
                {
                    var lastMessage = Items != null ? Items.LastOrDefault() : null;
                    if (lastMessage != null && lastMessage.Index == 1)
                    {
                        var chatMessages = CacheService.GetHistory(new TLInt(StateService.CurrentUserId), new TLPeerChat { Id = channel.MigratedFromChatId });
                        foreach (var message in chatMessages)
                        {
                            if (!SkipMessage(message))
                            {
                                Items.Add(message);
                            }
                        }
                    }
                }
            }
        }

        private void SearchUp()
        {
            if (_currentResults == null) return;
            if (_currentResults.Messages.Count == _currentResultIndex + 1) return;

            BeginOnUIThread(() => LoadResult(_currentResultIndex + 1));
        }

        private void SearchDown()
        {
            if (_currentResults == null) return;
            if (_currentResultIndex <= 0) return;

            BeginOnUIThread(() => LoadResult(_currentResultIndex - 1));
        }
    }
}
