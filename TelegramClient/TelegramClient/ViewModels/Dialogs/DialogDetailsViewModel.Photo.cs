using System;
using System.Collections.Generic;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
#if WP81
using Windows.Graphics.Imaging;
using Windows.Security.Cryptography.Core;
#endif
using Windows.Storage;
using Windows.Storage.Streams;
using Telegram.Api.TL;
using TelegramClient.Services;
using TelegramClient.ViewModels.Media;
using Buffer = Windows.Storage.Streams.Buffer;

namespace TelegramClient.ViewModels.Dialogs
{
    public partial class DialogDetailsViewModel
    {
        public ImageEditorViewModel ImageEditor { get; protected set; }

        public MultiImageEditorViewModel MultiImageEditor { get; protected set; }

#if WP81

        public async Task<TLMessage25> GetPhotoMessage(StorageFile file)
        {
            var volumeId = TLLong.Random();
            var localId = TLInt.Random();
            var secret = TLLong.Random();

            var fileLocation = new TLFileLocation
            {
                VolumeId = volumeId,
                LocalId = localId,
                Secret = secret,
                DCId = new TLInt(0),        //TODO: remove from here, replace with FileLocationUnavailable
                //Buffer = p.Bytes
            };

            var fileName = String.Format("{0}_{1}_{2}.jpg",
                fileLocation.VolumeId,
                fileLocation.LocalId,
                fileLocation.Secret);

            var stream = await file.OpenReadAsync();
            var resizedPhoto = await ResizeJpeg(stream, Constants.DefaultImageSize, file.DisplayName, fileName);

            var photoSize = new TLPhotoSize
            {
                Type = TLString.Empty,
                W = new TLInt(resizedPhoto.Width),
                H = new TLInt(resizedPhoto.Height),
                Location = fileLocation,
                Size = new TLInt(resizedPhoto.Bytes.Length)
            };

            var photo = new TLPhoto33
            {
                Id = new TLLong(0),
                AccessHash = new TLLong(0),
                Date = TLUtils.DateToUniversalTimeTLInt(MTProtoService.ClientTicksDelta, DateTime.Now),
                Sizes = new TLVector<TLPhotoSizeBase> { photoSize },
            };

            var media = new TLMessageMediaPhoto28 { Photo = photo, Caption = TLString.Empty, File = resizedPhoto.File };

            return GetMessage(TLString.Empty, media);
        }

        private async void SendPhoto(IReadOnlyCollection<StorageFile> files)
        {
            //threadpool
            if (files == null || files.Count == 0) return;

            if (MultiImageEditor != null && MultiImageEditor.IsOpen)
            {
                BeginOnUIThread(async () => await MultiImageEditor.AddFiles(new List<StorageFile>(files)));

                return;
            }

            var message = await GetPhotoMessage(files.First());

            if (MultiImageEditor == null)
            {
                MultiImageEditor = new MultiImageEditorViewModel
                {
                    CurrentItem = new PhotoFile{ Message = message, File = files.First() },
                    Files = files,
                    ContinueAction = ContinueSendPhoto,
                    GetPhotoMessage = file =>
                    {
                        var m = GetPhotoMessage(file).Result;
                        return m;
                    }
                };
                NotifyOfPropertyChange(() => MultiImageEditor);
            }
            else
            {
                MultiImageEditor.CurrentItem = new PhotoFile { Message = message, File = files.First() };
                MultiImageEditor.Files = files;

                BeginOnUIThread(() => MultiImageEditor.OpenEditor());
            }
        }

