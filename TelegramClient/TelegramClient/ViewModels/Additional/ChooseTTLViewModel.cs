using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;
using Caliburn.Micro;
using Microsoft.Phone.Controls.Primitives;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using TelegramClient.Resources;
using TelegramClient.Services;
using TelegramClient.ViewModels.Contacts;

namespace TelegramClient.ViewModels.Additional
{
    public class ChooseTTLViewModel : ViewModelBase
    {
        public LoopingObservableCollection<TimerSpan> Items { get; private set; }

        public ChooseTTLViewModel(ICacheService cacheService, ICommonErrorHandler errorHandler, IStateService stateService, INavigationService navigationService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator) : 
            base(cacheService, errorHandler, stateService, navigationService, mtProtoService, eventAggregator)
        {
            Items = new LoopingObservableCollection<TimerSpan>
            {
                new TimerSpan(AppResources.OffMasculine, string.Empty, 0),
                new TimerSpan(AppResources.SecondNominativeSingular,  "1", 1),
                new TimerSpan(AppResources.SecondNominativePlural, "2", 2),
                new TimerSpan(AppResources.SecondNominativePlural, "3", 3),
                new TimerSpan(AppResources.SecondNominativePlural, "4", 4),
                new TimerSpan(AppResources.SecondGenitivePlural, "5", 5),
                new TimerSpan(AppResources.SecondGenitivePlural, "6", 6),
                new TimerSpan(AppResources.SecondGenitivePlural, "7", 7),
                new TimerSpan(AppResources.SecondGenitivePlural, "8", 8),
                new TimerSpan(AppResources.SecondGenitivePlural, "9", 9),
                new TimerSpan(AppResources.SecondGenitivePlural, "10", 10),
                new TimerSpan(AppResources.SecondGenitivePlural, "11", 11),
                new TimerSpan(AppResources.SecondGenitivePlural, "12", 12),
                new TimerSpan(AppResources.SecondGenitivePlural, "13", 13),
                new TimerSpan(AppResources.SecondGenitivePlural, "14", 14),
                new TimerSpan(AppResources.SecondGenitivePlural, "15", 15),
                new TimerSpan(AppResources.SecondGenitivePlural, "30", 30),
                new TimerSpan(AppResources.MinuteNominativeSingular, "1", 60),
                new TimerSpan(AppResources.HourNominativeSingular, "1", (int) TimeSpan.FromHours(1.0).TotalSeconds),
                new TimerSpan(AppResources.DayNominativeSingular, "1", (int) TimeSpan.FromDays(1.0).TotalSeconds),
                new TimerSpan(AppResources.WeekNominativeSingular, "1", (int) TimeSpan.FromDays(7.0).TotalSeconds),
            };

            if (StateService.SelectedTimerSpan == null)
            {
                Items.SelectedItem = Items[0];
            }
            else
            {
                var selectedItem = Items.FirstOrDefault(x => x.Seconds == StateService.SelectedTimerSpan.Seconds);
                Items.SelectedItem = selectedItem ?? Items[0];
            }
            StateService.SelectedTimerSpan = null;
        }

        public void Done()
        {
            StateService.SelectedTimerSpan = (TimerSpan)Items.SelectedItem;
            NavigationService.GoBack();
        }

        public void Cancel()
        {
            NavigationService.GoBack();
        }
    }

    public class LoopingObservableCollection<T> : ObservableCollection<T>, ILoopingSelectorDataSource
    {
        public object GetNext(object relativeTo)
        {

            if (relativeTo == null) return this[0];

            var item = (T)relativeTo;
            var position = IndexOf(item);
            if (position + 1 == Count)
            {
                return this[0];
            }

            return this[position + 1];
        }

        public object GetPrevious(object relativeTo)
        {
            if (relativeTo == null) return this[Count - 1];

            var item = (T)relativeTo;
            var position = IndexOf(item);
            if (position == 0)
            {
                return this[Count - 1];
            }

            return this[position - 1];
        }

        public object SelectedItem { get; set; }

        public event EventHandler<SelectionChangedEventArgs> SelectionChanged;
    }
}
