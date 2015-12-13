#define DISABLE_INVISIBLEMODE
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using Telegram.Api.Aggregator;
using TelegramClient.ViewModels.Search;
#if WP8
using System.Threading.Tasks;
using Windows.Phone.PersonalInformation;
#endif
using System.Windows;
using Caliburn.Micro;
using Microsoft.Phone.Shell;
using Telegram.Api.Extensions;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.FileManager;
using Telegram.Api.Services.Updates;
using Telegram.Api.TL;
using TelegramClient.Helpers;
using TelegramClient.Resources;
using TelegramClient.Services;
using TelegramClient.ViewModels.Auth;
using TelegramClient.ViewModels.Dialogs;
using TelegramClient.ViewModels.Media;
using Execute = Telegram.Api.Helpers.Execute;

namespace TelegramClient.ViewModels.Additional
{
    public class SettingsViewModel : ViewModelBase, Telegram.Api.Aggregator.IHandle<UploadableItem>, Telegram.Api.Aggregator.IHandle<UserNameChangedEventArgs>
    {
        private TLUserBase _currentItem;

        public TLUserBase CurrentItem
        {
            get { return _currentItem; }
            set { SetField(ref _currentItem, value, () => CurrentItem); }
        }

        private IUpdatesService UpdateService
        {
            get { return IoC.Get<IUpdatesService>(); }
        }

        private IPushService PushService
        {
            get { return IoC.Get<IPushService>(); }
        }

        private IUploadFileManager UploadManager
        {
            get { return IoC.Get<IUploadFileManager>(); }
        }

        private IFileManager DownloadManager
        {
            get { return IoC.Get<IFileManager>(); }
        }

        private bool _locationServices;

        public SettingsViewModel(ICacheService cacheService, ICommonErrorHandler errorHandler, IStateService stateService, INavigationService navigationService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator) 
            : base(cacheService, errorHandler, stateService, navigationService, mtProtoService, eventAggregator)
        {
            eventAggregator.Subscribe(this);

            SuppressUpdateStatus = true;

            //tombstoning
            if (stateService.CurrentContact == null)
            {
                stateService.ClearNavigationStack = true;
                navigationService.UriFor<ShellViewModel>().Navigate();
                return;
            }

            CurrentItem = stateService.CurrentContact;
            stateService.CurrentContact = null;

            StateService.GetNotifySettingsAsync(
                settings =>
                {
                    _locationServices = settings.LocationServices;
                    _peopleHub = settings.PeopleHub;
                    _saveIncomingPhotos = settings.SaveIncomingPhotos;
                    _invisibleMode = settings.InvisibleMode;
#if DISABLE_INVISIBLEMODE
                    _invisibleMode = false;
#endif

                    BeginOnUIThread(() =>
                    {
                        NotifyOfPropertyChange(() => LocationServices);
                        NotifyOfPropertyChange(() => SaveIncomingPhotos);
                        NotifyOfPropertyChange(() => InvisibleMode);
                    });
                });

            if (CurrentItem == null)
            {
                BeginOnThreadPool(() =>
                {
                    MTProtoService.GetFullUserAsync(new TLInputUserSelf(),
                        userFull =>
                        {
                            CurrentItem = userFull.User;
                        });
                });
            }

            PropertyChanged += OnPropertyChanged;
        }

        public bool LocationServices
        {
            get { return _locationServices; }
            set
            {
                SetField(ref _locationServices, value, () => LocationServices); 
                StateService.GetNotifySettingsAsync(settings =>
                {
                    settings.LocationServices = value;
                    StateService.SaveNotifySettingsAsync(settings);
                });
            }
        }

        private bool _isPeopleHubEnabled = true;

        public bool IsPeopleHubEnabled
        {
            get { return _isPeopleHubEnabled; }
            set { SetField(ref _isPeopleHubEnabled, value, () => IsPeopleHubEnabled); }
        }

        private bool _peopleHub;

        public bool PeopleHub
        {
            get { return _peopleHub; }
            set
            {
                SetField(ref _peopleHub, value, () => PeopleHub);
                StateService.GetNotifySettingsAsync(settings =>
                {
                    settings.PeopleHub = value;
                    StateService.SaveNotifySettingsAsync(settings);
                });
            }
        }

        private bool _saveIncomingPhotos;

        public bool SaveIncomingPhotos
        {
            get { return _saveIncomingPhotos; }
            set
            {
                SetField(ref _saveIncomingPhotos, value, () => SaveIncomingPhotos);
                StateService.GetNotifySettingsAsync(settings =>
                {
                    settings.SaveIncomingPhotos = value;
                    StateService.SaveNotifySettingsAsync(settings);
                });
            }
        }

