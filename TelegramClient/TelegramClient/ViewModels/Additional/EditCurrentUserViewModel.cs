using System.IO;
using System.Windows;
using Caliburn.Micro;
using Microsoft.Phone.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Extensions;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.FileManager;
using Telegram.Api.TL;
using TelegramClient.Services;
using Execute = Telegram.Api.Helpers.Execute;
using TaskResult = Microsoft.Phone.Tasks.TaskResult;

namespace TelegramClient.ViewModels.Additional
{
    public class EditCurrentUserViewModel :
        ItemDetailsViewModelBase, Telegram.Api.Aggregator.IHandle<UploadableItem>
    {
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

        private readonly IUploadFileManager _uploadManager;

        public EditCurrentUserViewModel(IUploadFileManager uploadManager, ICacheService cacheService, ICommonErrorHandler errorHandler, IStateService stateService, INavigationService navigationService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator) : 
            base(cacheService, errorHandler, stateService, navigationService, mtProtoService, eventAggregator)
        {
            EventAggregator.Subscribe(this);

            _uploadManager = uploadManager;

            CurrentItem = CacheService.GetUser(new TLInt(StateService.CurrentUserId));

            FirstName = ((TLUserBase)CurrentItem).FirstName.Value;
            LastName = ((TLUserBase)CurrentItem).LastName.Value;
        }

        //~EditCurrentUserViewModel()
        //{
            
        //}

        public void SetProfilePhoto()
        {
            EditCurrentUserActions.EditPhoto(photo =>
            {
                var fileId = TLLong.Random();
                IsWorking = true;
                _uploadManager.UploadFile(fileId, new TLUserSelf(), photo);
            });
        }

        public void DeletePhoto()
        {
            MTProtoService.UpdateProfilePhotoAsync(new TLInputPhotoEmpty(), new TLInputPhotoCropAuto(), 
                result =>
                {
                    Execute.ShowDebugMessage("photos.updateProfilePhoto result " + result);
                },
                error =>
                {
                    Execute.ShowDebugMessage("photos.updateProfilePhoto error " + error);
                });
        }

        public void Done()
        {
            if (IsWorking) return;

            IsWorking = true;
            MTProtoService.UpdateProfileAsync(new TLString(FirstName), new TLString(LastName),
                statedMessage =>
                {
                    IsWorking = false;
                    //EventAggregator.Publish(statedMessage.Message);
                    BeginOnUIThread(() => NavigationService.GoBack());
                },
                error =>
                {
                    Execute.ShowDebugMessage("account.updateProfile error " + error);

                    IsWorking = false;
                    BeginOnUIThread(() => NavigationService.GoBack());
                });
        }

        public void Cancel()
        {
            NavigationService.GoBack();
        }

        public void Handle(UploadableItem item)
        {
            if (item.Owner is TLUserSelf)
            {
                IsWorking = false;
            }
        }
    }

    public static class EditCurrentUserActions
    {
        public static void EditPhoto(System.Action<byte[]> callback)
        {
            var photoChooserTask = new PhotoChooserTask { ShowCamera = true, PixelHeight = 800, PixelWidth = 800 };

            photoChooserTask.Completed += (o, e) =>
            {
                if (e.TaskResult == TaskResult.OK)
                {
                    byte[] bytes;
                    var sourceStream = e.ChosenPhoto;
                    using (var memoryStream = new MemoryStream())
                    {
                        sourceStream.CopyTo(memoryStream);
                        bytes = memoryStream.ToArray();
                    }

                    callback.SafeInvoke(bytes);
                }
            };
            photoChooserTask.Show();
        }
    }
}
