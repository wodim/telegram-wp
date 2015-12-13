using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Caliburn.Micro;
using Microsoft.Phone.Shell;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.Updates;
using Telegram.Api.TL;
using TelegramClient.Resources;
using TelegramClient.Services;
using Execute = Telegram.Api.Helpers.Execute;

namespace TelegramClient.ViewModels.Additional
{
    public class SessionsViewModel : ItemsViewModelBase<TLAccountAuthorization>, Telegram.Api.Aggregator.IHandle<TLUpdateNewAuthorization>
    {
        public TLAccountAuthorization Current { get; set; }

        private readonly DispatcherTimer _getAuthorizationsTimer = new DispatcherTimer();

        private void StartTimer()
        {
            _getAuthorizationsTimer.Start();
        }

        private void StopTimer()
        {
            _getAuthorizationsTimer.Stop();
        }

        public SessionsViewModel(ICacheService cacheService, ICommonErrorHandler errorHandler, IStateService stateService, INavigationService navigationService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator) 
            : base(cacheService, errorHandler, stateService, navigationService, mtProtoService, eventAggregator)
        {
            _getAuthorizationsTimer.Tick += OnGetAuthorizations;
            _getAuthorizationsTimer.Interval = TimeSpan.FromSeconds(10.0);

            EventAggregator.Subscribe(this);

            Status = AppResources.Loading;

            UpdateSessionsAsync();
        }

        private void OnGetAuthorizations(object sender, System.EventArgs e)
        {
            UpdateSessionsAsync();
        }

        protected override void OnActivate()
        {
            StartTimer();

            base.OnActivate();
        }

        protected override void OnDeactivate(bool close)
        {
            StopTimer();

            base.OnDeactivate(close);
        }

        private Dictionary<long, TLAccountAuthorization> _authorizationsCache = new Dictionary<long, TLAccountAuthorization>(); 

        private void UpdateSessionsAsync()
        {
            BeginOnThreadPool(() =>
            {
                IsWorking = true;
                MTProtoService.GetAuthorizationsAsync(
                    accountAuthorizations => Execute.BeginOnUIThread(() =>
                    {
                        Status = string.Empty;
                        IsWorking = false;

                        var firstRun = Current == null;
                        if (firstRun)
                        {
                            Items.Clear();
                            var firstChunkSize = 4;
                            var count = 0;
                            var delayedItems = new List<TLAccountAuthorization>();
                            for (var i = 0; i < accountAuthorizations.Authorizations.Count; i++)
                            {
                                var authorization = accountAuthorizations.Authorizations[i];
                                _authorizationsCache[authorization.Hash.Value] = authorization;
                                if (authorization.IsCurrent)
                                {
                                    Current = authorization;
                                    NotifyOfPropertyChange(() => Current);
                                }
                                else
                                {
                                    if (count < firstChunkSize)
                                    {
                                        Items.Add(authorization);
                                        count++;
                                    }
                                    else
                                    {
                                        delayedItems.Add(authorization);
                                    }
                                }
                            }

                            BeginOnUIThread(TimeSpan.FromSeconds(0.5), () =>
                            {
                                foreach (var authorization in delayedItems)
                                {
                                    Items.Add(authorization);
                                }
                            });
                        }
                        else
                        {
                            var newAuthorizationsCache = new Dictionary<long, TLAccountAuthorization>();
                            var itemsToAdd = new List<TLAccountAuthorization>();
                            for (var i = 0; i < accountAuthorizations.Authorizations.Count; i++)
                            {
                                var authorization = accountAuthorizations.Authorizations[i];
                                TLAccountAuthorization cachedAuthorization;
                                if (!_authorizationsCache.TryGetValue(authorization.Hash.Value, out cachedAuthorization))
                                {
                                    itemsToAdd.Add(authorization);
                                    newAuthorizationsCache[authorization.Hash.Value] = authorization;
                                }
                                else
                                {
                                    cachedAuthorization.Update(authorization);
                                    cachedAuthorization.NotifyOfPropertyChange(() => cachedAuthorization.AppFullName);
                                    cachedAuthorization.NotifyOfPropertyChange(() => cachedAuthorization.DateActive);
                                    cachedAuthorization.NotifyOfPropertyChange(() => cachedAuthorization.DeviceFullName);
                                    cachedAuthorization.NotifyOfPropertyChange(() => cachedAuthorization.ApiId);
                                    cachedAuthorization.NotifyOfPropertyChange(() => cachedAuthorization.IsOfficialApp);

                                    newAuthorizationsCache[authorization.Hash.Value] = cachedAuthorization;
                                }
                            }

                            for (var i = 0; i < Items.Count; i++)
                            {
                                if (!newAuthorizationsCache.ContainsKey(Items[i].Hash.Value))
                                {
                                    Items.RemoveAt(i--);
                                }
                            }

                            for (var i = 0; i < itemsToAdd.Count; i++)
                            {
                                Items.Insert(0, itemsToAdd[i]);
                            }

                            _authorizationsCache = newAuthorizationsCache;
                        }
                        
                    }),
                    error =>
                    {
                        Status = string.Empty;
                        IsWorking = false;
                        Execute.ShowDebugMessage("account.getAuthorizations error " + error);
                    });
            });
        }

        private void ProcessAccountAuthorizations(TLAccountAuthorizations accountAuthorizations)
        {
            
        }

        public void Terminate(TLAccountAuthorization authorization)
        {
            if (authorization == null) return;

            if (authorization.IsCurrent)
            {
                var result = MessageBox.Show(AppResources.LogOutConfirmation, AppResources.Confirm, MessageBoxButton.OKCancel);
                if (result != MessageBoxResult.OK) return;


                var updatesService = IoC.Get<IUpdatesService>();
                var pushService = IoC.Get<IPushService>();

                pushService.UnregisterDeviceAsync(() =>
                    MTProtoService.LogOutAsync(() =>
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
                    }));

                Telegram.Logs.Log.Write("StartupViewModel SessionsViewModel.Terminate");
                SettingsViewModel.LogOutCommon(EventAggregator, MTProtoService, updatesService, CacheService, StateService, pushService, NavigationService);
            }
            else
            {
                IsWorking = true;
                MTProtoService.ResetAuthorizationAsync(
                    authorization.Hash,
                    result => BeginOnUIThread(() =>
                    {
                        IsWorking = false;
                        Items.Remove(authorization);
                    }),
                    error =>
                    {
                        IsWorking = false;
                        Execute.ShowDebugMessage("auth.resetAuthotization error " + error);
                    });
            }
        }

        public void TerminateOtherSessions()
        {
            var confirmation = MessageBox.Show(AppResources.TerminateAllSessionsConfirmation, AppResources.Confirm, MessageBoxButton.OKCancel);
            if (confirmation != MessageBoxResult.OK)
            {
                return;
            }

            IsWorking = true;
            MTProtoService.ResetAuthorizationsAsync(
                result => BeginOnUIThread(() =>
                {
                    IsWorking = false;
                    Items.Clear();
                }),
                error =>
                {
                    IsWorking = false;
                    Execute.ShowDebugMessage("auth.resetAuthotizations error " + error);
                });
        }

        public void Handle(TLUpdateNewAuthorization update)
        {
            UpdateSessionsAsync();
        }
    }
}
