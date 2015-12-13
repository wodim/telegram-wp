using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.IsolatedStorage;
using System.Windows.Threading;
using Telegram.Api.Aggregator;
#if WP8
using Windows.Storage;
using Windows.System;
#endif
using Caliburn.Micro;
using Telegram.Api.Extensions;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using TelegramClient.Converters;
using TelegramClient.Helpers;
using TelegramClient.Resources;
using TelegramClient.Services;

namespace TelegramClient.ViewModels.Additional
{
    public class CacheViewModel : ViewModelBase
    {
        private string _status = AppResources.Calculating + "...";

        public string Status
        {
            get { return _status; }
            set { SetField(ref _status, value, () => Status); }
        }

        readonly DispatcherTimer _timer = new DispatcherTimer();

        private volatile bool _isCalculating;

        public CacheViewModel(ICacheService cacheService, ICommonErrorHandler errorHandler, IStateService stateService, INavigationService navigationService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator) 
            : base(cacheService, errorHandler, stateService, navigationService, mtProtoService, eventAggregator)
        {
            Files = new ObservableCollection<TelegramFileInfo>();

            _timer.Interval = TimeSpan.FromSeconds(5.0);
            _timer.Tick += OnTimerTick;

            CalculateCacheSizeAsync(size =>
            {
                Status = FileSizeConverter.Convert(size);
            });

            PropertyChanged += (sender, e) =>
            {
                if (Property.NameEquals(e.PropertyName, () => IsWorking))
                {
                    NotifyOfPropertyChange(() => CanClearCache);
                }
            };
        }

        protected override void OnActivate()
        {
            _timer.Start();

            base.OnActivate();
        }

        protected override void OnDeactivate(bool close)
        {
            _timer.Stop();

            base.OnDeactivate(close);
        }

        private void OnTimerTick(object sender, System.EventArgs e)
        {
            if (_isCalculating) return;

            CalculateCacheSizeAsync(result =>
            {
                Status = FileSizeConverter.Convert(result);
            });
        }

        public ObservableCollection<TelegramFileInfo> Files { get; set; } 

        private void CalculateCacheSizeAsync(Action<long> callback)
        {
            BeginOnThreadPool(() =>
            {
                _isCalculating = true;
                
                var length = 0L;
                var files = new List<TelegramFileInfo>();
                using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                {
                    var fileNames = store.GetFileNames();

                    foreach (var fileName in fileNames)
                    {
                        try
                        {
                            var fileInfo = new TelegramFileInfo {Name = fileName};
                            if (store.FileExists(fileName))
                            {
                                using (var file = new IsolatedStorageFileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read, store))
                                {
                                    fileInfo.Length = file.Length;
                                    if (IsValidCacheFileName(fileName))
                                    {
                                        length += file.Length;
                                        fileInfo.IsValidCacheFileName = true;
                                    }
                                }
                            }
                            files.Add(fileInfo);
                        }
                        catch (Exception ex)
                        {
                            TLUtils.WriteException("CalculateCacheSizeAsync OpenFile: " + fileName, ex);
                        }
                    }

                    var directoryNames = store.GetDirectoryNames();
                    foreach (var fileName in directoryNames)
                    {
                        try
                        {
                            var fileInfo = new TelegramFileInfo { Name = fileName, Length = -1};
                            files.Add(fileInfo);
                        }
                        catch (Exception ex)
                        {
                            TLUtils.WriteException("CalculateCacheSizeAsync OpenFile: " + fileName, ex);
                        }
                    }

                }

                _isCalculating = false; 

                callback.SafeInvoke(length);

                BeginOnUIThread(() =>
                {
                    Files.Clear();
                    foreach (var file in files)
                    {
                        Files.Add(file);
                    }
                });
            });
        }

        public bool CanClearCache
        {
            get { return !IsWorking; }
        }

        private static bool IsValidCacheFileName(string fileName)
        {
            if (fileName == null)
            {
                return false;
            }

            if (fileName.EndsWith(".dat"))
            {
                return false;
            }

            return fileName.StartsWith("document") || fileName.StartsWith("video") || fileName.StartsWith("audio") || fileName.StartsWith("encrypted");
        }

        public void ClearCache()
        {
            if (IsWorking) return;

            IsWorking = true;
            BeginOnThreadPool(() =>
            {
                var length = 0L;
                using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                {
                    var fileNames = store.GetFileNames();
                    foreach (var fileName in fileNames)
                    {
                        if (IsValidCacheFileName(fileName))
                        {
                            try
                            {
                                store.DeleteFile(fileName);
                            }
                            catch (Exception ex)
                            {
                                TLUtils.WriteException(ex);
                            }
                        }
                        //else
                        //{
                        //    try
                        //    {
                        //        using (var file = store.OpenFile(fileName, FileMode.Open, FileAccess.Read))
                        //        {
                        //            length += file.Length;
                        //        }
                        //    }
                        //    catch (Exception ex)
                        //    {
                        //        TLUtils.WriteException(ex);
                        //    }
                        //}
                    }
                }
                Status = FileSizeConverter.Convert(length);
                IsWorking = false;
            });
        }

#if WP8
        public async void OpenFile(TelegramFileInfo fileInfo)
        {
            var store = IsolatedStorageFile.GetUserStoreForApplication();
            if (store.FileExists(fileInfo.Name))
            {
                var file = await ApplicationData.Current.LocalFolder.GetFileAsync(fileInfo.Name);

                if (file != null)
                {
                    Launcher.LaunchFileAsync(file);
                    return;
                }
            }
        }
#endif
    }

    public class TelegramFileInfo
    {
        public string Name { get; set; }

        public long Length { get; set; }

        public bool IsValidCacheFileName { get; set; }
    }
}
