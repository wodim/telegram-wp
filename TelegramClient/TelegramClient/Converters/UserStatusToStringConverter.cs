using System;
using System.Globalization;
using System.Threading;
using System.Windows.Data;
using Caliburn.Micro;
using Telegram.Api.Services;
using Telegram.Api.TL;
using TelegramClient.Resources;
using TelegramClient.Utils;

namespace TelegramClient.Converters
{
    public class UserStatusToStringConverter : IValueConverter
    {
        public static string GetShortTimePattern(ref CultureInfo ci)
        {
            if (ci.DateTimeFormat.ShortTimePattern.Contains("H"))
            {
                return "H:mm";
            }

            ci.DateTimeFormat.AMDesignator = "am";
            ci.DateTimeFormat.PMDesignator = "pm";
            return "h:mmt";
        }

        public static string Convert(TLUserStatus status)
        {
            if (status == null)
            {
                return AppResources.LastSeenLongTimeAgo;
            }

            if (!(status is TLUserStatusEmpty))
            {
                if (status is TLUserStatusOnline)
                {
                    return LowercaseConverter.Convert(AppResources.Online);
                }

                if (status is TLUserStatusRecently)
                {
                    return LowercaseConverter.Convert(AppResources.LastSeenRecently);
                }

                if (status is TLUserStatusLastMonth)
                {
                    return LowercaseConverter.Convert(AppResources.LastSeenWithinMonth);
                }

                if (status is TLUserStatusLastWeek)
                {
                    return LowercaseConverter.Convert(AppResources.LastSeenWithinWeek);
                }

                if (status is TLUserStatusOffline)
                {
                    var cultureInfo = (CultureInfo)Thread.CurrentThread.CurrentCulture.Clone();
                    var shortTimePattern = GetShortTimePattern(ref cultureInfo);

                    var clientDelta = IoC.Get<IMTProtoService>().ClientTicksDelta;
                    var utc0SecsLong = (((TLUserStatusOffline)status).WasOnline).Value * 4294967296 - clientDelta;
                    var utc0SecsInt = utc0SecsLong / 4294967296.0;

                    //var utc0SecsInt = (((TLUserStatusOffline) status).WasOnline).Value;
                    var lastSeen = Telegram.Api.Helpers.Utils.UnixTimestampToDateTime(utc0SecsInt);

                    var lastSeenTimeSpan = DateTime.Now - lastSeen;

                    // Just now
                    if (lastSeenTimeSpan.TotalMinutes <= 1)
                    {
                        return AppResources.LastSeenJustNow.ToLowerInvariant();
                    }

                    // Up to one hour
                    if (lastSeenTimeSpan < TimeSpan.FromMinutes(60.0))
                    {
                        var minutes = Language.Declension(
                            lastSeenTimeSpan.Minutes == 0 ? 1 : lastSeenTimeSpan.Minutes,
                            AppResources.MinuteAccusative,
                            null,
                            AppResources.MinuteGenitiveSingular,
                            AppResources.MinuteGenitivePlural,
                            lastSeenTimeSpan.Minutes < 2
                                ? string.Format(AppResources.Minute, 1).ToLowerInvariant()
                                : string.Format(AppResources.Minutes, Math.Abs(lastSeenTimeSpan.Minutes)));

                        return string.Format(AppResources.LastSeen, minutes).ToLowerInvariant();
                    }

                    // Today
                    if (lastSeen.Date == DateTime.Now.Date)
                    {
                        return string.Format(
                            AppResources.LastSeenAt.ToLowerInvariant(),
                            AppResources.Today.ToLowerInvariant(),
                            new DateTime(lastSeen.TimeOfDay.Ticks).ToString(shortTimePattern, cultureInfo));
                    }

                    // Yesterday
                    if (lastSeen.Date.AddDays(1.0) == DateTime.Now.Date)
                    {
                        return string.Format(
                            AppResources.LastSeenAt.ToLowerInvariant(),
                            AppResources.Yesterday.ToLowerInvariant(),
                            new DateTime(lastSeen.TimeOfDay.Ticks).ToString(shortTimePattern, cultureInfo));
                    }

                    // this year
                    if (lastSeen.Date.AddDays(365) >= DateTime.Now.Date)
                    {
                        return string.Format(
                            AppResources.LastSeenAtDate.ToLowerInvariant(),
                            lastSeen.ToString(AppResources.UserStatusDayFormat, cultureInfo),
                            new DateTime(lastSeen.TimeOfDay.Ticks).ToString(shortTimePattern, cultureInfo));
                    }

                    return string.Format(
                        AppResources.LastSeenAtDate.ToLowerInvariant(),
                        lastSeen.ToString(AppResources.UserStatusYearDayFormat, cultureInfo),
                        new DateTime(lastSeen.TimeOfDay.Ticks).ToString(shortTimePattern, cultureInfo));
                }
                
            }

            return LowercaseConverter.Convert(AppResources.LastSeenLongTimeAgo);
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var userBase = value as TLUserBase;
            if (userBase == null) return null;

            var user = userBase as TLUser;
            if (user != null)
            {
                if (user.IsBotAllHistory)
                {
                    return AppResources.SeesAllMessages.ToLowerInvariant();
                }

                return AppResources.OnlySeesMessagesStartingWithSlash.ToLowerInvariant();
            }

            var status = userBase.Status;
            if (status == null) return LowercaseConverter.Convert(AppResources.LastSeenLongTimeAgo);

            return Convert(status);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
