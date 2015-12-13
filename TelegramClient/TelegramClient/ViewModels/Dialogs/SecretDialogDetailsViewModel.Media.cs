using System;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Windows;
using TelegramClient.ViewModels.Contacts;
#if WP8
using Windows.Storage;
#endif
using Caliburn.Micro;
using Microsoft.Phone.Tasks;
using Telegram.Api.Services.FileManager;
using Telegram.Api.TL;
using TelegramClient.ViewModels.Media;
using Execute = Telegram.Api.Helpers.Execute;

namespace TelegramClient.ViewModels.Dialogs
{
    public partial class SecretDialogDetailsViewModel
    {
        private void SendMedia()
        {
            if (!string.IsNullOrEmpty(StateService.VideoIsoFileName))
            {
                var videoIsoFileName = StateService.VideoIsoFileName;
                var videoDuration = StateService.Duration;
                StateService.VideoIsoFileName = null;
                StateService.Duration = 0;
                SendVideo(videoIsoFileName, videoDuration);
                return;
            }

            if (StateService.Photo != null)
            {
                var photo = StateService.Photo;
                StateService.Photo = null;

                SendPhoto(photo);
                return;
            }

            if (StateService.GeoPoint != null)
            {
                var geoPoint = StateService.GeoPoint;
                StateService.GeoPoint = null;

                SendGeoPoint(geoPoint);
                return;
            }

            if (StateService.Document != null)
            {
                var document = StateService.Document;
                StateService.Document = null;

                SendDocument(document);
                return;
            }
        }

        public void Attach()
        {
            BeginOnUIThread(() => ChooseAttachment.Open());
        }

        public TLDecryptedMessage SecretPhoto { get; set; }

        public bool OpenSecretPhoto(TLDecryptedMessageMediaPhoto mediaPhoto)
        {
            if (mediaPhoto == null) return false;

            TLDecryptedMessage17 message = null;
            for (var i = 0; i < Items.Count; i++)
            {
                var message17 = Items[i] as TLDecryptedMessage17;
                if (message17 != null && message17.Media == mediaPhoto)
                {
                    message = message17;
                    break;
                }
            }

            if (message == null) return false;
            if (message.Status == MessageStatus.Sending) return false;


            var result = false;
            if (!message.Out.Value)
            {
                if (message.TTL != null && message.TTL.Value > 0 && message.TTL.Value <= 60.0)
                {
                    if (mediaPhoto.TTLParams == null)
                    {
                        message.IsTTLStarted = true;
                        message.DeleteDate = new TLLong(DateTime.Now.Ticks + message.TTL.Value * TimeSpan.TicksPerSecond);
                        mediaPhoto.TTLParams = new TTLParams
                        {
                            StartTime = DateTime.Now,
                            IsStarted = true,
                            Total = message.TTL.Value
                        };
                        message.Unread = new TLBool(false);
                        message.Status = MessageStatus.Read;
                        CacheService.SyncDecryptedMessage(message, Chat, r =>
                        {
                            var chat = Chat as TLEncryptedChat;
                            if (chat == null) return;

                            var action = new TLDecryptedMessageActionReadMessages();
                            action.RandomIds = new TLVector<TLLong>{ message.RandomId };

                            var decryptedTuple = GetDecryptedServiceMessageAndObject(action, chat, MTProtoService.CurrentUserId, CacheService);

                            SendEncryptedService(chat, decryptedTuple.Item2, MTProtoService, CacheService,
                                sentEncryptedMessage =>
                                {

                                });

                        });
                    }
                    
                    SecretPhoto = message;
                    NotifyOfPropertyChange(() => SecretPhoto);

                    result = true;
                }
            }
            else
            {
                SecretPhoto = message;
                NotifyOfPropertyChange(() => SecretPhoto);

                result = true;
            }

            return result;
        }

#if WP8
        public async void OpenMedia(TLDecryptedMessage message)
#else
        public void OpenMedia(TLDecryptedMessage message)
#endif
        {
            if (message == null) return;
            if (message.Status == MessageStatus.Sending) return;

            var mediaPhoto = message.Media as TLDecryptedMessageMediaPhoto;
            if (mediaPhoto != null)
            {
                if (message.TTL != null && message.TTL.Value > 0 && message.TTL.Value <= 60.0)
                {
                    var decryptedMessage = message as TLDecryptedMessage17;
                    if (decryptedMessage != null)
                    {
                        return;
                    }
                }
                message.Unread = new TLBool(false);
                message.Status = MessageStatus.Read;
                CacheService.SyncDecryptedMessage(message, Chat, r => { });

                {
                    StateService.CurrentDecryptedPhotoMessage = message;
                    StateService.CurrentDecryptedMediaMessages =
                        Items
                        .OfType<TLDecryptedMessage>()
                        .Where(x => x.Media is TLDecryptedMessageMediaPhoto || x.Media is TLDecryptedMessageMediaVideo)
                        .ToList();

                    if (ImageViewer == null)
                    {
                        ImageViewer = new DecryptedImageViewerViewModel(StateService, true)
                        {
                            DialogDetails = this
                        };
                        NotifyOfPropertyChange(() => ImageViewer);
                    }

                    ImageViewer.OpenViewer();
                }

                return;
            }

            var mediaGeo = message.Media as TLDecryptedMessageMediaGeoPoint;
            if (mediaGeo != null)
            {
                StateService.DecryptedMediaMessage = message;
                NavigationService.UriFor<MapViewModel>().Navigate();
                return;
            }

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
                        var mediaVideo17 = mediaVideo as TLDecryptedMessageMediaVideo17;
                        if (mediaVideo17 != null)
                        {
                            if (!message.Out.Value)
                            {
                                if (message.TTL != null && message.TTL.Value > 0 && message.TTL.Value <= 60.0)
                                {
                                    if (mediaVideo17.TTLParams == null)
                                    {
                                        message.IsTTLStarted = true;
                                        message.DeleteDate = new TLLong(DateTime.Now.Ticks + Math.Max(mediaVideo17.Duration.Value + 1, message.TTL.Value) * TimeSpan.TicksPerSecond);
                                        mediaVideo17.TTLParams = new TTLParams
                                        {
                                            StartTime = DateTime.Now,
                                            IsStarted = true,
                                            Total = message.TTL.Value
                                        };
                                        message.Unread = new TLBool(false);
                                        message.Status = MessageStatus.Read;
                                        CacheService.SyncDecryptedMessage(message, Chat, r =>
                                        {
                                            var chat = Chat as TLEncryptedChat;
                                            if (chat == null) return;

                                            var action = new TLDecryptedMessageActionReadMessages();
                                            action.RandomIds = new TLVector<TLLong>{ message.RandomId };

                                            var decryptedTuple = GetDecryptedServiceMessageAndObject(action, chat, MTProtoService.CurrentUserId, CacheService);

                                            SendEncryptedService(chat, decryptedTuple.Item2, MTProtoService, CacheService,
                                                sentEncryptedMessage =>
                                                {

                                                });

                                        });
                                    }
                                }
                            }
                        }

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
                    }
                }

