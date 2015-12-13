using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Threading;
using System.Windows;
using Caliburn.Micro;
using Microsoft.Phone.Tasks;
using Microsoft.Xna.Framework.Media;
using Telegram.Api.Extensions;
using TelegramClient.ViewModels.Dialogs;
using TelegramClient.Views.Media;
#if WP8
using Windows.Storage.Streams;
using Windows.Storage;
using Microsoft.Xna.Framework.Media.PhoneExtensions;
#endif
using Telegram.Api.Services.FileManager;
using Telegram.Api.TL;
using TelegramClient.Resources;
using TelegramClient.Services;
using Execute = Telegram.Api.Helpers.Execute;

namespace TelegramClient.ViewModels.Media
{
    public class ImageViewerViewModel : PropertyChangedBase
    {

        private IList<TLMessage> _items = new List<TLMessage>(); 

        private int _currentIndex;

        private TLMessageBase _currentItem;

        public TLMessageBase CurrentItem
        {
            get { return _currentItem; }
            set
            {
                if (_currentItem != value)
                {
                    _currentItem = value;
                    NotifyOfPropertyChange(() => CurrentItem);
                }
            }
        }

        public IStateService StateService { get; protected set; }

        private readonly IVideoFileManager _downloadVideoFileManager;

        public bool ShowOpenMediaListButton { get; protected set; }

        public DialogDetailsViewModel DialogDetails { get; set; }

        public ImageViewerViewModel(IStateService stateService, IVideoFileManager downloadVideoFileManager, bool showMediaButton = false)
        {
            StateService = stateService;
            _downloadVideoFileManager = downloadVideoFileManager;

            ShowOpenMediaListButton = showMediaButton;
        }

        public void Delete()
        {
            if (CurrentItem == null) return;
            if (DialogDetails == null) return;

            var previousItem = CurrentItem as TLMessage;

            DialogDetails.DeleteMessageById(
                CurrentItem, 
                () =>
                {

                    if (CanSlideLeft)
                    {
                        SlideLeft();
                    }
                    else if (_items.Count == 1)
                    {
                        CloseViewer();
                    }
                    else
                    {
                        SlideRight();
                    }
                    if (previousItem != null)
                    {
                        _items.Remove(previousItem);
                        if (_currentIndex > 0) _currentIndex--;
                    }
                });
        }

        public void Forward()
        {
            if (CurrentItem == null) return;
            //if (DialogDetails == null) return;

            DialogDetailsViewModel.ForwardMessagesCommon(new List<TLMessageBase>{ CurrentItem }, StateService, IoC.Get<INavigationService>());
        }

        public void OpenMediaDetails()
        {
            if (DialogDetails == null) return;

            StateService.MediaTab = true;
            DialogDetails.OpenPeerDetails();
        }

        public void OpenViewer()
        {
            CurrentItem = StateService.CurrentPhotoMessage;
            _items = StateService.CurrentMediaMessages;
            if (_items != null)
            {
                _currentIndex = _items.IndexOf(StateService.CurrentPhotoMessage);
            }
            _isOpen = CurrentItem != null;
            NotifyOfPropertyChange(() => CurrentItem);
            NotifyOfPropertyChange(() => IsOpen);
            NotifyOfPropertyChange(() => CanZoom);

            StateService.CurrentPhotoMessage = null;
            StateService.CurrentMediaMessages = null;

            RaiseOpen();
        }

        public void CloseViewer()
        {
            _isOpen = false;
            NotifyOfPropertyChange(() => IsOpen);

            RaiseClose();
        }

        private bool _isOpen;

        public bool IsOpen { get { return _isOpen; } }

        public event EventHandler Open;

        protected virtual void RaiseOpen()
        {
            var handler = Open;
            if (handler != null) handler(this, System.EventArgs.Empty);
        }

        public event EventHandler Close;

