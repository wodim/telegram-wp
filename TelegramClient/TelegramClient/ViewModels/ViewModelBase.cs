using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Windows;
using Caliburn.Micro;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using TelegramClient.Services;

namespace TelegramClient.ViewModels
{
    public abstract class ViewModelBase : Screen
    {
        private Visibility _visibility;

        public Visibility Visibility
        {
            get { return _visibility; }
            set { SetField(ref _visibility, value, () => Visibility); }
        }

        protected bool SetField<T>(ref T field, T value, string propertyName)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            NotifyOfPropertyChange(propertyName);
            return true;
        }

        protected bool SetField<T>(ref T field, T value, Expression<Func<T>> selectorExpression)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            NotifyOfPropertyChange(selectorExpression);
            return true;
        }

        private bool _isLoadingError;

        public bool IsLoadingError
        {
            get { return _isLoadingError; }
            set { SetField(ref _isLoadingError, value, () => IsLoadingError); }
        }

        private bool _isWorking;

        public bool IsWorking
        {
            get { return _isWorking; }
            set { SetField(ref _isWorking, value, () => IsWorking); }
        }
        
        public IMTProtoService MTProtoService { get; private set; }

        protected readonly INavigationService NavigationService;

        public IStateService StateService { get; private set; }

        protected readonly ITelegramEventAggregator EventAggregator;

        protected readonly ICommonErrorHandler ErrorHandler;

        protected readonly ICacheService CacheService;

        private static DateTime _lastStatusTime;

        protected ViewModelBase(ICacheService cacheService, ICommonErrorHandler errorHandler, IStateService stateService, INavigationService navigationService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator)
        {
            CacheService = cacheService;
            ErrorHandler = errorHandler;
            StateService = stateService;
            MTProtoService = mtProtoService;
            NavigationService = navigationService;
            EventAggregator = eventAggregator;
        }

        protected bool SuppressUpdateStatus { get;  set; }

        protected override void OnActivate()
        {
            if (SuppressUpdateStatus) return;

            if ((DateTime.Now - _lastStatusTime).TotalSeconds < 20.0)
            {
                return;
            }
            _lastStatusTime = DateTime.Now;

            BeginOnThreadPool(() => 
                StateService.GetNotifySettingsAsync(
                    settings =>
                    {
                        var isAuthorized = SettingsHelper.GetValue<bool>(Constants.IsAuthorizedKey);

                        if (isAuthorized && !settings.InvisibleMode)
                        {
                            MTProtoService.RaiseSendStatus(new SendStatusEventArgs(new TLBool(false)));
                        }
                    }));

            base.OnActivate();
        }

        public void BeginOnUIThread(System.Action action)
        {
            Telegram.Api.Helpers.Execute.BeginOnUIThread(action);
        }

        public void BeginOnUIThread(TimeSpan delay, System.Action action)
        {
            Telegram.Api.Helpers.Execute.BeginOnUIThread(delay, action);
        }

        public void BeginOnThreadPool(System.Action action)
        {
            Telegram.Api.Helpers.Execute.BeginOnThreadPool(action);
        }

        public void Subscribe()
        {
            EventAggregator.Subscribe(this);
        }

        public void Unsubscribe()
        {
            EventAggregator.Unsubscribe(this);
        }
    }
}