        private bool _invisibleMode;

        public bool InvisibleMode
        {
            get { return _invisibleMode; }
            set
            {
                SetField(ref _invisibleMode, value, () => InvisibleMode);
                StateService.GetNotifySettingsAsync(settings =>
                {
                    settings.InvisibleMode = value;
                    StateService.SaveNotifySettingsAsync(settings);
                });
            }
        }

        private AskQuestionConfirmationViewModel _askQuestion;

        public AskQuestionConfirmationViewModel AskQuestion
        {
            get
            {
                return _askQuestion = _askQuestion ?? new AskQuestionConfirmationViewModel();
            }
        }

        

        protected override void OnActivate()
        {
            if (StateService.DCOption != null)
            {
                var option = StateService.DCOption;
                StateService.DCOption = null;
                Execute.ShowDebugMessage("New DCOption=" + option);
            }

            base.OnActivate();
        }

        public static ContactsOperationToken PreviousToken;

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (Property.NameEquals(e.PropertyName, () => PeopleHub))
            {
#if WP8
                if (PreviousToken != null)
                {
                    PreviousToken.IsCanceled = true;
                }

                if (PeopleHub)
                {
                    var contacts = CacheService.GetContacts().OfType<TLUserContact>().ToList();
                    var token = new ContactsOperationToken();

                    PreviousToken = token;
                    ContactsHelper.ImportContactsAsync(DownloadManager, token, contacts, 
                        tuple =>
                        {
                            var importedCount = tuple.Item1;
                            var totalCount = tuple.Item2;

                            var isComplete = importedCount == totalCount;
                            if (isComplete)
                            {
                                PreviousToken = null;
                            }

                            var duration = isComplete ? 0.5 : 2.0;
                            MTProtoService.SetMessageOnTime(duration,
                                string.Format(AppResources.SyncContactsProgress, importedCount, totalCount));
                        },
                        () =>
                        {
                            MTProtoService.SetMessageOnTime(0.0, string.Empty);
                        });
                }
                else
                {
                    IsPeopleHubEnabled = false; 
                    MTProtoService.SetMessageOnTime(25.0, AppResources.DeletingContacts);
                    ContactsHelper.DeleteContactsAsync(() =>
                    {
                        IsPeopleHubEnabled = true;
                        MTProtoService.SetMessageOnTime(0.0, string.Empty);
                    });
                }
#endif
            }
        }

        public void OpenNotifications()
        {
            NavigationService.UriFor<NotificationsViewModel>().Navigate();
        }

        public void Support()
        {
            AskQuestion.Open(result =>
            {
                if (result == MessageBoxResult.OK)
                {
                    IsWorking = true;
                    MTProtoService.GetSupportAsync(support =>
                    {
                        IsWorking = false;
                        StateService.With = support.User;
                        BeginOnUIThread(() => NavigationService.UriFor<DialogDetailsViewModel>().Navigate());
                    },
                    error =>
                    {
                        IsWorking = false;
                    });
                }
            });
        }

        public void OpenPhoto()
        {
            var user = CurrentItem;
            if (user != null)
            {
                var photo = user.Photo as TLUserProfilePhoto;
                if (photo != null)
                {
                    StateService.CurrentPhoto = photo;
                    StateService.CurrentContact = user;
                    NavigationService.UriFor<ProfilePhotoViewerViewModel>().Navigate();
                    return;
                }

                var photoEmpty = user.Photo as TLUserProfilePhotoEmpty;
                if (photoEmpty != null)
                {
                    EditCurrentUserActions.EditPhoto(result =>
                    {
                        var fileId = TLLong.Random();
                        IsWorking = true;
                        UploadManager.UploadFile(fileId, new TLUserSelf(), result);
                    });
                }
            }
        }

        public void LogOut()
        {
            var result = MessageBox.Show(AppResources.LogOutConfirmation, AppResources.Confirm, MessageBoxButton.OKCancel);
            if (result != MessageBoxResult.OK) return;

            PushService.UnregisterDeviceAsync(() => 
                    MTProtoService.LogOutAsync(logOutResult =>
                    {
                        ContactsHelper.DeleteContactsAsync(null);

                        Execute.BeginOnUIThread(() =>
                        {
                            foreach (var activeTile in ShellTile.ActiveTiles)
                            {
                                if (activeTile.NavigationUri.ToString().Contains("Action=SecondaryTile"))
                                {
                                    activeTile.Delete();
                                }
                            }
                        });

                        //MTProtoService.LogOutTransportsAsync(
                        //    () =>
                        //    {

                        //    },
                        //    errors =>
                        //    {

                        //    });
                    },
                    error =>
                    {
                        Execute.ShowDebugMessage("account.logOut error " + error);
                    }));

            LogOutCommon(EventAggregator, MTProtoService, UpdateService, CacheService, StateService, PushService, NavigationService);            
        }

