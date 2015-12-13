using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using Caliburn.Micro;
using Microsoft.Phone.Tasks;
using Microsoft.Phone.UserData;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.FileManager;
using Telegram.Api.TL;
using TelegramClient.Converters;
using TelegramClient.Helpers;
using TelegramClient.Models;
using TelegramClient.Resources;
using TelegramClient.Services;
using TelegramClient.ViewModels.Additional;
using TelegramClient.ViewModels.Dialogs;
using TelegramClient.ViewModels.Search;
using Execute = Telegram.Api.Helpers.Execute;
using TaskResult = Microsoft.Phone.Tasks.TaskResult;

namespace TelegramClient.ViewModels.Contacts
{
    public class ContactsViewModel : ItemsViewModelBase<TLUserBase>, Telegram.Api.Aggregator.IHandle<string>, Telegram.Api.Aggregator.IHandle<TLUserBase>, Telegram.Api.Aggregator.IHandle<InvokeImportContacts>, Telegram.Api.Aggregator.IHandle<TLUpdatePrivacy>, Telegram.Api.Aggregator.IHandle<TLUpdateUserName>, Telegram.Api.Aggregator.IHandle<TLUpdateUserPhoto>
    {
        private ObservableCollection<AlphaKeyGroup<TLUserBase>> _contacts;

        public ObservableCollection<AlphaKeyGroup<TLUserBase>> Contacts
        {
            get { return _contacts; }
            set { SetField(ref _contacts, value, () => Contacts); }
        }

        public bool FirstRun { get; set; }

        private readonly IFileManager _fileManager;

        public ContactsViewModel(IFileManager fileManager, ICacheService cacheService, ICommonErrorHandler errorHandler, IStateService stateService, INavigationService navigationService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator)
            : base(cacheService, errorHandler, stateService, navigationService, mtProtoService, eventAggregator)
        {
            _fileManager = fileManager;

            Items = new AlphaKeyGroup<TLUserBase>("@");

            _contacts = new ObservableCollection<AlphaKeyGroup<TLUserBase>> { (AlphaKeyGroup<TLUserBase>) Items };

            DisplayName = LowercaseConverter.Convert(AppResources.Contacts);
            Status = AppResources.Loading;

            
        }

        public void GetContactsAsync()
        {
            var contactIds = string.Join(",", Items.Select(x => x.Index).Union(new []{StateService.CurrentUserId}).OrderBy(x => x));
            var hash = MD5Core.GetHash(contactIds);
            var hashString = BitConverter.ToString(hash).Replace("-", string.Empty).ToLower();

            IsWorking = true;
            MTProtoService.GetContactsAsync(new TLString(hashString),
                result =>
                {
                    IsWorking = false;
                    var contacts = result as TLContacts;
                    if (contacts != null)
                    {
                        InsertContacts(contacts.Users, false);
                    }
                },
                error =>
                {
                    IsWorking = false;
                    Execute.ShowDebugMessage("contacts.getContacts error: " + error);
                });
        }

        private bool _runOnce = true;

        protected override void OnActivate()
        {
            base.OnActivate();

            if (!_runOnce)
            {
                UpdateStatusesAsync();

                return;
            }
            _runOnce = false;

            LoadCacheAsync();
        }

        private void LoadCache()
        {
            var isAuthorized = SettingsHelper.GetValue<bool>(Constants.IsAuthorizedKey);
            if (!isAuthorized)
            {
                return;
            }

            Status = string.Empty;

            var contacts = CacheService.GetContacts();
            var orderedContacts = contacts.OrderBy(x => x.FullName).ToList();
            var count = 0;

            Items.Clear();
            LazyItems.Clear();
            for (var i = 0; i < orderedContacts.Count; i++)
            {
                if (!(orderedContacts[i] is TLUserEmpty)
                    && orderedContacts[i].Index != StateService.CurrentUserId)
                {
                    if (count < 10)
                    {
                        Items.Add(orderedContacts[i]);
                    }
                    else
                    {
                        LazyItems.Add(orderedContacts[i]);
                    }
                    count++;
                }
            }

            Status = Items.Count == 0 && LazyItems.Count == 0 ? AppResources.Loading : string.Empty;
            
            if (LazyItems.Count > 0)
            {
                PopulateItems(() =>
                {
                    ImportContactsAsync();
                    GetContactsAsync(); 
                    EventAggregator.Subscribe(this);
                });
            }
            else
            {
                ImportContactsAsync();
                GetContactsAsync(); 
                EventAggregator.Subscribe(this);
            }
        }

