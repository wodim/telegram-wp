//#define TEST_TRANSCODING
using System;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Windows;
using Caliburn.Micro;
using Telegram.Api.Extensions;
using TelegramClient.Converters;
using TelegramClient.Resources;
#if WP8
using Windows.Storage;
#endif
#if WP81
using Windows.Foundation;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage.AccessCache;
#endif
using Telegram.Api.TL;
using TelegramClient.ViewModels.Media;
using Action = System.Action;

namespace TelegramClient.ViewModels.Dialogs
{
    public partial class DialogDetailsViewModel
    {
#if WP81

        private static async void TranscodeFile(VideoEncodingQuality quality, StorageFile srcFile, StorageFile destFile, Action<StorageFile, ulong> callback, Action<IAsyncActionWithProgress<double>> faultCallback)
        {
            var profile = MediaEncodingProfile.CreateMp4(quality);
            profile.Video.Bitrate = 750000;
            profile.Audio.ChannelCount = 1;
            profile.Audio.Bitrate = 62000;
            var transcoder = new MediaTranscoder();

            var prepareOp = await transcoder.PrepareFileTranscodeAsync(srcFile, destFile, profile);
            //message.PrepareTranscodeResult = prepareOp;

            if (prepareOp.CanTranscode)
            {
                var transcodeOp = prepareOp.TranscodeAsync();
                transcodeOp.Progress += TranscodeProgress;
                transcodeOp.Completed += async (o, e) =>
                {
                    var properties = await destFile.GetBasicPropertiesAsync();
                    var size = properties.Size;

                    TranscodeComplete(o, e, () => callback(destFile, size), faultCallback);
                };
                
            }
            else
            {
                faultCallback.SafeInvoke(null);

                switch (prepareOp.FailureReason)
                {
                    case TranscodeFailureReason.CodecNotFound:
                        Telegram.Api.Helpers.Execute.BeginOnUIThread(() => MessageBox.Show(AppResources.CodecWasNotFound, AppResources.Error, MessageBoxButton.OK));
                        break;
                    case TranscodeFailureReason.InvalidProfile:
                        Telegram.Api.Helpers.Execute.BeginOnUIThread(() => MessageBox.Show(AppResources.ProfileIsInvalid, AppResources.Error, MessageBoxButton.OK));
                        break;
                    default:
                        Telegram.Api.Helpers.Execute.BeginOnUIThread(() => MessageBox.Show(AppResources.UnknownFailure, AppResources.Error, MessageBoxButton.OK));
                        break;
                }
            }
        }

        private static void TranscodeProgress(IAsyncActionWithProgress<double> asyncinfo, double progressinfo)
        {
            
        }

        private static void TranscodeComplete(IAsyncActionWithProgress<double> asyncInfo, AsyncStatus asyncStatus, Action callback, Action<IAsyncActionWithProgress<double>> faultCallback)
        {
            asyncInfo.GetResults();
            if (asyncInfo.Status == AsyncStatus.Completed)
            {
                callback.SafeInvoke();
            }
            else if (asyncInfo.Status == AsyncStatus.Canceled)
            {
                Telegram.Api.Helpers.Execute.ShowDebugMessage("Transcode canceled result " + asyncInfo.Status);
                faultCallback.SafeInvoke(asyncInfo);
            }
            else
            {
                Telegram.Api.Helpers.Execute.ShowDebugMessage("Transcode error result=" + asyncInfo.Status + " exception \n" + asyncInfo.ErrorCode);
                faultCallback.SafeInvoke(asyncInfo);
            }
        }

        public void EditVideo(StorageFile file)
        {
            if (file == null) return;

            StateService.VideoFile = file;           
            BeginOnUIThread(() => NavigationService.UriFor<EditVideoViewModel>().Navigate());
        }

