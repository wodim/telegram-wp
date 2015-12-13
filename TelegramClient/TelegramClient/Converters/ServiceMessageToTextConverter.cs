using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Caliburn.Micro;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Telegram.Controls.Utils;
using TelegramClient.Resources;
using TelegramClient.Services;
using Language = TelegramClient.Utils.Language;

namespace TelegramClient.Converters
{
    public class DecryptedServiceMessageToTextConverter : IValueConverter
    {
        private static readonly Dictionary<Type, Func<TLDecryptedMessageActionBase, int, string, string>> _actionsCache = new Dictionary<Type, Func<TLDecryptedMessageActionBase, int, string, string>>
        {
            { 
                typeof(TLDecryptedMessageActionAcceptKey), (action, fromUserId, fromUserFullName) =>
                {
#if DEBUG
                    return "TLDecryptedMessageActionAcceptKey";
#endif
                    return string.Empty;
                } 
            },
            { 
                typeof(TLDecryptedMessageActionRequestKey), (action, fromUserId, fromUserFullName) =>
                {
#if DEBUG
                    return "TLDecryptedMessageActionRequestKey";
#endif
                    return string.Empty;
                } 
            },
            { 
                typeof(TLDecryptedMessageActionAbortKey), (action, fromUserId, fromUserFullName) =>
                {
#if DEBUG
                    return "TLDecryptedMessageActionAbortKey";
#endif
                    return string.Empty;
                } 
            },
            { 
                typeof(TLDecryptedMessageActionCommitKey), (action, fromUserId, fromUserFullName) =>
                {
#if DEBUG
                    return "TLDecryptedMessageActionCommitKey";
#endif
                    return string.Empty;
                } 
            },
            { 
                typeof(TLDecryptedMessageActionNoop), (action, fromUserId, fromUserFullName) =>
                {
#if DEBUG
                    return "TLDecryptedMessageActionNoop";
#endif
                    return string.Empty;
                } 
            },
            { 
                typeof(TLDecryptedMessageActionEmpty), (action, fromUserId, fromUserFullName) =>
                {
#if DEBUG
                    return AppResources.MessageActionEmpty;
#endif
                    return string.Empty;
                } 
            },
            {
                typeof(TLDecryptedMessageActionSetMessageTTL), (action, fromUserId, fromUserFullName) =>
                {
                    var currentUserId = IoC.Get<IStateService>().CurrentUserId;
                    var resourceActionSetMessageTTL = string.Format(AppResources.MessageActionSetMessageTTL, fromUserFullName, @"{0}");
                    if (currentUserId == fromUserId)
                    {
                        resourceActionSetMessageTTL = AppResources.MessageActionYouSetMessageTTL;
                    }

                    var seconds = ((TLDecryptedMessageActionSetMessageTTL)action).TTLSeconds.Value;

                    if (seconds == 0)
                    {
                        if (currentUserId == fromUserId)
                        {
                            return AppResources.MessageActionYouDisableMessageTTL;
                        }

                        return string.Format(AppResources.MessageActionDisableMessageTTL, fromUserFullName);
                    }

                    string secondsString;
                    if (seconds < 60)
                    {
                        secondsString = Utils.Language.Declension(seconds, 
                            AppResources.SecondNominativeSingular,
                            AppResources.SecondNominativePlural, 
                            AppResources.SecondGenitiveSingular,
                            AppResources.SecondGenitiveSingular);
                    }
                    else if (seconds < 60 * 60)
                    {
                        secondsString = Utils.Language.Declension(seconds / 60,
                            AppResources.MinuteNominativeSingular,
                            AppResources.MinuteNominativePlural,
                            AppResources.MinuteGenitiveSingular,
                            AppResources.MinuteGenitiveSingular);
                    }
                    else if (seconds < TimeSpan.FromHours(24.0).TotalSeconds)
                    {
                        secondsString = Utils.Language.Declension((int)(seconds / TimeSpan.FromHours(1.0).TotalSeconds),
                            AppResources.HourNominativeSingular,
                            AppResources.HourNominativePlural,
                            AppResources.HourGenitiveSingular,
                            AppResources.HourGenitiveSingular);
                    }
                    else if (seconds < TimeSpan.FromDays(7.0).TotalSeconds)
                    {
                        secondsString = Utils.Language.Declension((int)(seconds / TimeSpan.FromDays(1.0).TotalSeconds),
                            AppResources.DayNominativeSingular,
                            AppResources.DayNominativePlural,
                            AppResources.DayGenitiveSingular,
                            AppResources.DayGenitiveSingular);
                    }
                    else if (seconds == TimeSpan.FromDays(7.0).TotalSeconds)
                    {
                        secondsString = Utils.Language.Declension(1,
                            AppResources.WeekNominativeSingular,
                            AppResources.WeekNominativePlural,
                            AppResources.WeekGenitiveSingular,
                            AppResources.WeekGenitiveSingular);
                    }
                    else
                    {
                        secondsString = Utils.Language.Declension(seconds,
                            AppResources.SecondNominativeSingular,
                            AppResources.SecondNominativePlural,
                            AppResources.SecondGenitiveSingular,
                            AppResources.SecondGenitiveSingular);
                    }


                    return string.Format(resourceActionSetMessageTTL, secondsString.ToLowerInvariant());
                }
            },
            { typeof(TLDecryptedMessageActionScreenshotMessages), (action, fromUserId, fromUserFullName) =>
                {
                    var currentUserId = IoC.Get<IStateService>().CurrentUserId;
                    var resourceActionScreenshortMessage = string.Format(AppResources.MessageActionScreenshotMessages, fromUserFullName, @"{0}");
                    if (currentUserId == fromUserId)
                    {
                        resourceActionScreenshortMessage = AppResources.MessageActionYouScreenshotMessages;
                    }

                    return resourceActionScreenshortMessage;
                }
            },
            { 
                typeof(TLDecryptedMessageActionReadMessages), (action, fromUserId, fromUserFullName) =>
                {
#if DEBUG
                    return "TLDecryptedMessageActionReadMessages";
#endif
                    return string.Empty;
                }
            },
            { 
                typeof(TLDecryptedMessageActionDeleteMessages), (action, fromUserId, fromUserFullName) => 
                {
#if DEBUG
                    return "TLDecryptedMessageActionDeleteMessages";
#endif
                    return string.Empty;
                }
            },
            { 
                typeof(TLDecryptedMessageActionFlushHistory), (action, fromUserId, fromUserFullName) => 
                {
#if DEBUG
                    return "TLDecryptedMessageActionFlushHistory";
#endif
                    return string.Empty;
                }
            },
            { 
                typeof(TLDecryptedMessageActionNotifyLayer), (action, fromUserId, fromUserFullName) => 
                {
#if DEBUG
                    return "TLDecryptedMessageActionNotifyLayer";
#endif
                    return string.Empty;
                }
            },
        };