        public void OpenSnapshots()
        {
            NavigationService.UriFor<SnapshotsViewModel>().Navigate();
        }

        public static void LogOutCommon(ITelegramEventAggregator eventAggregator, IMTProtoService mtProtoService, IUpdatesService updateService, ICacheService cacheService, IStateService stateService, IPushService pushService, INavigationService navigationService)
        {
            eventAggregator.Publish(Commands.LogOutCommand);

            SettingsHelper.SetValue(Constants.IsAuthorizedKey, false);
            SettingsHelper.RemoveValue(Constants.CurrentUserKey);
            mtProtoService.ClearQueue();
            updateService.ClearState();
            cacheService.ClearAsync();
            stateService.ResetPasscode();
            stateService.GetAllStickersAsync(result =>
            {
                var allStickers29 = result as TLAllStickers29;
                if (allStickers29 != null)
                {
                    allStickers29.RecentlyUsed = new TLVector<TLRecentlyUsedSticker>();
                }

                stateService.SaveAllStickersAsync(allStickers29);
            });
            SearchViewModel.DeleteRecentAsync();

            if (navigationService.CurrentSource == navigationService.UriFor<StartupViewModel>().BuildUri()
                || navigationService.CurrentSource == navigationService.UriFor<SignInViewModel>().BuildUri()
                || navigationService.CurrentSource == navigationService.UriFor<ConfirmViewModel>().BuildUri()
                || navigationService.CurrentSource == navigationService.UriFor<SignUpViewModel>().BuildUri())
            {
                return;
            }

            stateService.ClearNavigationStack = true;
            Telegram.Logs.Log.Write("StartupViewModel SettingsViewModel.LogOutCommon");
            navigationService.UriFor<StartupViewModel>().Navigate();
        }

        public void OpenPrivacySecurity()
        {
            NavigationService.UriFor<PrivacySecurityViewModel>().Navigate();
        }

        public void OpenLockScreenSettings()
        {
#if WP8
            Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings-lock:"));
#endif
        }

        public void OpenBackgrounds()
        {
            NavigationService.UriFor<ChooseBackgroundViewModel>().Navigate();
        }

        public void OpenStickers()
        {
            NavigationService.UriFor<StickersViewModel>().Navigate();
        }

        public void OpenCacheSettings()
        {
            NavigationService.UriFor<CacheViewModel>().Navigate();
        }

        public void EditProfile()
        {
            NavigationService.UriFor<EditCurrentUserViewModel>().Navigate();
        }

        public void EditProfilePhoto()
        {
            EditCurrentUserActions.EditPhoto(photo =>
            {
                var fileId = TLLong.Random();
                IsWorking = true;
                UploadManager.UploadFile(fileId, new TLUserSelf(), photo);
            });
        }

        public void EditPhoneNumber()
        {
            var currentUser = CurrentItem;
            if (currentUser == null) return;

            StateService.CurrentContact = currentUser;
            NavigationService.UriFor<EditPhoneNumberViewModel>().Navigate();
        }

        public void EditUsername()
        {
            NavigationService.UriFor<EditUsernameViewModel>().Navigate();
        }

        public void SendLogs()
        {
            var fileName = "log_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss.fff", CultureInfo.InvariantCulture) + ".txt";

