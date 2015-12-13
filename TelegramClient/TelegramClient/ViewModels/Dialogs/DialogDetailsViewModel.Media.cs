using System;
using System.Diagnostics;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Windows;
using Id3;
using Microsoft.Phone.BackgroundAudio;
using Telegram.Api;
using Telegram.Api.Extensions;
using Telegram.Api.Services.FileManager;
using Telegram.EmojiPanel;
using TelegramClient.Services;
#if WP8
using Windows.Storage;
using Windows.System;
#endif
using Caliburn.Micro;
using Microsoft.Phone.Tasks;
using Telegram.Api.TL;
using TelegramClient.ViewModels.Additional;
using TelegramClient.ViewModels.Contacts;
using TelegramClient.ViewModels.Media;
using Execute = Telegram.Api.Helpers.Execute;

namespace TelegramClient.ViewModels.Dialogs
{
    public partial class DialogDetailsViewModel
    {
        private void SendMedia()
        {
            if (StateService.RecordedVideo != null)
            {
                var recordedVideo = StateService.RecordedVideo;
                StateService.RecordedVideo = null;
                SendVideo(recordedVideo);
                return;
            }

#if WP81
            if (StateService.CompressingVideoFile != null)
            {
                var videoFile = StateService.CompressingVideoFile;
                StateService.CompressingVideoFile = null;
                SendVideo(videoFile);
                return;
            }
#endif

            if (StateService.GeoPoint != null)
            {
                var geoPoint = StateService.GeoPoint;
                StateService.GeoPoint = null;
                SendGeoPoint(geoPoint);
                return;
            }

            if (StateService.Venue != null)
            {
                var venue = StateService.Venue;
                StateService.Venue = null;
                SendVenue(venue);
                return;
            }

#if WP8 && MULTIPLE_PHOTOS
            if (App.Photos != null)
            {
                var photos = App.Photos;
                App.Photos = null;
                SendPhoto(photos);
            }
#endif

            if (StateService.Photo != null)
            {
                var photo = StateService.Photo;
                StateService.Photo = null;

                //SendPhoto
                SendPhoto(photo);
                //var fileId = new TLFile{}
                //UploadFileManager.UploadFile(fielId)
                return;
            }

            if (StateService.Document != null)
            {
                var document = StateService.Document;
                StateService.Document = null;

                //SendPhoto
                SendDocument(document);
                //var fileId = new TLFile{}
                //UploadFileManager.UploadFile(fielId)
                return;
            }

            SendSharedContact();
        }

#if WP8
        public async void OpenMedia(TLMessageBase messageBase)
#else
        public void OpenMedia(TLMessageBase messageBase)
#endif
        {
            if (messageBase == null) return;
            if (messageBase.Status == MessageStatus.Failed
                || messageBase.Status == MessageStatus.Sending) return;

            var serviceMessage = messageBase as TLMessageService;
            if (serviceMessage != null)
            {
                var editPhotoAction = serviceMessage.Action as TLMessageActionChatEditPhoto;
                if (editPhotoAction != null)
                {
                    var photo = editPhotoAction.Photo;
                    if (photo != null)
                    {
                        StateService.CurrentPhoto = photo;
                        NavigationService.UriFor<ProfilePhotoViewerViewModel>().Navigate();
                    }
                }

                return;
            }

            var message = messageBase as TLMessage;
            if (message == null) return;

            var mediaPhoto = message.Media as TLMessageMediaPhoto;
            if (mediaPhoto != null)
            {
                StateService.CurrentPhotoMessage = message;
                StateService.CurrentMediaMessages = 
                    Items
                    .OfType<TLMessage>()
                    .Where(x => x.Media is TLMessageMediaPhoto || x.Media is TLMessageMediaVideo)
                    .ToList();

                OpenImageViewer();

                return;
            }

            var mediaWebPage = message.Media as TLMessageMediaWebPage;
            if (mediaWebPage != null)
            {
                var webPage = mediaWebPage.WebPage as TLWebPage;
                if (webPage != null && webPage.Type != null)
                {
                    if (webPage.Type != null)
                    {
                        var type = webPage.Type.ToString();
                        if (string.Equals(type, "photo", StringComparison.OrdinalIgnoreCase))
                        {
                            StateService.CurrentPhotoMessage = message;

                            OpenImageViewer();

                            return;
                        }
                        if (string.Equals(type, "video", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!TLString.Equals(webPage.SiteName, new TLString("youtube"), StringComparison.OrdinalIgnoreCase)
                                && !TLString.IsNullOrEmpty(webPage.EmbedUrl))
                            {
                                var launcher = new MediaPlayerLauncher
                                {
                                    Location = MediaLocationType.Data,
                                    Media = new Uri(webPage.EmbedUrl.ToString(), UriKind.Absolute),
                                    Orientation = MediaPlayerOrientation.Portrait
                                };
                                launcher.Show();
                                return;
                            }

                            if (!TLString.IsNullOrEmpty(webPage.Url))
                            {
                                var webBrowserTask = new WebBrowserTask();
                                webBrowserTask.Uri = new Uri(webPage.Url.ToString(), UriKind.Absolute);
                                webBrowserTask.Show();
                            }

                            return;
                        }
                    }
                }
            }

            var mediaGeo = message.Media as TLMessageMediaGeo;
            if (mediaGeo != null)
            {
                StateService.MediaMessage = message;
                NavigationService.UriFor<MapViewModel>().Navigate();
                return;
            }

            var mediaContact = message.Media as TLMessageMediaContact;
            if (mediaContact != null)
            {
                var phoneNumber = mediaContact.PhoneNumber.ToString();

                if (mediaContact.UserId.Value == 0)
                {
                    
                    if (!string.IsNullOrEmpty(phoneNumber))
                    {
                        if (!phoneNumber.StartsWith("+"))
                        {
                            phoneNumber = "+" + phoneNumber;
                        }

                        var task = new PhoneCallTask();
                        task.DisplayName = mediaContact.FullName;
                        task.PhoneNumber = phoneNumber;
                        task.Show();
                    }
                    
                }
                else
                {
                    var user = mediaContact.User;

                    OpenMediaContact(mediaContact.UserId, user, new TLString(phoneNumber));
                }
                return;
            }

            var mediaVideo = message.Media as TLMessageMediaVideo;
            if (mediaVideo != null)
            {
                var video = mediaVideo.Video as TLVideo;
                if (video == null) return;

                var videoFileName = video.GetFileName();
                var store = IsolatedStorageFile.GetUserStoreForApplication();
#if WP81
                var file = mediaVideo.File ?? await GetStorageFile(mediaVideo);
#endif


                if (!store.FileExists(videoFileName) 
#if WP81
                    && file == null
#endif
                    )
                {
                    mediaVideo.IsCanceled = false;
                    mediaVideo.DownloadingProgress = mediaVideo.LastProgress > 0.0? mediaVideo.LastProgress : 0.001;
                    DownloadVideoFileManager.DownloadFileAsync(
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
                    //ReadMessageContents(message);

#if WP81

                    //var localFile = await GetFileFromLocalFolder(videoFileName);
                    var videoProperties = await file.Properties.GetVideoPropertiesAsync();
                    var musicProperties = await file.Properties.GetMusicPropertiesAsync();

                    if (file != null)
                    {
                        Launcher.LaunchFileAsync(file);
                        return;
                    }


#endif

                    var launcher = new MediaPlayerLauncher
                    {
                        Location = MediaLocationType.Data,
                        Media = new Uri(videoFileName, UriKind.Relative)
                    };
                    launcher.Show();
                }
                return;
            }

            var mediaAudio = message.Media as TLMessageMediaAudio;
            if (mediaAudio != null)
            {
                var audio = mediaAudio.Audio as TLAudio;
                if (audio == null) return;

                var store = IsolatedStorageFile.GetUserStoreForApplication();
                var audioFileName = audio.GetFileName();
                if (TLString.Equals(audio.MimeType, new TLString("audio/mpeg"), StringComparison.OrdinalIgnoreCase))
                {
                    if (!store.FileExists(audioFileName))
                    {
                        mediaAudio.IsCanceled = false;
                        mediaAudio.DownloadingProgress = mediaAudio.LastProgress > 0.0 ? mediaAudio.LastProgress : 0.001;
                        BeginOnThreadPool(() =>
                        {
                            DownloadAudioFileManager.DownloadFile(audio.DCId, audio.ToInputFileLocation(), message, audio.Size);
                        });
                    }
                    else
                    {
                        ReadMessageContents(message as TLMessage25);
                    }

                    return;
                }

                var wavFileName = Path.GetFileNameWithoutExtension(audioFileName) + ".wav";

                if (!store.FileExists(wavFileName))
                {
                    mediaAudio.IsCanceled = false;
                    mediaAudio.DownloadingProgress = mediaAudio.LastProgress > 0.0 ? mediaAudio.LastProgress : 0.001;
                    BeginOnThreadPool(() =>
                    {
                        DownloadAudioFileManager.DownloadFile(audio.DCId, audio.ToInputFileLocation(), message, audio.Size);
                    });
                }
                else
                {
                    ReadMessageContents(message as TLMessage25);
                }

                return;
            }

            var mediaDocument = message.Media as TLMessageMediaDocument;
            if (mediaDocument != null)
            {
                OpenDocumentCommon(message, StateService, DownloadDocumentFileManager,
                    () =>
                    {
                        StateService.CurrentPhotoMessage = message;

                        if (AnimatedImageViewer == null)
                        {
                            AnimatedImageViewer = new AnimatedImageViewerViewModel(StateService);
                            NotifyOfPropertyChange(() => AnimatedImageViewer);
                        }

                        Execute.BeginOnUIThread(() => AnimatedImageViewer.OpenViewer());
                    });

                return;
            }

            Execute.ShowDebugMessage("tap on media");
        }

        private void OpenImageViewer()
        {
            if (ImageViewer == null)
            {
                ImageViewer = new ImageViewerViewModel(StateService, DownloadVideoFileManager, true)
                {
                    DialogDetails = this
                };
                NotifyOfPropertyChange(() => ImageViewer);
            }
            BeginOnUIThread(() => ImageViewer.OpenViewer());
        }


#if WP8
        public static async void OpenDocumentCommon(TLMessage message, IStateService stateService, IDocumentFileManager documentFileManager, System.Action openGifCallback)
#else
        public static void OpenDocumentCommon(TLMessage message, IStateService stateService, IDocumentFileManager documentFileManager, System.Action openGifCallback)
#endif
        {
            var mediaDocument = message.Media as TLMessageMediaDocument;
            if (mediaDocument != null)
            {
                var document = mediaDocument.Document as TLDocument;
                if (document == null) return;

                var documentLocalFileName = document.GetFileName();
                var store = IsolatedStorageFile.GetUserStoreForApplication();
#if WP81
                var documentFile = mediaDocument.File ?? await GetStorageFile(mediaDocument);
#endif

                if (!store.FileExists(documentLocalFileName)
#if WP81
                    && documentFile == null
#endif
)
                {
                    mediaDocument.IsCanceled = false;
                    mediaDocument.DownloadingProgress = mediaDocument.LastProgress > 0.0 ? mediaDocument.LastProgress : 0.001;
                    //_downloadVideoStopwatch = Stopwatch.StartNew();
                    documentFileManager.DownloadFileAsync(
                        document.FileName, document.DCId, document.ToInputFileLocation(), message, document.Size,
                        progress =>
                        {
                            if (progress > 0.0)
                            {
                                mediaDocument.DownloadingProgress = progress;
                            }
                        });
                }
                else
                {
                    if (documentLocalFileName.EndsWith(".gif")
                        || string.Equals(document.MimeType.ToString(), "image/gif", StringComparison.OrdinalIgnoreCase))
                    {
                        openGifCallback.SafeInvoke();

                        return;
                    }
                    
                    if (documentLocalFileName.EndsWith(".mp3")
                        || string.Equals(document.MimeType.ToString(), "audio/mpeg", StringComparison.OrdinalIgnoreCase))
                    {
                        var url = new Uri(documentLocalFileName, UriKind.Relative);
                        var title = document.FileName.ToString();
                        var performer = "Unknown Artist";
                        var readId3Tags = true;
#if WP81

                        try
                        {
                            var storageFile = await ApplicationData.Current.LocalFolder.GetFileAsync(documentLocalFileName);
                            var audioProperties = await storageFile.Properties.GetMusicPropertiesAsync();
                            title = audioProperties.Title;
                            performer = audioProperties.Artist;
                            readId3Tags = false;
                        }
                        catch (Exception ex) { }
#endif
#if WP81
                        if (documentFile == null)
                        {
                            try
                            {
                                documentFile = await ApplicationData.Current.LocalFolder.GetFileAsync(documentLocalFileName);
                            }
                            catch (Exception ex)
                            {
                                Execute.ShowDebugMessage("LocalFolder.GetFileAsync docLocal exception \n" + ex);
                            }
                        }
                        Launcher.LaunchFileAsync(documentFile);
                        return;
#elif WP8
                        var file = await ApplicationData.Current.LocalFolder.GetFileAsync(documentLocalFileName);
                        Launcher.LaunchFileAsync(file);
                        return;
#endif

                        //if (readId3Tags)
                        //{
                        //    if (store.FileExists(documentLocalFileName))
                        //    {
                        //        using (var file = store.OpenFile(documentLocalFileName, FileMode.Open, FileAccess.Read))
                        //        {
                        //            var mp3Stream = new Mp3Stream(file);
                        //            if (mp3Stream.HasTags)
                        //            {
                        //                var tag = mp3Stream.GetTag(Id3TagFamily.FileStartTag);
                        //                title = tag.Title;
                        //                performer = tag.Artists;
                        //            }
                        //        }
                        //    }
                        //}

                        //var track = BackgroundAudioPlayer.Instance.Track;
                        //if (track == null || track.Source != url)
                        //{
                        //    BackgroundAudioPlayer.Instance.Track = new AudioTrack(url, title, performer, null, null);
                        //}
                        //BackgroundAudioPlayer.Instance.Play();

                        return;
                    }
                    else
                    {
#if WP81
                        if (documentFile == null)
                        {
                            try
                            {
                                documentFile = await ApplicationData.Current.LocalFolder.GetFileAsync(documentLocalFileName);
                            }
                            catch (Exception ex)
                            {
                                Execute.ShowDebugMessage("LocalFolder.GetFileAsync docLocal exception \n" + ex);
                            }
                        }
                        Launcher.LaunchFileAsync(documentFile);
                        return;
#elif WP8
                        var file = await ApplicationData.Current.LocalFolder.GetFileAsync(documentLocalFileName);
                        Launcher.LaunchFileAsync(file);
                        return;
#endif
                    }
                }
                return;
            }
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

        public void Attach()
        {
            if (ChooseAttachment == null)
            {
                ChooseAttachment = new ChooseAttachmentViewModel(With, CacheService, EventAggregator, NavigationService, StateService);
                NotifyOfPropertyChange(() => ChooseAttachment);
            }
            BeginOnUIThread(() => ChooseAttachment.Open());
        }
    }
}
