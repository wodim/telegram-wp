using System;
using System.Collections.Generic;
using System.IO;
using System.IO.IsolatedStorage;
using System.Threading;
using System.Windows;
using Caliburn.Micro;
using Microsoft.Phone.Tasks;
using Microsoft.Xna.Framework.Media;
#if WP8
using Windows.Storage;
using Windows.Storage.Streams;
using Microsoft.Xna.Framework.Media.PhoneExtensions;
#endif
using Telegram.Api.Services.FileManager;
using Telegram.Api.TL;
using TelegramClient.Resources;
using TelegramClient.Services;
using TelegramClient.ViewModels.Dialogs;

namespace TelegramClient.ViewModels.Media
{
    public class DecryptedImageViewerViewModel : PropertyChangedBase
    {
        private IList<TLDecryptedMessage> _items = new List<TLDecryptedMessage>();

        private int _currentIndex;

        private TLDecryptedMessageBase _currentItem;

        public TLDecryptedMessageBase CurrentItem
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

        public bool ShowOpenMediaListButton { get; protected set; }

        public SecretDialogDetailsViewModel DialogDetails { get; set; }

        public DecryptedImageViewerViewModel(IStateService stateService, bool showMediaButton = false)
        {
            StateService = stateService;

            ShowOpenMediaListButton = showMediaButton;
            //OpenViewer();
        }

        public void Delete()
        {
            if (CurrentItem == null) return;
            if (DialogDetails == null) return;

            var previousItem = CurrentItem as TLDecryptedMessage;

            DialogDetails.DeleteMessage((TLDecryptedMessage)CurrentItem);
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
        }

        public void Forward()
        {
            if (CurrentItem == null) return;
            if (DialogDetails == null) return;

            //SecretDialogDetails.ForwardMessage(CurrentItem);
        }

        public void OpenMediaDetails()
        {
            if (DialogDetails == null) return;

            StateService.MediaTab = true;
            StateService.CurrentDecryptedMediaMessages = _items;
            DialogDetails.OpenPeerDetails();
        }

#if DEBUG
        //~ImageViewerViewModel()
        //{
        //    TLUtils.WritePerformance("++ImageViewerVM dstr");
        //}
#endif

        private bool _isOpen;

        public bool IsOpen { get { return _isOpen; } }

        public event EventHandler Open;

        protected virtual void RaiseOpen()
        {
            EventHandler handler = Open;
            if (handler != null) handler(this, System.EventArgs.Empty);
        }

        public void OpenViewer()
        {
            CurrentItem = StateService.CurrentDecryptedPhotoMessage;
            _items = StateService.CurrentDecryptedMediaMessages;
            if (_items != null)
            {
                _currentIndex = _items.IndexOf(StateService.CurrentDecryptedPhotoMessage);
            }
            _isOpen = CurrentItem != null;
            NotifyOfPropertyChange(() => CurrentItem);
            NotifyOfPropertyChange(() => IsOpen);
            NotifyOfPropertyChange(() => CanZoom);


            StateService.CurrentDecryptedPhotoMessage = null;
            StateService.CurrentDecryptedMediaMessages = null;

            RaiseOpen();
        }

        public event EventHandler Close;

        protected virtual void RaiseClose()
        {
            EventHandler handler = Close;
            if (handler != null) handler(this, System.EventArgs.Empty);
        }


