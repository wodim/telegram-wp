using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Shapes;
using Caliburn.Micro;
using Coding4Fun.Toolkit.Controls;
using Coding4Fun.Toolkit.Controls.Converters;
using Telegram.Api.Extensions;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Telegram.Controls.VirtualizedView;
using Telegram.EmojiPanel.Controls.Emoji;
using Telegram.EmojiPanel.Controls.Utilites;
using TelegramClient.Controls;
using TelegramClient.Converters;
using TelegramClient.Resources;
using TelegramClient.Services;
using TelegramClient.Utils;
using TelegramClient.ViewModels;
using TelegramClient.ViewModels.Dialogs;
using TelegramClient.ViewModels.Search;
using TelegramClient.Views.Dialogs;
#if WP8
using System.Windows.Navigation;
#endif
using Microsoft.Phone.Controls;
using Execute = Telegram.Api.Helpers.Execute; 

namespace TelegramClient.Views
{
    public class TelegramViewBase : PhoneApplicationPage
    {
// fast resume
#if WP8
        private bool _isFastResume;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.NavigationMode == NavigationMode.Reset)
            {
                _isFastResume = true;
            }

            if (e.NavigationMode == NavigationMode.Refresh
                && _isFastResume)
            {
                _isFastResume = false;

                if (e.Uri.OriginalString.StartsWith("/Protocol?encodedLaunchUri"))
                {
                    NavigateToTelegramUriAsync(e.Uri);
                }
                else if (e.Uri.OriginalString.StartsWith("/PeopleExtension?action=Show_Contact"))
                {
                    NavigateToContactFromPeopleHub(e.Uri);
                }
                else if (e.Uri.OriginalString.StartsWith("/Views/Additional/SettingsView.xaml?Action=DC_UPDATE"))
                {
                    UpdateDCOptions(e.Uri);
                }
            }

            if (e.NavigationMode == NavigationMode.New)
            {
                if (e.Uri.OriginalString.StartsWith("/Protocol?encodedLaunchUri"))
                {
                    NavigateToTelegramUriAsync(e.Uri);
                }
                else if (e.Uri.OriginalString.StartsWith("/PeopleExtension?action=Show_Contact"))
                {
                    NavigateToContactFromPeopleHub(e.Uri);
                }
                else if (e.Uri.OriginalString.StartsWith("/Views/Additional/SettingsView.xaml?Action=DC_UPDATE"))
                {
                    UpdateDCOptions(e.Uri);
                }
            }

            base.OnNavigatedTo(e);
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            var frame = Application.Current.RootVisual as TelegramTransitionFrame;
            if (frame != null)
            {
                if (frame.IsBlockingProgressOpen())
                {
                    e.Cancel = true;
                    return;
                }
            }

            if (_isFastResume
                && e.NavigationMode == NavigationMode.New
                && (e.Uri.OriginalString.EndsWith("ShellView.xaml")
                    || e.Uri.OriginalString.StartsWith("/Protocol?encodedLaunchUri")
                    || e.Uri.OriginalString.StartsWith("/PeopleExtension?action=Show_Contact")
                    || e.Uri.OriginalString.StartsWith("/Views/Additional/SettingsView.xaml?Action=DC_UPDATE")))
            {
                _isFastResume = false;
                e.Cancel = true;

                if (e.Uri.OriginalString.StartsWith("/Protocol?encodedLaunchUri"))
                {
                    NavigateToTelegramUriAsync(e.Uri);
                }
                else if (e.Uri.OriginalString.StartsWith("/PeopleExtension?action=Show_Contact"))
                {
                    NavigateToContactFromPeopleHub(e.Uri);
                }
                else if (e.Uri.OriginalString.StartsWith("/Views/Additional/SettingsView.xaml?Action=DC_UPDATE"))
                {
                    UpdateDCOptions(e.Uri);
                }

                return;
            }