            Telegram.Logs.Log.CopyTo(fileName,
                result => BeginOnUIThread(() =>
                {
                    StateService.LogFileName = result;
                    NavigationService.UriFor<ChooseDialogViewModel>().Navigate();
                }));
        }

        public void ClearLogs()
        {
            Telegram.Logs.Log.Clear(
                () => BeginOnUIThread(() =>
                {
                    MessageBox.Show("Complete");
                }));
        }

        public void Handle(UploadableItem item)
        {
            if (item.Owner is TLUserSelf)
            {
                IsWorking = false;
            }
        }

        public void Handle(UserNameChangedEventArgs args)
        {
            var currentUser = CurrentItem;
            var userName = args.User as IUserName;

            if (currentUser != null
                && userName != null
                && args.User.Index == currentUser.Index)
            {
                CurrentItem = args.User;
                CurrentItem.NotifyOfPropertyChange(() => userName.UserName);
            }
        }
    }

    public class ContactsOperationToken
    {
        public volatile bool IsCanceled;
    }

    public static class ContactsHelper
    {
        private static readonly object _delayedContactsSyncRoot = new object();

        private static TLVector<TLInt> _delayedContacts; 

        public static void GetDelayedContactsAsync(Action<TLVector<TLInt>> callback)
        {
            if (_delayedContacts != null)
            {
                callback.SafeInvoke(_delayedContacts);
            }

            Execute.BeginOnThreadPool(() =>
            {
                _delayedContacts = TLUtils.OpenObjectFromMTProtoFile<TLVector<TLInt>>(_delayedContactsSyncRoot, Constants.DelayedContactsFileName) ?? new TLVector<TLInt>();
                callback.SafeInvoke(_delayedContacts);
            });
        }

        public static void SaveDelayedContactsAsync(TLVector<TLInt> contacts)
        {
            Execute.BeginOnThreadPool(() =>
            {
                TLUtils.SaveObjectToMTProtoFile(_delayedContactsSyncRoot, Constants.DelayedContactsFileName, contacts);
            });
        }

        public static void UpdateDelayedContactsAsync(ICacheService cacheService, IMTProtoService mtProtoService)
        {
#if WP8
            GetDelayedContactsAsync(contactIds =>
            {
                var contacts = new List<TLUserContact>();
                foreach (var contactId in contactIds)
                {
                    var contact = cacheService.GetUser(contactId) as TLUserContact;
                    if (contact != null)
                    {
                        contacts.Add(contact);
                    }
                }

                if (contacts.Count > 0)
                {
                    var token = new ContactsOperationToken();
                    var fileManager = IoC.Get<IFileManager>();
                    SettingsViewModel.PreviousToken = token;
                    ImportContactsAsync(fileManager, token, contacts,
                        tuple =>
                        {
                            var importedCount = tuple.Item1;
                            var totalCount = tuple.Item2;

                            var isComplete = importedCount == totalCount;
                            if (isComplete)
                            {
                                SettingsViewModel.PreviousToken = null;
                            }

                            var duration = isComplete ? 0.5 : 2.0;
                            mtProtoService.SetMessageOnTime(duration,
                                string.Format(AppResources.SyncContactsProgress, importedCount, totalCount));
                        },
                        () =>
                        {
                            mtProtoService.SetMessageOnTime(0.0, string.Empty);
                        });
                }
            });
#endif
        }

        public static void UpdateContactAsync(IFileManager fileManager, IStateService stateService, TLUserContact contact)
        {
#if WP8
            Execute.BeginOnThreadPool(() => 
                stateService.GetNotifySettingsAsync(
                async settings =>
                {
                    if (settings.PeopleHub)
                    {
                        var store = await ContactStore.CreateOrOpenAsync();
                        var delayedContact = await UpdateContactInternalAsync(contact, fileManager, store, false);
                        if (delayedContact != null)
                        {
                            GetDelayedContactsAsync(contacts =>
                            {
                                contacts.Add(delayedContact.Id);
                                SaveDelayedContactsAsync(contacts);
                            });
                        }
                    }
                }));
#endif
        }

        public static void DeleteContactAsync(IStateService stateService, TLInt userId)
        {
#if WP8
            Execute.BeginOnThreadPool(() => 
                stateService.GetNotifySettingsAsync(
                async settings =>
                {
                    if (settings.PeopleHub)
                    {
                        var store = await ContactStore.CreateOrOpenAsync();
                        var phoneContact = await store.FindContactByRemoteIdAsync(userId.ToString());
                        await store.DeleteContactAsync(phoneContact.Id);
                    }
                }));
#endif
        }

        public static void CreateContactAsync(IFileManager fileManager, IStateService stateService, TLUserContact contact)
        {
#if WP8
            Execute.BeginOnThreadPool(() => 
                stateService.GetNotifySettingsAsync(
                async settings =>
                {
                    if (settings.PeopleHub)
                    {
                        var store = await ContactStore.CreateOrOpenAsync();
                        var delayedContact = await UpdateContactInternalAsync(contact, fileManager, store, true);
                        if (delayedContact != null)
                        {
                            GetDelayedContactsAsync(contacts =>
                            {
                                contacts.Add(delayedContact.Id);
                                SaveDelayedContactsAsync(contacts);
                            });
                        }
                    }
                }));
#endif
        }

        public static void DeleteContactsAsync(System.Action callback)
        {
#if WP8
            Execute.BeginOnThreadPool(
                async () =>
                {
                    var store = await ContactStore.CreateOrOpenAsync();
                    try
                    {
                        await store.DeleteAsync();
                        FileUtils.Delete(_delayedContactsSyncRoot, Constants.DelayedContactsFileName);
                    }
                    catch (Exception ex)
                    {
                        Execute.ShowDebugMessage("store.DeleteAsync ex " + ex);
                    }
                    finally
                    {
                        callback.SafeInvoke();
                    }
                });
#endif
        }

#if WP8
        public static async Task<TLUserContact> UpdateContactInternalAsync(TLUserContact contact, IFileManager fileManager, ContactStore store, bool updateOrCreate)
        {
            TLUserContact delayedContact = null;
            var remoteId = contact.Index.ToString(CultureInfo.InvariantCulture);
            var phoneContact = await store.FindContactByRemoteIdAsync(remoteId);

            if (updateOrCreate)
            {
                phoneContact = phoneContact ?? new StoredContact(store);
            }

            if (phoneContact == null)
            {
                return delayedContact;
            }

            phoneContact.RemoteId = remoteId;
            phoneContact.GivenName = contact.FirstName.ToString(); //FirstName
            phoneContact.FamilyName = contact.LastName.ToString(); //LastName

            var userProfilePhoto = contact.Photo as TLUserProfilePhoto;
            if (userProfilePhoto != null)
            {
                var location = userProfilePhoto.PhotoSmall as TLFileLocation;
                if (location != null)
                {
                    var fileName = String.Format("{0}_{1}_{2}.jpg",
                        location.VolumeId,
                        location.LocalId,
                        location.Secret);
                    using (var isoStore = IsolatedStorageFile.GetUserStoreForApplication())
                    {
                        if (isoStore.FileExists(fileName))
                        {
                            using (var file = isoStore.OpenFile(fileName, FileMode.Open, FileAccess.Read))
                            {
                                await phoneContact.SetDisplayPictureAsync(file.AsInputStream());
                            }
                        }
                        else
                        {
                            fileManager.DownloadFile(location, userProfilePhoto, new TLInt(0));
                            delayedContact = contact;
                        }
                    }
                }
            }

            var emptyPhoto = contact.Photo as TLPhotoEmpty;
            if (emptyPhoto != null)
            {
                try
                {
                    await phoneContact.SetDisplayPictureAsync(null);
                }
                catch (Exception ex)
                {
                    
                }
            }

            var props = await phoneContact.GetPropertiesAsync();
            var mobilePhone = contact.Phone.ToString();
            if (mobilePhone.Length > 0)
            {
                props[KnownContactProperties.MobileTelephone] = mobilePhone.StartsWith("+")
                    ? mobilePhone
                    : "+" + mobilePhone;
            }

            var usernameContact = contact as IUserName;
            if (usernameContact != null)
            {
                var username = usernameContact.UserName.ToString();

                if (username.Length > 0)
                {
                    props[KnownContactProperties.Nickname] = username;
                }
            }

            await phoneContact.SaveAsync();

            return delayedContact;
        }
#endif

        public static void ImportContactsAsync(IFileManager fileManager, ContactsOperationToken token, IList<TLUserContact> contacts, Action<Telegram.Api.WindowsPhone.Tuple<int, int>> progressCallback, System.Action cancelCallback)
        {
#if WP8
            Execute.BeginOnThreadPool(async () =>
            {
                //var contacts = _cacheService.GetContacts();
                var totalCount = contacts.Count;
                if (totalCount == 0) return;


                var store = await ContactStore.CreateOrOpenAsync();
                var importedCount = 0;
                var delayedContacts = new TLVector<TLInt>();
                foreach (var contact in contacts)
                {

                    if (token.IsCanceled)
                    {
                        cancelCallback.SafeInvoke();
                        return;
                    }

                    try
                    {
                        var delayedContact = await UpdateContactInternalAsync(contact, fileManager, store, true);
                        if (delayedContact != null)
                        {
                            delayedContacts.Add(delayedContact.Id);
                        }
                    }
                    catch (Exception ex)
                    {
                        // continue import after failed contact
                    }
                    //Thread.Sleep(100);
                    importedCount++;
                    progressCallback.SafeInvoke(new Telegram.Api.WindowsPhone.Tuple<int, int>(importedCount, totalCount));
                    //var duration = importedCount == totalCount ? 0.5 : 2.0;
                    //_mtProtoService.SetMessageOnTime(duration, string.Format("Sync contacts ({0} of {1})...", importedCount, totalCount));
                }

                var result = new TLVector<TLInt>();
                foreach (var delayedContact in delayedContacts)
                {
                    result.Add(delayedContact);
                }
                SaveDelayedContactsAsync(result);
            });
#endif
        }
    }

}
