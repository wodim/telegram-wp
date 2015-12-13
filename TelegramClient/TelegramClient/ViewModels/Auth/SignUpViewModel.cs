using System;
using System.IO;
using System.Windows;
using Caliburn.Micro;
using Microsoft.Phone.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.FileManager;
using Telegram.Api.TL;
using TelegramClient.Helpers;
using TelegramClient.Models;
using TelegramClient.Resources;
using TelegramClient.Services;
using TaskResult = Microsoft.Phone.Tasks.TaskResult;

namespace TelegramClient.ViewModels.Auth
{
    public class SignUpViewModel : ViewModelBase, Telegram.Api.Aggregator.IHandle<TaskCompleted<PhotoResult>>, Telegram.Api.Aggregator.IHandle<UploadableItem>, Telegram.Api.Aggregator.IHandle<string>
    {
        public byte[] PhotoBytes { get; set; }

        private string _firstName;

        public string FirstName
        {
            get { return _firstName; }
            set { SetField(ref _firstName, value, () => FirstName); }
        }

        private string _lastName;

        public string LastName
        {
            get { return _lastName; }
            set { SetField(ref _lastName, value, () => LastName); }
        }

        public IUploadFileManager FileManager { get; private set; }


        public SignUpViewModel(IUploadFileManager fileManager, ICacheService cacheService, ICommonErrorHandler errorHandler, IStateService stateService, INavigationService navigationService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator)
            : base(cacheService, errorHandler, stateService, navigationService, mtProtoService, eventAggregator)
        {

            SuppressUpdateStatus = true;

            FileManager = fileManager;
            EventAggregator.Subscribe(this);
            PropertyChanged += (sender, args) =>
            {
                if (Property.NameEquals(args.PropertyName, () => FirstName)
                    || Property.NameEquals(args.PropertyName, () => LastName))
                {
                    NotifyOfPropertyChange(() => CanSignUp);
                }
            };
        }

        protected override void OnActivate()
        {
            if (StateService.ClearNavigationStack)
            {
                StateService.ClearNavigationStack = false;
                while (NavigationService.RemoveBackEntry() != null) { }
            }

            if (StateService.RemoveBackEntry)
            {
                StateService.RemoveBackEntry = false;
                NavigationService.RemoveBackEntry();
            }

            base.OnActivate();
        }

        public bool CanSignUp
        {
            get
            {
                return !IsWorking 
                    && FirstName != null && FirstName.Length >= 2
                    && LastName != null && LastName.Length >= 2;
            }
        }

        public void ChoosePhoto()
        {
            try
            {
                var task = new PhotoChooserTask();
                task.ShowCamera = true;
                task.PixelHeight = 800;
                task.PixelWidth = 800;
                task.Show();
            }
            catch (Exception e)
            {

            }
        }

