using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Caliburn.Micro;
using Microsoft.Phone.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using TelegramClient.Services;

namespace TelegramClient.ViewModels
{
    public class DebugViewModel : ViewModelBase
    {
        public DebugViewModel(ICacheService cacheService, ICommonErrorHandler errorHandler, IStateService stateService, INavigationService navigationService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator) 
            : base(cacheService, errorHandler, stateService, navigationService, mtProtoService, eventAggregator)
        {
            DisplayName = "debug";
        }

        public IList<string> Items { get { return TLUtils.DebugItems; } }

        public bool IsDebugEnabled
        {
            get { return TLUtils.IsDebugEnabled; }
            set { TLUtils.IsDebugEnabled = value; }
        }
       
         
        public void Send()
        {
            var body = new StringBuilder();
            foreach (var debugItem in TLUtils.DebugItems)
            {
                body.Append(debugItem + "\n");
            }

            var task = new EmailComposeTask();
            task.Body = body.ToString();
            task.To = "johnnypmpu@bk.ru";
            task.Subject = "Debug log";
            task.Show();
        }

        public void Clear()
        {
            TLUtils.DebugItems.Clear();
            NotifyOfPropertyChange(() => Items);
        }
    }
}
