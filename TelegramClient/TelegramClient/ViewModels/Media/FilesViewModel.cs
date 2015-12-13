using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using Telegram.Api.Aggregator;
using TelegramClient.Helpers;
using Caliburn.Micro;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.FileManager;
using Telegram.Api.TL;
using Telegram.Api.TL.Interfaces;
using TelegramClient.Converters;
using TelegramClient.Resources;
using TelegramClient.Services;
using TelegramClient.ViewModels.Dialogs;
using TelegramClient.ViewModels.Search;
using Execute = Telegram.Api.Helpers.Execute;

namespace TelegramClient.ViewModels.Media
{
    public class FilesViewModel<T> : FilesViewModelBase<T> where T : IInputPeer
    {
        public override TLInputMessagesFilterBase InputMessageFilter
        {
            get { return new TLInputMessagesFilterDocument(); }
        }

        public override string EmptyListImageSource
        {
            get
            {
                var isLightTheme = (Visibility)Application.Current.Resources["PhoneLightThemeVisibility"] == Visibility.Visible;

                if (isLightTheme)
                {
                    return "/Images/Messages/file.white-WXGA.png";
                }

                return "/Images/Messages/file.black-WXGA.png";
            }
        }

        public FilesViewModel(ICacheService cacheService, ICommonErrorHandler errorHandler, IStateService stateService, INavigationService navigationService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator)
            : base(cacheService, errorHandler, stateService, navigationService, mtProtoService, eventAggregator)
        {
            DisplayName = LowercaseConverter.Convert(AppResources.SharedFiles);
        }

        protected override bool SkipMessage(TLMessageBase messageBase)
        {
            var message = messageBase as TLMessage;
            if (message == null)
            {
                return true;
            }

            var mediaDocument = message.Media as TLMessageMediaDocument;
            if (mediaDocument == null)
            {
                return true;
            }

            var document = mediaDocument.Document as TLDocument;
            if (document == null)
            {
                return true;
            }

            if (message.IsSticker()
                || document.FileName.ToString().EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }
    }