        public static string Convert(TLDecryptedMessageService serviceMessage)
        {
            var fromId = serviceMessage.FromId;
            var fromUser = IoC.Get<ICacheService>().GetUser(fromId);
            var fromUserFullName = fromUser != null ? fromUser.FullName : AppResources.User;

            var action = serviceMessage.Action;
            if (action != null && _actionsCache.ContainsKey(action.GetType()))
            {
                return _actionsCache[action.GetType()](action, fromId.Value, fromUserFullName);
            }

#if DEBUG
            return serviceMessage.GetType().Name;
#endif

            return AppResources.MessageActionEmpty;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var serviceMessage = value as TLDecryptedMessageService;
            if (serviceMessage != null)
            {
                return Convert(serviceMessage);
            }

            return AppResources.MessageActionEmpty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ServiceMessageToTextConverter : IValueConverter
    {
        private static readonly Dictionary<Type, Func<TLMessageActionBase, int, string, bool, string>> _actionsCache = new Dictionary<Type, Func<TLMessageActionBase, int, string, bool, string>>
        { 
            { typeof(TLMessageActionEmpty), (action, fromUserId, fromUserFullName, useActiveLinks) => AppResources.MessageActionEmpty },
            { typeof(TLMessageActionChatCreate), (action, fromUserId, fromUserFullName, useActiveLinks) => string.Format(AppResources.MessageActionChatCreate, GetUserFullNameString(fromUserFullName, fromUserId, useActiveLinks), ((TLMessageActionChatCreate) action).Title) },
            //{ typeof(TLMessageActionChannelCreate), (action, fromUserId, fromUserFullName, useActiveLinks) => string.Format(AppResources.MessageActionChannelCreate, GetFullNameString(fromUserFullName, fromUserId, useActiveLinks), ((TLMessageActionChannelCreate) action).Title) },
            { typeof(TLMessageActionChatEditPhoto), (action, fromUserId, fromUserFullName, useActiveLinks) => string.Format(AppResources.MessageActionChatEditPhoto, GetUserFullNameString(fromUserFullName, fromUserId, useActiveLinks)) },
            { typeof(TLMessageActionChatEditTitle), (action, fromUserId, fromUserFullName, useActiveLinks) => string.Format(AppResources.MessageActionChatEditTitle, GetUserFullNameString(fromUserFullName, fromUserId, useActiveLinks), ((TLMessageActionChatEditTitle) action).Title) },
            { typeof(TLMessageActionChatDeletePhoto), (action, fromUserId, fromUserFullName, useActiveLinks) => string.Format(AppResources.MessageActionChatDeletePhoto, GetUserFullNameString(fromUserFullName, fromUserId, useActiveLinks)) },
            { typeof(TLMessageActionChatAddUser), (action, fromUserId, fromUserFullName, useActiveLinks) =>
                {
                    var userId = ((TLMessageActionChatAddUser)action).UserId;
                    var user = IoC.Get<ICacheService>().GetUser(userId);
                    var userFullName = GetUserFullName(user, useActiveLinks);

                    return string.Format(AppResources.MessageActionChatAddUser, GetUserFullNameString(fromUserFullName, fromUserId, useActiveLinks), userFullName);
                }
            },
            { typeof(TLMessageActionChatDeleteUser), (action, fromUserId, fromUserFullName, useActiveLinks) =>
                {
                    var userId = ((TLMessageActionChatDeleteUser)action).UserId;
                    var user = IoC.Get<ICacheService>().GetUser(userId);
                    var userFullName = GetUserFullName(user, useActiveLinks);

                    if (userId.Value == fromUserId)
                    {
                        return string.Format(AppResources.MessageActionUserLeftGroup, GetUserFullNameString(fromUserFullName, fromUserId, useActiveLinks));
                    }

                    return string.Format(AppResources.MessageActionChatDeleteUser, GetUserFullNameString(fromUserFullName, fromUserId, useActiveLinks), userFullName);
                }
            },
            { typeof(TLMessageActionUnreadMessages), (action, fromUserId, fromUserFullName, useActiveLinks) => AppResources.UnreadMessages.ToLowerInvariant() },
            { typeof(TLMessageActionContactRegistered), (action, fromUserId, fromUserFullName, useActiveLinks) =>
                {
                    var userId = ((TLMessageActionContactRegistered)action).UserId;
                    var user = IoC.Get<ICacheService>().GetUser(userId);
                    var userFullName = user != null ? user.FirstName.ToString() : AppResources.User;

                    if (string.IsNullOrEmpty(userFullName) && user != null)
                    {
                        userFullName = user.FullName;
                    }

                    return string.Format(AppResources.ContactRegistered, userFullName);
                }
            },
            { typeof(TLMessageActionChatJoinedByLink), (action, fromUserId, fromUserFullName, useActiveLinks) =>
                {
                    var userId = ((TLMessageActionChatJoinedByLink)action).InviterId;
                    var user = IoC.Get<ICacheService>().GetUser(userId);
                    var userFullName = GetUserFullName(user, useActiveLinks);

                    return string.Format(AppResources.MessageActionChatJoinedByLink, userFullName);
                }
            },
            { typeof(TLMessageActionMessageGroup), (action, fromUserId, fromUserFullName, useActiveLinks) =>
                {
                    var count = ((TLMessageActionMessageGroup) action).Group.Count.Value;

                    return Language.Declension(
                        count,
                        AppResources.CommentNominativeSingular,
                        AppResources.CommentNominativePlural,
                        AppResources.CommentGenitiveSingular,
                        AppResources.CommentGenitivePlural).ToLower(CultureInfo.CurrentUICulture);
                }
            },
            { typeof(TLMessageActionChatMigrateTo), (action, fromUserId, fromUserFullName, useActiveLinks) =>
                {
                    var channelId = ((TLMessageActionChatMigrateTo)action).ChannelId;
                    var channel = IoC.Get<ICacheService>().GetChat(channelId) as TLChannel;
                    var channelFullName = channel != null ? channel.FullName : string.Empty;

                    return string.Format(AppResources.MessageActionChatMigrateTo, GetChannelFullNameString(channelFullName, channelId.Value, useActiveLinks));
                }
            },
            { typeof(TLMessageActionChannelMigrateFrom), (action, fromUserId, fromUserFullName, useActiveLinks) =>
                {
                    var chatId = ((TLMessageActionChannelMigrateFrom)action).ChatId;
                    var chat = IoC.Get<ICacheService>().GetChat(chatId);
                    var chatFullName = chat != null ? chat.FullName : string.Empty;

                    return string.Format(AppResources.MessageActionChannelMigrateFrom, GetChatFullNameString(chatFullName, chatId.Value, useActiveLinks));
                }
            },
            { typeof(TLMessageActionChatActivate), (action, fromUserId, fromUserFullName, useActiveLinks) =>
                {
                    return AppResources.MessageActionChatActivate;
                }
            },
            { typeof(TLMessageActionChatDeactivate), (action, fromUserId, fromUserFullName, useActiveLinks) =>
                {
                    return AppResources.MessageActionChatDeactivate;
                }
            },
        };

        private static string GetUserFullName(TLUserBase user, bool useActiveLinks)
        {
            if (user == null) return AppResources.User;

            return GetUserFullNameString(user.FullName, user.Index, useActiveLinks);
        }

        private static string GetUserFullNameString(string fullName, int userId, bool useActiveLinks)
        {
            if (!useActiveLinks)
            {
                return fullName;
            }

            return '\a' + "tlg://?action=profile&user_id=" + userId + '\b' + fullName + '\a';
        }

        private static string GetChatFullNameString(string fullName, int userId, bool useActiveLinks)
        {
            return fullName;

            if (!useActiveLinks)
            {
                return fullName;
            }

            return '\a' + "tlg://?action=profile&chat_id=" + userId + '\b' + fullName + '\a';
        }

        private static string GetChannelFullNameString(string fullName, int userId, bool useActiveLinks)
        {
            return fullName;

            if (!useActiveLinks)
            {
                return fullName;
            }

            return '\a' + "tlg://?action=profile&channel_id=" + userId + '\b' + fullName + '\a';
        }

        public static string Convert(TLMessageService serviceMessage, bool useActiveLinks = false)
        {

            var fromId = serviceMessage.FromId;
            var fromUser = IoC.Get<ICacheService>().GetUser(fromId);
            var fromUserFullName = fromUser != null ? fromUser.FullName : AppResources.User;

            //var stateService = IoC.Get<IStateService>();
            //if (fromId.Value == stateService.CurrentUserId)
            //{
            //    fromUserFullName = AppResources.You;
            //}

            var action = serviceMessage.Action;

            if (serviceMessage.ToId is TLPeerChannel)
            {
                //var channel = IoC.Get<ICacheService>().GetChat(serviceMessage.ToId.Id) as TLChannel;
                var isMegaGroup = false;// channel != null && channel.IsMegaGroup;

                var actionChannelCreate = action as TLMessageActionChannelCreate;
                if (actionChannelCreate != null)
                {
                    return isMegaGroup
                        ? string.Format(AppResources.MessageActionChatCreate, GetUserFullNameString(fromUserFullName, fromId.Value, useActiveLinks), ((TLMessageActionChannelCreate)action).Title)
                        : string.Format(AppResources.MessageActionChannelCreate, GetUserFullNameString(fromUserFullName, fromId.Value, useActiveLinks), ((TLMessageActionChannelCreate) action).Title);
                }

                var actionChatEditPhoto = action as TLMessageActionChatEditPhoto;
                if (actionChatEditPhoto != null)
                {
                    return isMegaGroup
                        ? AppResources.MessageActionChatEditPhoto 
                        : AppResources.MessageActionChannelEditPhoto;
                }

                var actionChatDeletePhoto = action as TLMessageActionChatDeletePhoto;
                if (actionChatDeletePhoto != null)
                {
                    return isMegaGroup 
                        ? AppResources.MessageActionChatDeletePhoto 
                        : AppResources.MessageActionChannelDeletePhoto;
                }

                var actionChantEditTitle = action as TLMessageActionChatEditTitle;
                if (actionChantEditTitle != null)
                {
                    return isMegaGroup
                        ? string.Format(AppResources.MessageActionChatEditTitle, actionChantEditTitle.Title)
                        : string.Format(AppResources.MessageActionChannelEditTitle, actionChantEditTitle.Title);
                }
            }
            if (action != null && _actionsCache.ContainsKey(action.GetType()))
            {
                return _actionsCache[action.GetType()](action, fromId.Value, fromUserFullName, useActiveLinks);
            }

#if DEBUG
            return action != null ? action.GetType().Name : AppResources.MessageActionEmpty;
#endif

            return AppResources.MessageActionEmpty;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var serviceMessage = value as TLMessageService;
            if (serviceMessage != null)
            {
                return Convert(serviceMessage, true);
            }

            return AppResources.MessageActionEmpty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ServiceMessageToText2Converter : IValueConverter
    {
        public static readonly Dictionary<Type, Func<TLMessageActionBase, int, string, bool, string>> _actionsCache = new Dictionary<Type, Func<TLMessageActionBase, int, string, bool, string>>
        {
            { 
                typeof(TLMessageActionChannelMigrateFrom), (action, fromUserId, fromUserFullName, useActiveLinks) =>
                {
                    return AppResources.MessageActionChannelMigrateFrom2;
                }
            },
        };

        public static string Convert(TLMessageService serviceMessage, bool useActiveLinks = false)
        {
            TLInt fromId = new TLInt(0);//serviceMessage.FromId;
            TLUserBase fromUser = null;//IoC.Get<ICacheService>().GetUser(fromId);
            string fromUserFullName = null;//fromUser != null ? fromUser.FullName : AppResources.User;

            var action = serviceMessage.Action;
            if (action != null && _actionsCache.ContainsKey(action.GetType()))
            {
                return _actionsCache[action.GetType()](action, fromId.Value, fromUserFullName, useActiveLinks);
            }

            return null;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var serviceMessage = value as TLMessageService;
            if (serviceMessage != null)
            {
                return Convert(serviceMessage, true);
            }

            return AppResources.MessageActionEmpty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ServiceMessageToText2VisibilityConverter : IValueConverter
    {

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var serviceMessage = value as TLMessageService;
            if (serviceMessage != null)
            {
                var str =  ServiceMessageToText2Converter.Convert(serviceMessage, true);

                return string.IsNullOrEmpty(str) ? Visibility.Collapsed : Visibility.Visible;
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