        protected virtual void RaiseClose()
        {
            var handler = Close;
            if (handler != null) handler(this, System.EventArgs.Empty);
        }

#if WP8
        private static async void SaveFileAsync(string fileName, string fileExt, Action<string> callback = null)
        {
            using (var store = IsolatedStorageFile.GetUserStoreForApplication())
            {
                if (store.FileExists(fileName))
                {
                    using (var fileStream = store.OpenFile(fileName, FileMode.Open))
                    {
                        var telegramFolder = await KnownFolders.PicturesLibrary.CreateFolderAsync(Constants.TelegramFolderName, CreationCollisionOption.OpenIfExists);
                        if (telegramFolder == null) return;

                        var ext = fileExt.StartsWith(".") ? fileExt : "." + fileExt;
                        var storageFile = await telegramFolder.CreateFileAsync(Guid.NewGuid() + ext, CreationCollisionOption.ReplaceExisting);

                        var stopwatch1 = Stopwatch.StartNew();
                        using (var storageStream = await storageFile.OpenStreamForWriteAsync())
                        {
                            fileStream.CopyTo(storageStream);
                        }

                        if (callback != null)
                        {
                            callback.Invoke(storageFile.Path);
                        }
                        else
                        {
                            var elapsed1 = stopwatch1.Elapsed;
                            //var stopwatch2 = Stopwatch.StartNew();
                            ////using (var storageStream = await storageFile.OpenStreamForWriteAsync())
                            //using (var storageStream = await storageFile.OpenAsync(FileAccessMode.ReadWrite))
                            //{
                            //    await RandomAccessStream.CopyAndCloseAsync(fileStream.AsInputStream(), storageStream.GetOutputStreamAt(0));
                            //}
                            //var elapsed2 = stopwatch2.Elapsed;
                            Execute.BeginOnUIThread(() => MessageBox.Show(AppResources.SaveFileMessage
#if DEBUG
                                + "\n Time: " + elapsed1
                                //+ "\n Time2: " + elapsed2
#endif

                            ));
                        }
                    }
                }
            }
        }
#endif


#if WP8
        private static void SaveVideo(string fileName)
        {
            SaveFileAsync(fileName, "mp4");
        }
#else
        private static void SaveVideo(string fileName)
        {

        }
#endif

#if WP8
        public static void SavePhoto(string fileName, Action<string> callback = null)
        {
            SaveFileAsync(fileName, "jpg", callback);
        }
#else
        public static void SavePhoto(string photoFileName, Action<string> callback = null)
        {
            using (var store = IsolatedStorageFile.GetUserStoreForApplication())
            {
                if (store.FileExists(photoFileName))
                {
                    using (var fileStream = store.OpenFile(photoFileName, FileMode.Open))
                    {
                        var photoUrl = Guid.NewGuid().ToString(); //.jpg will be added automatically            
                        var mediaLibrary = new MediaLibrary();
                        var photoFile = mediaLibrary.SavePicture(photoUrl, fileStream);

                        if (callback != null)
                        {
                            
                        }
                        else
                        {
                            Execute.BeginOnUIThread(() => MessageBox.Show(AppResources.SavePhotoMessage));
                        }
                    }
                }
            }
        }
#endif

        private static void SavePhotoAsync(TLMessageMediaPhoto mediaPhoto, Action<string> callback = null)
        {
            var photo = mediaPhoto.Photo as TLPhoto;
            if (photo == null) return;

            TLPhotoSize size = null;
            var sizes = photo.Sizes.OfType<TLPhotoSize>();
            const double width = 800.0;
            foreach (var photoSize in sizes)
            {
                if (size == null
                    || Math.Abs(width - size.W.Value) > Math.Abs(width - photoSize.W.Value))
                {
                    size = photoSize;
                }
            }
            if (size == null) return;

            var location = size.Location as TLFileLocation;
            if (location == null) return;

            var fileName = String.Format("{0}_{1}_{2}.jpg",
                location.VolumeId,
                location.LocalId,
                location.Secret);

            Execute.BeginOnThreadPool(() => SavePhoto(fileName, callback));
        }

        private static void SaveVideoAsync(TLMessageMediaVideo mediaVideo)
        {
            var video = mediaVideo.Video as TLVideo;
            if (video == null) return;

            var fileName = video.GetFileName();

            Execute.BeginOnThreadPool(() => SaveVideo(fileName));
        }

