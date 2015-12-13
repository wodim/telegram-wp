using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Caliburn.Micro;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.FileManager;
using Telegram.Api.TL;
using TelegramClient.Services;
using Microsoft.Phone.Tasks;
using Execute = Telegram.Api.Helpers.Execute;

namespace TelegramClient.ViewModels.Media
{
    public class ProfilePhotoViewerViewModel : PropertyChangedBase, Telegram.Api.Aggregator.IHandle<DownloadableItem>, Telegram.Api.Aggregator.IHandle<ProgressChangedEventArgs>
    {
        protected bool SetField<T>(ref T field, T value, string propertyName)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            NotifyOfPropertyChange(propertyName);
            return true;
        }

        protected bool SetField<T>(ref T field, T value, Expression<Func<T>> selectorExpression)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            NotifyOfPropertyChange(selectorExpression);
            return true;
        }

        private readonly IList<TLPhotoBase> _items = new List<TLPhotoBase>();

        private int _currentIndex;

        private TLUserBase _currentContact;

        private TLPhotoBase _currentItem;

        public TLPhotoBase CurrentItem
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

        private bool _isWorking;

        public bool IsWorking
        {
            get { return _isWorking; }
            set { SetField(ref _isWorking, value, () => IsWorking); }
        }

        public IStateService StateService { get; protected set; }

        public IMTProtoService MTProtoService { get; protected set; }

        public ITelegramEventAggregator EventAggregator { get; protected set; }

        private INavigationService _navigationService;

        private bool _external;

        public ProfilePhotoViewerViewModel(IStateService stateService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator, INavigationService navigationService, bool internalPage = true)
        {
            StateService = stateService;
            MTProtoService = mtProtoService;
            EventAggregator = eventAggregator;
            EventAggregator.Subscribe(this);

            _navigationService = navigationService;

            //return;
            if (!internalPage)
            {
                _external = true;
                OpenViewer();    
            }
        }

        private void SetInitState()
        {
            CurrentItem = StateService.CurrentPhoto;
            StateService.CurrentPhoto = null;

            if (StateService.CurrentContact != null)
            {
                _currentContact = StateService.CurrentContact;
                StateService.CurrentContact = null;

                Execute.BeginOnThreadPool(() =>
                {
                    IsWorking = true;
                    MTProtoService.GetUserPhotosAsync(_currentContact.ToInputUser(), new TLInt(0), new TLLong(0), new TLInt(0),
                        photos =>
                        {
                            _items.Clear();
                            foreach (var photo in photos.Photos)
                            {
                                _items.Add(photo);
                            }
                            IsWorking = false;
                        },
                        error =>
                        {
                            IsWorking = false;
                            Execute.ShowDebugMessage("photos.getUserPhotos error " + error);
                        });
                });
            }
        }

        public void Handle(DownloadableItem item)
        {
            if (CurrentItem == item.Owner)
            {
                NotifyOfPropertyChange(() => CurrentItem);
            }
        }

        public void Handle(ProgressChangedEventArgs args)
        {
            //if (CurrentItem == args.Item.Owner)
            //{
            //    Progress = args.Progress;
            //    NotifyOfPropertyChange(() => Progress);
            //}
        }

        public void SavePhoto()
        {
            SavePhotoAsync();
        }

        private void SavePhotoAsync(Action<string> callback = null)
        {
            TLFileLocation location = null;
            var profilePhoto = CurrentItem as TLUserProfilePhoto;
            if (profilePhoto != null)
            {
                location = profilePhoto.PhotoBig as TLFileLocation;
            }

            var photo = CurrentItem as TLPhoto;
            if (photo != null)
            {
                TLPhotoSize size = null;
                var sizes = photo.Sizes.OfType<TLPhotoSize>();
                const double width = 320.0;
                foreach (var photoSize in sizes)
                {
                    if (size == null
                        || Math.Abs(width - size.W.Value) > Math.Abs(width - photoSize.W.Value))
                    {
                        size = photoSize;
                    }
                }
                if (size == null) return;

                location = size.Location as TLFileLocation;
            }

            if (location == null) return;

            var fileName = String.Format("{0}_{1}_{2}.jpg",
                location.VolumeId,
                location.LocalId,
                location.Secret);

            Execute.BeginOnThreadPool(() => ImageViewerViewModel.SavePhoto(fileName, callback));
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
        }

        public void SharePhoto()
        {
#if WP8
            SavePhotoAsync(path =>
            {
                var task = new ShareMediaTask { FilePath = path };
                task.Show();
            });
#endif   
        }

        public void DeletePhoto()
        {
            if (_currentIndex < 0 || _currentIndex >= _items.Count)
            {
                return;
            }

            var photo = _items[_currentIndex];
            if (photo == null)
            {
                return;
            }

            IsWorking = true;
            MTProtoService.UpdateProfilePhotoAsync(new TLInputPhotoEmpty(), new TLInputPhotoCropAuto(),
                result =>
                {
                    IsWorking = false;

                    if (CanSlideLeft)
                    {
                        _items.Remove(photo);
                        _currentIndex--;

                        SlideLeft();
                    }
                    else
                    {
                        
                        Execute.BeginOnUIThread(() =>
                        {
                            CloseViewer();
                            //NavigationService.GoBack()
                        });
                    }
                },
                error =>
                {
                    IsWorking = false;
                    Execute.ShowDebugMessage("photos.updateProfilePhoto error " + error);
                });
        }

        public void SetPhoto()
        {
            var photo = CurrentItem as TLPhoto;
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

            IsWorking = true;
            MTProtoService.UpdateProfilePhotoAsync(new TLInputPhoto { Id = photo.Id, AccessHash = photo.AccessHash }, new TLInputPhotoCropAuto(),
                result =>
                {
                    IsWorking = false;
                    _items.Insert(0, result);
                    _currentIndex++;
                    MTProtoService.GetUserPhotosAsync(_currentContact.ToInputUser(), new TLInt(1), new TLLong(0), new TLInt(1),
                        photos =>
                        {
                            var previousPhoto = photos.Photos.FirstOrDefault();

                            if (previousPhoto != null)
                            {
                                _items.RemoveAt(1);
                                _items.Insert(1, previousPhoto);
                            }
                        },
                        error =>
                        {
                            Execute.ShowDebugMessage("photos.getUserPhotos error " + error);
                        });
                },
                error =>
                {
                    IsWorking = false;
                    Execute.ShowDebugMessage("photos.updateProfilePhoto error " + error);
                });
        }

        public void OpenViewer()
        {
            SetInitState();
            _isOpen = CurrentItem != null;
            NotifyOfPropertyChange(() => CurrentItem);
            NotifyOfPropertyChange(() => IsOpen);
            //NotifyOfPropertyChange(() => CanZoom);
        }

        public void CloseViewer()
        {
            _isOpen = false;
            NotifyOfPropertyChange(() => IsOpen);

            if (_external)
            {
                _navigationService.GoBack();
            }
        }

        private bool _isOpen;

        public bool IsOpen { get { return _isOpen; } }
    }
}
