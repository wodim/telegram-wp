using System.IO;
using Caliburn.Micro;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.FileManager;
using Telegram.Api.TL;
using TelegramClient.Services;
using TelegramClient.ViewModels.Additional;

namespace TelegramClient.ViewModels.Contacts
{
    public class EditContactViewModel : ItemDetailsViewModelBase
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

        private IFileManager _fileManager;

        public EditContactViewModel(IFileManager fileManager, ICacheService cacheService, ICommonErrorHandler errorHandler, IStateService stateService, INavigationService navigationService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator) 
            : base(cacheService, errorHandler, stateService, navigationService, mtProtoService, eventAggregator)
        {
            EventAggregator.Subscribe(this);

            _fileManager = fileManager;

            CurrentItem = StateService.CurrentContact;
            StateService.CurrentContact = null;
            var contact = CurrentItem as TLUserBase;
            if (contact != null)
            {
                if (contact.ExtendedInfo != null)
                {
                    FirstName = contact.ExtendedInfo.FirstName.ToString();
                    LastName = contact.ExtendedInfo.LastName.ToString();
                }
                else
                {
                    FirstName = contact.FirstName.ToString();
                    LastName = contact.LastName.ToString();                    
                }
            }
        }

        public void Done()
        {
            if (IsWorking) return;

            if (string.IsNullOrEmpty(FirstName) && string.IsNullOrEmpty(LastName)) return;

            var user = CurrentItem as TLUserBase;
            if (user == null) return;
            if (user is TLUserForeign || user is TLUserDeleted || user is TLUserEmpty) return;

            if (user.FirstName.ToString() != FirstName
                || user.LastName.ToString() != LastName)
            {
                user.ExtendedInfo = new TLUserExtendedInfo();
                user.ExtendedInfo.FirstName = new TLString(FirstName ?? "");
                user.ExtendedInfo.LastName = new TLString(LastName ?? "");
            }
            else
            {
                user.ExtendedInfo = null;
            }

            IsWorking = true;
            MTProtoService.ImportContactsAsync(
                new TLVector<TLInputContactBase>
                {
                    new TLInputContact{ClientId = new TLLong(0), FirstName = new TLString(FirstName), LastName = new TLString(LastName), Phone = user.Phone}
                }, 
                new TLBool(false), 
                importedContacts =>
                {
                    IsWorking = false;
                    user.NotifyOfPropertyChange("FullName");

                    var dialog = CacheService.GetDialog(new TLPeerUser {Id = user.Id});
                    if (dialog != null)
                    {
                        dialog.With = user;
                        dialog.NotifyOfPropertyChange("With") ;

                    }

                    var contact = user as TLUserContact;
                    if (contact != null)
                    {
                        ContactsHelper.UpdateContactAsync(_fileManager, StateService, contact);
                    }

                    //EventAggregator.Publish(statedMessage.Message);
                    BeginOnUIThread(() => NavigationService.GoBack());
                },
                error =>
                {
                    IsWorking = false;
                });
        }

        public void Cancel()
        {
            NavigationService.GoBack();
        }

        public void EditPhoto()
        {

        }
    }
}
