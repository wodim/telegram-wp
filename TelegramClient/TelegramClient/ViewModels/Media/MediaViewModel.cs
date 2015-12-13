using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Caliburn.Micro;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.FileManager;
using Telegram.Api.TL;
using Telegram.Api.TL.Interfaces;
using TelegramClient.Converters;
using TelegramClient.Resources;
using TelegramClient.Services;
using Execute = Telegram.Api.Helpers.Execute;

namespace TelegramClient.ViewModels.Media
{
    public class MediaViewModel<T> : ItemsViewModelBase<MessagesRow>, Telegram.Api.Aggregator.IHandle<DownloadableItem>,
        ISliceLoadable
        where T : IInputPeer
    {
        public ObservableCollection<TimeKeyGroup<MessagesRow>> Media { get; set; } 

        public bool IsEmptyList { get; protected set; }

        public string EmptyListImageSource
        {
            get
            {
                var isLightTheme = (Visibility)Application.Current.Resources["PhoneLightThemeVisibility"] == Visibility.Visible;

                if (isLightTheme)
                {
                    return "/Images/Messages/media.white-WXGA.png";
                }

                return "/Images/Messages/media.black-WXGA.png";
            }
        }

        public T CurrentItem { get; set; }

        private readonly IFileManager _downloadFileManager;

        public ImageViewerViewModel ImageViewer { get; set; }

        private readonly IList<TLMessage> _items = new List<TLMessage>();

        public MediaViewModel(IFileManager downloadFileManager, ICacheService cacheService, ICommonErrorHandler errorHandler, IStateService stateService, INavigationService navigationService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator) 
            : base(cacheService, errorHandler, stateService, navigationService, mtProtoService, eventAggregator)
        {
            IsEmptyList = false;
            Items = new ObservableCollection<MessagesRow>();
            Media = new ObservableCollection<TimeKeyGroup<MessagesRow>>();

            _downloadFileManager = downloadFileManager;

            DisplayName = LowercaseConverter.Convert(AppResources.Media);
            EventAggregator.Subscribe(this);
        }

        protected override void OnInitialize()
        {
            Status = string.Empty;  //AppResources.Loading;
            BeginOnThreadPool(() => 
                CacheService.GetHistoryAsync(
                    new TLInt(StateService.CurrentUserId), 
                    TLUtils.InputPeerToPeer(CurrentItem.ToInputPeer(), StateService.CurrentUserId),
                    messages => BeginOnUIThread(() =>
                    {
                        Items.Clear();

                        AddMessages(messages);

                        Status = Items.Count > 0 ? string.Empty : Status; 

                        LoadNextSlice();
                    })));

            base.OnInitialize();
        }

        private void AddMessages(IList<TLMessageBase> messages)
        {
            var isNewRow = false;
            var row = Items.LastOrDefault();
            if (row == null || row.IsFull())
            {
                row = new MessagesRow();
                isNewRow = true;
            }

            for (var i = 0; i < messages.Count; i++)
            {
                var message = messages[i] as TLMessage;
                if (message == null) continue;

                if (message.Media is TLMessageMediaPhoto
                    || message.Media is TLMessageMediaVideo)
                {
                    _items.Add(message);
                    if (!row.AddMessage(message))
                    {
                        if (isNewRow)
                        {
                            AddToTimeKeyCollection(row);
                            Items.Add(row);
                        }

                        row = new MessagesRow();
                        isNewRow = true;
                        row.AddMessage(message);
                    }
                }
            }

            if (isNewRow && !row.IsEmpty())
            {
                AddToTimeKeyCollection(row);
                Items.Add(row);
            }
        }

        private void AddToTimeKeyCollection(MessagesRow row)
        {
            var date = TLUtils.ToDateTime(row.Message1.Date);
            var yearMonthKey = new DateTime(date.Year, date.Month, 1);
            var timeKeyGroup = Media.FirstOrDefault(x => x.Key == yearMonthKey);
            if (timeKeyGroup != null)
            {
                timeKeyGroup.Add(row);
            }
            else
            {
                Media.Add(new TimeKeyGroup<MessagesRow>(yearMonthKey) {row});
            }
        }

        private bool _isLastSliceLoaded;

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

            var maxId = 0;
            var lastItem = _items.LastOrDefault();
            if (lastItem != null)
            {
                maxId = lastItem.Index;
            }

            IsWorking = true;
            MTProtoService.SearchAsync(
                CurrentItem.ToInputPeer(),
                TLString.Empty,
                new TLInputMessagesFilterPhotoVideo(),
                new TLInt(0), new TLInt(0), new TLInt(0), new TLInt(maxId), new TLInt(Constants.PhotoVideoSliceLength),
                messages => BeginOnUIThread(() =>
                {
                    AddMessages(messages.Messages.ToList());

                    if (messages.Messages.Count < Constants.PhotoVideoSliceLength)
                    {
                        _isLastSliceLoaded = true;
                    }

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

        public void OpenMedia(TLMessage message)
        {
            if (message == null) return;

            StateService.CurrentMediaMessages = _items;
            StateService.CurrentPhotoMessage = message;

            if (ImageViewer != null)
            {
                ImageViewer.OpenViewer();
            }
        }

        public void Handle(DownloadableItem item)
        {
            var photo = item.Owner as TLPhoto;
            if (photo != null)
            {
                var isUpdated = false;
                var messages = _items;
                foreach (var m in messages)
                {
                    var media = m.Media as TLMessageMediaPhoto;
                    if (media != null && media.Photo == photo)
                    {
                        media.NotifyOfPropertyChange(() => media.Photo);
                        media.NotifyOfPropertyChange(() => media.Self);
                        isUpdated = true;
                        break;
                    }
                }

                if (isUpdated) return;

                var serviceMessages = Items.OfType<TLMessageService>();
                foreach (var serviceMessage in serviceMessages)
                {
                    var editPhoto = serviceMessage.Action as TLMessageActionChatEditPhoto;
                    if (editPhoto != null && editPhoto.Photo == photo)
                    {
                        editPhoto.NotifyOfPropertyChange(() => editPhoto.Photo);
                        isUpdated = true;
                        break;
                    }
                }
            }

            var video = item.Owner as TLVideo;
            if (video != null)
            {
                var messages = _items;
                foreach (var m in messages)
                {
                    var media = m.Media as TLMessageMediaVideo;
                    if (media != null && media.Video == video)
                    {
                        media.NotifyOfPropertyChange(() => media.Video);
                        break;
                    }
                }
            }

            var message = item.Owner as TLMessage;
            if (message != null)
            {
                var messages = _items;
                foreach (var m in messages)
                {
                    var media = m.Media as TLMessageMediaVideo;
                    if (media != null && m == item.Owner)
                    {
                        m.Media.LastProgress = 0.0;
                        m.Media.DownloadingProgress = 0.0;
                        m.Media.IsoFileName = item.IsoFileName;
                        break;
                    }
                }
                return;
            }
        }

        public void CancelDownloading(TLPhotoBase photo)
        {
            _downloadFileManager.CancelDownloadFile(photo);
        }

        public void CancelDownloading()
        {
            BeginOnThreadPool(() =>
            {
                foreach (var item in _items)
                {
                    var mediaPhoto = item.Media as TLMessageMediaPhoto;
                    if (mediaPhoto != null)
                    {
                        CancelDownloading(mediaPhoto.Photo);
                    }
                }
            });
        }
    }

    public class MessagesRow : PropertyChangedBase
    {
        public TLMessage Message1 { get; set; }

        public TLMessage Message2 { get; set; }

        public TLMessage Message3 { get; set; }

        public TLMessage Message4 { get; set; }

        public bool AddMessage(TLMessage message)
        {
            if (Message1 == null)
            {
                Message1 = message;
                NotifyOfPropertyChange(() => Message1);
                return true;
            }

            var date1 = TLUtils.ToDateTime(Message1.Date);
            var date = TLUtils.ToDateTime(message.Date);
            if (date1.Year != date.Year || date1.Month != date.Month)
            {
                return false;
            }

            if (Message2 == null)
            {
                Message2 = message;
                NotifyOfPropertyChange(() => Message2);
                return true;
            }

            if (Message3 == null)
            {
                Message3 = message;
                NotifyOfPropertyChange(() => Message3);
                return true;
            }

            if (Message4 == null)
            {
                Message4 = message;
                NotifyOfPropertyChange(() => Message4);
                return true;
            }

            return false;
        }

        public bool IsEmpty()
        {
            return Message1 == null;
        }

        public bool IsFull()
        {
            return Message4 != null;
        }
    }
}
