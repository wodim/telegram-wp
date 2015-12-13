using System.Linq;
using Caliburn.Micro;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using TelegramClient.Resources;
using TelegramClient.Services;

namespace TelegramClient.ViewModels.Contacts
{
    public class ShareContactViewModel : ItemsViewModelBase<TLUserBase>
    {
        public ShareContactViewModel(ICacheService cacheService, ICommonErrorHandler errorHandler, IStateService stateService, INavigationService navigationService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator) 
            : base(cacheService, errorHandler, stateService, navigationService, mtProtoService, eventAggregator)
        {
            Status = AppResources.Loading;
            BeginOnThreadPool(() => 
                CacheService.GetContactsAsync(
                contacts =>
                {
                    var currentUser = contacts.FirstOrDefault(x => x.Index == StateService.CurrentUserId);
                    if (currentUser == null)
                    {
                        currentUser = CacheService.GetUser(new TLInt(StateService.CurrentUserId));
                        if (currentUser != null)
                        {
                            contacts.Add(currentUser);
                        }
                    }

                    Status = string.Empty;
                    Items.Clear();
                    foreach (var contact in contacts.OrderBy(x => x.FullName))
                    {
                        if (!(contact is TLUserEmpty))
                        {
                            Items.Add(contact);
                        }
                    } 

                    if (Items.Count == 0)
                    {
                        Status = string.Format("{0}", AppResources.NoUsersHere);
                    }
                }));
        }

        protected override void OnActivate()
        {
            if (StateService.RemoveBackEntry)
            {
                StateService.RemoveBackEntry = false;
                NavigationService.RemoveBackEntry();
            }

            base.OnActivate();
        }

        #region Action
        public void UserAction(TLUserBase user)
        {
            if (user == null)   return;

            StateService.SharedContact = user;
            NavigationService.GoBack();
        }

        public void Search()
        {

        }
        #endregion
    }
}