        public static async Task<Photo> ResizeJpeg(IRandomAccessStream chosenPhoto, uint size, string originalFileName, string localFileName)
        {
            //Debug.WriteLine("ResizeJpeg.ThreadId=" + Thread.CurrentThread.ManagedThreadId);

            Photo photo;
            using (var sourceStream = chosenPhoto)
            {
                var decoder = await BitmapDecoder.CreateAsync(sourceStream);

                if (decoder.DecoderInformation != null
                    && decoder.DecoderInformation.CodecId == BitmapDecoder.JpegDecoderId)
                {
                    var maxDimension = Math.Max(decoder.PixelWidth, decoder.PixelHeight);
                    var scale = (double)size / maxDimension;
                    var orientedScaledHeight = (uint)(decoder.OrientedPixelHeight * scale);
                    var orientedScaledWidth = (uint)(decoder.OrientedPixelWidth * scale);
                    var scaledHeight = (uint)(decoder.PixelHeight * scale);
                    var scaledWidth = (uint)(decoder.PixelWidth * scale);

                    var transform = new BitmapTransform { ScaledHeight = scaledHeight, ScaledWidth = scaledWidth };
                    var pixelData = await decoder.GetPixelDataAsync(
                        decoder.BitmapPixelFormat,
                        decoder.BitmapAlphaMode,
                        transform,
                        ExifOrientationMode.RespectExifOrientation,
                        ColorManagementMode.DoNotColorManage);

                    using (var destinationStream = new InMemoryRandomAccessStream())
                    {
                        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, destinationStream);
                        encoder.SetPixelData(decoder.BitmapPixelFormat, decoder.BitmapAlphaMode, orientedScaledWidth, orientedScaledHeight, decoder.DpiX, decoder.DpiY, pixelData.DetachPixelData());
                        await encoder.FlushAsync();

                        var reader = new DataReader(destinationStream.GetInputStreamAt(0));
                        var bytes = new byte[destinationStream.Size];
                        await reader.LoadAsync((uint)destinationStream.Size);
                        reader.ReadBytes(bytes);

                        photo = new Photo
                        {
                            Bytes = bytes,
                            Width = (int)orientedScaledWidth,
                            Height = (int)orientedScaledHeight,
                            FileName = originalFileName
                        };

                        if (!string.IsNullOrEmpty(localFileName))
                        {
                            //await ComputeMD5(destinationStream);
                            photo.File = await SaveToLocalFolderAsync(destinationStream.AsStream(), localFileName);
                        }
                    }
                }
                else
                {
                    var reader = new DataReader(chosenPhoto.GetInputStreamAt(0));
                    var bytes = new byte[chosenPhoto.Size];
                    await reader.LoadAsync((uint)chosenPhoto.Size);
                    reader.ReadBytes(bytes);

                    photo = new Photo
                    {
                        Bytes = bytes,
                        Width = (int)decoder.OrientedPixelWidth,
                        Height = (int)decoder.OrientedPixelHeight,
                        FileName = originalFileName
                    };

                    if (!string.IsNullOrEmpty(localFileName))
                    {
                        //await ComputeMD5(destinationStream);
                        photo.File = await SaveToLocalFolderAsync(chosenPhoto.AsStream(), localFileName);
                    }
                }
            }

            return photo;
        }

        public static async Task<byte[]> ComputeMD5(IRandomAccessStream stream)
        {
            var alg = HashAlgorithmProvider.OpenAlgorithm(HashAlgorithmNames.Md5);
            var inputStream = stream.GetInputStreamAt(0);
            uint capacity = 1024 * 1024;
            var buffer = new Buffer(capacity);
            var hash = alg.CreateHash();

            while (true)
            {
                await inputStream.ReadAsync(buffer, capacity, InputStreamOptions.None);
                if (buffer.Length > 0)
                    hash.Append(buffer);
                else
                    break;
            }

            return hash.GetValueAndReset().ToArray();

            //string hashText = CryptographicBuffer.EncodeToHexString(hash.GetValueAndReset()).ToUpper();
            //var alg = HashAlgorithmProvider.OpenAlgorithm(HashAlgorithmNames.Md5);
            ////IBuffer buff = 
            //var hashed = alg.HashData(str.)
            //var res = CryptographicBuffer.EncodeToHexString(hashed);
            //return res;
        }

