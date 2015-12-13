using System;
using System.Collections.Generic;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using Telegram.Api.TL;
using TelegramClient.Resources;
using Execute = Telegram.Api.Helpers.Execute;

namespace TelegramClient.ViewModels.Dialogs
{
    public partial class DialogDetailsViewModel
    {
        private TLMessage31 _replyMarkupMessage;

        private TLReplyKeyboardBase _replyMarkup;

        public TLReplyKeyboardBase ReplyMarkup
        {
            get { return _replyMarkup; }
            set { SetField(ref _replyMarkup, value, () => ReplyMarkup); }
        }

        private void SetReplyMarkup(TLMessage31 message)
        {
            if (Reply != null && message != null) return;

            if (message != null 
                && message.ReplyMarkup != null)
            {
                var replyMarkup = message.ReplyMarkup as TLReplyKeyboardMarkup;
                if (replyMarkup != null
                    && replyMarkup.IsPersonal
                    && !message.IsMention)
                {
                    return;
                }

                var keyboardHide = message.ReplyMarkup as TLReplyKeyboardHide;
                if (keyboardHide != null)
                {
                    if (_replyMarkupMessage != null
                        && _replyMarkupMessage.FromId.Value != message.FromId.Value)
                    {
                        return;
                    }
                }

                var forceReply = message.ReplyMarkup as TLReplyKeyboardForceReply;
                if (forceReply != null 
                    && !forceReply.HasResponse)
                {
                    _replyMarkupMessage = null;
                    ReplyMarkup = null;
                    Reply = message;

                    return;
                }
            }


            _replyMarkupMessage = message;
            ReplyMarkup = message != null? message.ReplyMarkup : null;
        }


        private TLMessageBase _reply;

        public TLMessageBase Reply
        {
            get { return _reply; }
            set
            {
                var notifyChanges = _reply != value;
                SetField(ref _reply, value, () => Reply);
                if (notifyChanges)
                {
                    NotifyOfPropertyChange(() => ReplyInfo);
                    NotifyOfPropertyChange(() => CanSend);
                }
            }
        }

        public ReplyInfo ReplyInfo
        {
            get
            {
                if (_reply != null)
                {
                    return new ReplyInfo {Reply = _reply, ReplyToMsgId = _reply.Id};
                }

                return null;
            }
        }

        private Dictionary<string, IList<TLMessageBase>> _getHistoryCache = new Dictionary<string, IList<TLMessageBase>>(); 

        private TLMessageBase _previousScrollPosition;

        private void HighlightMessage(TLMessageBase message)
        {
            message.IsHighlighted = true;
            BeginOnUIThread(TimeSpan.FromSeconds(2.0), () =>
            {
                message.IsHighlighted = false;
            });
        }

