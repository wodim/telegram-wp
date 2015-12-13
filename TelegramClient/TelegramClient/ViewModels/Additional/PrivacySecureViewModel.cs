using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using Caliburn.Micro;
using ImageTools;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using TelegramClient.Converters;
using TelegramClient.Resources;
using TelegramClient.Services;
using TelegramClient.Utils;
using Execute = Telegram.Api.Helpers.Execute;

namespace TelegramClient.ViewModels.Additional
{
    public class PrivacySecurityViewModel : ViewModelBase, Telegram.Api.Aggregator.IHandle<TLUpdateUserBlocked>, Telegram.Api.Aggregator.IHandle<TLUpdatePrivacy>
    {
        private int _blockedUsersCount;

        private string _blockedUsersSubtitle = " ";

        public string BlockedUsersSubtitle
        {
            get { return _blockedUsersSubtitle; }
            set { SetField(ref _blockedUsersSubtitle, value, () => BlockedUsersSubtitle); }
        }

        private TLPrivacyRules _privacyRules;

        private string _lastSeenSubtitle = " ";

        public string LastSeenSubtitle
        {
            get { return _lastSeenSubtitle; }
            set { SetField(ref _lastSeenSubtitle, value, () => LastSeenSubtitle); }
        }

        private int _accountDaysTTL;

        private string _accountSelfDestructsSubtitle = " ";

        public string AccountSelfDestructsSubtitle
        {
            get { return _accountSelfDestructsSubtitle; }
            set { SetField(ref _accountSelfDestructsSubtitle, value, () => AccountSelfDestructsSubtitle); }
        }

        public PrivacySecurityViewModel(ICacheService cacheService, ICommonErrorHandler errorHandler, IStateService stateService, INavigationService navigationService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator) 
            : base(cacheService, errorHandler, stateService, navigationService, mtProtoService, eventAggregator)
        {
            EventAggregator.Subscribe(this);
        }

        protected override void OnActivate()
        {
            base.OnActivate();

            MTProtoService.GetBlockedAsync(new TLInt(0), new TLInt(int.MaxValue),
                result =>
                {
                    var contacts = result as TLContactsBlocked;
                    if (contacts != null)
                    {
                        var count = contacts.Blocked.Count;

                        UpdateBlockedUsersString(count);
                    }
                },
                error => Execute.ShowDebugMessage("contacts.getBlocked error " + error));

            MTProtoService.GetAccountTTLAsync(
                result =>
                {
                    var days = result.Days.Value;

                    UpdateAccountTTLString(days);
                },
                error => Execute.ShowDebugMessage("account.getAccountTTL error " + error));

            MTProtoService.GetPrivacyAsync(new TLInputPrivacyKeyStatusTimestamp(), 
                result =>
                {
                    UpdateLastSeenString(result);
                },
                error => Execute.ShowDebugMessage("account.getPrivacy error " + error));
        }

        private void UpdateLastSeenString(TLPrivacyRules rules)
        {
            _privacyRules = rules;

            TLPrivacyRuleBase mainRule = null;
            var mainRuleString = string.Empty;
            var minusCount = 0;
            var plusCount = 0;
            foreach (var rule in rules.Rules)
            {
                if (rule is TLPrivacyValueAllowAll)
                {
                    mainRule = rule;
                    mainRuleString = AppResources.Everybody;
                }

                if (rule is TLPrivacyValueAllowContacts)
                {
                    mainRule = rule;
                    mainRuleString = AppResources.MyContacts;
                }

                if (rule is TLPrivacyValueDisallowAll)
                {
                    mainRule = rule;
                    mainRuleString = AppResources.Nobody;
                }

                if (rule is TLPrivacyValueDisallowUsers)
                {
                    minusCount += ((TLPrivacyValueDisallowUsers) rule).Users.Count;
                }

                if (rule is TLPrivacyValueAllowUsers)
                {
                    plusCount += ((TLPrivacyValueAllowUsers) rule).Users.Count;
                }
            }

            if (mainRule == null)
            {
                mainRule = new TLPrivacyValueDisallowAll();
                mainRuleString = AppResources.Nobody;
            }

            var countStrings = new List<string>();
            if (minusCount > 0)
            {
                countStrings.Add("-" + minusCount);
            }
            if (plusCount > 0)
            {
                countStrings.Add("+" + plusCount);
            }
            if (countStrings.Count > 0)
            {
                mainRuleString += string.Format(" ({0})", string.Join(", ", countStrings));
            }

            LastSeenSubtitle = mainRuleString.ToLowerInvariant();
        }