        public static async Task<StorageFile> SaveToLocalFolderAsync(Stream file, string fileName)
        {
            var localFolder = ApplicationData.Current.LocalFolder;
            var storageFile = await localFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
            using (Stream outputStream = await storageFile.OpenStreamForWriteAsync())
            {
                await file.CopyToAsync(outputStream);
            }

            return storageFile;
        }
#endif

        private void SendPhoto(Photo p)
        {
            var volumeId = TLLong.Random();
            var localId = TLInt.Random();
            var secret = TLLong.Random();

            var fileLocation = new TLFileLocation
            {
                VolumeId = volumeId,
                LocalId = localId,
                Secret = secret,
                DCId = new TLInt(0),        //TODO: remove from here, replace with FileLocationUnavailable
                //Buffer = p.Bytes
            };

            var fileName = String.Format("{0}_{1}_{2}.jpg",
                fileLocation.VolumeId,
                fileLocation.LocalId,
                fileLocation.Secret);

            using (var store = IsolatedStorageFile.GetUserStoreForApplication())
            {
                using (var fileStream = store.CreateFile(fileName))
                {
                    fileStream.Write(p.Bytes, 0, p.Bytes.Length);
                }
            }

            var photoSize = new TLPhotoSize
            {
                Type = TLString.Empty,
                W = new TLInt(p.Width),
                H = new TLInt(p.Height),
                Location = fileLocation,
                Size = new TLInt(p.Bytes.Length)
            };

            var photo = new TLPhoto33
            {
                Id = new TLLong(0),
                AccessHash = new TLLong(0),
                Date = TLUtils.DateToUniversalTimeTLInt(MTProtoService.ClientTicksDelta, DateTime.Now),
                Sizes = new TLVector<TLPhotoSizeBase> { photoSize },   
            };

            var media = new TLMessageMediaPhoto28 {Photo = photo, Caption = TLString.Empty};

            var message = GetMessage(TLString.Empty, media);

            if (ImageEditor == null)
            {
                ImageEditor = new ImageEditorViewModel
                {
                    CurrentItem = message,
                    ContinueAction = ContinueSendPhoto
                };
                NotifyOfPropertyChange(() => ImageEditor);
            }
            else
            {
                ImageEditor.CurrentItem = message;
            }

            BeginOnUIThread(() => ImageEditor.OpenEditor());
        }

        private void ContinueSendPhoto(TLMessage25 message)
        {
            BeginOnUIThread(() =>
            {
                var previousMessage = InsertSendingMessage(message);
                IsEmptyDialog = Items.Count == 0 && LazyItems.Count == 0;

                BeginOnThreadPool(() =>
                 CacheService.SyncSendingMessage(
                     message, previousMessage,
                     TLUtils.InputPeerToPeer(Peer, StateService.CurrentUserId),
                     m => SendPhotoInternal(message)));
            });
        }

#if WP8 && MULTIPLE_PHOTOS
        private void ContinueSendPhoto(IList<TLMessage> messages)
        {
            BeginOnUIThread(() => SendMessages(messages, SendPhotoInternal));
        }
#endif

        private void SendPhotoInternal(IList<TLMessage> messages)
        {
            //if (messages.Count == 3)
            //{
            //    SendPhotoInternal((TLMessage25)messages[1]);
            //    SendPhotoInternal((TLMessage25)messages[0]);
            //    SendPhotoInternal((TLMessage25)messages[2]);
            //}
            //else
            {
                for (var i = 0; i < messages.Count; i++)
                {
                    var message = messages[i];

                    //var photo = ((TLMessageMediaPhoto)message.Media).Photo as TLPhoto;
                    //if (photo == null) return;

                    //var photoSize = photo.Sizes[0] as TLPhotoSize;
                    //if (photoSize == null) return;

                    //var fileLocation = photoSize.Location;
                    //if (fileLocation == null) return;

                    //byte[] bytes = null;
                    //var fileName = String.Format("{0}_{1}_{2}.jpg",
                    //    fileLocation.VolumeId,
                    //    fileLocation.LocalId,
                    //    fileLocation.Secret);

                    //using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                    //{
                    //    using (var fileStream = store.OpenFile(fileName, FileMode.Open, FileAccess.Read))
                    //    {
                    //        if (fileStream.Length > 0)
                    //        {
                    //            bytes = new byte[fileStream.Length];
                    //            fileStream.Read(bytes, 0, bytes.Length);
                    //        }
                    //    }
                    //}

                    //if (bytes == null) return;

                    var fileId = TLLong.Random();
                    message.Media.FileId = fileId;
                    message.Media.UploadingProgress = 0.001;
                    UploadFileManager.UploadFile(fileId, message, message.Media.File);
                    //SendPhotoInternal((TLMessage25)messages[i]);
                }
            }
        }