    public abstract class FilesViewModelBase<T> : ItemsViewModelBase<TLMessage>,
        Telegram.Api.Aggregator.IHandle<DownloadableItem>, 
        Telegram.Api.Aggregator.IHandle<DeleteMessagesEventArgs>,
        ISliceLoadable
        where T : IInputPeer
    {
        public abstract TLInputMessagesFilterBase InputMessageFilter { get; }

        private bool _isSelectionEnabled;

        public bool IsSelectionEnabled
        {
            get { return _isSelectionEnabled; }
            set { SetField(ref _isSelectionEnabled, value, () => IsSelectionEnabled); }
        }

        public bool IsGroupActionEnabled
        {
            get { return Items.Any(x => x.IsSelected); }
        }

        public ObservableCollection<TimeKeyGroup<TLMessageBase>> Files { get; set; } 

        public bool IsEmptyList { get; protected set; }

        public abstract string EmptyListImageSource { get; }

        public T CurrentItem { get; set; }

        private IDocumentFileManager _downloadDocumentFileManager;

        private IDocumentFileManager DownloadDocumentFileManager
        {
            get { return _downloadDocumentFileManager ?? (_downloadDocumentFileManager = IoC.Get<IDocumentFileManager>()); }
        }

        public AnimatedImageViewerViewModel AnimatedImageViewer { get; protected set; }

        public FilesViewModelBase(ICacheService cacheService, ICommonErrorHandler errorHandler, IStateService stateService, INavigationService navigationService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator)
            : base(cacheService, errorHandler, stateService, navigationService, mtProtoService, eventAggregator)
        {
            Files = new ObservableCollection<TimeKeyGroup<TLMessageBase>>();

            IsEmptyList = false;
            Items = new ObservableCollection<TLMessage>();

            EventAggregator.Subscribe(this);

            PropertyChanged += OnPropertyChanged;
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (Property.NameEquals(e.PropertyName, () => IsSelectionEnabled))
            {
                if (!IsSelectionEnabled)
                {
                    foreach (var item in Items)
                    {
                        item.IsSelected = false;
                    }
                }
            }
        }

        protected override void OnInitialize()
        {
            Status = AppResources.Loading;
            BeginOnThreadPool(() => 
                CacheService.GetHistoryAsync(
                    new TLInt(StateService.CurrentUserId),
                    TLUtils.InputPeerToPeer(CurrentItem.ToInputPeer(), StateService.CurrentUserId),
                    messages => BeginOnUIThread(() =>
                    {
                        Items.Clear();

                        if (messages.Count > 0)
                        {
                            _lastMinId = messages.Min(x => x.Index);
                        }
                        AddMessages(messages);

                        Status = Items.Count > 0 ? string.Empty : Status;

                        LoadNextSlice();
                    })));


            base.OnInitialize();
        }

        private  bool _isLastSliceLoaded;

        private int _lastMinId;

        public void LoadNextSlice()
        {
            if (LazyItems.Count > 0) return;
            if (IsWorking) return;
            if (_isLastSliceLoaded) return;

            if (CurrentItem is TLBroadcastChat && !(CurrentItem is TLChannel))
            {
                Status = string.Empty;
                if (Items.Count == 0)
                {
                    IsEmptyList = true;
                    NotifyOfPropertyChange(() => IsEmptyList);
                }

                return;
            }

            IsWorking = true;
            MTProtoService.SearchAsync(
                CurrentItem.ToInputPeer(),
                TLString.Empty,
                InputMessageFilter,
                new TLInt(0), new TLInt(0), new TLInt(0), new TLInt(_lastMinId), new TLInt(Constants.FileSliceLength),
                messages => BeginOnUIThread(() =>
                {
                    if (messages.Messages.Count == 0
                        || messages.Messages.Count < Constants.FileSliceLength)
                    {
                        _isLastSliceLoaded = true;
                    }

                    if (messages.Messages.Count > 0)
                    {
                        _lastMinId = messages.Messages.Min(x => x.Index);
                    }
                    AddMessages(messages.Messages.ToList());

                    Status = string.Empty;
                    if (Items.Count == 0)
                    {
                        IsEmptyList = true;
                        NotifyOfPropertyChange(() => IsEmptyList);
                    }

                    IsWorking = false;
                }),
                error =>
                {
                    Execute.ShowDebugMessage("messages.search error " + error);
                    Status = string.Empty;
                    IsWorking = false;
                });
        }

        public void ChangeGroupActionStatus()
        {
            NotifyOfPropertyChange(() => IsGroupActionEnabled);
        }

        public void Manage()
        {
            IsSelectionEnabled = !IsSelectionEnabled;
        }

        protected abstract bool SkipMessage(TLMessageBase message);

        private void AddMessages(IList<TLMessageBase> messages)
        {
            foreach (var messageBase in messages)
            {
                if (SkipMessage(messageBase))
                {
                    continue;
                }

                var date = TLUtils.ToDateTime(((TLMessage)messageBase).Date);
                var yearMonthKey = new DateTime(date.Year, date.Month, 1);
                var timeKeyGroup = Files.FirstOrDefault(x => x.Key == yearMonthKey);
                if (timeKeyGroup != null)
                {
                    timeKeyGroup.Add(messageBase);
                }
                else
                {
                    Files.Add(new TimeKeyGroup<TLMessageBase>(yearMonthKey) { messageBase });
                }

                Items.Add(messageBase);
            }
        }

        public void DeleteMessage(TLMessageBase message)
        {
            if (message == null) return;

            var messages = new List<TLMessageBase> { message };

            var owner = CurrentItem as TLObject;

            if (CurrentItem is TLBroadcastChat)
            {
                DeleteMessagesInternal(owner, null, messages);
                return;
            }

            if ((message.Id == null || message.Id.Value == 0) && message.RandomIndex != 0)
            {
                DeleteMessagesInternal(owner, null, messages);
                return;
            }

            DialogDetailsViewModel.DeleteMessages(MTProtoService, null, null, messages, null, (result1, result2) => DeleteMessagesInternal(owner, result1, result2));
        }

        private void DeleteMessagesInternal(TLObject owner, TLMessageBase lastMessage, IList<TLMessageBase> messages)
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
                    for (var j = 0; j < Files.Count; j++)
                    {
                        for (var k = 0; k < Files[j].Count; k++)
                        {
                            if (Files[j][k].Index == messages[i].Index)
                            {
                                Files[j].RemoveAt(k);
                                break;
                            }
                        }
                    }
                    messages[i].IsSelected = false;
                    Items.Remove(messages[i]);
                }
            });

            EventAggregator.Publish(new DeleteMessagesEventArgs { Owner = owner, Messages = messages });
        }

        public void DeleteMessages()
        {
            if (Items == null) return;

            var messages = new List<TLMessageBase>();
            foreach (var item in Items.Where(x => x.IsSelected))
            {
                messages.Add(item);
            }

            if (messages.Count == 0) return;

            var randomItems = messages.Where(x => (x.Id == null || x.Id.Value == 0) && x.RandomId != null).ToList();
            var items = messages.Where(x => x.Id != null && x.Id.Value != 0).ToList();

            if (randomItems.Count == 0 && items.Count == 0)
            {
                return;
            }

            IsSelectionEnabled = false;

            var owner = CurrentItem as TLObject;

            if (CurrentItem is TLBroadcastChat)
            {
                DeleteMessagesInternal(owner, null, randomItems);
                DeleteMessagesInternal(owner, null, items);
                return;
            }

            DialogDetailsViewModel.DeleteMessages(MTProtoService, null, randomItems, items, (result1, result2) => DeleteMessagesInternal(owner, result1, result2), (result1, result2) => DeleteMessagesInternal(owner, result1, result2));
        }

        public void ForwardMessages()
        {
            if (Items == null) return;

            var messages = new List<TLMessageBase>();
            foreach (var item in Items.Where(x => x.IsSelected))
            {
                messages.Add(item);
            }

            if (messages.Count == 0) return;

            IsSelectionEnabled = false;

            DialogDetailsViewModel.ForwardMessagesCommon(messages, StateService, NavigationService);
        }

        public void ForwardMessage(TLMessageBase message)
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

        public void CancelDocumentDownloading(TLMessageMediaDocument mediaDocument)
        {
            BeginOnThreadPool(() =>
            {
                BeginOnUIThread(() =>
                {
                    var message = Items.FirstOrDefault(x => x.Media == mediaDocument);

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
                        var media = m.Media as TLMessageMediaDocument;
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
                var mediaDocument1 = message.Media as TLMessageMediaDocument;
                if (mediaDocument1 == null) return;

                BeginOnUIThread(() =>
                {
                    foreach (var m in Items)
                    {
                        var mediaDocument2 = m.Media as TLMessageMediaDocument;
                        if (mediaDocument2 != null && TLDocumentBase.DocumentEquals(mediaDocument1.Document, mediaDocument2.Document))
                        {
                            m.Media.IsCanceled = false;
                            m.Media.LastProgress = 0.0;
                            m.Media.DownloadingProgress = 0.0;
                            m.Media.NotifyOfPropertyChange(() => m.Media.Self); // update download icon for documents
                            m.NotifyOfPropertyChange(() => m.Self);
                            m.Media.IsoFileName = item.IsoFileName;
                        }
                    }
                });
            }
        }

        public void Search()
        {
            StateService.CurrentInputPeer = CurrentItem;
            var source = new List<TLMessageBase>(Items.Count);
            foreach (var item in Items)
            {
                source.Add(item);
            }

            StateService.Source = source;
            NavigationService.UriFor<SearchFilesViewModel>().Navigate();
        }

        public void Handle(DeleteMessagesEventArgs args)
        {
            var owner = CurrentItem as TLObject;
            if (owner == null) return;

            if (owner == args.Owner)
            {
                BeginOnUIThread(() =>
                {
                    for (var j = 0; j < args.Messages.Count; j++)
                    {
                        for (var i = 0; i < Items.Count; i++)
                        {
                            if (Items[i].Index == args.Messages[j].Index)
                            {
                                Items.RemoveAt(i);
                                break;
                            }
                        }
                    }
                });
            }
        }
    }

    public class TimeKeyGroup<T> : ObservableCollection<T>
    {
        public DateTime Key { get; private set; }

        public string KeyString { get { return Key.ToString("MMMM yyyy"); } }

        public TimeKeyGroup(DateTime key)
        {
            Key = key;
        }
    }

    public interface ISliceLoadable
    {
        void LoadNextSlice();
    }

    public class DeleteMessagesEventArgs
    {
        public TLObject Owner { get; set; }

        public IList<TLMessageBase> Messages { get; set; }
    }
}