        private static async void GetCompressedFile(CompressingVideoFile file, Action<StorageFile, ulong> callback, Action<IAsyncActionWithProgress<double>> faultCallback)
        {
            //Compression here
            var fileName = Path.GetFileName(file.Source.Name);
            var transcodedFileName = "vid_" + fileName;

            var fulltranscodedFileName = Path.Combine(KnownFolders.CameraRoll.Path, transcodedFileName);
            if (File.Exists(fulltranscodedFileName))
            {
                StorageFile transcodedFile = null;
                ulong transcodedLength = 0;
                try
                {
                    transcodedFile = await KnownFolders.CameraRoll.GetFileAsync(transcodedFileName);
                    if (transcodedFile != null)
                    {
                        transcodedLength = (ulong) new FileInfo(fulltranscodedFileName).Length;
                    }
                }
                catch (Exception ex)
                {
                    Telegram.Api.Helpers.Execute.ShowDebugMessage("Get transcoded file ex: \n" + ex);    
                }

                if (transcodedFile != null && transcodedLength > 0)
                {
                    callback.SafeInvoke(transcodedFile, transcodedLength);
                    return;
                }
            }

            var dest = await KnownFolders.CameraRoll.CreateFileAsync(transcodedFileName, CreationCollisionOption.ReplaceExisting);
            TranscodeFile(VideoEncodingQuality.Vga, file.Source, dest, callback, faultCallback);
        } 

        public void SendVideo(CompressingVideoFile videoFile)
        {
            if (videoFile == null) return;

            var file = videoFile.Source;
            if (file == null) return;

            if (!CheckDocumentSize(videoFile.Size))
            {
                MessageBox.Show(
                    string.Format(AppResources.MaximumFileSizeExceeded, MediaSizeConverter.Convert((int)Telegram.Api.Constants.MaximumUploadedFileSize)),
                    AppResources.Error,
                    MessageBoxButton.OK);
                return;
            }

            // to get access to the file with StorageFile.GetFileFromPathAsync in future
            AddFileToFutureAccessList(file);

            var video = new TLVideo
            {
                Id = new TLLong(0),
                Caption = new TLString(Path.GetFileName(file.Name)),
                AccessHash = new TLLong(0),
                Date = TLUtils.DateToUniversalTimeTLInt(MTProtoService.ClientTicksDelta, DateTime.Now),
                UserId = new TLInt(StateService.CurrentUserId),
                Duration = new TLInt((int)videoFile.Duration),
                MimeType = new TLString(file.ContentType),
                Size = new TLInt((int)videoFile.Size),
                Thumb = videoFile.ThumbPhoto ?? new TLPhotoSizeEmpty { Type = TLString.Empty },
                DCId = new TLInt(0),
                W = new TLInt((int)videoFile.Width),
                H = new TLInt((int)videoFile.Height)
            };

            var media = new TLMessageMediaVideo28 { Video = video, IsoFileName = file.Path, File = file, Caption = TLString.Empty };

            var message = GetMessage(TLString.Empty, media);

            BeginOnUIThread(() =>
            {
                var previousMessage = InsertSendingMessage(message);
                IsEmptyDialog = Items.Count == 0 && LazyItems.Count == 0;

                BeginOnThreadPool(() =>
                    CacheService.SyncSendingMessage(
                        message, previousMessage,
                        TLUtils.InputPeerToPeer(Peer, StateService.CurrentUserId),
                        m => SendVideoInternal(message, videoFile)));
            });
        }
#endif