        private void LoadCacheAsync()
        {
#if WP8
            LoadCache();
#else
            BeginOnUIThread(TimeSpan.FromSeconds(0.4), LoadCache);
#endif
        }

        private DateTime? _lastUpdateStatusesTime;

        private void UpdateStatusesAsync()
        {
            BeginOnThreadPool(() =>
            {
                Thread.Sleep(1000);
                try
                {
                    if (_lastUpdateStatusesTime.HasValue
                        && _lastUpdateStatusesTime.Value.AddSeconds(30.0) > DateTime.Now)
                    {
                        return;
                    }

                    for (var i = 0; i < Items.Count; i++)
                    {
                        Items[i].NotifyOfPropertyChange("Status");
                    }

                    _lastUpdateStatusesTime = DateTime.Now;
                }
                catch (Exception e)
                {
                    TLUtils.WriteException(e);
                }
            });
        }

        #region Commands

        public void Search()
        {
            StateService.NavigateToDialogDetails = true;
            NavigationService.UriFor<SearchViewModel>().Navigate();
        }

        public void AddContact()
        {
            var task = new SaveContactTask();
            task.Completed += (o, e) =>
            {
                if (e.TaskResult == TaskResult.OK)
                {
                    ImportContactsAsync();
                }
            };
            task.Show();
        }

        public void DeleteContact(TLUserBase user)
        {
            if (user == null) return;

            MTProtoService.DeleteContactAsync(
                user.ToInputUser(),
                link => BeginOnUIThread(() => Items.Remove(user)),
                error => Execute.ShowDebugMessage("contacts.deleteContact error: " + error));
        }

        public void UserAction(TLUserBase user)
        {
            if (user == null) return;

            OpenContactDetails(user);
        }

        public FrameworkElement OpenContactElement;

        public void SetOpenContactElement(object element)
        {
            OpenContactElement = element as FrameworkElement;
        }

        public void OpenContactDetails(TLUserBase user)
        {
            if (user == null || user is TLUserEmpty) return;

            if (user is TLUserNotRegistered)
            {
                var task = new SmsComposeTask();
                task.Body = AppResources.InviteFriendMessage;
                task.To = user.Phone != null ? user.Phone.ToString() : string.Empty;
                task.Show();

                return;
            }

            StateService.With = user;
            StateService.AnimateTitle = true;
            NavigationService.UriFor<DialogDetailsViewModel>().Navigate();
        }

        private Stopwatch _stopwatch;
        private readonly object _importedPhonesRoot = new object();

        public void ImportContactsAsync(bool fullReplace = false)
        {
            var contacts = new Microsoft.Phone.UserData.Contacts();
            contacts.SearchCompleted += (e, args) => OnSearchCompleted(args, fullReplace);
            _stopwatch = Stopwatch.StartNew();
            contacts.SearchAsync(string.Empty, FilterKind.None, null);
        }