            base.OnNavigatingFrom(e);
        }

        private void UpdateDCOptions(Uri uri)
        {
            Execute.BeginOnThreadPool(() =>
            {
                try
                {
                    var tempUri = uri.OriginalString.Replace("%3A", ":");
                    var uriParams = TelegramUriMapper.ParseQueryString(tempUri);

                    var id = Convert.ToInt32(uriParams["dc"]);
                    var addressParams = uriParams["addr"].Split(':');
                    var ipAddress = addressParams[0];
                    var port = Convert.ToInt32(addressParams[1]);

                    IoC.Get<IMTProtoService>().UpdateTransportInfoAsync(id, ipAddress, port,
                        result =>
                        {
                            Execute.BeginOnUIThread(() => MessageBox.Show("App settings have been successfully updated.", AppResources.Info, MessageBoxButton.OK));
                        });
                }
                catch (Exception ex)
                {

                }
            });
        }

        protected override void OnRemovedFromJournal(JournalEntryRemovedEventArgs e)
        {
            //Execute.ShowDebugMessage("OnRemovedFromJournal " + GetType());

            var viewModelBase = DataContext as ViewModelBase;
            if (viewModelBase != null)
            {
                Execute.BeginOnThreadPool(viewModelBase.Unsubscribe);
            }

            base.OnRemovedFromJournal(e);
        }

        private void NavigateToContactFromPeopleHub(Uri uri)
        {
            Execute.BeginOnThreadPool(() =>
            {
                var tempUri = HttpUtility.UrlDecode(uri.ToString());
                try
                {
                    var uriParams = TelegramUriMapper.ParseQueryString(tempUri);

                    var userId = Convert.ToInt32(uriParams["contact_ids"]);
                    var cachedContact = IoC.Get<ICacheService>().GetUser(new TLInt(userId));

                    if (cachedContact != null)
                    {
                        Thread.Sleep(1000); // waiting for backwardin animations
                        NavigateToUser(cachedContact, string.Empty);
                    }
                }
                catch (Exception ex)
                {
                    Execute.ShowDebugMessage(tempUri + " ex\n" + ex);
                }
            });
        }

        private void NavigateToTelegramUriAsync(Uri uri)
        {

            Execute.BeginOnThreadPool(() =>
            {
                var tempUri = HttpUtility.UrlDecode(uri.ToString());

                Dictionary<string, string> uriParams = null;
                try
                {
                    uriParams = TelegramUriMapper.ParseQueryString(tempUri);
                }
                catch (Exception ex)
                {
                    Execute.ShowDebugMessage("Parse uri exception " + tempUri + ex);
                }
                if (uriParams != null)
                {
                    if (tempUri.Contains("domain"))
                    {
                        // /Protocol?encodedLaunchUri=tg://resolve/?domain=<username>&start=<access_token>
                        // /Protocol?encodedLaunchUri=tg://resolve/?domain=<username>&startgroup=<access_token>
                        var domain = uriParams["domain"];
                        PageKind pageKind;
                        var accessToken = GetAccessToken(uriParams, out pageKind);

                        var cachedContact = IoC.Get<ICacheService>().GetUsers().OfType<IUserName>().FirstOrDefault(x => string.Equals(x.UserName.ToString(), domain, StringComparison.OrdinalIgnoreCase)) as TLUserBase;

                        if (cachedContact != null)
                        {
                            Thread.Sleep(1000); // waiting for backwardin animations
                            NavigateToUser(cachedContact, accessToken, pageKind);
                        }
                        else
                        {
                            var mtProtoService = IoC.Get<IMTProtoService>();
                            NavigateToUsername(mtProtoService, domain, accessToken, pageKind);
                        }
                    }
                    else if (tempUri.Contains("invite"))
                    {
                        // /Protocol?encodedLaunchUri=tg://join/?invite=<group_access_token>
                        var link = uriParams["invite"];

                        var mtProtoService = IoC.Get<IMTProtoService>();
                        NavigateToInviteLink(mtProtoService, link);
                    }
                    else if (tempUri.Contains("set"))
                    {
                        // /Protocol?encodedLaunchUri=tg://addstickers/?set=<set_name>
                        var link = uriParams["set"];

                        var inputStickerSet = new TLInputStickerSetShortName { ShortName = new TLString(link) };

                        var mtProtoService = IoC.Get<IMTProtoService>();
                        var stateService = IoC.Get<IStateService>();
                        NavigateToStickers(mtProtoService, stateService, inputStickerSet);
                    }
                    else if (tempUri.Contains("url"))
                    {
                        // /Protocol?encodedLaunchUri=tg://msg_url/?url=<url_address>&text=<description>
                        var url = uriParams["url"];
                        string text = string.Empty;
                        if (uriParams.ContainsKey("text"))
                        {
                            text = uriParams["text"];
                        }

                        NavigateToForwarding(url, text);
                    }
                    else
                    {
                        Execute.ShowDebugMessage(tempUri);
                    }
                }
            });
        }