        public void OpenReply(TLMessageBase message)
        {
            if (message == null) return;

            var reply = message.Reply as TLMessageCommon;
            if (reply == null) return;
            if (reply.Index == 0) return;

            // migrated reply
            var channel = With as TLChannel;
            if (channel != null)
            {
                var replyPeerChat = reply.ToId as TLPeerChat;
                if (replyPeerChat != null)
                {
                    for (var i = 0; i < Items.Count; i++)
                    {
                        var item = Items[i] as TLMessageCommon;
                        if (item != null)
                        {
                            var peerChat = item.ToId as TLPeerChat;
                            if (peerChat != null)
                            {
                                if (Items[i].Index == reply.Index)
                                {
                                    RaiseScrollTo(new ScrollToEventArgs(Items[i]));

                                    //waiting ScrollTo to complete
                                    BeginOnUIThread(TimeSpan.FromSeconds(0.1), () =>
                                    {
                                        HighlightMessage(Items[i]);

                                        _previousScrollPosition = message;
                                        ScrollToBottomVisibility = Visibility.Visible;
                                    });

                                    return;
                                }
                            }
                        }
                    }

                    return;
                }
            }

            for (var i = 0; i < Items.Count; i++)
            {
                if (Items[i].Index == reply.Index)
                {
                    RaiseScrollTo(new ScrollToEventArgs(Items[i]));

                    //waiting ScrollTo to complete
                    BeginOnUIThread(TimeSpan.FromSeconds(0.1), () =>
                    {
                        HighlightMessage(Items[i]);

                        _previousScrollPosition = message;
                        ScrollToBottomVisibility = Visibility.Visible;
                    });
                    return;
                }
            }

            return;

            // load separated slice with reply
            Items.Clear();
            Items.Add(message.Reply);
            ScrollToBottomVisibility = Visibility.Visible;
            _isFirstSliceLoaded = false;

            var key = string.Format("{0}", message.Reply.Index);
            IList<TLMessageBase> cachedMessage;
            if (_getHistoryCache.TryGetValue(key, out cachedMessage))
            {
                OpenReplyInternal(message.Reply, cachedMessage);
            }
            else
            {
                IsWorking = true;
                MTProtoService.GetHistoryAsync(
                    "OpenReply",
                    Peer,
                    TLUtils.InputPeerToPeer(Peer, StateService.CurrentUserId),
                    false,
                    new TLInt(-15),
                    new TLInt(message.Reply.Index),
                    new TLInt(30),
                    result =>
                    {
                        ProcessRepliesAndAudio(result.Messages);
                        _getHistoryCache[key] = result.Messages;

                        BeginOnUIThread(() =>
                        {
                            OpenReplyInternal(message.Reply, result.Messages);
                            IsWorking = false;
                        });
                    },
                    error =>
                    {
                        Execute.ShowDebugMessage("messages.getHistory error " + error);
                        IsWorking = false;
                    });
            }
        }

        private void OpenReplyInternal(TLMessageBase reply, IList<TLMessageBase> messages)
        {
            IsFirstSliceLoaded = false;

            var startPosition = 0;
            for (var i = 0; i < messages.Count; i++)
            {
                startPosition = i;
                if (messages[i].Index == reply.Index)
                {
                    break;
                }
            }

            for (var i = startPosition + 1; i < messages.Count; i++)
            {
                Items.Add(messages[i]);
            }

            HoldScrollingPosition = true;
            BeginOnUIThread(() =>
            {
                for (var i = 0; i < startPosition - 1; i++)
                {
                    Items.Insert(i, messages[i]);
                }
                HoldScrollingPosition = false;
            });
        }

        public void ReplyMessage(TLMessageBase message)
        {
            if (message == null) return;
            var messageService = message as TLMessageService;
            if (messageService != null)
            {
                var action = messageService.Action;
                if (action is TLMessageActionEmpty
                    || action is TLMessageActionUnreadMessages)
                {
                    return;
                }
            }
            if (message.Index <= 0) return;

            var message31 = message as TLMessage31;
            if (message31 != null && !message31.Out.Value)
            {
                var fromId = message31.FromId;
                var user = CacheService.GetUser(fromId) as TLUser;
                if (user != null && user.IsBot)
                {
                    SetReplyMarkup(message31);
                }
            }

            Reply = message;
        }

        public void DeleteReply()
        {
            var message31 = Reply as TLMessage31;
            if (message31 != null)
            {
                if (message31.ReplyMarkup != null)
                {
                    message31.ReplyMarkup.HasResponse = true;
                }
            }

            if (_previousReply != null)
            {
                Reply = _previousReply;
                _previousReply = null;
            }
            else
            {
                if (_replyMarkupMessage == Reply)
                {
                    SetReplyMarkup(null);
                }
                Reply = null;
            }
        }