        public void OpenBlockedUsers()
        {
            NavigationService.UriFor<BlockedContactsViewModel>().Navigate();
        }

        private void UpdateBlockedUsersString(int count)
        {
            _blockedUsersCount = count;
            if (count == 0)
            {
                BlockedUsersSubtitle = LowercaseConverter.Convert(AppResources.NoUsers);
            }
            else if (count > 0)
            {
                BlockedUsersSubtitle = Language.Declension(
                    count,
                    AppResources.UserNominativeSingular,
                    AppResources.UserNominativePlural,
                    AppResources.UserGenitiveSingular,
                    AppResources.UserGenitivePlural).ToLower(CultureInfo.CurrentUICulture);
            }
        }

        private void UpdateAccountTTLString(int days)
        {
            _accountDaysTTL = days;
            if (days >= 365)
            {
                var years = days/365;
                var yearsString = Language.Declension(
                    years,
                    AppResources.YearNominativeSingular,
                    AppResources.YearNominativePlural,
                    AppResources.YearGenitiveSingular,
                    AppResources.YearGenitivePlural).ToLower(CultureInfo.CurrentUICulture);
                AccountSelfDestructsSubtitle = string.Format("{0} {1}", AppResources.IfYouAreAwayFor, yearsString);
            }
            else
            {
                var months = days/30;

                var monthsString = Language.Declension(
                    months,
                    AppResources.MonthNominativeSingular,
                    AppResources.MonthNominativePlural,
                    AppResources.MonthGenitiveSingular,
                    AppResources.MonthGenitivePlural).ToLower(CultureInfo.CurrentUICulture);
                AccountSelfDestructsSubtitle = string.Format("{0} {1}", AppResources.IfYouAreAwayFor, monthsString);
            }
        }

        public void TerminateAllSessions()
        {
            NavigationService.UriFor<SessionsViewModel>().Navigate();
        }

        public void LastSeen()
        {
            StateService.PrivacyRules = _privacyRules;
            NavigationService.UriFor<LastSeenViewModel>().Navigate();
        }

        public void AccountSelfDestructs()
        {
            StateService.AccountDaysTTL = _accountDaysTTL;
            NavigationService.UriFor<AccountSelfDestructsViewModel>().Navigate();
        }

        public void Passcode()
        {
            if (!PasscodeUtils.IsEnabled)
            {
                NavigationService.UriFor<PasscodeViewModel>().Navigate();
            }
            else
            {
                NavigationService.UriFor<EnterPasscodeViewModel>().Navigate();
            }
        }

        public void TwoStepVerification()
        {
            if (IsWorking) return;

            IsWorking = true;
            MTProtoService.GetPasswordAsync(
                result => BeginOnUIThread(() =>
                {
                    IsWorking = false;
                    var noPassword = result as TLPassword;
                    if (noPassword != null)
                    {
                        StateService.Password = result;
                        NavigationService.UriFor<EnterPasswordViewModel>().Navigate();
                        return;
                    }

                    StateService.Password = result;
                    NavigationService.UriFor<PasswordViewModel>().Navigate();
                }),
                error =>
                {
                    IsWorking = false;
                    Execute.ShowDebugMessage("account.getPassword error " + error);
                });
        }

        public void Handle(TLUpdateUserBlocked update)
        {
            var count = _blockedUsersCount;
            if (update.Blocked.Value)
            {
                _blockedUsersCount++;
                count++;
            }
            else if (count > 0)
            {
                _blockedUsersCount--;
                count--;
            }

            UpdateBlockedUsersString(count);
        }

        public void Handle(TLUpdatePrivacy privacy)
        {
            UpdateLastSeenString(new TLPrivacyRules{Rules = privacy.Rules});
        }
    }
}