        private void SendVideo(RecordedVideo recorderVideo)
        {
            if (recorderVideo == null) return;

            long size = 0;
            using (var storage = IsolatedStorageFile.GetUserStoreForApplication())
            {
                using (var file = storage.OpenFile(recorderVideo.FileName, FileMode.Open, FileAccess.Read))
                {
                    size = file.Length;
                }
            }

            long photoSize = 0;
            using (var storage = IsolatedStorageFile.GetUserStoreForApplication())
            {
                using (var file = storage.OpenFile(recorderVideo.FileName + ".jpg", FileMode.Open, FileAccess.Read))
                {
                    photoSize = file.Length;
                }
            }

            var volumeId = TLLong.Random();
            var localId = TLInt.Random();
            var secret = TLLong.Random();

            var thumbLocation = new TLFileLocation //TODO: replace with TLFileLocationUnavailable
            {
                DCId = new TLInt(0),
                VolumeId = volumeId,
                LocalId = localId,
                Secret = secret,
            };

            var fileName = String.Format("{0}_{1}_{2}.jpg",
                thumbLocation.VolumeId,
                thumbLocation.LocalId,
                thumbLocation.Secret);

            // заменяем имя на стандартное для всех каритинок
            using (var store = IsolatedStorageFile.GetUserStoreForApplication())
            {
                store.CopyFile(recorderVideo.FileName + ".jpg", fileName, true);
                store.DeleteFile(recorderVideo.FileName + ".jpg");
            }

            var thumbSize = new TLPhotoSize
            {
                W = new TLInt(640),
                H = new TLInt(480),
                Size = new TLInt((int) photoSize),
                Type = new TLString(""),
                Location = thumbLocation,
            };

            var video = new TLVideo
            {
                Id = new TLLong(0),
                Caption = new TLString(recorderVideo.FileName),
                AccessHash = new TLLong(0),
                Date = TLUtils.DateToUniversalTimeTLInt(MTProtoService.ClientTicksDelta, DateTime.Now),
                UserId = new TLInt(StateService.CurrentUserId),
                Duration = new TLInt((int)recorderVideo.Duration),
                MimeType = new TLString("video/mp4"),
                Size = new TLInt((int)size),
                Thumb = thumbSize,
                DCId = new TLInt(0),
                W = new TLInt(640),
                H = new TLInt(480)
            };

            var media = new TLMessageMediaVideo28
            {
                FileId = recorderVideo.FileId ?? TLLong.Random(),
                Video = video,
                IsoFileName = recorderVideo.FileName,
                Caption = TLString.Empty
            };

            var message = GetMessage(TLString.Empty, media);

            BeginOnUIThread(() =>
            {
                var previousMessage = InsertSendingMessage(message);
                IsEmptyDialog = Items.Count == 0 && LazyItems.Count == 0;

                BeginOnThreadPool(() =>
                    CacheService.SyncSendingMessage(
                        message, previousMessage,
                        TLUtils.InputPeerToPeer(Peer, StateService.CurrentUserId),
                        m => SendVideoInternal(message, null)));
            });
        }

#if WP81
        private void SendCompressedVideoInternal(TLMessage message, StorageFile file)
#else
        private void SendCompressedVideoInternal(TLMessage message, object o)
#endif
        {
            var videoMedia = message.Media as TLMessageMediaVideo;
            if (videoMedia == null) return;

            var fileName = videoMedia.IsoFileName;
            if (string.IsNullOrEmpty(fileName)) return;

            var video = videoMedia.Video as TLVideo;
            if (video == null) return;


            byte[] thumbBytes = null;
            var photoThumb = video.Thumb as TLPhotoSize;
            if (photoThumb != null)
            {
                var location = photoThumb.Location as TLFileLocation;
                if (location == null) return;

                var thumbFileName = String.Format("{0}_{1}_{2}.jpg",
                    location.VolumeId,
                    location.LocalId,
                    location.Secret);

                using (var storage = IsolatedStorageFile.GetUserStoreForApplication())
                {
                    using (var thumbFile = storage.OpenFile(thumbFileName, FileMode.Open, FileAccess.Read))
                    {
                        thumbBytes = new byte[thumbFile.Length];
                        thumbFile.Read(thumbBytes, 0, thumbBytes.Length);
                    }
                }
            }

            var fileId = message.Media.FileId ?? TLLong.Random();
            message.Media.FileId = fileId;
            message.Media.UploadingProgress = 0.001;

#if WP81
            if (file != null)
            {
                UploadVideoFileManager.UploadFile(fileId, message, file);
            }
            else if (!string.IsNullOrEmpty(fileName))
            {
                UploadVideoFileManager.UploadFile(fileId, message, fileName);
            }
            else
            {
                return;
            }
#else
            UploadVideoFileManager.UploadFile(fileId, message, fileName);
#endif

            if (thumbBytes != null)
            {
                var fileId2 = TLLong.Random();
                UploadFileManager.UploadFile(fileId2, message.Media, thumbBytes);
            }
        }

#if WP81
        private void SendVideoInternal(TLMessage message, CompressingVideoFile file)
        {
            if (file.IsCompressionEnabled)
            {
                message.Status = MessageStatus.Compressing;

                GetCompressedFile(file,
                    (compressedFile, compressedSize) =>
                    {
                        message.Media.IsoFileName = compressedFile.Path;
                        message.Media.File = compressedFile;
                        message.Status = MessageStatus.Sending;
                        var mediaVideo = message.Media as TLMessageMediaVideo;
                        if (mediaVideo != null)
                        {
                            var video = mediaVideo.Video as TLVideo;
                            if (video != null)
                            {
                                video.Size = new TLInt((int)compressedSize);
                                SendCompressedVideoInternal(message, compressedFile);
                            }
                        }
                    },
                    error =>
                    {
                        message.Status = MessageStatus.Failed;
                    });
            }
            else
            {
                SendCompressedVideoInternal(message, file.Source);
            }
        }
#else
        private void SendVideoInternal(TLMessage message, object videoFile)
        {
            SendCompressedVideoInternal(message, videoFile);
        }
#endif
    }
}