#endif

        public static string GetAccessToken(Dictionary<string, string> uriParams, out PageKind pageKind)
        {
            pageKind = PageKind.Dialog;
            var accessToken = string.Empty;
            if (uriParams.ContainsKey("start"))
            {
                accessToken = uriParams["start"];
            }
            else if (uriParams.ContainsKey("startgroup"))
            {
                pageKind = PageKind.Search;
                accessToken = uriParams["startgroup"];
            }

            return accessToken;
        }

        public static void NavigateToStickers(IMTProtoService mtProtoService, IStateService stateService, TLInputStickerSetBase inputStickerSet)
        {
#if WP8
            if (mtProtoService != null)
            {
                Execute.BeginOnUIThread(() =>
                {
                    var frame = Application.Current.RootVisual as TelegramTransitionFrame;
                    if (frame != null) frame.OpenBlockingProgress();

                    stateService.GetAllStickersAsync(cachedStickers =>
                    {
                        mtProtoService.GetStickerSetAsync(inputStickerSet,
                            stickerSet => Execute.BeginOnUIThread(() =>
                            {
                                if (frame != null) frame.CloseBlockingProgress();

                                var emoticons = new Dictionary<long, string>();
                                for (var i = 0; i < stickerSet.Packs.Count; i++)
                                {
                                    var emoticon = stickerSet.Packs[i].Emoticon.ToString();
                                    foreach (var document in stickerSet.Packs[i].Documents)
                                    {
                                        emoticons[document.Value] = emoticon;
                                    }
                                }

                                stickerSet.Set.Stickers = new TLVector<TLObject>();
                                for (var i = 0; i < stickerSet.Documents.Count; i++)
                                {
                                    var document22 = stickerSet.Documents[i] as TLDocument22;
                                    if (document22 != null)
                                    {
                                        string emoticon;
                                        if (emoticons.TryGetValue(document22.Id.Value, out emoticon))
                                        {
                                            document22.Emoticon = emoticon;
                                        }

                                        stickerSet.Set.Stickers.Add(new TLStickerItem { Document = document22 });
                                    }
                                }

                                var isCancelVisible = true;
                                var allStickers29 = cachedStickers as TLAllStickers29;
                                if (allStickers29 != null)
                                {
                                    isCancelVisible = allStickers29.Sets.FirstOrDefault(x => x.Id.Value == stickerSet.Set.Id.Value) == null;
                                }

                                ShowMessagePrompt(isCancelVisible, stickerSet.Set, prompt =>
                                {
                                    if (prompt == PopUpResult.Ok && isCancelVisible)
                                    {
                                        mtProtoService.InstallStickerSetAsync(inputStickerSet,
                                            result => Execute.BeginOnUIThread(() =>
                                            {
                                                var instance = EmojiControl.GetInstance();
                                                instance.AddStickerSet(stickerSet);

                                                mtProtoService.SetMessageOnTime(2.0, AppResources.NewStickersAdded);
                                            }),
                                            error => Execute.BeginOnUIThread(() =>
                                            {
                                                if (error.CodeEquals(ErrorCode.BAD_REQUEST))
                                                {
                                                    if (error.TypeEquals(ErrorType.STICKERSET_INVALID))
                                                    {
                                                        MessageBox.Show(AppResources.StickersNotFound, AppResources.Error, MessageBoxButton.OK);
                                                    }
                                                    else
                                                    {
                                                        Execute.ShowDebugMessage("messages.importChatInvite error " + error);
                                                    }
                                                }
                                                else
                                                {
                                                    Execute.ShowDebugMessage("messages.importChatInvite error " + error);
                                                }
                                            }));
                                    }
                                });
                            }),
                            error => Execute.BeginOnUIThread(() =>
                            {
                                if (frame != null) frame.CloseBlockingProgress();
                                if (error.CodeEquals(ErrorCode.BAD_REQUEST))
                                {
                                    if (error.TypeEquals(ErrorType.STICKERSET_INVALID))
                                    {
                                        MessageBox.Show(AppResources.StickersNotFound, AppResources.Error, MessageBoxButton.OK);
                                    }
                                    else
                                    {
                                        Execute.ShowDebugMessage("messages.getStickerSet error " + error);
                                    }
                                }
                                else
                                {
                                    Execute.ShowDebugMessage("messages.getStickerSet error " + error);
                                }
                            }));
                    });
                });
            }
#endif
        }

        protected static MessagePrompt _lastMessagePrompt;

        private static void ShowMessagePrompt(bool isCancelVisible, TLStickerSetBase stickerSet, Action<PopUpResult> callback)
        {
            if (stickerSet == null) return;

            var scrollViewer = new ScrollViewer();
            scrollViewer.Height = 400.0;
            var panel = new MyVirtualizingPanel{ VerticalAlignment = VerticalAlignment.Top };
            scrollViewer.Content = panel;
            panel.InitializeWithScrollViewer(scrollViewer);
            var sprites = CreateStickerSetSprites(stickerSet);
            const int firstSliceLength = 4;

            var messagePrompt = new MessagePrompt();
            messagePrompt.Title = stickerSet.Title.ToString();
            messagePrompt.VerticalAlignment = VerticalAlignment.Center;
            messagePrompt.Message = (string)new StickerSetToCountStringConverter().Convert(stickerSet, null, null, null);
            messagePrompt.Body = new TextBlock
            {
                Height = scrollViewer.Height,
                Text = AppResources.Loading,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Style = (Style)Application.Current.Resources["PhoneTextGroupHeaderStyle"]
            };
            messagePrompt.IsCancelVisible = isCancelVisible;
            messagePrompt.IsAppBarVisible = true;
            if (!isCancelVisible)
            {
                messagePrompt.ActionPopUpButtons.Clear();
                var cancelButton = new RoundButton();
                cancelButton.Click += (sender, args) =>
                {
                    messagePrompt.OnCompleted(new PopUpEventArgs<string, PopUpResult>{PopUpResult = PopUpResult.Cancelled});
                };
                cancelButton.Content = CreateXamlCancel(cancelButton);
                messagePrompt.ActionPopUpButtons.Add(cancelButton);
            }
            messagePrompt.Opened += (o, args) =>
            {
                Execute.BeginOnUIThread(TimeSpan.FromSeconds(0.1), () =>
                {
                    messagePrompt.Body = scrollViewer;
                    panel.AddItems(sprites.Take(firstSliceLength).Cast<VListItemBase>());
                    Execute.BeginOnUIThread(() =>
                    {
                        panel.AddItems(sprites.Skip(firstSliceLength).Cast<VListItemBase>());
                    });
                });
            };
            messagePrompt.Completed += (o, e) =>
            {
                callback.SafeInvoke(e.PopUpResult);
            };
            _lastMessagePrompt = messagePrompt;
            messagePrompt.Show();
        }

        public static Path CreateXamlCancel(FrameworkElement control)
        {
            var path = XamlReader.Load("<Path \r\n\t\t\t\t\txmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"\r\n\t\t\t\t\tStretch=\"Uniform\" \r\n\t\t\t\t\tData=\"M15.047,0 L17.709,2.663 L11.5166,8.85499 L17.71,15.048 L15.049,17.709 L8.8553,11.5161 L2.662,17.709 L0,15.049 L6.19351,8.85467 L0.002036,2.66401 L2.66304,0.002015 L8.85463,6.19319 z\"\r\n\t\t\t\t\t/>") as Path;
            if (path != null)
                ApplyBinding(control, path, "ButtonHeight", HeightProperty, new NumberMultiplierConverter(), 0.25);
            return path;
        }

        public static void ApplyBinding(FrameworkElement source, FrameworkElement target, string propertyPath, DependencyProperty property, IValueConverter converter = null, object converterParameter = null)
        {
            if (source == null || target == null)
                return;
            target.SetBinding(property, new Binding()
            {
                Source = (object)source,
                Path = new PropertyPath(propertyPath, new object[0]),
                Converter = converter,
                ConverterParameter = converterParameter
            });
        }

        private static List<StickerSpriteItem> CreateStickerSetSprites(TLStickerSetBase stickerSet)
        {
            if (stickerSet == null) return null;

            const int stickersPerRow = 4;
            var sprites = new List<StickerSpriteItem>();
            var stickers = new List<TLStickerItem>();
            for (var i = 1; i <= stickerSet.Stickers.Count; i++)
            {
                stickers.Add((TLStickerItem)stickerSet.Stickers[i - 1]);

                if (i % stickersPerRow == 0 || i == stickerSet.Stickers.Count)
                {
                    var item = new StickerSpriteItem(stickersPerRow, stickers, 96.0, 438.0, true);
                    sprites.Add(item);
                    stickers.Clear();
                }
            }

            return sprites;
        }

        public static void NavigateToInviteLink(IMTProtoService mtProtoService, string link)
        {
            if (mtProtoService != null)
            {
                Execute.BeginOnUIThread(() =>
                {
                    var frame = Application.Current.RootVisual as TelegramTransitionFrame;
                    if (frame != null) frame.OpenBlockingProgress();

                    mtProtoService.CheckChatInviteAsync(new TLString(link),
                        chatInviteBase => Execute.BeginOnUIThread(() =>
                        {
                            if (frame != null) frame.CloseBlockingProgress();

                            var chatInviteAlready = chatInviteBase as TLChatInviteAlready;
                            if (chatInviteAlready != null)
                            {
                                var chat = chatInviteAlready.Chat;
                                NavigateToGroup(chat);
                                return;
                            }

                            var chatInvite = chatInviteBase as TLChatInvite;
                            if (chatInvite != null)
                            {
                                var confirmationString = AppResources.JoinGroupConfirmation;
                                var chatInvite40 = chatInvite as TLChatInvite40;
                                if (chatInvite40 != null && chatInvite40.IsChannel)
                                {
                                    confirmationString = AppResources.JoinChannelConfirmation;
                                }

                                var confirmation = MessageBox.Show(string.Format(confirmationString, chatInvite.Title), AppResources.Confirm, MessageBoxButton.OKCancel);
                                if (confirmation == MessageBoxResult.OK)
                                {
                                    mtProtoService.ImportChatInviteAsync(new TLString(link),
                                        result =>
                                        {
                                            var updates = result as TLUpdates;
                                            if (updates != null)
                                            {
                                                var chat = updates.Chats.FirstOrDefault();
                                                if (chat != null)
                                                {
                                                    var channel = chat as TLChannel;
                                                    if (channel != null)
                                                    {
                                                        mtProtoService.GetHistoryAsync("NavigateToInviteLink",
                                                            new TLInputPeerChannel { ChatId = channel.Id, AccessHash = channel.AccessHash },
                                                            new TLPeerChannel {Id = channel.Id}, false, new TLInt(0),
                                                            new TLInt(0), new TLInt(Constants.MessagesSlice),
                                                            result2 =>
                                                            {
                                                                var id = new TLVector<TLInt>();
                                                                foreach (var message in result2.Messages)
                                                                {
                                                                    id.Add(message.Id);
                                                                }
                                                                IoC.Get<ICacheService>().DeleteChannelMessages(channel.Id, id);
                                                                IoC.Get<ICacheService>().SyncMessages(result2, new TLPeerChannel{Id = channel.Id}, true, false,
                                                                    result3 =>
                                                                    {
                                                                        NavigateToGroup(chat);
                                                                    });

                                                            },
                                                            error2 =>
                                                            {
                                                                Execute.ShowDebugMessage("messages.getHistory error " + error2);
                                                            });
                                                    }
                                                    else
                                                    {
                                                        NavigateToGroup(chat);
                                                    }
                                                }
                                            }
                                        },
                                        error => Execute.BeginOnUIThread(() =>
                                        {
                                            if (error.CodeEquals(ErrorCode.BAD_REQUEST))
                                            {
                                                if (error.TypeEquals(ErrorType.INVITE_HASH_EMPTY)
                                                    || error.TypeEquals(ErrorType.INVITE_HASH_INVALID)
                                                    || error.TypeEquals(ErrorType.INVITE_HASH_EXPIRED))
                                                {
                                                    MessageBox.Show(AppResources.GroupNotExistsError, AppResources.Error, MessageBoxButton.OK);
                                                }
                                                else if (error.TypeEquals(ErrorType.USERS_TOO_MUCH))
                                                {
                                                    MessageBox.Show(AppResources.GroupFullError, AppResources.Error, MessageBoxButton.OK);
                                                }
                                                else if (error.TypeEquals(ErrorType.USER_ALREADY_PARTICIPANT))
                                                {
                                                    //Execute.BeginOnUIThread(() => MessageBox.Show(string.Format(AppResources.CantFindContactWithUsername, username), AppResources.Error, MessageBoxButton.OK));
                                                }
                                                else
                                                {
                                                    Execute.ShowDebugMessage("messages.importChatInvite error " + error);
                                                }
                                            }
                                            else
                                            {
                                                Execute.ShowDebugMessage("messages.importChatInvite error " + error);
                                            }
                                        }));
                                }
                            }
                        }),
                        error => Execute.BeginOnUIThread(() =>
                        {
                            if (frame != null) frame.CloseBlockingProgress();
                            if (error.CodeEquals(ErrorCode.BAD_REQUEST))
                            {
                                if (error.TypeEquals(ErrorType.INVITE_HASH_EMPTY)
                                    || error.TypeEquals(ErrorType.INVITE_HASH_INVALID)
                                    || error.TypeEquals(ErrorType.INVITE_HASH_EXPIRED))
                                {
                                    MessageBox.Show(AppResources.GroupNotExistsError, AppResources.Error,  MessageBoxButton.OK);
                                }
                                else
                                {
                                    Execute.ShowDebugMessage("messages.checkChatInvite error " + error);
                                }
                            }
                            else
                            {
                                Execute.ShowDebugMessage("messages.checkChatInvite error " + error);
                            }
                        }));
                });
            }
        }

        private static void NavigateToGroup(TLChatBase chat)
        {
            if (chat == null) return;

            Execute.BeginOnUIThread(() =>
            {
                var navigationService = IoC.Get<INavigationService>();
                IoC.Get<IStateService>().With = chat;
                IoC.Get<IStateService>().RemoveBackEntries = true;
                navigationService.Navigate(new Uri("/Views/Dialogs/DialogDetailsView.xaml?rndParam=" + TLInt.Random(), UriKind.Relative));
            });
        }

        public static void NavigateToUsername(IMTProtoService mtProtoService, string username, string accessToken, PageKind pageKind = PageKind.Dialog)
        {
            if (mtProtoService != null)
            {
                TelegramTransitionFrame frame = null;

                Execute.BeginOnUIThread(() =>
                {
                    frame = Application.Current.RootVisual as TelegramTransitionFrame;
                    if (frame != null) frame.OpenBlockingProgress();
                });

                mtProtoService.ResolveUsernameAsync(new TLString(username),
                    result => Execute.BeginOnUIThread(() => 
                    {
                        if (frame != null) frame.CloseBlockingProgress();

                        var peerUser = result.Peer as TLPeerUser;
                        if (peerUser != null)
                        {
                            var user = result.Users.FirstOrDefault();
                            if (user != null)
                            {
                                NavigateToUser(user, accessToken, pageKind);
                                return;
                            }
                        }

                        var peerChannel = result.Peer as TLPeerChannel;
                        var peerChat = result.Peer as TLPeerChat;
                        if (peerChannel != null || peerChat != null)
                        {
                            var chat = result.Chats.FirstOrDefault();
                            if (chat != null)
                            {
                                NavigateToChat(chat);
                                return;
                            }
                        }

                        MessageBox.Show(string.Format(AppResources.CantFindContactWithUsername, username), AppResources.Error, MessageBoxButton.OK);
                    }),
                    error => Execute.BeginOnUIThread(() =>
                    {
                        if (frame != null) frame.CloseBlockingProgress();

                        if (error.CodeEquals(ErrorCode.BAD_REQUEST)
                            && error.TypeEquals(ErrorType.USERNAME_NOT_OCCUPIED))
                        {
                            MessageBox.Show(string.Format(AppResources.CantFindContactWithUsername, username), AppResources.Error, MessageBoxButton.OK);
                        }
                        else
                        {
                            Execute.ShowDebugMessage("contacts.resolveUsername error " + error);
                        }
                    }));
            }
        }

        public static void NavigateToHashtag(string hashtag)
        {
            if (string.IsNullOrEmpty(hashtag)) return;

            Execute.BeginOnUIThread(() =>
            {
                IoC.Get<IStateService>().Hashtag = hashtag;
                //IoC.Get<IStateService>().RemoveBackEntries = true;
                //var navigationService = IoC.Get<INavigationService>();
                //navigationService.Navigate(new Uri("/Views/Dialogs/DialogDetailsView.xaml?rndParam=" + TLInt.Random(), UriKind.Relative)); // fix DialogDetailsView -> DialogDetailsView
                IoC.Get<INavigationService>().UriFor<SearchShellViewModel>().Navigate();
            });
        }

        public static void NavigateToUser(TLUserBase userBase, string accessToken, PageKind pageKind = PageKind.Dialog)
        {
            if (userBase == null) return;

            Execute.BeginOnUIThread(() =>
            {
                
                var navigationService = IoC.Get<INavigationService>();
                if (pageKind == PageKind.Profile)
                {
                    IoC.Get<IStateService>().CurrentContact = userBase;
                    //IoC.Get<IStateService>().RemoveBackEntries = true;
                    navigationService.Navigate(new Uri("/Views/Contacts/ContactView.xaml", UriKind.Relative));
                }
                else if (pageKind == PageKind.Search)
                {
                    var user = userBase as TLUser;
                    if (user != null && user.IsBotGroupsBlocked)
                    {
                        MessageBox.Show(AppResources.AddBotToGroupsError, AppResources.Error, MessageBoxButton.OK);
                        return;
                    }

                    IoC.Get<IStateService>().With = userBase;
                    IoC.Get<IStateService>().RemoveBackEntries = true;
                    IoC.Get<IStateService>().AccessToken = accessToken;
                    IoC.Get<IStateService>().Bot = userBase;
                    navigationService.Navigate(new Uri("/Views/Dialogs/ChooseDialogView.xaml?rndParam=" + TLInt.Random(), UriKind.Relative));
                }
                else
                {
                    IoC.Get<IStateService>().With = userBase;
                    IoC.Get<IStateService>().RemoveBackEntries = true;
                    IoC.Get<IStateService>().AccessToken = accessToken;
                    IoC.Get<IStateService>().Bot = userBase;
                    navigationService.Navigate(new Uri("/Views/Dialogs/DialogDetailsView.xaml?rndParam=" + TLInt.Random(), UriKind.Relative));
                }
                 // fix DialogDetailsView -> DialogDetailsView
                //IoC.Get<INavigationService>().UriFor<DialogDetailsViewModel>().Navigate();
            });
        }

        public static void NavigateToChat(TLChatBase chatBase)
        {
            if (chatBase == null) return;

            Execute.BeginOnUIThread(() =>
            {
                IoC.Get<IStateService>().With = chatBase;
                IoC.Get<IStateService>().RemoveBackEntries = true;
                IoC.Get<INavigationService>().Navigate(new Uri("/Views/Dialogs/DialogDetailsView.xaml?rndParam=" + TLInt.Random(), UriKind.Relative));
            });
        }

        private static void NavigateToForwarding(string url, string urlText)
        {
            Execute.BeginOnUIThread(() =>
            {
                IoC.Get<IStateService>().Url = url;
                IoC.Get<IStateService>().UrlText = urlText;
                IoC.Get<INavigationService>().UriFor<ChooseDialogViewModel>().Navigate();
            });
        }

        private static void NavigateToShareTarget(string weblink)
        {
            if (string.IsNullOrEmpty(weblink)) return;

            Execute.BeginOnUIThread(() =>
            {
                Execute.ShowDebugMessage(weblink);
                var navigationService = IoC.Get<INavigationService>();
                navigationService.Navigate(new Uri("/Views/Dialogs/ChooseDialogView.xaml?rndParam=" + TLInt.Random(), UriKind.Relative));
            });
        }
    }

    public enum PageKind
    {
        Dialog,
        Profile,
        Search
    }
}
