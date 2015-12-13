using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using ExifLib;
using Microsoft.Phone;
using Telegram.Api.Aggregator;
using Telegram.Api.Extensions;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using TelegramClient.Extensions;
using TelegramClient.Resources;
#if WP81
using Windows.Graphics.Imaging;
using Windows.Storage.Pickers;
#endif
using Caliburn.Micro;
using Microsoft.Phone.Tasks;
using TelegramClient.Services;
using TelegramClient.ViewModels.Contacts;
using TelegramClient.ViewModels.Media;
using Execute = Telegram.Api.Helpers.Execute;
using TaskResult = Microsoft.Phone.Tasks.TaskResult;

namespace TelegramClient.ViewModels.Additional
{
    public class ChooseAttachmentViewModel : PropertyChangedBase
    {

        private bool _isOpen;

        public bool IsOpen
        {
            get { return _isOpen; }
            set
            {
                if (_isOpen != value)
                {
                    _isOpen = value;
                    NotifyOfPropertyChange(() => IsOpen);
                }
            }
        }

        private readonly bool _contactEnabled;

        public Visibility OpenContactVisibility
        {
            get
            {
                return _contactEnabled ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public Visibility OpenCamcorderVisibility
        {
            get
            {
#if WP81
                return _contactEnabled ? Visibility.Visible : Visibility.Collapsed;
#else
                return Visibility.Collapsed;
#endif
            }
        }

        private readonly ICacheService _cacheService;

        private readonly IStateService _stateService;

        private readonly INavigationService _navigationService;

        private readonly ITelegramEventAggregator _eventAggregator;

        private TLObject _with;

        public ChooseAttachmentViewModel(TLObject with, ICacheService cacheService, ITelegramEventAggregator eventAggregator, INavigationService navigationService, IStateService stateService, bool contactEnabled = true)
        {
            _with = with;
            _cacheService = cacheService;
            _stateService = stateService;
            _navigationService = navigationService;
            _eventAggregator = eventAggregator;

            _contactEnabled = contactEnabled;
            _eventAggregator.Subscribe(this);
        }

        public void OpenLocation()
        {
            IsOpen = false;

            Execute.BeginOnUIThread(TimeSpan.FromSeconds(0.25), () =>
            {
                _navigationService.UriFor<MapViewModel>().Navigate();
            });
        }

        private void CheckDisabledFeature(TLObject with, string pmFeatureKey, string chatFeatureKey, string bigChatFeatureKey, System.Action callback)
        {
            _cacheService.CheckDisabledFeature(
                with, 
                pmFeatureKey, 
                chatFeatureKey, 
                bigChatFeatureKey,
                () => Execute.BeginOnUIThread(callback.SafeInvoke),
                disabledFeature => Execute.BeginOnUIThread(() => MessageBox.Show(disabledFeature.Description.ToString(), AppResources.AppName, MessageBoxButton.OK)));
        }

        public void OpenPhoto()
        {
            IsOpen = false;

            Execute.BeginOnUIThread(TimeSpan.FromSeconds(0.25), () =>
            {
                CheckDisabledFeature(_with,
                    Constants.FeaturePMUploadPhoto,
                    Constants.FeatureChatUploadPhoto,
                    Constants.FeatureBigChatUploadPhoto,
                    () =>
                    {
#if WP81
                        if (_contactEnabled)
                        {
                            ((App)Application.Current).ChooseFileInfo = new ChooseFileInfo(DateTime.Now);
                            var fileOpenPicker = new FileOpenPicker();
                            fileOpenPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                            fileOpenPicker.ViewMode = PickerViewMode.Thumbnail;
                            fileOpenPicker.FileTypeFilter.Clear();
                            fileOpenPicker.FileTypeFilter.Add(".bmp");
                            fileOpenPicker.FileTypeFilter.Add(".png");
                            fileOpenPicker.FileTypeFilter.Add(".jpeg");
                            fileOpenPicker.FileTypeFilter.Add(".jpg");
                            fileOpenPicker.ContinuationData.Add("From", "DialogDetailsView");
                            fileOpenPicker.ContinuationData.Add("Type", "Image");

#if MULTIPLE_PHOTOS
                            fileOpenPicker.PickMultipleFilesAndContinue();
#else
                            fileOpenPicker.PickSingleFileAndContinue();
#endif
                            //return;
                            //((App)Application.Current).ChooseFileInfo = new ChooseFileInfo(DateTime.Now);
                            //var task = new PhotoChooserTask { ShowCamera = true };
                            //task.Completed += (o, e) =>
                            //{
                            //    if (e.TaskResult != TaskResult.OK)
                            //    {
                            //        return;
                            //    }

                            //    Handle(_stateService, e.ChosenPhoto, e.OriginalFileName);
                            //};
                            //task.Show();
                        }
                        else
                        {
                            //((App)Application.Current).ChooseFileInfo = new ChooseFileInfo(DateTime.Now);
                            //var fileOpenPicker = new FileOpenPicker();
                            //fileOpenPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                            //fileOpenPicker.ViewMode = PickerViewMode.Thumbnail;
                            //fileOpenPicker.FileTypeFilter.Clear();
                            //fileOpenPicker.FileTypeFilter.Add(".bmp");
                            //fileOpenPicker.FileTypeFilter.Add(".png");
                            //fileOpenPicker.FileTypeFilter.Add(".jpeg");
                            //fileOpenPicker.FileTypeFilter.Add(".jpg");
                            //fileOpenPicker.ContinuationData.Add("From", "SecretDialogDetailsView");
                            //fileOpenPicker.ContinuationData.Add("Type", "Image");
                            //fileOpenPicker.PickSingleFileAndContinue();
                            //return;
                            ((App)Application.Current).ChooseFileInfo = new ChooseFileInfo(DateTime.Now);
                            var task = new PhotoChooserTask { ShowCamera = true };
                            task.Completed += (o, e) =>
                            {
                                if (e.TaskResult != TaskResult.OK)
                                {
                                    return;
                                }
                                Handle(_stateService, e.ChosenPhoto, e.OriginalFileName);
                            };
                            task.Show();
                        }
#else
                        ((App) Application.Current).ChooseFileInfo = new ChooseFileInfo(DateTime.Now);
                        var task = new PhotoChooserTask { ShowCamera = true };
                        task.Completed += (o, e) => Handle(_stateService, e.ChosenPhoto, e.OriginalFileName);
                        task.Show();
#endif
                    });
            });
        }

        public void OpenVideo()
        {
            IsOpen = false;

            Execute.BeginOnUIThread(TimeSpan.FromSeconds(0.25), () =>
            {
                CheckDisabledFeature(_with,
                    Constants.FeaturePMUploadDocument,
                    Constants.FeatureChatUploadDocument,
                    Constants.FeatureBigChatUploadDocument,
                    () =>
                    {
#if WP81
                        if (_contactEnabled)
                        {
                            ((App) Application.Current).ChooseFileInfo = new ChooseFileInfo(DateTime.Now);
                            var fileOpenPicker = new FileOpenPicker();
                            fileOpenPicker.ViewMode = PickerViewMode.Thumbnail;
                            fileOpenPicker.FileTypeFilter.Add(".wmv");
                            fileOpenPicker.FileTypeFilter.Add(".mp4");
                            fileOpenPicker.FileTypeFilter.Add(".avi");

                            fileOpenPicker.ContinuationData.Add("From", "DialogDetailsView");
                            fileOpenPicker.ContinuationData.Add("Type", "Video");
                            fileOpenPicker.PickSingleFileAndContinue();
                        }
                        else
                        {
                            _navigationService.UriFor<VideoCaptureViewModel>().Navigate();
                        }
#else
                    _navigationService.UriFor<VideoCaptureViewModel>().Navigate();
#endif
                    });
            });
        }

        public void OpenCamcorder()
        {
            IsOpen = false;
            Execute.BeginOnUIThread(TimeSpan.FromSeconds(0.25), () =>
            {
                CheckDisabledFeature(_with,
                    Constants.FeaturePMUploadDocument,
                    Constants.FeatureChatUploadDocument,
                    Constants.FeatureBigChatUploadDocument,
                    () =>
                    {
                        _navigationService.UriFor<VideoCaptureViewModel>().Navigate();
                    });
            });
        }

        public void OpenContact()
        {
            IsOpen = false;

            Execute.BeginOnUIThread(TimeSpan.FromSeconds(0.25), () =>
            {
                _navigationService.UriFor<ShareContactViewModel>().Navigate();
            });
        }

        public void OpenDocument()
        {
            IsOpen = false;

            Execute.BeginOnUIThread(TimeSpan.FromSeconds(0.25), () =>
            {
                CheckDisabledFeature(_with,
                    Constants.FeaturePMUploadDocument,
                    Constants.FeatureChatUploadDocument,
                    Constants.FeatureBigChatUploadDocument,
                    () =>
                    {
#if WP81
                        if (_contactEnabled)
                        {
                            ((App) Application.Current).ChooseFileInfo = new ChooseFileInfo(DateTime.Now);
                            var fileOpenPicker = new FileOpenPicker();
                            fileOpenPicker.FileTypeFilter.Add("*");
                            fileOpenPicker.ContinuationData.Add("From", "DialogDetailsView");
                            fileOpenPicker.ContinuationData.Add("Type", "Document");
                            fileOpenPicker.PickSingleFileAndContinue();
                        }
                        else
                        {
                            ((App) Application.Current).ChooseFileInfo = new ChooseFileInfo(DateTime.Now);
                            var fileOpenPicker = new FileOpenPicker();
                            fileOpenPicker.FileTypeFilter.Add("*");
                            fileOpenPicker.ContinuationData.Add("From", "SecretDialogDetailsView");
                            fileOpenPicker.ContinuationData.Add("Type", "Document");
                            fileOpenPicker.PickSingleFileAndContinue();
                        }
#else
                    ((App) Application.Current).ChooseFileInfo = new ChooseFileInfo(DateTime.Now);
                    var task = new PhotoChooserTask { ShowCamera = true };
                    task.Completed += (o, e) => Handle(_stateService, e.ChosenPhoto, e.OriginalFileName);
                    task.Show();
#endif
                    });
            });
        }

        private static bool GetAngleFromExif(Stream imageStream, out int angle)
        {
            angle = 0;
            var position = imageStream.Position;
            
            imageStream.Position = 0;
            var info = ExifReader.ReadJpeg(imageStream);
            imageStream.Position = position;

            if (!info.IsValid)
            {
                return false;
            }

            var orientation = info.Orientation;
            switch (orientation)
            {
                case ExifOrientation.TopRight:
                    angle = 90;
                    break;
                case ExifOrientation.BottomRight:
                    angle = 180;
                    break;
                case ExifOrientation.BottomLeft:
                    angle = 270;
                    break;
                case ExifOrientation.TopLeft:
                case ExifOrientation.Undefined:
                default:
                    angle = 0;
                    break;
            }

            return true;
        }

        private static WriteableBitmap RotateBitmap(WriteableBitmap source, int width, int height, int angle)
        {
            var target = new WriteableBitmap(width, height);
            int sourceIndex = 0;
            int targetIndex = 0;
            for (int x = 0; x < source.PixelWidth; x++)
            {
                for (int y = 0; y < source.PixelHeight; y++)
                {
                    sourceIndex = x + y * source.PixelWidth;
                    switch (angle)
                    {
                        case 90:
                            targetIndex = (source.PixelHeight - y - 1) 
                                + x * target.PixelWidth;
                            break;
                        case 180:  
                            targetIndex = (source.PixelWidth - x - 1) 
                                + (source.PixelHeight - y - 1) * source.PixelWidth;
                            break;
                        case 270:  
                            targetIndex = y + (source.PixelWidth - x - 1) 
                                * target.PixelWidth;
                            break;
                    }
                    target.Pixels[targetIndex] = source.Pixels[sourceIndex];
                }
            }
            return target;
        }

        private static WriteableBitmap DecodeImage(Stream imageStream, int angle)
        {
            var source = PictureDecoder.DecodeJpeg(imageStream); 
 
            switch(angle)
            {
                case 90: 
                case 270: 
                    return RotateBitmap(source, source.PixelHeight, source.PixelWidth, angle);
                case 180: 
                    return RotateBitmap(source, source.PixelWidth, source.PixelHeight, angle);
                default:
                    return source;
            }
        }


#if WP81

        public static async Task<Photo> ResizeJpeg(IRandomAccessStream chosenPhoto, uint size, string originalFileName)
        {
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
                            Width = (int) orientedScaledWidth,
                            Height = (int) orientedScaledHeight,
                            FileName = originalFileName
                        };
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
                }
            }

