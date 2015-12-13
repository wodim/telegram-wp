using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Windows;
using Caliburn.Micro;
using Microsoft.Phone.Shell;
using Telegram.Api.Extensions;
using Telegram.Api.Services;
using Telegram.Api.TL;
using Telegram.Api.TL.Interfaces;
using TelegramClient.Converters;
using TelegramClient.Resources;
using TelegramClient.ViewModels.Search;
using Execute = Telegram.Api.Helpers.Execute;

namespace TelegramClient.ViewModels.Dialogs
{
    public partial class DialogsViewModel
    {

        public void Handle(TLUpdateServiceNotification serviceNotification)
        {
            if (serviceNotification.Popup.Value)
            {
                Execute.BeginOnUIThread(() => MessageBox.Show(serviceNotification.Message.ToString(), AppResources.AppName, MessageBoxButton.OK));
            }
            else
            {
                var fromId = new TLInt(Constants.TelegramNotificationsId);
                var telegramUser = CacheService.GetUser(fromId);
                if (telegramUser == null)
                {
                    return;
                }

                var message = GetServiceMessage(fromId, serviceNotification.Message, serviceNotification.Media);

                CacheService.SyncMessage(message, new TLPeerUser { Id = fromId }, m => { });
            }
        }

        public void Handle(TLUpdateNewAuthorization newAuthorization)
        {
            var user = CacheService.GetUser(new TLInt(StateService.CurrentUserId));
            if (user == null)
            {
                return;
            }

            var telegramUser = CacheService.GetUser(new TLInt(Constants.TelegramNotificationsId));
            if (telegramUser == null)
            {
                return;
            }

            var firstName = user.FirstName;
            var date = TLUtils.ToDateTime(newAuthorization.Date);

            var text = string.Format(AppResources.NewAuthorization,
                firstName,
                date.ToString("dddd"),
                date.ToString("M"),
                date.ToString("t"),
                newAuthorization.Device,
                newAuthorization.Location);

            var fromId = new TLInt(Constants.TelegramNotificationsId);

            var message = GetServiceMessage(fromId, new TLString(text), new TLMessageMediaEmpty(), newAuthorization.Date);

            CacheService.SyncMessage(message, new TLPeerUser{ Id = fromId }, m => { });
        }

        private TLMessageBase GetServiceMessage(TLInt fromId, TLString text, TLMessageMediaBase media, TLInt date = null)
        {
            var message = TLUtils.GetMessage(
                    fromId,
                    new TLPeerUser { Id = new TLInt(StateService.CurrentUserId) },
                    MessageStatus.Confirmed,
                    TLBool.False,
                    TLBool.True,
                    date?? TLUtils.DateToUniversalTimeTLInt(MTProtoService.ClientTicksDelta, DateTime.Now),
                    text,
                    media,
                    TLLong.Random(),
                    new TLInt(0)
                );
            message.Id = new TLInt(0);

            return message;
        }

        private int _offset = 0;

        public void LoadNextSlice()
        {
            if (IsWorking 
                || LazyItems.Count > 0 
                || IsLastSliceLoaded
#if WP8
                || !_isUpdated
#endif
                )
            {
                return;
            }

            IsWorking = true;
            var maxId = Items.Count == 0 ? 0 : Items.OfType<TLDialog>().Last(x => x.TopMessage != null).TopMessage.Index;
            var offset = _offset;
            var limit = Constants.DialogsSlice;
            //TLUtils.WriteLine(string.Format("{0} messages.getDialogs offset={1} limit={2}", DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture), offset, limit), LogSeverity.Error);
            MTProtoService.GetDialogsAsync(
#if LAYER_40
                new TLInt(offset), new TLInt(limit),
#else
                new TLInt(0), new TLInt(maxId), new TLInt(limit),
#endif
                result => BeginOnUIThread(() =>
                {
                    if (_offset != offset)
                    {
                        return;
                    }
                    _offset += Constants.DialogsSlice;

                    foreach (var dialog in result.Dialogs)
                    {
                        Items.Add(dialog);
                    }
                    AddChannels(_channels);

                    IsWorking = false;
                    IsLastSliceLoaded = result.Dialogs.Count < limit;
                    Status = LazyItems.Count > 0 || Items.Count > 0 ? string.Empty : Status;
                    //TLUtils.WriteLine(string.Format("messages.getDialogs offset={0} limit={1} result={2}", offset, limit, result.Dialogs.Count), LogSeverity.Error);
                }),
                error => BeginOnUIThread(() =>
                {
                    IsWorking = false;
                    Status = string.Empty;

                    //TLUtils.WriteLine(string.Format("messages.getDialogs offset={0} limit={1} error={2}", offset, limit, error), LogSeverity.Error);
                    Execute.ShowDebugMessage("messages.getDialogs error " + error);
                }));
        }