        public void ProcessRepliesAndAudio(IList<TLMessageBase> messages)
        {
            for (var i = 0; i < messages.Count; i++)
            {
                var message = messages[i] as TLMessage;
                if (message != null)
                {
                    var mediaAudio = message.Media as TLMessageMediaAudio;
                    if (mediaAudio != null)
                    {
                        var audio = mediaAudio.Audio as TLAudio;
                        if (audio == null) return;

                        var store = IsolatedStorageFile.GetUserStoreForApplication();
                        var audioFileName = audio.GetFileName();
                        if (TLString.Equals(audio.MimeType, new TLString("audio/mpeg"), StringComparison.OrdinalIgnoreCase))
                        {
                            if (!store.FileExists(audioFileName))
                            {
                                mediaAudio.IsCanceled = false;
                                mediaAudio.DownloadingProgress = mediaAudio.LastProgress > 0.0 ? mediaAudio.LastProgress : 0.001;
                                BeginOnThreadPool(() =>
                                {
                                    DownloadAudioFileManager.DownloadFile(audio.DCId, audio.ToInputFileLocation(), message, audio.Size);
                                });
                                continue;
                            }
                        }

                        var wavFileName = Path.GetFileNameWithoutExtension(audioFileName) + ".wav";

                        if (!store.FileExists(wavFileName))
                        {
                            mediaAudio.IsCanceled = false;
                            mediaAudio.DownloadingProgress = mediaAudio.LastProgress > 0.0 ? mediaAudio.LastProgress : 0.001;
                            BeginOnThreadPool(() =>
                            {
                                DownloadAudioFileManager.DownloadFile(audio.DCId, audio.ToInputFileLocation(), message, audio.Size);
                            });
                            continue;
                        }
                    }
                }
            }

            var replyToMsgIds = new TLVector<TLInt>();
            var replyToMsgs = new List<TLMessage25>();
            for (var i = 0; i < messages.Count; i++)
            {
                var message25 = messages[i] as TLMessage25;
                if (message25 != null)
                {
                    if (message25.ReplyToMsgId != null)
                    {
                        var replyToMsgId = message25.ReplyToMsgId;
                        if (replyToMsgId != null
                            && replyToMsgId.Value != 0)
                        {
                            TLInt channelId = null;
                            var peerChannel = message25.ToId as TLPeerChannel;
                            if (peerChannel != null)
                            {
                                channelId = peerChannel.Id;
                            }

                            var reply = CacheService.GetMessage(replyToMsgId, channelId);
                            if (reply != null)
                            {
                                messages[i].Reply = reply;
                            }
                            else
                            {
                                replyToMsgIds.Add(replyToMsgId);
                                replyToMsgs.Add(message25);
                            }
                        }
                    }

                    if (message25.NotListened)
                    {
                        if (message25.Media != null)
                        {
                            message25.Media.NotListened = true;
                        }
                    }
                }
            }

            if (replyToMsgIds.Count > 0)
            {
                var channel = With as TLChannel;
                if (channel != null)
                {
                    var firstReplyToMsg = replyToMsgs.FirstOrDefault();
                    var peerChat = firstReplyToMsg != null ? firstReplyToMsg.ToId as TLPeerChat : null;
                    if (peerChat != null)
                    {
                        MTProtoService.GetMessagesAsync(
                            replyToMsgIds,
                            result =>
                            {
                                CacheService.AddChats(result.Chats, results => { });
                                CacheService.AddUsers(result.Users, results => { });

                                for (var i = 0; i < result.Messages.Count; i++)
                                {
                                    for (var j = 0; j < replyToMsgs.Count; j++)
                                    {
                                        var messageToReply = replyToMsgs[j];
                                        if (messageToReply != null
                                            && messageToReply.ReplyToMsgId.Value == result.Messages[i].Index)
                                        {
                                            replyToMsgs[j].Reply = result.Messages[i];
                                        }
                                    }
                                }

                                //Execute.BeginOnUIThread(() =>
                                //{
                                for (var i = 0; i < replyToMsgs.Count; i++)
                                {
                                    replyToMsgs[i].NotifyOfPropertyChange(() => replyToMsgs[i].ReplyInfo);
                                }
                                //});
                            },
                            error =>
                            {
                                Execute.ShowDebugMessage("messages.getMessages error " + error);
                            });
                    }
                    else
                    {
                        MTProtoService.GetMessagesAsync(
                           channel.ToInputChannel(),
                           replyToMsgIds,
                           result =>
                           {
                               CacheService.AddChats(result.Chats, results => { });
                               CacheService.AddUsers(result.Users, results => { });

                               for (var i = 0; i < result.Messages.Count; i++)
                               {
                                   for (var j = 0; j < replyToMsgs.Count; j++)
                                   {
                                       var messageToReply = replyToMsgs[j];
                                       if (messageToReply != null
                                           && messageToReply.ReplyToMsgId.Value == result.Messages[i].Index)
                                       {
                                           replyToMsgs[j].Reply = result.Messages[i];
                                       }
                                   }
                               }

                               //Execute.BeginOnUIThread(() =>
                               //{
                               for (var i = 0; i < replyToMsgs.Count; i++)
                               {
                                   replyToMsgs[i].NotifyOfPropertyChange(() => replyToMsgs[i].ReplyInfo);
                               }
                               //});
                           },
                           error =>
                           {
                               Execute.ShowDebugMessage("channels.getMessages error " + error);
                           });
                    }
                }
                else
                {
                    MTProtoService.GetMessagesAsync(
                        replyToMsgIds,
                        result =>
                        {
                            CacheService.AddChats(result.Chats, results => { });
                            CacheService.AddUsers(result.Users, results => { });

                            for (var i = 0; i < result.Messages.Count; i++)
                            {
                                for (var j = 0; j < replyToMsgs.Count; j++)
                                {
                                    var messageToReply = replyToMsgs[j];
                                    if (messageToReply != null
                                        && messageToReply.ReplyToMsgId.Value == result.Messages[i].Index)
                                    {
                                        replyToMsgs[j].Reply = result.Messages[i];
                                    }
                                }
                            }

                            //Execute.BeginOnUIThread(() =>
                            //{
                            for (var i = 0; i < replyToMsgs.Count; i++)
                            {
                                replyToMsgs[i].NotifyOfPropertyChange(() => replyToMsgs[i].ReplyInfo);
                            }
                            //});
                        },
                        error =>
                        {
                            Execute.ShowDebugMessage("messages.getMessages error " + error);
                        });
                }
            }
        }