                return;
            }

            var mediaAudio = message.Media as TLDecryptedMessageMediaAudio;
            if (mediaAudio != null)
            {
                var fileLocation = mediaAudio.File as TLEncryptedFile;
                if (fileLocation == null) return;

                var fileName = String.Format("audio{0}_{1}.wav",
                    fileLocation.Id,
                    fileLocation.AccessHash);

                using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                {
                    if (!store.FileExists(fileName))
                    {
                        mediaAudio.DownloadingProgress = 0.001;
                        var fileManager = IoC.Get<IEncryptedFileManager>();
                        fileManager.DownloadFile(fileLocation, mediaAudio);
                    }
                    else
                    {
                        var mediaAudio17 = mediaAudio as TLDecryptedMessageMediaAudio17;
                        if (mediaAudio17 != null)
                        {
                            if (!message.Out.Value)
                            {
                                if (message.TTL != null && message.TTL.Value > 0 && message.TTL.Value <= 60.0)
                                {
                                    if (mediaAudio17.TTLParams == null)
                                    {
                                        message.IsTTLStarted = true;
                                        message.DeleteDate = new TLLong(DateTime.Now.Ticks + Math.Max(mediaAudio17.Duration.Value + 1, message.TTL.Value) * TimeSpan.TicksPerSecond);
                                        mediaAudio17.TTLParams = new TTLParams
                                        {
                                            StartTime = DateTime.Now,
                                            IsStarted = true,
                                            Total = message.TTL.Value
                                        };
                                        message.Unread = new TLBool(false);
                                        message.Status = MessageStatus.Read;
                                        CacheService.SyncDecryptedMessage(message, Chat, r =>
                                        {
                                            var chat = Chat as TLEncryptedChat;
                                            if (chat == null) return;

                                            var action = new TLDecryptedMessageActionReadMessages();
                                            action.RandomIds = new TLVector<TLLong> { message.RandomId };

                                            var decryptedTuple = GetDecryptedServiceMessageAndObject(action, chat, MTProtoService.CurrentUserId, CacheService);

                                            SendEncryptedService(chat, decryptedTuple.Item2, MTProtoService, CacheService,
                                                sentEncryptedMessage =>
                                                {

                                                });

                                        });
                                    }
                                }
                            }
                        }     
                    }
                }

                return;
            }

            var mediaDocument = message.Media as TLDecryptedMessageMediaDocument;
            if (mediaDocument != null)
            {
                var fileLocation = mediaDocument.File as TLEncryptedFile;
                if (fileLocation == null) return;

                var fileName = String.Format("{0}_{1}_{2}.{3}",
                    fileLocation.Id,
                    fileLocation.DCId,
                    fileLocation.AccessHash,
                    fileLocation.FileExt);

                using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                {
                    if (store.FileExists(fileName))
                    {
#if WP8
                        StorageFile pdfFile = await ApplicationData.Current.LocalFolder.GetFileAsync(fileName);
                        Windows.System.Launcher.LaunchFileAsync(pdfFile);
#endif
                    }
                    else
                    {
                        mediaDocument.DownloadingProgress = 0.001;
                        var fileManager = IoC.Get<IEncryptedFileManager>();
                        fileManager.DownloadFile(fileLocation, mediaDocument);
                    }
                }

                return;
            }
#if DEBUG
            MessageBox.Show("Tap on media");
#endif
        }

        public void OpenMediaContact(TLUserBase user)
        {
            OpenContactInternal(user, null);
        }

        public void OpenMediaContact(TLInt userId, TLUserBase user, TLString phoneNumber)
        {
            if (user == null)
            {
                MTProtoService.GetFullUserAsync(new TLInputUserContact { UserId = userId },
                    userFull => OpenContactInternal(userFull.User, phoneNumber));
            }
            else
            {
                OpenContactInternal(user, phoneNumber);
            }
        }

        private void OpenContactInternal(TLUserBase user, TLString phoneNumber)
        {
            if (user == null) return;

            StateService.CurrentContact = user;
            StateService.CurrentContactPhone = phoneNumber;
            NavigationService.UriFor<ContactViewModel>().Navigate();
        }
    }
}
