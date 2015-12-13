using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Telegram.Api.Aggregator;
using Caliburn.Micro;
using Telegram.Api.Extensions;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.FileManager;
using Telegram.Api.TL;
using Telegram.Api.TL.Interfaces;
using TelegramClient.Resources;
using TelegramClient.Services;
using TelegramClient.ViewModels.Dialogs;
using TelegramClient.ViewModels.Media;
using Execute = Telegram.Api.Helpers.Execute;

namespace TelegramClient.ViewModels.Search
{
    public class SearchFilesViewModel : SearchFilesViewModelBase
    {
        public override TLInputMessagesFilterBase InputMessageFilter
        {
            get { return new TLInputMessagesFilterDocument(); }
        }

        public SearchFilesViewModel(ICacheService cacheService, ICommonErrorHandler errorHandler, IStateService stateService, INavigationService navigationService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator)
            : base(cacheService, errorHandler, stateService, navigationService, mtProtoService, eventAggregator)
        {
            Status = AppResources.SearchAmongYourFiles;
        }
    }

    public abstract class SearchFilesViewModelBase : ItemsViewModelBase<TLObject>,
        Telegram.Api.Aggregator.IHandle<DownloadableItem>
    {
        public abstract TLInputMessagesFilterBase InputMessageFilter { get; }

        private bool _isSelectionEnabled;

        public bool IsSelectionEnabled
        {
            get { return _isSelectionEnabled; }
            set { SetField(ref _isSelectionEnabled, value, () => IsSelectionEnabled); }
        }

        public bool IsEmptyList
        {
            get { return Items.Count == 0 && LazyItems.Count == 0; }
        }

        public IInputPeer Peer { get; protected set; }

        public string Text { get; set; }

        private IDocumentFileManager _downloadDocumentFileManager;

        private IDocumentFileManager DownloadDocumentFileManager
        {
            get { return _downloadDocumentFileManager ?? (_downloadDocumentFileManager = IoC.Get<IDocumentFileManager>()); }
        }

        protected SearchFilesViewModelBase(ICacheService cacheService, ICommonErrorHandler errorHandler, IStateService stateService, INavigationService navigationService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator) 
            : base(cacheService, errorHandler, stateService, navigationService, mtProtoService, eventAggregator)
        {
            EventAggregator.Subscribe(this);

            Items = new ObservableCollection<TLObject>();

            if (StateService.CurrentInputPeer != null)
            {
                Peer = StateService.CurrentInputPeer;
                StateService.CurrentInputPeer = null;
            }

            if (StateService.Source != null)
            {
                _source = StateService.Source;
                StateService.Source = null;
            }
        }

        #region Searching

        private SearchDocumentsRequest _lastDocumentsRequest;

        private readonly List<TLMessageBase> _source;

        private readonly Dictionary<string, SearchDocumentsRequest> _searchResultsCache = new Dictionary<string, SearchDocumentsRequest>();

        public void Search()
        {
            if (_lastDocumentsRequest != null)
            {
                _lastDocumentsRequest.Cancel();
            }

            var text = Text.Trim();

            if (string.IsNullOrEmpty(text))
            {
                LazyItems.Clear();
                Items.Clear();
                Status = string.IsNullOrEmpty(Text) ? AppResources.SearchAmongYourFiles : AppResources.NoResults;
                return;
            }

            SearchDocumentsRequest nextDocumentsRequest;
            if (!_searchResultsCache.TryGetValue(text, out nextDocumentsRequest))
            {
                IList<TLMessageBase> source;

                if (_lastDocumentsRequest != null
                    && text.IndexOf(_lastDocumentsRequest.Text, StringComparison.OrdinalIgnoreCase) != -1)
                {
                    source = _lastDocumentsRequest.Source;
                }
                else
                {
                    source = _source;
                }

                nextDocumentsRequest = new SearchDocumentsRequest(Peer.ToInputPeer(), text, source);
            }

            IsWorking = true;
            nextDocumentsRequest.ProcessAsync(results =>
                Execute.BeginOnUIThread(() =>
                {
                    if (nextDocumentsRequest.IsCanceled) return;

                    Status = results.Count > 0? string.Empty : Status;
                    Items.Clear();
                    LazyItems.Clear();
                    for (var i = 0; i < results.Count; i++)
                    {
                        if (i < 6)
                        {
                            Items.Add(results[i]);
                        }
                        else
                        {
                            LazyItems.Add(results[i]);
                        }
                    }

                    IsWorking = false;
                    NotifyOfPropertyChange(() => IsEmptyList);

                    if (LazyItems.Count > 0)
                    {
                        PopulateItems(() => ProcessGlobalSearch(nextDocumentsRequest));
                    }
                    else
                    {
                        ProcessGlobalSearch(nextDocumentsRequest);
                    }
                }));

            _searchResultsCache[nextDocumentsRequest.Text] = nextDocumentsRequest;
            _lastDocumentsRequest = nextDocumentsRequest;
        }

        private void ProcessGlobalSearch(SearchDocumentsRequest nextDocumentsRequest)
        {
            if (nextDocumentsRequest.GlobalResults != null)
            {
                if (nextDocumentsRequest.GlobalResults.Count > 0)
                {
                    BeginOnUIThread(() =>
                    {
                        if (nextDocumentsRequest.IsCanceled) return;

                        foreach (var user in nextDocumentsRequest.GlobalResults)
                        {
                            Items.Add(user);
                        }
                        NotifyOfPropertyChange(() => IsEmptyList);
                        Status = Items.Count > 0 ? string.Empty : AppResources.NoResults;
                    });
                }
            }
            else
            {
                IsWorking = true;
                MTProtoService.SearchAsync(
                    nextDocumentsRequest.InputPeer, 
                    new TLString(nextDocumentsRequest.Text), 
                    InputMessageFilter, 
                    new TLInt(0), new TLInt(0), new TLInt(0), new TLInt(0), new TLInt(100),  
                    result =>
                    {
                        IsWorking = false;
                        nextDocumentsRequest.GlobalResults = new List<TLMessageBase>(result.Messages.Count);

                        foreach (var message in result.Messages)
                        {
                            if (nextDocumentsRequest.ResultsIndex == null
                                || !nextDocumentsRequest.ResultsIndex.ContainsKey(message.Index))
                            {
                                nextDocumentsRequest.GlobalResults.Add(message);
                            }
                        }


                        BeginOnUIThread(() =>
                        {
                            if (nextDocumentsRequest.IsCanceled) return;

                            if (nextDocumentsRequest.GlobalResults.Count > 0)
                            {
                                foreach (var message in nextDocumentsRequest.GlobalResults)
                                {
                                    Items.Add(message);
                                }
                                NotifyOfPropertyChange(() => IsEmptyList);
                            }

                            Status = Items.Count > 0 ? string.Empty : AppResources.NoResults;
                        });

                    },
                    error =>
                    {
                        IsWorking = false;

                        if (TLRPCError.CodeEquals(error, ErrorCode.BAD_REQUEST)
                            && TLRPCError.TypeEquals(error, ErrorType.QUERY_TOO_SHORT))
                        {
                            nextDocumentsRequest.GlobalResults = new List<TLMessageBase>();
                        }
                        else if (TLRPCError.CodeEquals(error, ErrorCode.FLOOD))
                        {
                            nextDocumentsRequest.GlobalResults = new List<TLMessageBase>();
                            BeginOnUIThread(() => MessageBox.Show(AppResources.FloodWaitString + Environment.NewLine + "(" + error.Message + ")", AppResources.Error, MessageBoxButton.OK));
                        }

                        Execute.ShowDebugMessage("messages.search error " + error);
                    });
            }
        }

        #endregion

        #region Actions
        public void DeleteMessage(TLMessage message)
        {
            if (message == null) return;

            var messages = new List<TLMessageBase> { message };

            var owner = Peer as TLObject;

            if (Peer is TLBroadcastChat)
            {
                DeleteMessagesInternal(owner, null, messages);
                return;
            }

            if ((message.Id == null || message.Id.Value == 0) && message.RandomIndex != 0)
            {
                DeleteMessagesInternal(owner, null, messages);
                return;
            }

            DialogDetailsViewModel.DeleteMessages(MTProtoService, null, messages, null, (result1, result2) => DeleteMessagesInternal(owner, result1, result2));
        }

        private void DeleteMessagesInternal(TLObject owner, TLMessageBase message, IList<TLMessageBase> messages)
        {
            var ids = new TLVector<TLInt>();
            for (int i = 0; i < messages.Count; i++)
            {
                ids.Add(messages[i].Id);
            }

            // duplicate: deleting performed through updates
            CacheService.DeleteMessages(ids);

            BeginOnUIThread(() =>
            {
                for (var i = 0; i < messages.Count; i++)
                {
                    Items.Remove(messages[i]);
                }
            });

            EventAggregator.Publish(new DeleteMessagesEventArgs { Owner = owner, Messages = messages });
        }

        public void DeleteMessages()
        {
            if (Items == null) return;

            var messages = Items.Where(x => ((TLMessageBase)x).IsSelected).Cast<TLMessageBase>().ToList();
            if (messages.Count == 0) return;

            var randomItems = messages.Where(x => (x.Id == null || x.Id.Value == 0) && x.RandomId != null).ToList();
            var items = messages.Where(x => x.Id != null && x.Id.Value != 0).ToList();

            if (randomItems.Count > 0 || items.Count > 0)
            {
                IsSelectionEnabled = false;
            }

            var owner = Peer as TLObject;

            if (Peer is TLBroadcastChat)
            {
                DeleteMessagesInternal(owner, null, randomItems);
                DeleteMessagesInternal(owner, null, items);
                return;
            }

            DialogDetailsViewModel.DeleteMessages(MTProtoService, null, randomItems, items, (result1, result2) => DeleteMessagesInternal(owner, result1, result2), (result1, result2) => DeleteMessagesInternal(owner, result1, result2));
        }

        public void ForwardMessage(TLMessage message)
        {
            if (message == null) return;

            DialogDetailsViewModel.ForwardMessagesCommon(new List<TLMessageBase>{ message }, StateService, NavigationService);
        }

        public void SaveMedia(TLMessage message)
        {
            if (message == null) return;

#if WP81
            DialogDetailsViewModel.SaveMediaCommon(message);
#endif
        }

#if WP8
        public async void OpenMedia(TLMessage message)
#else
        public void OpenMedia(TLMessage message)
#endif
        {
            if (message == null) return;

            var mediaDocument = message.Media as TLMessageMediaDocument;
            if (mediaDocument != null)
            {
                DialogDetailsViewModel.OpenDocumentCommon(message, StateService, DownloadDocumentFileManager, () => { });
            }
        }
        #endregion

        public void CancelDocumentDownloading(TLMessageMediaDocument mediaDocument)
        {
            BeginOnThreadPool(() =>
            {
                BeginOnUIThread(() =>
                {
                    var message = Items.FirstOrDefault(x => ((TLMessage)x).Media == mediaDocument);

                    DownloadDocumentFileManager.CancelDownloadFileAsync(message);

                    mediaDocument.IsCanceled = true;
                    mediaDocument.LastProgress = mediaDocument.DownloadingProgress;
                    mediaDocument.DownloadingProgress = 0.0;
                });
            });
        }

        public void Handle(DownloadableItem item)
        {
            var document = item.Owner as TLDocument;
            if (document != null)
            {
                BeginOnUIThread(() =>
                {
                    var messages = Items;
                    foreach (var m in messages)
                    {
                        var media = ((TLMessage)m).Media as TLMessageMediaDocument;
                        if (media != null && TLDocumentBase.DocumentEquals(media.Document, document))
                        {
                            media.NotifyOfPropertyChange(() => media.Document);
                            break;
                        }
                    }
                });
            }

            var message = item.Owner as TLMessage;
            if (message != null)
            {
                BeginOnUIThread(() =>
                {
                    var messages = Items;
                    foreach (var m in messages)
                    {
                        var doc = ((TLMessage)m).Media as TLMessageMediaDocument;
                        if (doc != null && m == item.Owner)
                        {
                            ((TLMessage)m).Media.IsCanceled = false;
                            ((TLMessage)m).Media.LastProgress = 0.0;
                            ((TLMessage)m).Media.DownloadingProgress = 0.0;
                            ((TLMessage)m).Media.NotifyOfPropertyChange(() => ((TLMessage)m).Media.Self); // update download icon for documents
                            m.NotifyOfPropertyChange(() => ((TLMessage)m).Self);
                            ((TLMessage)m).Media.IsoFileName = item.IsoFileName;
                            break;
                        }
                    }
                });
            }
        }
    }

