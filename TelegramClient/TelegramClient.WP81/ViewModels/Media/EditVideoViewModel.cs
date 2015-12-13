using System;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.System;
using Caliburn.Micro;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using TelegramClient.Converters;
using TelegramClient.Services;

namespace TelegramClient.ViewModels.Media
{
    public class EditVideoViewModel : ViewModelBase
    {
        private bool _compression = true;

        public bool Compression
        {
            get { return _compression; }
            set { SetField(ref _compression, value, () => Compression); }
        }

        public ulong Size { get; set; }

        public ulong EditedSize { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }

        public double Duration { get; set; }

        public string DurationString { get; set; }

        public StorageFile VideoFile { get; set; }

        public TLPhotoSizeBase ThumbPhoto { get; set; }

        public string OriginalVideoParameters { get; protected set; }

        public string EditedVideoParameters { get; protected set; }

        public bool IsCompressionEnabled { get; set; }

        public double TrimStart { get; set; }

        public double TrimEnd { get; set; }

        public EditVideoViewModel(ICacheService cacheService, ICommonErrorHandler errorHandler, IStateService stateService, INavigationService navigationService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator) 
            : base(cacheService, errorHandler, stateService, navigationService, mtProtoService, eventAggregator)
        {
            VideoFile = StateService.VideoFile;
            StateService.VideoFile = null;

            IsCompressionEnabled = true;
            NotifyOfPropertyChange(() => IsCompressionEnabled);

            BeginOnThreadPool(async () =>
            {
                var properties = await VideoFile.GetBasicPropertiesAsync();
                var videoProperties = await VideoFile.Properties.GetVideoPropertiesAsync();

                Size = properties.Size;
                Duration = videoProperties.Duration.TotalSeconds;
                Width = videoProperties.Width;
                Height = videoProperties.Height;

                DurationString = GetDurationString(videoProperties.Duration);
                NotifyOfPropertyChange(() => DurationString);
                var originalSizeString = FileSizeConverter.Convert((long) properties.Size);

                OriginalVideoParameters = string.Format("{0}x{1}, {2}, {3}", videoProperties.Width, videoProperties.Height, DurationString, originalSizeString);
                NotifyOfPropertyChange(() => OriginalVideoParameters);
                
                var maxLength = Math.Max(videoProperties.Width, videoProperties.Height);
                var scaleFactor = maxLength > 640.0 ? 640.0 / maxLength : 1.0;
                if (scaleFactor == 1.0)
                {
                    IsCompressionEnabled = false;
                    NotifyOfPropertyChange(() => IsCompressionEnabled);
                    Compression = false;
                }

                EditedSize = (ulong)(properties.Size*scaleFactor*scaleFactor);
                var editedSizeString = FileSizeConverter.Convert((long)EditedSize);
                var editedHeight = videoProperties.Height*scaleFactor;
                var editedWidth = videoProperties.Width*scaleFactor;

                EditedVideoParameters = string.Format("{0}x{1}, {2}, ~{3}", editedWidth, editedHeight, DurationString, editedSizeString);
                NotifyOfPropertyChange(() => EditedVideoParameters);

                ThumbPhoto = await GetFileThumbAsync(VideoFile);
                NotifyOfPropertyChange(() => ThumbPhoto);
            });
        }

        public void OpenVideo()
        {
            Launcher.LaunchFileAsync(VideoFile);
        }

        private static async Task<TLPhotoSizeBase> GetFileThumbAsync(StorageFile file)
        {
            try
            {
                var thumb = await file.GetThumbnailAsync(ThumbnailMode.PicturesView, 190, ThumbnailOptions.ResizeThumbnail);

                var volumeId = TLLong.Random();
                var localId = TLInt.Random();
                var secret = TLLong.Random();

                var thumbLocation = new TLFileLocation
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

                var thumbFile = await ApplicationData.Current.LocalFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
                var thumbBuffer = new Windows.Storage.Streams.Buffer(Convert.ToUInt32(thumb.Size));
                var iBuf = await thumb.ReadAsync(thumbBuffer, thumbBuffer.Capacity, InputStreamOptions.None);
                using (var thumbStream = await thumbFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    await thumbStream.WriteAsync(iBuf);
                }

                var thumbSize = new TLPhotoSize
                {
                    W = new TLInt((int)thumb.OriginalWidth),
                    H = new TLInt((int)thumb.OriginalHeight),
                    Size = new TLInt((int)thumb.Size),
                    Type = new TLString(""),
                    Location = thumbLocation,
                };

                return thumbSize;
            }
            catch (Exception ex)
            {
                Telegram.Api.Helpers.Execute.ShowDebugMessage("GetFileThumbAsync exception " + ex);
            }

            return null;
        }

        private static string GetDurationString(TimeSpan duration)
        {
            if (duration.Hours > 0)
            {
                return duration.ToString(@"h\:mm\:ss");
            }

            return duration.ToString(@"m\:ss");
        }

        public void Done()
        {
            StateService.CompressingVideoFile = new CompressingVideoFile
            {
                IsCompressionEnabled = IsCompressionEnabled,
                Size = IsCompressionEnabled ? EditedSize : Size,
                Duration = Duration,
                Width = Width,
                Height = Height,
                Source = VideoFile,
                ThumbPhoto = ThumbPhoto,
                TrimStart = TrimStart,
                TrimEnd = TrimEnd
            };

            NavigationService.GoBack();
        }

        public void Cancel()
        {
            NavigationService.GoBack();
        }
    }

    public class CompressingVideoFile
    {
        public ulong Size { get; set; }

        public double Duration { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }

        public StorageFile Source { get; set; }

        public TLPhotoSizeBase ThumbPhoto { get; set; }

        public double TrimStart { get; set; }

        public double TrimEnd { get; set; }

        public bool IsCompressionEnabled { get; set; }
    }
}
