using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Caliburn.Micro;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using TelegramClient.Resources;
using TelegramClient.Services;
using TelegramClient.ViewModels.Chats;
using TelegramClient.ViewModels.Search;
using Execute = Telegram.Api.Helpers.Execute;

namespace TelegramClient.ViewModels.Dialogs
{
    public class ChooseParticipantsViewModel : ItemsViewModelBase<TLUserBase>
    {
        public string Text { get; set; }

        private SearchUsersRequest _lastSearchRequest;

        private List<TLUserBase> _source;

        private readonly Dictionary<string, SearchUsersRequest> _searchResultsCache = new Dictionary<string, SearchUsersRequest>();

        private volatile bool _isFullResults;

        public ChooseParticipantsViewModel(ICacheService cacheService, ICommonErrorHandler errorHandler, IStateService stateService, INavigationService navigationService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator) 
            : base(cacheService, errorHandler, stateService, navigationService, mtProtoService, eventAggregator)
        {
            Items = new ObservableCollection<TLUserBase>();
            Status = AppResources.Loading;
            BeginOnThreadPool(() =>
            {
                _source = _source ??
                        CacheService.GetContacts()
                        .Where(x => !(x is TLUserEmpty) && x.Index != StateService.CurrentUserId)
                        .OrderBy(x => x.FullName)
                        .ToList();

                Status = string.Empty;
                foreach (var contact in _source)
                {
                    LazyItems.Add(contact);
                }

                if (Items.Count == 0 && LazyItems.Count == 0)
                {
                    Status = AppResources.NoUsersHere;
                }

                BeginOnUIThread(() => PopulateItems(() => { _isFullResults = true; }));
            });
        }

        #region Action
        public void UserAction(TLUserBase user)
        {
            OpenUserChat(user);
        }

        public void OpenUserChat(TLUserBase user)
        {
            if (user == null) return;

            StateService.RemoveBackEntry = true;
            StateService.With = user;
            StateService.AnimateTitle = true;
            NavigationService.UriFor<DialogDetailsViewModel>().Navigate();
        }

        public void NewGroup()
        {
            CacheService.CheckDisabledFeature(Constants.FeatureChatCreate,
                () =>
                {
                    StateService.RemoveBackEntry = true;
                    NavigationService.UriFor<CreateDialogViewModel>().Navigate();
                },
                disabledFeature =>
                {
                    Execute.BeginOnUIThread(() => MessageBox.Show(disabledFeature.Description.ToString(), AppResources.AppName, MessageBoxButton.OK));
                });
        }

        public void NewSecretChat()
        {
            StateService.RemoveBackEntry = true;
            NavigationService.UriFor<AddSecretChatParticipantViewModel>().Navigate();
        }

        public void NewBroadcastList()
        {
            CacheService.CheckDisabledFeature(Constants.FeatureBroadcastCreate,
                () =>
                {
                    StateService.RemoveBackEntry = true;
                    NavigationService.UriFor<CreateBroadcastViewModel>().Navigate();
                },
                disabledFeature =>
                {
                    Execute.BeginOnUIThread(() => MessageBox.Show(disabledFeature.Description.ToString(), AppResources.AppName, MessageBoxButton.OK));
                });
        }

        public void NewChannel()
        {
            CacheService.CheckDisabledFeature(Constants.FeatureBroadcastCreate,
                () => ChannelIntroViewModel.CheckIntroEnabledAsync(
                    enabled => BeginOnUIThread(() =>
                    {
                        if (enabled)
                        {
                            StateService.RemoveBackEntry = true;
                            NavigationService.UriFor<ChannelIntroViewModel>().Navigate();
                        }
                        else
                        {
                            StateService.RemoveBackEntry = true;
                            NavigationService.UriFor<CreateChannelStep1ViewModel>().Navigate();
                        }
                    })),
                disabledFeature =>
                {
                    Execute.BeginOnUIThread(() => MessageBox.Show(disabledFeature.Description.ToString(), AppResources.AppName, MessageBoxButton.OK));
                });
        }
        #endregion

        public void Search()
        {
            if (_lastSearchRequest != null)
            {
                _lastSearchRequest.Cancel();
            }

            var text = Text.Trim();

            if (string.IsNullOrEmpty(text))
            {
                if (_isFullResults) return;

                LazyItems.Clear();               
                Items.Clear();

                foreach (var contact in _source)
                {
                    Items.Add(contact);
                }

                _isFullResults = true;

                return;
            }

            var nextSearchRequest = CreateSearchRequest(text);

            _isFullResults = false;
            IsWorking = true;
            nextSearchRequest.ProcessAsync(results =>
                Execute.BeginOnUIThread(() =>
                {
                    if (nextSearchRequest.IsCanceled) return;

                    Items.Clear();
                    LazyItems.Clear();

                    for (var i = 0; i < results.Count; i++)
                    {
                        if (i < 6)
                        {
                            Items.Add(results[i]);
                        }
                        else
                        {
                            LazyItems.Add(results[i]);
                        }
                    }

                    IsWorking = false;
                    Status = Items.Count == 0 ? AppResources.NoResults : string.Empty;

                    PopulateItems();
                }));

            _searchResultsCache[nextSearchRequest.Text] = nextSearchRequest;
            _lastSearchRequest = nextSearchRequest;
        }

        private SearchUsersRequest CreateSearchRequest(string text)
        {
            SearchUsersRequest request;
            if (!_searchResultsCache.TryGetValue(text, out request))
            {
                IList<TLUserBase> source;

                if (_lastSearchRequest != null
                    && text.IndexOf(_lastSearchRequest.Text, StringComparison.OrdinalIgnoreCase) != -1)
                {
                    source = _lastSearchRequest.Source;
                }
                else
                {
                    _source = _source ??
                              CacheService.GetContacts()
                                  .Where(x => !(x is TLUserEmpty) && x.Index != StateService.CurrentUserId)
                                  .OrderBy(x => x.FullName)
                                  .ToList();

                    source = _source;
                }

                request = new SearchUsersRequest(text, source);
            }
            return request;
        }
    }
}