    public class SearchDocumentsRequest
    {
        public volatile bool IsCanceled;

        public TLInputPeerBase InputPeer { get; private set; }

        public string Text { get; private set; }

        public IList<TLMessageBase> Source { get; private set; }

        public IList<TLMessageBase> Results { get; private set; }

        public Dictionary<int, int> ResultsIndex { get; private set; } 

        public IList<TLMessageBase> GlobalResults { get; set; }

        public SearchDocumentsRequest(TLInputPeerBase inputPeer, string text, IList<TLMessageBase> source)
        {
            InputPeer = inputPeer;
            Text = text;
            Source = source;
        }

        public void ProcessAsync(Action<IList<TLMessageBase>> callback)
        {
            if (Results != null)
            {
                IsCanceled = false;
                callback.SafeInvoke(Results);
                return;
            }

            var source = Source;
            Execute.BeginOnThreadPool(() =>
            {
                var items = new List<TLMessageBase>(source.Count);
                var itemsIndex = new Dictionary<int, int>(source.Count);
                foreach (var messageBase in source)
                {
                    var message = messageBase as TLMessage;
                    if (message == null) continue;

                    var mediaDocument = message.Media as TLMessageMediaDocument;
                    if (mediaDocument == null) continue;

                    var document = mediaDocument.Document as TLDocument;
                    if (document == null) continue;

                    var fileName = document.FileName.ToString();
                    if (string.IsNullOrEmpty(fileName)) continue;

                    if (fileName.IndexOf(Text, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        itemsIndex[messageBase.Index] = messageBase.Index;
                        items.Add(messageBase);
                    }
                }
                ResultsIndex = itemsIndex;
                Results = items;
                callback.SafeInvoke(Results);
            });
        }

        public void Cancel()
        {
            IsCanceled = true;
        }

        public void CancelAsync()
        {
            Execute.BeginOnThreadPool(Cancel);
        }
    }
}
