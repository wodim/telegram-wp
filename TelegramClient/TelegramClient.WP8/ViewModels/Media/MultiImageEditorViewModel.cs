using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Pickers;
using Caliburn.Micro;
using Telegram.Api.Extensions;
using Telegram.Api.TL;
using Telegram.Logs;
using TelegramClient.Services;

namespace TelegramClient.ViewModels.Media
{
    public class MultiImageEditorViewModel : PropertyChangedBase
    {
        private PhotoFile _currentItem;

        public PhotoFile CurrentItem
        {
            get { return _currentItem; }
            set
            {
                if (_currentItem != value)
                {
                    SwitchSelection(value, _currentItem);
                    _currentItem = value;
                    NotifyOfPropertyChange(() => CurrentItem);
                    NotifyOfPropertyChange(() => Caption);
                }
            }
        }

        private void SwitchSelection(PhotoFile currentItem, PhotoFile previousItem)
        {
            if (currentItem != null)
            {
                currentItem.IsSelected = true;
            }

            if (previousItem != null)
            {
                previousItem.IsSelected = false;
            }
        }

        public ObservableCollection<PhotoFile> Items { get; set; }

        public MultiImageEditorViewModel()
        {
            Items = new ObservableCollection<PhotoFile>();
        }

        public IReadOnlyCollection<StorageFile> Files { get; set; }

        public Action<IList<TLMessage>> ContinueAction { get; set; }

        public string Caption
        {
            get
            {
                if (_currentItem == null) return null;

                var message = _currentItem.Message;
                if (message != null)
                {
                    var media = message.Media as TLMessageMediaPhoto28;
                    if (media != null)
                    {
                        return media.Caption.ToString();
                    }
                }

                return null;
            }
            set
            {
                var message = _currentItem.Message;
                if (message != null)
                {
                    var media = message.Media as TLMessageMediaPhoto28;
                    if (media != null)
                    {
                        media.Caption = new TLString(value);
                    }
                }
            }
        }

        private bool _isOpen;

        public bool IsOpen { get { return _isOpen; } }

        private bool _isDoneEnabled;

        public bool IsDoneEnabled
        {
            get { return _isDoneEnabled; }
            protected set
            {
                if (_isDoneEnabled != value)
                {
                    _isDoneEnabled = value;
                    NotifyOfPropertyChange(() => IsDoneEnabled);
                }
            }
        }

        public bool IsDeleteEnabled
        {
            get { return Items.Count > 1; }
        }

        public Func<StorageFile, TLMessage25> GetPhotoMessage { get; set; }

        public void Done()
        {
            _isOpen = false;
            NotifyOfPropertyChange(() => IsOpen);

            var logString = new StringBuilder();
            logString.AppendLine("photos");
            var messages = new List<TLMessage>();
            var randomIndex = new Dictionary<long, long>();
            foreach (var item in Items)
            {
                if (item.IsButton) continue;

                if (item.Message != null)
                {
                    if (item.Message.RandomIndex == 0)
                    {
                        logString.AppendLine(string.Format("random_id=0 msg={0} original_file_name={1}", item.Message, item.File.Name));
                        continue;
                    }

                    if (randomIndex.ContainsKey(item.Message.RandomIndex))
                    {
                        logString.AppendLine(string.Format("random_id exists msg={0} original_file_name={1}", item.Message, item.File.Name));
                        continue;
                    }

                    randomIndex[item.Message.RandomIndex] = item.Message.RandomIndex;

                    var mediaPhoto = item.Message.Media as TLMessageMediaPhoto28;
                    var photo = mediaPhoto.Photo as TLPhoto28;
                    var size = photo.Sizes.First() as TLPhotoSize;
                    var fileLocation = size.Location;
                    var fileName = String.Format("{0}_{1}_{2}.jpg",
                        fileLocation.VolumeId,
                        fileLocation.LocalId,
                        fileLocation.Secret);

                    item.Message.Media.UploadingProgress = 0.001;

                    messages.Add(item.Message);
                    logString.AppendLine(string.Format("msg={0} file_name={1}", item.Message, fileName));
                }
                else
                {
                    logString.AppendLine(string.Format("empty msg original_file_name={0}", item.File.Name));
                }
            }

#if MULTIPLE_PHOTOS
            Log.Write(logString.ToString());
#endif

            ContinueAction.SafeInvoke(messages);
        }

        public void Cancel()
        {
            CloseEditor();
        }

        public void OpenEditor()
        {
            Items.Clear();
            //_items = new List<TLMessage> { CurrentItem };

            IsDoneEnabled = false;
            _isOpen = CurrentItem != null;
            NotifyOfPropertyChange(() => IsOpen);
            NotifyOfPropertyChange(() => IsDeleteEnabled);
        }

        public void CloseEditor()
        {
            _isOpen = false;
            NotifyOfPropertyChange(() => IsOpen);

            _currentItem = null;
        }