        public void CloseViewer()
        {
            _isOpen = false;
            NotifyOfPropertyChange(() => IsOpen);

            RaiseClose();
        }

#if WP8
        public async void Save(Stream fileStream)
        {
            if (fileStream == null) return;

            var telegramFolder = await KnownFolders.PicturesLibrary.CreateFolderAsync(Constants.TelegramFolderName, CreationCollisionOption.OpenIfExists);
            if (telegramFolder == null) return;

            var storageFile = await telegramFolder.CreateFileAsync(Guid.NewGuid() + ".jpg", CreationCollisionOption.ReplaceExisting);

            using (var storageStream = await storageFile.OpenAsync(FileAccessMode.ReadWrite))
            {
                var outputStream = storageStream.GetOutputStreamAt(0);
                await RandomAccessStream.CopyAndCloseAsync(fileStream.AsInputStream(), outputStream);
            }

            MessageBox.Show(AppResources.SavePhotoMessage);
        }
#else
        public void Save(Stream fileStream)
        {
            var photoUrl = Guid.NewGuid().ToString(); //.jpg will be added automatically            
            var mediaLibrary = new MediaLibrary();
            mediaLibrary.SavePicture(photoUrl, fileStream);

            MessageBox.Show(AppResources.SavePhotoMessage);
        }
#endif

        public void Share(Stream fileStream)
        {
            if (fileStream == null) return;

            var photoUrl = Guid.NewGuid().ToString(); //.jpg will be added automatically

            var mediaLibrary = new MediaLibrary();
            var picture = mediaLibrary.SavePicture(photoUrl, fileStream);

#if WP8
            var task = new ShareMediaTask { FilePath = picture.GetPath() };
            task.Show();
#endif
        }

        public bool CanZoom
        {
            get
            {
                //return true;
                return CurrentItem != null && ((TLDecryptedMessage)CurrentItem).Media is TLDecryptedMessageMediaPhoto;
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
            var message = CurrentItem as TLDecryptedMessage;
            if (message == null) return;

            var mediaVideo = message.Media as TLDecryptedMessageMediaVideo;
            if (mediaVideo != null)
            {
                var fileLocation = mediaVideo.File as TLEncryptedFile;
                if (fileLocation == null) return;

                var fileName = String.Format("{0}_{1}_{2}.mp4",
                fileLocation.Id,
                fileLocation.DCId,
                fileLocation.AccessHash);

                using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                {
                    if (store.FileExists(fileName))
                    {
                        var launcher = new MediaPlayerLauncher();
                        launcher.Location = MediaLocationType.Data;
                        launcher.Media = new Uri(fileName, UriKind.Relative);
                        launcher.Show();
                    }
                    else
                    {
                        mediaVideo.DownloadingProgress = 0.001;
                        var fileManager = IoC.Get<IEncryptedFileManager>();
                        fileManager.DownloadFile(fileLocation, mediaVideo);

                        //DownloadVideoFileManager.DownloadFileAsync(mediaVideo.ToInputFileLocation(), message, mediaVideo.Size);
                    }
                }
            }
        }

        public void Handle(DownloadableItem item)
        {
            var message = item.Owner as TLDecryptedMessage;
            if (message != null && _items != null)
            {
                var messages = _items;
                foreach (var m in messages)
                {
                    var media = m.Media as TLDecryptedMessageMediaVideo;
                    if (media != null && m == item.Owner)
                    {
                        m.Media.DownloadingProgress = 0.0;
                        //m.Media.IsoFileName = item.IsoFileName;
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
            var message = CurrentItem as TLDecryptedMessage;
            if (message == null) return;

            message.Media.DownloadingProgress = 0.0;
            var fileManager = IoC.Get<IEncryptedFileManager>();
            fileManager.CancelDownloadFile(message.Media);
            //DownloadVideoFileManager.CancelDownloadFileAsync(message);
        }

        //public void CancelVideoDownloading()
        //{
        //    var message = CurrentItem as TLDecryptedMessage;
        //    if (message == null) return;

        //    var mediaVideo = message.Media as TLDecryptedMessageMediaVideo;
        //    if (mediaVideo != null)
        //    {
        //        ThreadPool.QueueUserWorkItem(state =>
        //        {
        //            mediaVideo.DownloadingProgress = 0.0;
        //            _downloadVideoFileManager.CancelDownloadFileAsync(message);
        //        });
        //    }
        //}
    }
}