            return photo;
        }

        public static async Task Handle(IStateService stateService, IRandomAccessStream chosenPhoto, string originalFileName)
        {

            var log = new StringBuilder();
            var stopwatch = Stopwatch.StartNew();

            var photo = await ResizeJpeg(chosenPhoto, 1280, originalFileName);
            log.AppendLine("save_jpeg " + stopwatch.Elapsed);
            stateService.Photo = photo;

            //Execute.ShowDebugMessage(log.ToString());
        }

#endif

        public static void Handle(IStateService stateService, Stream chosenPhoto, string originalFileName)
        {
            var log = new StringBuilder();
            var stopwatch = Stopwatch.StartNew();
            WriteableBitmap writeableBitmap;
            int angle;
            var result = GetAngleFromExif(chosenPhoto, out angle);
            log.AppendLine("get_angle result=" + angle + " " + stopwatch.Elapsed);
            if (result)
            {
                writeableBitmap = DecodeImage(chosenPhoto, angle);
                log.AppendLine("decode_image " + stopwatch.Elapsed);
            }
            else
            {
                var bitmap = new BitmapImage { CreateOptions = BitmapCreateOptions.None };
                bitmap.SetSource(chosenPhoto);
                writeableBitmap = new WriteableBitmap(bitmap);
                log.AppendLine("writeable_bitmap " + stopwatch.Elapsed);
            }

            var maxDimension = Math.Max(writeableBitmap.PixelWidth, writeableBitmap.PixelHeight);
            var scale = 1280.0 / maxDimension;
            var newHeight = writeableBitmap.PixelHeight * scale;
            var newWidth = writeableBitmap.PixelWidth * scale;
            var ms = new MemoryStream();
            writeableBitmap.SaveJpeg(ms, (int)newWidth, (int)newHeight, 0, 87);

            log.AppendLine("save_jpeg " + stopwatch.Elapsed);

            stateService.Photo = new Photo
            {
                FileName = originalFileName,
                Bytes = ms.ToArray(),
                Width = (int)newWidth,
                Height = (int)newHeight
            };

            Execute.ShowDebugMessage(log.ToString());
        }

        public void Open()
        {
            IsOpen = true;
        }

        public void Close()
        {
            IsOpen = false;
        }
    }
}