        public bool OpenDialogDetails(TLDialogBase dialog)
        {
            //Execute.ShowDebugMessage("OpenDialogDetails");

            if (dialog == null)
            {
                Execute.ShowDebugMessage("OpenDialogDetails dialog=null");
                return false;
            }
            if (dialog.With == null)
            {
                Execute.ShowDebugMessage("OpenDialogDetails dialog.With=null");
                return false;
            }          

            if (dialog.IsEncryptedChat)
            {
                var encryptedChat = CacheService.GetEncryptedChat(dialog.Peer.Id);

                var user = dialog.With as TLUserBase;
                if (user == null)
                {
                    Execute.ShowDebugMessage("OpenDialogDetails encrypted dialog.With=null");
                    return false;
                }

                var cachedUser = CacheService.GetUser(user.Id);
                StateService.Participant = cachedUser ?? user; 
                StateService.With = encryptedChat;
                StateService.Dialog = dialog;
                StateService.AnimateTitle = true;
                NavigationService.UriFor<SecretDialogDetailsViewModel>().Navigate();
            }
            else
            {
                var settings = dialog.With as INotifySettings;
                if (settings != null)
                {
                    settings.NotifySettings = settings.NotifySettings ?? dialog.NotifySettings;
                }

                StateService.With = dialog.With;
                StateService.Dialog = dialog;
                StateService.AnimateTitle = true;
                NavigationService.UriFor<DialogDetailsViewModel>().Navigate();
            }

            return true;
        }

        public void DeleteAndStop(TLDialogBase dialog)
        {
            if (dialog == null) return;

            var user = dialog.With as TLUser;
            if (user == null || !user.IsBot) return;

            var confirmation = MessageBox.Show(AppResources.DeleteChat, AppResources.Confirm, MessageBoxButton.OKCancel);
            if (confirmation != MessageBoxResult.OK) return;

            IsWorking = true;
            MTProtoService.BlockAsync(user.ToInputUser(),
                blocked =>
                {
                    user.Blocked = TLBool.True;
                    CacheService.Commit();

                    DeleteHistoryAsync(user.ToInputPeer(),
                        result => BeginOnUIThread(() =>
                        {
                            IsWorking = false;
                            CacheService.DeleteDialog(dialog); // TODO : move this line to MTProtoService

                            if (dialog.With != null)
                            {
                                dialog.With.Bitmap = null;
                            }
                            Items.Remove(dialog);
                        }),
                        error => BeginOnUIThread(() =>
                        {
                            IsWorking = false;
                            Execute.ShowDebugMessage("messages.deleteHistory error " + error);
                        }));
                },
                error => BeginOnUIThread(() =>
                {
                    IsWorking = false;
                    Execute.ShowDebugMessage("contacts.Block error " + error);
                }));
        }

