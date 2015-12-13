using System;
using System.Linq;
using Caliburn.Micro;
using Telegram.Api.TL;
using TelegramClient.Services;
using TelegramClient.ViewModels.Dialogs;
using TelegramClient.ViewModels.Media;

namespace TelegramClient.ViewModels.Contacts
{
    public class ContactViewModel : Conductor<ViewModelBase>.Collection.OneActive
    {
        public bool IsViewerOpen
        {
            get { return ProfilePhotoViewer != null && ProfilePhotoViewer.IsOpen; }
        }

        public TLUserBase Contact { get; protected set; }

        public TLString ContactPhone { get; protected set; }

        public ContactDetailsViewModel ContactDetails { get; protected set; }

        private readonly IStateService _stateService;

        private readonly INavigationService _navigationService;

        public ProfilePhotoViewerViewModel ProfilePhotoViewer { get { return ContactDetails.ProfilePhotoViewer; } }

        public ContactViewModel(
            ContactDetailsViewModel contactDetails, 
            IStateService stateService,
            INavigationService navigationService)
        {
            //tombstoning
            if (stateService.CurrentContact == null)
            {
                stateService.ClearNavigationStack = true;
                navigationService.UriFor<ShellViewModel>().Navigate();
                return;
            }

            Contact = stateService.CurrentContact;
            stateService.CurrentContact = null;

            ContactPhone = stateService.CurrentContactPhone;
            stateService.CurrentContactPhone = null;

            ContactDetails = contactDetails;
            ContactDetails.ProfilePhotoViewer = ProfilePhotoViewer;

            _stateService = stateService;
            _navigationService = navigationService;
        }

        protected override void OnInitialize()
        {
            if (Contact == null) return;

            ContactDetails.CurrentItem = Contact;
            var notifySettings = Contact.NotifySettings as TLPeerNotifySettings;
            if (notifySettings != null)
            {
                var sound = _stateService.Sounds.FirstOrDefault(x => string.Equals(x, notifySettings.Sound.Value, StringComparison.OrdinalIgnoreCase));
                ContactDetails.SetSelectedSound(sound ?? _stateService.Sounds[0]);
            }
            ContactDetails.CurrentPhone = ContactPhone;

            NotifyOfPropertyChange(() => Contact);

            Items.Add(ContactDetails);
            ActivateItem(ContactDetails);

            base.OnInitialize();
        }

        protected override void OnActivate()
        {
            if (_stateService.SelectedTimerSpan != null)
            {
                ContactDetails.SelectedSpan = _stateService.SelectedTimerSpan;
                _stateService.SelectedTimerSpan = null;
            }

            ContactDetails.StartTimer();

            base.OnActivate();
        }

        protected override void OnDeactivate(bool close)
        {
            ContactDetails.StopTimer();

            base.OnDeactivate(close);
        }

        public void Edit()
        {
            _stateService.CurrentContact = Contact;
            _navigationService.UriFor<EditContactViewModel>().Navigate(); 
        }

        public void Share()
        {
            _stateService.SharedContact = Contact;
            _navigationService.UriFor<ChooseDialogViewModel>().Navigate(); 
        }

        public void OnBackKeyPressed()
        {
            _stateService.SharedContact = null;
        }

        public void BlockContact()
        {
            ContactDetails.BlockContact();
        }

        public void UnblockContact()
        {
            ContactDetails.UnblockContact();
        }

        public void AddContact()
        {
            ContactDetails.AddContact();
        }

        public void DeleteContact()
        {
            ContactDetails.DeleteContact();
        }

        public void AddToGroup()
        {
            ContactDetails.AddToGroup();
        }
    }
}