        private void OnSearchCompleted(ContactsSearchEventArgs args, bool fullReplace)
        {
            TLUtils.WritePerformance("::Search contacts time: " + _stopwatch.Elapsed);
            _stopwatch = Stopwatch.StartNew();
            var contacts = args.Results;

            var phonesCache = new Dictionary<string, Contact>();
            var notRegisteredContacts = new List<TLUserBase>();

            foreach (var contact in contacts)
            {
                foreach (var phoneNumber in contact.PhoneNumbers)
                {
                    phonesCache[phoneNumber.PhoneNumber] = contact;
                }

                var completeName = contact.CompleteName;
                var firstName = completeName != null ? completeName.FirstName ?? "" : "";
                var lastName = completeName != null ? completeName.LastName ?? "" : "";

                if (string.IsNullOrEmpty(firstName)
                    && string.IsNullOrEmpty(lastName))
                {
                    if (!string.IsNullOrEmpty(contact.DisplayName))
                    {
                        firstName = contact.DisplayName;
                    }
                    else
                    {
                        continue;
                    }
                }

                var clientId = contact.GetHashCode();
                var phone = contact.PhoneNumbers.FirstOrDefault();
                if (phone != null)
                {
                    var notRegisteredUser = new TLUserNotRegistered
                    {
                        Id = new TLInt(-1),
                        Phone = new TLString(phone.PhoneNumber),
                        _firstName = new TLString(firstName),
                        _lastName = new TLString(lastName),
                        ClientId = new TLLong(clientId),
                        _photo = new TLPhotoEmpty(),
                        PhoneNumbers = contact.PhoneNumbers
                    };

                    if (lastName.Length > 0 || firstName.Length > 0)
                    {
                        notRegisteredContacts.Add(notRegisteredUser);
                    }
                }
            }

            TLUtils.WritePerformance("::Get not registered phones time: " + _stopwatch.Elapsed);

            _stopwatch = Stopwatch.StartNew();

            var groups = AlphaKeyGroup<TLUserBase>.CreateGroups(
                notRegisteredContacts,
                Thread.CurrentThread.CurrentUICulture,
                x => x.FullName,
                false);

            TLUtils.WritePerformance("::Get groups time: " + _stopwatch.Elapsed);

            var contactKeys = new Dictionary<string, string>();
            foreach (var contact in Contacts)
            {
                contactKeys[contact.Key] = contact.Key;
            }
            BeginOnThreadPool(() =>
            {
                foreach (var @group in groups)
                {
                    var gr = new AlphaKeyGroup<TLUserBase>(@group.Key);
                    foreach (var u in @group.OrderBy(x => x.FullName))
                    {
                        gr.Add(u);
                    }

                    if (!contactKeys.ContainsKey(gr.Key))
                    {
                        BeginOnUIThread(() => Contacts.Add(gr));
                    }
                }
            });

            var importedPhonesCache = GetImportedPhones();


            var phones = phonesCache.Keys.Take(Constants.MaxImportingContactsCount).ToList();
            var importingContacts = new TLVector<TLInputContactBase>();
            var importingPhones = new List<string>();
            foreach (var phone in phones)
            {
                if (importedPhonesCache.ContainsKey(phone))
                {
                    continue;
                }

                var completeName = phonesCache[phone].CompleteName;

                var firstName = completeName != null ? completeName.FirstName ?? "" : "";
                var lastName = completeName != null ? completeName.LastName ?? "" : "";

                if (string.IsNullOrEmpty(firstName)
                    && string.IsNullOrEmpty(lastName))
                {
                    if (!string.IsNullOrEmpty(phonesCache[phone].DisplayName))
                    {
                        firstName = phonesCache[phone].DisplayName;
                    }
                    else
                    {
                        continue;
                    }
                }

                if (firstName == "" && lastName == "") continue;

                var contact = new TLInputContact
                {
                    Phone = new TLString(phone),
                    FirstName = new TLString(firstName),
                    LastName = new TLString(lastName),
                    ClientId = new TLLong(phonesCache[phone].GetHashCode())
                };

                importingContacts.Add(contact);
                importingPhones.Add(phone);
            }

            if (importingContacts.Count > 0)
            {
                IsWorking = true;
                MTProtoService.ImportContactsAsync(importingContacts, new TLBool(false),
                    importedContacts =>
                    {
                        IsWorking = false;
                        Status = Items.Count == 0 && LazyItems.Count == 0 && importedContacts.Users.Count == 0
                            ? string.Format("{0}", AppResources.NoContactsHere)
                            : string.Empty;

                        var retryContactsCount = importedContacts.RetryContacts.Count;
                        if (retryContactsCount > 0)
                        {
                            Execute.ShowDebugMessage("contacts.importContacts error: retryContacts count=" + retryContactsCount);
                        }

                        InsertContacts(importedContacts.Users, fullReplace);

                        SaveImportedPhones(importedPhonesCache, importingPhones);
                    },
                    error =>
                    {
                        IsWorking = false;
                        Status = string.Empty;

                        Execute.ShowDebugMessage("contacts.importContacts error: " + error);
                    });
            }
            else
            {
                Status = Items.Count == 0 && LazyItems.Count == 0
                    ? string.Format("{0}", AppResources.NoContactsHere)
                    : string.Empty;
            }
            
        }

        private void SaveImportedPhones(Dictionary<string, string> importedPhonesCache, List<string> importingPhones)
        {
            foreach (var importingPhone in importingPhones)
            {
                importedPhonesCache[importingPhone] = importingPhone;
            }

            var importedPhones = new TLVector<TLString>(importedPhonesCache.Keys.Count);
            foreach (var importedPhone in importedPhonesCache.Keys)
            {
                importedPhones.Add(new TLString(importedPhone));
            }

            TLUtils.SaveObjectToMTProtoFile(_importedPhonesRoot, Constants.ImportedPhonesFileName, importedPhones);
        }

        private Dictionary<string, string> GetImportedPhones()
        {
            var importedPhones =
                TLUtils.OpenObjectFromMTProtoFile<TLVector<TLString>>(_importedPhonesRoot, Constants.ImportedPhonesFileName) ??
                new TLVector<TLString>();

            var importedPhonesCache = new Dictionary<string, string>();
            foreach (var importedPhone in importedPhones)
            {
                var phone = importedPhone.ToString();
                importedPhonesCache[phone] = phone;
            }

            return importedPhonesCache;
        }