        public void SignUp()
        {
            var result = MessageBox.Show(
                AppResources.ConfirmAgeMessage,
                AppResources.ConfirmAgeTitle,
                MessageBoxButton.OKCancel);

            if (result != MessageBoxResult.OK) return;

            IsWorking = true;
            NotifyOfPropertyChange(() => CanSignUp);
            MTProtoService.SignUpAsync(
                StateService.PhoneNumber, StateService.PhoneCodeHash, StateService.PhoneCode, 
                new TLString(FirstName), new TLString(LastName), 
                auth => BeginOnUIThread(() =>
                {
                    TLUtils.IsLogEnabled = false;
                    TLUtils.LogItems.Clear();

                    result = MessageBox.Show(
                        AppResources.ConfirmPushMessage,
                        AppResources.ConfirmPushTitle,
                        MessageBoxButton.OKCancel);

                    if (result != MessageBoxResult.OK)
                    {
                        StateService.GetNotifySettingsAsync(settings =>
                        {
                            var s = settings ?? new Settings();
                            s.ContactAlert = false;
                            s.ContactMessagePreview = true;
                            s.ContactSound = StateService.Sounds[0];
                            s.GroupAlert = false;
                            s.GroupMessagePreview = true;
                            s.GroupSound = StateService.Sounds[0];

                            s.InAppMessagePreview = true;
                            s.InAppSound = true;
                            s.InAppVibration = true;

                            StateService.SaveNotifySettingsAsync(s);
                        });

                        MTProtoService.UpdateNotifySettingsAsync(
                            new TLInputNotifyUsers(),
                            new TLInputPeerNotifySettings
                            {
                                EventsMask = new TLInt(1),
                                MuteUntil = new TLInt(int.MaxValue),
                                ShowPreviews = new TLBool(true),
                                Sound = new TLString(StateService.Sounds[0])
                            },
                            r => { });

                        MTProtoService.UpdateNotifySettingsAsync(
                            new TLInputNotifyChats(),
                            new TLInputPeerNotifySettings
                            {
                                EventsMask = new TLInt(1),
                                MuteUntil = new TLInt(int.MaxValue),
                                ShowPreviews = new TLBool(true),
                                Sound = new TLString(StateService.Sounds[0])
                            },
                            r => { });
                    }
                    else
                    {
                        StateService.GetNotifySettingsAsync(settings =>
                        {
                            var s = settings ?? new Settings();
                            s.ContactAlert = true;
                            s.ContactMessagePreview = true;
                            s.ContactSound = StateService.Sounds[0];
                            s.GroupAlert = true;
                            s.GroupMessagePreview = true;
                            s.GroupSound = StateService.Sounds[0];

                            s.InAppMessagePreview = true;
                            s.InAppSound = true;
                            s.InAppVibration = true;

                            StateService.SaveNotifySettingsAsync(s);
                        });

                        MTProtoService.UpdateNotifySettingsAsync(
                            new TLInputNotifyUsers(),
                            new TLInputPeerNotifySettings
                            {
                                EventsMask = new TLInt(1),
                                MuteUntil = new TLInt(0),
                                ShowPreviews = new TLBool(true),
                                Sound = new TLString(StateService.Sounds[0])
                            },
                            r => { });

                        MTProtoService.UpdateNotifySettingsAsync(
                            new TLInputNotifyChats(),
                            new TLInputPeerNotifySettings
                            {
                                EventsMask = new TLInt(1),
                                MuteUntil = new TLInt(0),
                                ShowPreviews = new TLBool(true),
                                Sound = new TLString(StateService.Sounds[0])
                            },
                            r => { });
                    }
                    MTProtoService.SetInitState();

                    StateService.CurrentUserId = auth.User.Index;
                    StateService.ClearNavigationStack = true;
                    StateService.FirstRun = true;
                    SettingsHelper.SetValue(Constants.IsAuthorizedKey, true);


                    NavigationService.UriFor<ShellViewModel>().Navigate();
                    IsWorking = false;
                    NotifyOfPropertyChange(() => CanSignUp);

                    if (StateService.ProfilePhotoBytes != null)
                    {
                        var bytes = StateService.ProfilePhotoBytes;
                        StateService.ProfilePhotoBytes = null;
                        var fileId = TLLong.Random();
                        FileManager.UploadFile(fileId, new TLUserSelf(), bytes);
                    }
                }),
                error => 
                {
                    IsWorking = false;
                    NotifyOfPropertyChange(() => CanSignUp);

                    if (error.TypeEquals(ErrorType.PHONE_NUMBER_INVALID))
                    {
                        MessageBox.Show(AppResources.PhoneNumberInvalidString, AppResources.Error, MessageBoxButton.OK);
                    }
                    else if (error.TypeEquals(ErrorType.PHONE_CODE_INVALID))
                    {
                        MessageBox.Show(AppResources.PhoneCodeInvalidString, AppResources.Error, MessageBoxButton.OK);
                    }
                    else if (error.TypeEquals(ErrorType.PHONE_CODE_EMPTY))
                    {
                        MessageBox.Show(AppResources.PhoneCodeEmpty, AppResources.Error, MessageBoxButton.OK);
                    }
                    else if (error.TypeEquals(ErrorType.PHONE_CODE_EXPIRED))
                    {
                        MessageBox.Show(AppResources.PhoneCodeExpiredString, AppResources.Error, MessageBoxButton.OK);
                        ClearViewModel();
                        NavigationService.GoBack();
                    }
                    else if (error.CodeEquals(ErrorCode.FLOOD))
                    {
                        MessageBox.Show(AppResources.FloodWaitString, AppResources.Error, MessageBoxButton.OK);
                    }
                    else if (error.TypeEquals(ErrorType.FIRSTNAME_INVALID))
                    {
                        MessageBox.Show(AppResources.FirstNameInvalidString, AppResources.Error, MessageBoxButton.OK);
                    }
                    else if (error.TypeEquals(ErrorType.LASTNAME_INVALID))
                    {
                        MessageBox.Show(AppResources.LastNameInvalidString, AppResources.Error, MessageBoxButton.OK);
                    }
                    else
                    {
#if DEBUG
                        MessageBox.Show(error.ToString());
#endif
                    }
                });        
        }

        public void Handle(TaskCompleted<PhotoResult> result)
        {
            if (result.Result.TaskResult == TaskResult.OK)
            {
                byte[] bytes;
                var sourceStream = result.Result.ChosenPhoto;
                var fileName = result.Result.OriginalFileName;
                using (var memoryStream = new MemoryStream())
                {
                    sourceStream.CopyTo(memoryStream);
                    bytes = memoryStream.ToArray();
                }

                StateService.ProfilePhotoBytes = bytes;
                PhotoBytes = bytes;
                NotifyOfPropertyChange(() => PhotoBytes);
            }
        }

        public void Handle(string command)
        {
            if (string.Equals(command, Commands.LogOutCommand))
            {
                ClearViewModel();
            }
        }

        private void ClearViewModel()
        {
            FirstName = string.Empty;
            LastName = string.Empty;
            PhotoBytes = null;
            IsWorking = false;
        }

        public void Handle(UploadableItem item)
        {
            
        }
    }
}