        public CommandHintsViewModel CommandHints { get; protected set; }

        private void CreateCommandHints()
        {
            if (CommandHints == null)
            {
                CommandHints = new CommandHintsViewModel(With);
                NotifyOfPropertyChange(() => CommandHints);
            }
        }

        private void ClearCommandHints()
        {
            if (CommandHints != null)
            {
                CommandHints.Hints.Clear();
            }
        }

        private static bool IsValidCommandSymbol(char symbol)
        {
            if ((symbol >= 'a' && symbol <= 'z')
                || (symbol >= 'A' && symbol <= 'Z')
                || (symbol >= '0' && symbol <= '9')
                || symbol == '_')
            {
                return true;
            }

            return false;
        }

        private readonly Dictionary<string, IList<TLBotCommand>> _cachedCommandResults = new Dictionary<string, IList<TLBotCommand>>();

        private IList<TLBotCommand> _commands; 

        private IList<TLBotCommand> GetCommands()
        {
            if (_commands != null)
            {
                return _commands;
            }

            var user = With as TLUserBase;
            if (user != null)
            {
                if (user.BotInfo == null)
                {
                    return null;
                }

                var botInfo = user.BotInfo as TLBotInfo;
                if (botInfo != null)
                {
                    foreach (var command in botInfo.Commands)
                    {
                        command.Bot = user;
                    }

                    _commands = botInfo.Commands;
                }
            }

            var chat = With as TLChat;
            if (chat != null)
            {
                if (chat.BotInfo == null)
                {
                    return null;
                }

                var commands = new TLVector<TLBotCommand>();
                foreach (var botInfoBase in chat.BotInfo)
                {
                    var botInfo = botInfoBase as TLBotInfo;
                    if (botInfo != null)
                    {
                        user = CacheService.GetUser(botInfo.UserId);

                        foreach (var command in botInfo.Commands)
                        {
                            command.Bot = user;
                            commands.Add(command);
                        }
                    }
                }

                _commands = commands;
            }

            return _commands;
        } 