        public void DeleteAndExit(TLDialogBase dialog)
        {
            if (dialog == null) return;
            if (dialog.Peer is TLPeerUser) return;

            var result = MessageBox.Show(string.Format("{0}?", AppResources.DeleteAndExit), AppResources.Confirm, MessageBoxButton.OKCancel);
            if (result != MessageBoxResult.OK) return;

            if (dialog.Peer is TLPeerBroadcast)
            {
                CacheService.DeleteDialog(dialog);
                UnpinFromStart(dialog);
                BeginOnUIThread(() => Items.Remove(dialog));

                return;
            }

            if (dialog.Peer is TLPeerEncryptedChat)
            {
                var encryptedChat = CacheService.GetEncryptedChat(dialog.Peer.Id);
                if (encryptedChat is TLEncryptedChatDiscarded)
                {
                    CacheService.DeleteDialog(dialog);
                    UnpinFromStart(dialog);
                    BeginOnUIThread(() => Items.Remove(dialog));
                }
                else
                {
                    IsWorking = true;
                    MTProtoService.DiscardEncryptionAsync(dialog.Peer.Id,
                    r =>
                    {
                        IsWorking = false;
                        CacheService.DeleteDialog(dialog);
                        UnpinFromStart(dialog);
                        BeginOnUIThread(() => Items.Remove(dialog));
                    },
                    error =>
                    {
                        IsWorking = false;
                        if (error.CodeEquals(ErrorCode.BAD_REQUEST)
                            && error.Message.Value == "ENCRYPTION_ALREADY_DECLINED")
                        {
                            CacheService.DeleteDialog(dialog);
                            UnpinFromStart(dialog);
                            BeginOnUIThread(() => Items.Remove(dialog));
                        }

                        Execute.ShowDebugMessage("messages.discardEncryption error " + error);
                    });
                }

                return;
            }

            if (dialog.Peer is TLPeerChat)
            {
                DeleteAndExitDialogCommon(
                dialog.With as TLChatBase,
                MTProtoService,
                () =>
                {
                    CacheService.DeleteDialog(dialog);
                    UnpinFromStart(dialog);

                    BeginOnUIThread(() => Items.Remove(dialog));
                },
                error =>
                {
                    Execute.ShowDebugMessage("DeleteAndExitDialogCommon error " + error);
                });
                return;
            }
        }

        public static void DeleteAndExitDialogCommon(TLChatBase chatBase, IMTProtoService mtProtoService, System.Action callback, Action<TLRPCError> faultCallback = null)
        {
            if (chatBase == null) return;

            var inputPeer = chatBase.ToInputPeer();

            if (chatBase is TLChatForbidden)
            {
                DeleteHistoryAsync(
                    mtProtoService,
                    inputPeer, new TLInt(0),
                    affectedHistory => callback.SafeInvoke(),
                    faultCallback.SafeInvoke);
            }
            else
            {
                var chat = chatBase as TLChat;
                if (chat != null)
                {
                    if (chat.Left.Value)
                    {
                        DeleteHistoryAsync(
                            mtProtoService,
                            inputPeer, new TLInt(0),
                            affectedHistory => callback.SafeInvoke(),
                            faultCallback.SafeInvoke);
                    }
                    else
                    {
                        mtProtoService.DeleteChatUserAsync(
                            chat.Id, new TLInputUserSelf(),
                            statedMessage =>
                                DeleteHistoryAsync(
                                    mtProtoService,
                                    inputPeer, new TLInt(0),
                                    affectedHistory => callback.SafeInvoke(),
                                    faultCallback.SafeInvoke),
                            faultCallback.SafeInvoke);
                    }
                }
            }
        }

        public static void DeleteDialogCommon(TLUserBase userBase, IMTProtoService mtProtoService, System.Action callback, Action<TLRPCError> faultCallback = null)
        {
            if (userBase == null) return;

            var inputPeer = userBase.ToInputPeer();

            DeleteHistoryAsync(mtProtoService, inputPeer, new TLInt(0),
                result => callback.SafeInvoke(),
                faultCallback.SafeInvoke);
        }