        private void SendPhotoInternal(TLMessage25 message)
        {
            var photo = ((TLMessageMediaPhoto)message.Media).Photo as TLPhoto;
            if (photo == null) return;

            var photoSize = photo.Sizes[0] as TLPhotoSize;
            if (photoSize == null) return;

            var fileLocation = photoSize.Location;
            if (fileLocation == null) return;

            byte[] bytes = null;
            var fileName = String.Format("{0}_{1}_{2}.jpg",
                fileLocation.VolumeId,
                fileLocation.LocalId,
                fileLocation.Secret);

            using (var store = IsolatedStorageFile.GetUserStoreForApplication())
            {
                using (var fileStream = store.OpenFile(fileName, FileMode.Open, FileAccess.Read))
                {
                    if (fileStream.Length > 0)
                    {
                        bytes = new byte[fileStream.Length];
                        fileStream.Read(bytes, 0, bytes.Length);
                    }
                }
            }

            if (bytes == null) return;

            var md5Bytes = Telegram.Api.Helpers.Utils.ComputeMD5(bytes);
            var md5Checksum = BitConverter.ToInt64(md5Bytes, 0);
            
            StateService.GetServerFilesAsync(
                results =>
                {
                    var serverFile = results.FirstOrDefault(result => result.MD5Checksum.Value == md5Checksum);

#if MULTIPLE_PHOTOS
                    serverFile = null;
#endif

                    if (serverFile != null)
                    {
                        var media = serverFile.Media;
                        var captionMedia = media as IInputMediaCaption;
                        if (captionMedia == null)
                        {
                            var inputMediaPhoto = serverFile.Media as TLInputMediaPhoto;
                            if (inputMediaPhoto != null)
                            {
                                var inputMediaPhoto28 = new TLInputMediaPhoto28(inputMediaPhoto, TLString.Empty);
                                captionMedia = inputMediaPhoto28;
                                media = inputMediaPhoto28;
                                serverFile.Media = inputMediaPhoto28;
                                StateService.SaveServerFilesAsync(results);
                            }
                            var inputMediaUploadedPhoto = serverFile.Media as TLInputMediaUploadedPhoto;
                            if (inputMediaUploadedPhoto != null)
                            {
                                var inputMediaUploadedPhoto28 = new TLInputMediaUploadedPhoto28(inputMediaUploadedPhoto, TLString.Empty);
                                captionMedia = inputMediaUploadedPhoto28;
                                media = inputMediaUploadedPhoto28;
                                serverFile.Media = inputMediaUploadedPhoto28;
                                StateService.SaveServerFilesAsync(results);
                            }
                        }

                        if (captionMedia != null)
                        {
                            captionMedia.Caption = ((TLMessageMediaPhoto28)message.Media).Caption ?? TLString.Empty;
                        }

                        message.InputMedia = media;
                        ShellViewModel.SendMediaInternal(message, MTProtoService, StateService, CacheService);
                    }
                    else
                    {
                        var fileId = TLLong.Random();
                        message.Media.FileId = fileId;
                        message.Media.UploadingProgress = 0.001;
                        UploadFileManager.UploadFile(fileId, message, bytes);
                    }
                });
        }
    }
}