        private void GetCommandHints(string text)
        {
            var commands = GetCommands();

            if (text == null) return;
            text = text.TrimStart('/');

            if (commands == null)
            {
                GetFullInfo();
                return;
            }

            ClearCommandHints();

            IList<TLBotCommand> cachedResult;
            if (!_cachedCommandResults.TryGetValue(text, out cachedResult))
            {
                cachedResult = new List<TLBotCommand>();
                for (var i = 0; i < commands.Count; i++)
                {
                    var command = commands[i].Command.ToString();
                    if (!string.IsNullOrEmpty(command)
                        && (string.IsNullOrEmpty(text) || command.StartsWith(text, StringComparison.OrdinalIgnoreCase)))
                    {
                        cachedResult.Add(commands[i]);
                    }
                }

                _cachedCommandResults[text] = cachedResult;
            }

            if (cachedResult.Count > 0)
            {
                CreateCommandHints();

                for (var i = 0; i < cachedResult.Count; i++)
                {
                    if (i == MaxResults) break;

                    CommandHints.Hints.Add(cachedResult[i]);
                }
            }
        }

        private static bool SearchByCommands(string text, out string searchText)
        {
            var symbol = '/';

            var searchByCommands = true;
            var commandIndex = -1;
            searchText = string.Empty;
            for (var i = text.Length - 1; i >= 0; i--)
            {
                if (text[i] == symbol)
                {
                    if (i == 0
                        || text[i - 1] == ' ')
                    {
                        commandIndex = i;
                    }
                    else
                    {
                        searchByCommands = false;
                    }
                    break;
                }

                if (!IsValidCommandSymbol(text[i]))
                {
                    searchByCommands = false;
                    break;
                }
            }

            if (searchByCommands)
            {
                if (commandIndex == -1)
                {
                    return false;
                }

                searchText = text.Substring(commandIndex).TrimStart(symbol);
            }

            return searchByCommands;
        }

        public void ContinueCommandHints()
        {
            if (!string.IsNullOrEmpty(Text))
            {
                string searchText;
                var searchByCommands = SearchByCommands(Text, out searchText);

                if (searchByCommands)
                {
                    CreateCommandHints();

                    if (CommandHints.Hints.Count == MaxResults)
                    {
                        IList<TLBotCommand> cachedResult;
                        if (_cachedCommandResults.TryGetValue(searchText, out cachedResult))
                        {
                            for (var i = MaxResults; i < cachedResult.Count; i++)
                            {
                                CommandHints.Hints.Add(cachedResult[i]);
                            }
                        }
                    }
                }
            }
        }

        public UsernameHintsViewModel UsernameHints { get; protected set; }

        private void CreateUsernameHints()
        {
            if (UsernameHints == null)
            {
                UsernameHints = new UsernameHintsViewModel();
                NotifyOfPropertyChange(() => UsernameHints);
            }
        }

        private void ClearUsernameHints()
        {
            if (UsernameHints != null)
            {
                UsernameHints.Hints.Clear();
            }
        }

        private static bool IsValidUsernameSymbol(char symbol)
        {
            if ((symbol >= 'a' && symbol <= 'z')
                || (symbol >= 'A' && symbol <= 'Z')
                || (symbol >= '0' && symbol <= '9')
                || symbol == '_')
            {
                return true;
            }

            return false;
        }

        private readonly Dictionary<string, IList<TLUserBase>> _cachedUsernameResults = new Dictionary<string, IList<TLUserBase>>();

        private const int MaxResults = 5;

        private void GetUsernameHints(string text)
        {
            var chat = With as TLChat;
            if (chat == null) return;

            if (text == null) return;
            text = text.TrimStart('@');

            if (chat.Participants == null)
            {
                GetFullInfo();
                return;
            }

            var participants = chat.Participants as IChatParticipants;
            if (participants == null)
            {
                return;
            }

            ClearUsernameHints();

            IList<TLUserBase> cachedResult;
            if (!_cachedUsernameResults.TryGetValue(text, out cachedResult))
            {
                cachedResult = new List<TLUserBase>();
                for (var i = 0; i < participants.Participants.Count; i++)
                {
                    var user = CacheService.GetUser(participants.Participants[i].UserId);
                    if (user.Index != StateService.CurrentUserId)
                    {
                        var userName = user as IUserName;
                        if (userName != null)
                        {
                            var userNameValue = userName.UserName.ToString();

                            if (!string.IsNullOrEmpty(userNameValue)
                                && (string.IsNullOrEmpty(text) || userNameValue.StartsWith(text, StringComparison.OrdinalIgnoreCase)))
                            {
                                cachedResult.Add(user);
                            }
                        }
                    }
                }

                _cachedUsernameResults[text] = cachedResult;
            }

            if (cachedResult.Count > 0)
            {
                CreateUsernameHints();

                for (var i = 0; i < cachedResult.Count; i++)
                {
                    if (i == MaxResults) break;

                    UsernameHints.Hints.Add(cachedResult[i]);
                }
            }
        }