        public void ClearHistory(TLDialogBase dialog)
        {
            if (dialog == null) return;
            if (dialog.Peer is TLPeerUser) return;

            var confirmation = MessageBox.Show(string.Format("{0}?", AppResources.ClearHistory), AppResources.Confirm, MessageBoxButton.OKCancel);
            if (confirmation != MessageBoxResult.OK) return;


            if (dialog.Peer is TLPeerChat)
            {
                var chat = (TLChatBase) dialog.With;
                var inputPeer = chat.ToInputPeer();

                DeleteHistoryAsync(inputPeer,
                    result =>
                    {
                        CacheService.DeleteDialog(dialog); // TODO : move this line to MTProtoService
                        BeginOnUIThread(() =>
                        {
                            if (dialog.With != null)
                            {
                                dialog.With.Bitmap = null;
                            }
                            Items.Remove(dialog);
                        });
                    },
                    error =>
                    {
                        Execute.ShowDebugMessage("messages.deleteHistory error " + error);
                    });
            }
            else if (dialog.Peer is TLPeerEncryptedChat)
            {
                var chat = CacheService.GetEncryptedChat(dialog.Peer.Id) as TLEncryptedChat;
                if (chat == null) return;

                var flushHistoryAction = new TLDecryptedMessageActionFlushHistory();

                var decryptedTuple = SecretDialogDetailsViewModel.GetDecryptedServiceMessageAndObject(flushHistoryAction, chat, MTProtoService.CurrentUserId, CacheService);

                SecretDialogDetailsViewModel.SendEncryptedService(chat, decryptedTuple.Item2, MTProtoService,
                    CacheService,
                    sentEncryptedMessage =>
                    {
                        chat.SetBitmap(null);
                        CacheService.ClearDecryptedHistoryAsync(chat.Id);
                    });
            }
            else if (dialog.Peer is TLPeerBroadcast)
            {
                var broadcast = CacheService.GetBroadcast(dialog.Peer.Id);
                if (broadcast == null) return;

                CacheService.ClearBroadcastHistoryAsync(broadcast.Id);
            }
        }

        public void DeleteDialog(TLDialog dialog)
        {
            if (dialog == null) return;
            if (dialog.With is TLChat) return;

            var confirmation = MessageBox.Show(AppResources.DeleteChat, AppResources.Confirm, MessageBoxButton.OKCancel);
            if (confirmation != MessageBoxResult.OK) return;

            var user = (IInputPeer)dialog.With;
            var inputPeer = user.ToInputPeer();

            DeleteHistoryAsync(inputPeer,
                result =>
                {
                    CacheService.DeleteDialog(dialog); // TODO : move this line to MTProtoService
                    BeginOnUIThread(() =>
                    {
                        if (dialog.With != null)
                        {
                            dialog.With.Bitmap = null;
                        }
                        Items.Remove(dialog);
                    });
                },
                error =>
                {
                    Execute.ShowDebugMessage("messages.deleteHistory error " + error);
                });
        }

        private void DeleteHistoryAsync(TLInputPeerBase peer, Action<TLAffectedHistory> callback, Action<TLRPCError> faultCallback = null)
        {
            DeleteHistoryAsync(MTProtoService, peer, new TLInt(0), callback, faultCallback);
        }

        private static void DeleteHistoryAsync(IMTProtoService mtProtoService, TLInputPeerBase peer, TLInt offset, Action<TLAffectedHistory> callback, Action<TLRPCError> faultCallback = null)
        {
            mtProtoService.DeleteHistoryAsync(peer, offset,
                affectedHistory =>
                {
                    if (affectedHistory.Offset.Value > 0)
                    {
                        DeleteHistoryAsync(mtProtoService, peer, affectedHistory.Offset, callback, faultCallback);
                    }
                    else
                    {
                        callback.SafeInvoke(affectedHistory);
                    }
                },
                faultCallback.SafeInvoke);
        }

        public void CreateDialog()
        {
            NavigationService.UriFor<ChooseParticipantsViewModel>().Navigate();
        }

        public void Search()
        {
            StateService.LoadedDialogs = new List<TLDialogBase>(Items);
            NavigationService.UriFor<SearchViewModel>().Navigate();
            //NavigationService.UriFor<SearchShellViewModel>().Navigate();
        }

        public void Handle(TLUpdateNotifySettings notifySettings)
        {
            var notifyPeer = notifySettings.Peer as TLNotifyPeer;
            if (notifyPeer != null)
            {
                Execute.BeginOnUIThread(() =>
                {
                    for (var i = 0; i < Items.Count; i++)
                    {
                        var dialog = Items[i] as TLDialog;
                        if (dialog != null
                            && dialog.Peer != null
                            && dialog.Peer.Id.Value == notifyPeer.Peer.Id.Value
                            && dialog.Peer.GetType() == notifyPeer.Peer.GetType())
                        {
                            dialog.NotifyOfPropertyChange(() => dialog.NotifySettings);
                            break;
                        }
                    }
                });
            }
        }