        public async void OpenAnimationComplete()
        {
            Items.Add(CurrentItem);
            Items.Add(new PhotoFile { IsButton = true });
            
            Log.Write("send photos count=" + Files.Count);

            var files = new List<StorageFile>(Files);
            files.RemoveAt(0);
            await AddFiles(files);
        }

        public async Task AddFiles(IList<StorageFile> files)
        {
            IsDoneEnabled = false;

            var stopwatch = Stopwatch.StartNew();

            for (var i = 0; i < files.Count; i++)
            {
                var photoFile = new PhotoFile { File = files[i] };
                Items.Insert(Items.Count - 1, photoFile);
            }
            if (CurrentItem == null)
            {
                CurrentItem = Items.FirstOrDefault();
            }
            NotifyOfPropertyChange(() => IsDeleteEnabled);

            var maxCount = 9;
            var counter = 0;
            var firstSlice = new List<PhotoFile>();
            var secondSlice = new List<PhotoFile>();
            foreach (var item in Items)
            {
                if (item.IsButton)
                {
                    continue;
                }

                if (counter > maxCount)
                {
                    secondSlice.Add(item);
                }
                else
                {
                    firstSlice.Add(item);
                }
                counter++;
            }

            await UpdateThumbnails(firstSlice);
            Telegram.Api.Helpers.Execute.BeginOnUIThread(async () =>
            {
                //await UpdateThumbnails(secondSlice);
            });

            var count = Items.Count;
            if (count > 1)
            {
                //ProgressIndicator.Text = "Processing...";

                //Telegram.Api.Helpers.Execute.BeginOnThreadPool(async () =>
                {
                    TimeSpan elapsed;

                    var tasks = new List<Task>();
                    for (var i = 0; i < count; i++)
                    {


                        //var local = i;
                        var localItem = Items[i];
                        if (localItem.Message != null)
                        {
                            continue;
                        }
                        if (localItem.IsButton)
                        {
                            continue;
                        }


                        var task = Task.Run(() =>
                        {
                            try
                            {
                                var file = localItem.File;
                                var message = GetPhotoMessage(file);
                                localItem.Message = message;
                            }
                            catch (Exception ex)
                            {
                                Log.Write(ex.ToString());
                            }
                        });
                        tasks.Add(task);
                    }

                    await Task.WhenAll(tasks);

                    //Telegram.Api.Helpers.Execute.BeginOnUIThread(async () =>
                    {
                        if (!IsOpen)
                        {
                            return;
                        }

                        NotifyOfPropertyChange(() => CurrentItem);
                        var currentItem = CurrentItem;
                        var items = Items;
                        var date = CurrentItem.Message.Date;
                        foreach (var item in Items)
                        {
                            if (item != null && item.Message != null)
                            {
                                item.Message.Date = date;
                            }
                        }

                        elapsed = stopwatch.Elapsed;
                        //ProgressIndicator.Text = string.Empty;
                        //ProgressIndicator.IsVisible = false;
                        IsDoneEnabled = true;

                        await UpdateThumbnails(secondSlice);
                    }
                    //);
                }
                //);
            }
            else
            {
                IsDoneEnabled = true;
            }
        }

        private async Task UpdateThumbnails(IList<PhotoFile> items)
        {
            foreach (var item in items)
            {
                var thumbnail = await item.File.GetThumbnailAsync(ThumbnailMode.ListView, 99, ThumbnailOptions.None);
                item.Thumbnail = thumbnail;
                item.NotifyOfPropertyChange("Self");
            }
        }

        public void SelectMessage(PhotoFile file)
        {
            if (file.IsButton)
            {
#if WP81
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
                fileOpenPicker.PickMultipleFilesAndContinue();
#endif
            }
            else
            {
                CurrentItem = file;
            }
        }

        public void Delete(PhotoFile file)
        {
            var index = Items.IndexOf(file);
            if (index == -1)
            {
                return;
            }
            Items.RemoveAt(index);
            if (CurrentItem == file)
            {
                if (Items.Count > 1)
                {
                    if (Items.Count > index + 1)
                    {
                        CurrentItem = Items[index];
                    }
                    else
                    {
                        CurrentItem = Items[index - 1];
                    }
                }
                else
                {
                    CurrentItem = null;
                }
            }

            IsDoneEnabled = Items.FirstOrDefault(x => !x.IsButton) != null;
            NotifyOfPropertyChange(() => IsDeleteEnabled);

            if (Items.Count == 1)
            {
                _isOpen = false;
                NotifyOfPropertyChange(() => IsOpen);
            }
        }

        private bool _once;

        public void OnLoaded()
        {
            if (_once) return;

            _once = true;
            Telegram.Api.Helpers.Execute.BeginOnUIThread(OpenEditor);
        }
    }
}