        private static bool SearchByUsernames(string text, out string searchText)
        {
            var searchByUsernames = true;
            var usernameIndex = -1;
            searchText = string.Empty;
            for (var i = text.Length - 1; i >= 0; i--)
            {
                if (text[i] == '@')
                {
                    if (i == 0
                        || text[i - 1] == ' ')
                    {
                        usernameIndex = i;
                    }
                    else
                    {
                        searchByUsernames = false;
                    }
                    break;
                }

                if (!IsValidUsernameSymbol(text[i]))
                {
                    searchByUsernames = false;
                    break;
                }
            }

            if (searchByUsernames)
            {
                if (usernameIndex == -1)
                {
                    return false;
                }

                searchText = text.Substring(usernameIndex).TrimStart('@');
            }

            return searchByUsernames;
        } 

        public void ContinueUsernameHints()
        {
            if (!string.IsNullOrEmpty(Text))
            {
                string searchText;
                var searchByUsernames = SearchByUsernames(Text, out searchText);

                if (searchByUsernames)
                {
                    CreateUsernameHints();
                            
                    if (UsernameHints.Hints.Count == MaxResults)
                    {
                        IList<TLUserBase> cachedResult;
                        if (_cachedUsernameResults.TryGetValue(searchText, out cachedResult))
                        {
                            for (var i = MaxResults; i < cachedResult.Count; i++)
                            {
                                UsernameHints.Hints.Add(cachedResult[i]);
                            }
                        }
                    }
                }
            }
        }

        public HashtagHintsViewModel HashtagHints { get; protected set; }

        public void CreateHashtagHints()
        {
            if (HashtagHints == null)
            {
                HashtagHints = new HashtagHintsViewModel();
                NotifyOfPropertyChange(() => HashtagHints);
            }
        }

        public void ClearHashtagHints()
        {
            if (HashtagHints != null)
            {
                HashtagHints.Hints.Clear();
            }
        }

        private static bool IsValidHashtagSymbol(char symbol)
        {
            if ((symbol >= 'a' && symbol <= 'z')
                || (symbol >= 'A' && symbol <= 'Z')
                || (symbol >= 'а' && symbol <= 'я')
                || (symbol >= 'А' && symbol <= 'Я')
                || (symbol >= '0' && symbol <= '9')
                || symbol == '_')
            {
                return true;
            }

            return false;
        }

        private readonly Dictionary<string, IList<TLHashtagItem>> _cachedHashtagResults = new Dictionary<string, IList<TLHashtagItem>>();

        private void GetHashtagHints(string text)
        {
            if (text == null) return;
            text = text.TrimStart('#');

            ClearHashtagHints();

            var hashtags = GetHashtagsFromFile();

            IList<TLHashtagItem> cachedResult;
            if (!_cachedHashtagResults.TryGetValue(text, out cachedResult))
            {
                cachedResult = new List<TLHashtagItem>();
                for (var i = 0; i < hashtags.Count; i++)
                {
                    var hashtagItem = hashtags[i];
                    if (hashtagItem != null)
                    {
                        var hashtag = hashtagItem.Hashtag;
                        if (hashtag != null)
                        {
                            var hashtagValue = hashtag.ToString();

                            if (!string.IsNullOrEmpty(hashtagValue)
                                && (string.IsNullOrEmpty(text) || hashtagValue.StartsWith(text, StringComparison.OrdinalIgnoreCase)))
                            {
                                cachedResult.Add(hashtagItem);
                            }
                        }
                    }
                }

                _cachedHashtagResults[text] = cachedResult;
            }

            if (cachedResult.Count > 0)
            {
                CreateHashtagHints();

                for (var i = 0; i < cachedResult.Count; i++)
                {
                    if (i == MaxResults) break;
                    HashtagHints.Hints.Add(cachedResult[i]);
                }
            }
        }

