using System;
using System.IO.IsolatedStorage;
using System.Linq;
using System.ServiceModel.Description;
using System.Windows;
using Caliburn.Micro;
using Telegram.Api.Aggregator;
using Telegram.Api.Extensions;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.FileManager;
using Telegram.Api.TL;
using TelegramClient.Converters;
using TelegramClient.Helpers;
using TelegramClient.Resources;
using TelegramClient.Services;
using TelegramClient.ViewModels.Chats;
using Execute = Telegram.Api.Helpers.Execute;

namespace TelegramClient.ViewModels.Dialogs
{
    public class CreateChannelStep1ViewModel : CreateDialogViewModel, Telegram.Api.Aggregator.IHandle<UploadableItem>
    {
        private string _about;

        public string About
        {
            get { return _about; }
            set { SetField(ref _about, value, () => About); }
        }

        public string PlaceholderText
        {
            get { return PlaceholderDefaultTextConverter.GetText(new TLChannel {Title = new TLString(Title)}); }
        }

        public TLPhotoBase Photo { get; set; }

        private readonly IUploadFileManager _uploadManager;

        public CreateChannelStep1ViewModel(IUploadFileManager uploadManager, ICacheService cacheService, ICommonErrorHandler errorHandler, IStateService stateService, INavigationService navigationService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator)
            : base(cacheService, errorHandler, stateService, navigationService, mtProtoService, eventAggregator)
        {
            EventAggregator.Subscribe(this);

            _uploadManager = uploadManager;

            PropertyChanged += (sender, args) =>
            {
                if (Property.NameEquals(args.PropertyName, () => Title))
                {
                    NotifyOfPropertyChange(() => PlaceholderText);
                    NotifyOfPropertyChange(() => CanCreateChannel);
                }
            };
        }

        protected override void OnActivate()
        {
            base.OnActivate();

            if (StateService.RemoveBackEntry)
            {
                StateService.RemoveBackEntry = false;
                NavigationService.RemoveBackEntry();
            }
        }

        private bool _uploadingPhoto;

        public void SetChannelPhoto()
        {
            EditChatActions.EditPhoto(photo =>
            {
                var volumeId = TLLong.Random();
                var localId = TLInt.Random();
                var secret = TLLong.Random();

                var fileLocation = new TLFileLocation
                {
                    VolumeId = volumeId,
                    LocalId = localId,
                    Secret = secret,
                    DCId = new TLInt(0),
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
                        fileStream.Write(photo, 0, photo.Length);
                    }
                }

                Photo = new TLChatPhoto
                {
                    PhotoSmall = new TLFileLocation
                    {
                        DCId = fileLocation.DCId,
                        VolumeId = fileLocation.VolumeId,
                        LocalId = fileLocation.LocalId,
                        Secret = fileLocation.Secret
                    },
                    PhotoBig = new TLFileLocation
                    {
                        DCId = fileLocation.DCId,
                        VolumeId = fileLocation.VolumeId,
                        LocalId = fileLocation.LocalId,
                        Secret = fileLocation.Secret
                    }
                };
                NotifyOfPropertyChange(() => Photo);

                _uploadingPhoto = true;

                var fileId = TLLong.Random();
                _uploadManager.UploadFile(fileId, new TLChannel(), photo);
            });
        }

        public bool CanCreateChannel
        {
            get { return !IsWorking && !string.IsNullOrEmpty(Title); }
        }

        public void Next()
        {
            if (!CanCreateChannel) return;

            IsWorking = true;
            NotifyOfPropertyChange(() => CanCreateChannel);

#if LAYER_41
            // 1 broadcast
            // 2 mega group
            MTProtoService.CreateChannelAsync(new TLInt(2), new TLString(Title), new TLString(About), 
#else
            MTProtoService.CreateChannelAsync(new TLInt(1), new TLString(Title), new TLString(About), new TLVector<TLInputUserBase>(),
#endif
                result => Execute.BeginOnUIThread(() =>
                {
                    var updates = result as TLUpdates;
                    if (updates != null)
                    {
                        var channel = updates.Chats.FirstOrDefault() as TLChannel;
                        if (channel != null)
                        {
                            if (_photo != null)
                            {
                                ContinueUploadingPhoto(channel);
                            }
                            else
                            {
                                if (_uploadingPhoto)
                                {
                                    _uploadingCallback = () => ContinueUploadingPhoto(channel);
                                }
                                else
                                {
                                    ContinueNextStep(channel);
                                }
                            }
                        }
                    }

                }),
                error => Execute.BeginOnUIThread(() =>
                {
                    IsWorking = false; 
                    NotifyOfPropertyChange(() => CanCreateChannel);

                    Execute.ShowDebugMessage("channels.createChannel error " + error);
                }));
        }

        private void ContinueUploadingPhoto(TLChannel channel)
        {
            if (_photo != null)
            {
                MTProtoService.EditPhotoAsync(channel, new TLInputChatUploadedPhoto { File = _photo, Crop = new TLInputPhotoCropAuto() },
                    result2 => Execute.BeginOnUIThread(() =>
                    {
                        var updates2 = result2 as TLUpdates;
                        if (updates2 != null)
                        {
                            channel = updates2.Chats.FirstOrDefault() as TLChannel;
                            if (channel != null)
                            {
                                ContinueNextStep(channel);
                            }
                        }
                    }),
                    error2 => Execute.BeginOnUIThread(() =>
                    {
                        IsWorking = false;
                        NotifyOfPropertyChange(() => CanCreateChannel);

                        Execute.ShowDebugMessage("channels.editPhoto error " + error2);
                    }));
            }
        }

        private void ContinueNextStep(TLChannel channel)
        {
            IsWorking = false;
            NotifyOfPropertyChange(() => CanCreateChannel);

            StateService.NewChannel = channel;
            StateService.RemoveBackEntry = true;
            NavigationService.UriFor<CreateChannelStep2ViewModel>().Navigate();
        }

        public void ShowChannelHint()
        {
            MessageBox.Show(AppResources.WhatIsChannelDescription, AppResources.AppName, MessageBoxButton.OK);
        }

        private TLInputFile _photo;

        private System.Action _uploadingCallback;

        public void Handle(UploadableItem item)
        {
            if (item.Owner is TLChannel)
            {
                Execute.BeginOnUIThread(() =>
                {
                    _uploadingPhoto = false;

                    _photo = new TLInputFile
                    {
                        Id = item.FileId,
                        MD5Checksum = new TLString(MD5Core.GetHashString(item.Bytes).ToLowerInvariant()),
                        Name = new TLString(Guid.NewGuid() + ".jpg"),
                        Parts = new TLInt(item.Parts.Count)
                    };

                    _uploadingCallback.SafeInvoke();
                });
            }
        }
    }
}