        public void Save()
        {
            var message = CurrentItem as TLMessage;
            if (message == null) return;

            var mediaPhoto = message.Media as TLMessageMediaPhoto;
            if (mediaPhoto != null)
            {
                SavePhotoAsync(mediaPhoto);
                return;
            }

            var mediaVideo = message.Media as TLMessageMediaVideo;
            if (mediaVideo != null)
            {
                SaveVideoAsync(mediaVideo);
                return;
            }
        }

        public void Share()
        {
#if WP8
            var message = CurrentItem as TLMessage;
            if (message == null) return;

            var mediaPhoto = message.Media as TLMessageMediaPhoto;
            if (mediaPhoto != null)
            {
                SavePhotoAsync(mediaPhoto, path =>
                {
                    var task = new ShareMediaTask { FilePath = path };
                    task.Show();
                });
            }
#endif
        }

        public bool CanZoom
        {
            get
            {
                //return true;
                return CurrentItem != null && ((TLMessage) CurrentItem).Media is TLMessageMediaPhoto;
            }
        }

        public bool CanSlideRight
        {
            get { return _currentIndex > 0; }
        }

        public void SlideRight()
        {
            if (!CanSlideRight) return;

            var nextItem = _items[--_currentIndex];
            CurrentItem = nextItem;
            NotifyOfPropertyChange(() => CanZoom);
        }

        public bool CanSlideLeft
        {
            get { return _currentIndex < _items.Count - 1; }
        }

        public void SlideLeft()
        {
            if (!CanSlideLeft) return;

            var nextItem = _items[++_currentIndex];
            CurrentItem = nextItem;
            NotifyOfPropertyChange(() => CanZoom);
        }

        public void OpenMedia()
        {
            var message = CurrentItem as TLMessage;
            if (message == null) return;

            var mediaVideo = message.Media as TLMessageMediaVideo;
            if (mediaVideo != null)
            {
                var video = mediaVideo.Video as TLVideo;
                if (video == null) return;

                if (string.IsNullOrEmpty(mediaVideo.IsoFileName))
                {
                    mediaVideo.IsCanceled = false;
                    mediaVideo.DownloadingProgress = mediaVideo.LastProgress > 0.0 ? mediaVideo.LastProgress : 0.001;
                    _downloadVideoFileManager.DownloadFileAsync(
                        video.DCId, video.ToInputFileLocation(), message, video.Size,
                        progress =>
                        {
                            if (progress > 0.0)
                            {
                                mediaVideo.DownloadingProgress = progress;
                            }
                        });
                }
                else
                {
                    var launcher = new MediaPlayerLauncher();
                    launcher.Location = MediaLocationType.Data;
                    launcher.Media = new Uri(mediaVideo.IsoFileName, UriKind.Relative);
                    launcher.Show();
                }
                return;
            }
        }


        public void Handle(DownloadableItem item)
        {
            var message = item.Owner as TLMessage;
            if (message != null && _items != null)
            {
                var messages = _items;
                foreach (var m in messages)
                {
                    var media = m.Media as TLMessageMediaVideo;
                    if (media != null && m == item.Owner)
                    {
                        m.Media.IsCanceled = false;
                        m.Media.DownloadingProgress = 0.0;
                        m.Media.IsoFileName = item.IsoFileName;
                        //MessageBox.Show("Download video time: " + _downloadVideoStopwatch.Elapsed);
                        //media.NotifyOfPropertyChange(() => media.Video);
                        break;
                    }
                }
                return;
            }
        }

        public void CancelVideoDownloading()
        {
            var message = CurrentItem as TLMessage;
            if (message == null) return;

            var mediaVideo = message.Media as TLMessageMediaVideo;
            if (mediaVideo != null)
            {
                mediaVideo.IsCanceled = true;
                mediaVideo.LastProgress = mediaVideo.DownloadingProgress;
                mediaVideo.DownloadingProgress = 0.0;
                _downloadVideoFileManager.CancelDownloadFileAsync(message);
            }
        }
    }
}