        public override void RefreshItems()
        {
            ImportContactsAsync(true);
        }

        public void InviteFriends()
        {
            StateService.ShareLink = Constants.TelegramShare;
            StateService.ShareMessage = AppResources.InviteFriendMessage;
            StateService.ShareCaption = AppResources.InviteFriends;
            NavigationService.UriFor<ShareViewModel>().Navigate();
        }

        private void InsertContacts(IEnumerable<TLUserBase> newUsers, bool fullReplace)
        {
            var itemsCache = new Dictionary<int, TLUserContact>();

            for (int i = 0; i < Items.Count; i++)
            {
                var contact = Items[i] as TLUserContact;
                if (contact != null
                    && !itemsCache.ContainsKey(contact.Index))
                {
                    itemsCache[contact.Index] = contact;
                }
            }

            var users = newUsers.OrderByDescending(x => x.FullName);
            var addingUsers = new List<TLUserBase>();

            foreach (var user in users)
            {
                if (!itemsCache.ContainsKey(user.Index) && !(user is TLUserEmpty) && user.Index != StateService.CurrentUserId)
                {
                    addingUsers.Add(user);
                }
            }

            Status = addingUsers.Count != 0 || Items.Count != 0 || LazyItems.Count != 0 ? string.Empty : Status;
            foreach (var addingUser in addingUsers)
            {
                InsertContact(addingUser);
            }
        }

        private void InsertContact(TLUserBase user)
        {
            BeginOnUIThread(() =>
            {
                var comparer = Comparer<string>.Default;
                var position = 0;
                for (var i = 0; i < Items.Count; i++)
                {
                    if (comparer.Compare(Items[i].FullName, user.FullName) == 0)
                    {
                        position = -1;
                        break;
                    }
                    if (comparer.Compare(Items[i].FullName, user.FullName) > 0)
                    {
                        position = i;
                        break;
                    }
                }

                if (position != -1)
                {
                    Items.Insert(position, user);
                    //Thread.Sleep(20);
                }
            });
        }

        #endregion

        public void Handle(string command)
        {
            if (string.Equals(command, Commands.LogOutCommand))
            {
                _runOnce = true;
                LazyItems.Clear();
                Items.Clear();
                Status = string.Empty;
                IsWorking = false;
                FileUtils.Delete(_importedPhonesRoot, Constants.ImportedPhonesFileName);
            }
        }

        public void Handle(TLUserBase user)
        {
            BeginOnUIThread(() =>
            {
                if (LazyItems.Count > 0) return;
                

                var item = Items.FirstOrDefault(x => x.Index == user.Index);
                if (item != null)
                {
                    if (!(user is TLUserContact))
                    {
                        Items.Remove(item);
                    }
                    if (user is TLUserContact)
                    {
                        InsertContact(user);
                    }
                }
                else if (user is TLUserContact)
                {
                    InsertContact(user);
                }
            });
        }

        public void Handle(InvokeImportContacts message)
        {
            Execute.ShowDebugMessage("invokeImportContacts");
            GetContactsAsync();
            ImportContactsAsync();
        }

        public void Handle(TLUpdatePrivacy update)
        {
            Execute.ShowDebugMessage("update privacy");
            MTProtoService.GetStatusesAsync(
                statuses =>
                {
                    try
                    {
                        for (var i = 0; i < Items.Count; i++)
                        {
                            Items[i].NotifyOfPropertyChange("Status");
                        }

                        _lastUpdateStatusesTime = DateTime.Now;
                    }
                    catch (Exception e)
                    {
                        TLUtils.WriteException(e);
                    }
                },
                error =>
                {
                    Execute.ShowDebugMessage("contacts.getStatuses error " + error);
                });
        }

        public void Handle(TLUpdateUserName update)
        {
            // threadpool
            var userId = update.UserId;
            var contact = CacheService.GetUser(userId) as TLUserContact;
            if (contact != null)
            {
                ContactsHelper.UpdateContactAsync(_fileManager,StateService, contact);
            }
        }

        public void Handle(TLUpdateUserPhoto update)
        {
            //threadpool
            var userId = update.UserId;
            var contact = CacheService.GetUser(userId) as TLUserContact;
            if (contact != null)
            {
                ContactsHelper.UpdateContactAsync(_fileManager, StateService, contact);
            }
        }
    }

    public class InvokeImportContacts { }
}