        #region Tiles

        private static string GetTileNavigationParam(TLDialogBase dialog)
        {
            var user = dialog.With as TLUserBase;
            var chat = dialog.With as TLChat;
            var broadcast = dialog.With as TLBroadcastChat;
            if (user != null)
            {
                if (dialog is TLEncryptedDialog)
                {
                    return "Action=SecondaryTile&encrypteduser_id=" + ((TLUserBase)dialog.With).Id + "&encryptedchat_id=" + dialog.Peer.Id;
                }

                return "Action=SecondaryTile&from_id=" + user.Id;
            }

            if (chat != null)
            {
                return "Action=SecondaryTile&chat_id=" + chat.Id;
            }

            if (broadcast != null)
            {
                return "Action=SecondaryTile&broadcast_id=" + broadcast.Id;
            }

            return null;
        }

        private static TLFileLocation GetTileImageLocation(TLDialogBase dialog)
        {
            var user = dialog.With as TLUserBase;
            var chat = dialog.With as TLChat;

            if (user != null)
            {
                var userProfilePhoto = user.Photo as TLUserProfilePhoto;
                if (userProfilePhoto != null)
                {
                    return userProfilePhoto.PhotoSmall as TLFileLocation;
                }
            }
            else if (chat != null)
            {
                var chatPhoto = chat.Photo as TLChatPhoto;
                if (chatPhoto != null)
                {
                    return chatPhoto.PhotoSmall as TLFileLocation;
                }
            }

            return null;
        }

        private static Uri GetTileImageUri(TLFileLocation location)
        {
            if (location == null) return null;

            var photoPath = String.Format("{0}_{1}_{2}.jpg", location.VolumeId, location.LocalId, location.Secret);

            var store = IsolatedStorageFile.GetUserStoreForApplication();
            if (!string.IsNullOrEmpty(photoPath)
                && store.FileExists(photoPath))
            {
                const string imageFolder = @"\Shared\ShellContent";
                if (!store.DirectoryExists(imageFolder))
                {
                    store.CreateDirectory(imageFolder);
                }
                if (!store.FileExists(Path.Combine(imageFolder, photoPath)))
                {
                    store.CopyFile(photoPath, Path.Combine(imageFolder, photoPath));
                }

                return new Uri(@"isostore:" + Path.Combine(imageFolder, photoPath), UriKind.Absolute);
            }

            return null;
        }

        public void PinToStart(TLDialogBase dialog)
        {
            PinToStartCommon(dialog);
        }

        public static void PinToStartCommon(TLDialogBase dialog)
        {
            if (dialog == null) return;
            if (dialog.With == null) return;

            var title = DialogCaptionConverter.Convert(dialog.With);
            var standartTileData = new StandardTileData { BackContent = AppResources.AppName, Title = title, BackTitle = title };

            var tileNavigationParam = GetTileNavigationParam(dialog);
            var imageLocation = GetTileImageLocation(dialog);

            var imageUri = GetTileImageUri(imageLocation);
            if (imageUri != null)
            {
                standartTileData.BackgroundImage = imageUri;
            }

            try
            {
                ShellTile.Create(new Uri("/Views/ShellView.xaml?" + tileNavigationParam, UriKind.Relative), standartTileData);
            }
            catch (Exception ex)
            {
                Execute.ShowDebugMessage("Pin tile error " + ex);
            }
        }

        public static void UnpinFromStart(TLDialogBase dialog)
        {
            if (dialog == null) return;
            if (dialog.With == null) return;

            var tileNavigationParam = GetTileNavigationParam(dialog);

            var tile = ShellTile.ActiveTiles.FirstOrDefault(x => x.NavigationUri.ToString().Contains(tileNavigationParam));
            if (tile != null)
            {
                tile.Delete();
            }
        }

        #endregion

        public void OpenChatDetails(TLChatBase chat)
        {
            if (chat == null) return;

            StateService.With = chat;
            StateService.AnimateTitle = true;
            NavigationService.UriFor<DialogDetailsViewModel>().Navigate();
        }
    }
}