        private static TLVector<TLHashtagItem> _hashtagItems = new TLVector<TLHashtagItem>
            {
                new TLHashtagItem {Hashtag = new TLString("test")},
                new TLHashtagItem {Hashtag = new TLString("wp")},
                new TLHashtagItem {Hashtag = new TLString("telegram")},
                //new TLHashtagItem {Hashtag = new TLString("test4")},
                //new TLHashtagItem {Hashtag = new TLString("test5")}
            };

        private static Dictionary<string, string> _hashtagItemsDict = new Dictionary<string, string>(); 

        private TLVector<TLHashtagItem> GetHashtagsFromFile()
        {
            var result = _hashtagItems;

            foreach (var hashtagItem in _hashtagItems)
            {
                var hashtag = hashtagItem.Hashtag.ToString();
                _hashtagItemsDict[hashtag] = hashtag;
            }

            return result;
        }

        private void AddHashtagsToFile(IList<TLHashtagItem> items)
        {
            bool clearCache = false;
            foreach (var item in items)
            {
                var hashtag = item.Hashtag.ToString();
                if (!_hashtagItemsDict.ContainsKey(hashtag))
                {
                    _hashtagItemsDict[hashtag] = hashtag;
                    _hashtagItems.Insert(0, item);
                    clearCache = true;
                }
            }

            if (clearCache)
            {
                _cachedHashtagResults.Clear();
            }
        }

        private void CheckHashcodes(string text)
        {
            var regexp = new Regex("(^|\\s)#[\\w@\\.]+", RegexOptions.IgnoreCase);

            var hashtags = new List<TLHashtagItem>();
            foreach (var match in regexp.Matches(text))
            {
                hashtags.Add(new TLHashtagItem(match.ToString().Trim().TrimStart('#')));
            }
            regexp.Matches(text);

            AddHashtagsToFile(hashtags);
        }

        private void ClearHashtagsFile()
        {
            _hashtagItems.Clear();
        }

        private static bool SearchByHashtags(string text, out string searchText)
        {
            var searchByHashtags = true;
            var hashtagIndex = -1;
            searchText = string.Empty;
            for (var i = text.Length - 1; i >= 0; i--)
            {
                if (text[i] == '#')
                {
                    if (i == 0
                        || text[i - 1] == ' ')
                    {
                        hashtagIndex = i;
                    }
                    else
                    {
                        searchByHashtags = false;
                    }
                    break;
                }

                if (!IsValidHashtagSymbol(text[i]))
                {
                    searchByHashtags = false;
                    break;
                }
            }


            if (searchByHashtags)
            {
                if (hashtagIndex == -1)
                {
                    return false;
                }

                searchText = text.Substring(hashtagIndex).TrimStart('#');
            }

            return searchByHashtags;
        }

        public void ContinueHashtagHints()
        {
            if (!string.IsNullOrEmpty(Text))
            {
                string searchText;
                var searchByHashtags = SearchByHashtags(Text, out searchText);

                if (searchByHashtags)
                {
                    CreateHashtagHints();

                    if (HashtagHints.Hints.Count == MaxResults)
                    {
                        IList<TLHashtagItem> cachedResult;
                        if (_cachedHashtagResults.TryGetValue(searchText, out cachedResult))
                        {
                            for (var i = MaxResults; i < cachedResult.Count; i++)
                            {
                                HashtagHints.Hints.Add(cachedResult[i]);
                            }
                        }
                    }
                }
            }
        }

        public void ClearHashtags()
        {
            var result = MessageBox.Show("Clear search history?", AppResources.Confirm, MessageBoxButton.OKCancel);
            if (result == MessageBoxResult.OK)
            {
                _cachedHashtagResults.Clear();
                ClearHashtagHints();
                ClearHashtagsFile();
            }
        }
    }
}
