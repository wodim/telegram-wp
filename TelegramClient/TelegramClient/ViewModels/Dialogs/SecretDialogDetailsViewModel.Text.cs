using System;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using Caliburn.Micro;
using Telegram.Api.Helpers;
using Telegram.Api.Services.FileManager;
#if WP81
using Windows.Web.Http.Filters;
#endif
using Telegram.Api.Extensions;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using TelegramClient.Resources;
using Execute = Telegram.Api.Helpers.Execute;

namespace TelegramClient.ViewModels.Dialogs
{
    public partial class SecretDialogDetailsViewModel
    {
        public event EventHandler ScrollToBottom;

        protected virtual void RaiseScrollToBottom()
        {
            BeginOnUIThread(() =>
            {
                var handler = ScrollToBottom;
                if (handler != null) handler(this, System.EventArgs.Empty);
            });
        }

        public bool CanSend
        {
            get
            {
                var text = DialogDetailsViewModel.GetTrimmedText(Text);

                return !string.IsNullOrEmpty(text);
            }
        }

        public void Send()
        {
            var chat = Chat as TLEncryptedChat;
            if (chat == null) return;

            if (!CanSend) return;

            var text = DialogDetailsViewModel.GetTrimmedText(Text);

            if (ProcessSpecialCommands(text)) return;

            //check maximum message length
            if (text.Length > Constants.MaximumMessageLength)
            {
                MessageBox.Show(
                    String.Format(AppResources.MaximumMessageLengthExceeded, Constants.MaximumMessageLength),
                    AppResources.Error, MessageBoxButton.OK);

                return;
            }

            var decryptedTuple = GetDecryptedMessageAndObject(new TLString(text), new TLDecryptedMessageMediaEmpty(), chat);      

            Items.Insert(0, decryptedTuple.Item1);
            RaiseScrollToBottom();
            NotifyOfPropertyChange(() => DescriptionVisibility);
            Text = string.Empty;
            
            SendEncrypted(chat, decryptedTuple.Item2, MTProtoService, CacheService);
        }

        private bool ProcessSpecialCommands(string text)
        {
            //if (string.Equals(text, "/tlg_msgs_err", StringComparison.OrdinalIgnoreCase))
            //{
            //    ShowLastSyncErrors(info =>
            //    {
            //        try
            //        {
            //            Clipboard.SetText(info);
            //        }
            //        catch (Exception ex)
            //        {

            //        }
            //    });
            //    Text = string.Empty;
            //    return true;
            //}

            if (text != null
                && text.StartsWith("/tlg_msgs", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var parameters = text.Split(' ');
                    var limit = 15;
                    if (parameters.Length > 1)
                    {
                        limit = Convert.ToInt32(parameters[1]);
                    }

                    ShowMessagesInfo(limit, info =>
                    {
                        try
                        {
                            Clipboard.SetText(info);
                        }
                        catch (Exception ex)
                        {

                        }
                    });
                    Text = string.Empty;
                }
                catch (Exception ex)
                {
                    Execute.BeginOnUIThread(() => MessageBox.Show("Unknown command"));
                }
                return true;
            }

            //if (string.Equals(text, "/tlg_cfg", StringComparison.OrdinalIgnoreCase))
            //{
            //    ShowConfigInfo(info =>
            //    {
            //        Execute.BeginOnUIThread(() =>
            //        {
            //            try
            //            {

            //                MessageBox.Show(info);
            //                Clipboard.SetText(info);
            //            }
            //            catch (Exception ex)
            //            {
            //            }

            //        });
            //    });
            //    Text = string.Empty;
            //    return true;
            //}

            //if (string.Equals(text, "/tlg_tr", StringComparison.OrdinalIgnoreCase))
            //{
            //    ShowTransportInfo(info =>
            //    {
            //        Execute.BeginOnUIThread(() =>
            //        {
            //            try
            //            {

            //                MessageBox.Show(info);
            //                Clipboard.SetText(info);
            //            }
            //            catch (Exception ex)
            //            {
            //            }
            //        });
            //    });

            //    Text = string.Empty;
            //    return true;
            //}

            //if (text != null
            //    && text.StartsWith("/tlg_up_tr", StringComparison.OrdinalIgnoreCase))
            //{
            //    try
            //    {
            //        var parameters = text.Split(' ');
            //        var dcId = Convert.ToInt32(parameters[1]);
            //        var dcIpAddress = parameters[2];
            //        var dcPort = Convert.ToInt32(parameters[3]);

            //        MTProtoService.UpdateTransportInfoAsync(dcId, dcIpAddress, dcPort,
            //            result =>
            //            {
            //                Execute.BeginOnUIThread(() => MessageBox.Show("Complete /tlg_up_tr"));
            //            });

            //        Text = string.Empty;

            //        //ShowTransportInfo(info =>
            //        //{
            //        //    Execute.BeginOnUIThread(() =>
            //        //    {
            //        //        try
            //        //        {

            //        //            MessageBox.Show(info);
            //        //            Clipboard.SetText(info);
            //        //        }
            //        //        catch (Exception ex)
            //        //        {
            //        //        }

            //        //        Text = string.Empty;
            //        //    });
            //        //});
            //    }
            //    catch (Exception ex)
            //    {
            //        Execute.BeginOnUIThread(() => MessageBox.Show("Unknown command"));
            //    }
            //    return true;
            //}

            return false;
        }

        public static TLDecryptedMessageService GetDecryptedServiceMessage(TLObject obj)
        {
            TLDecryptedMessageService message = null;

            var messageLayer17 = obj as TLDecryptedMessageLayer17;
            if (messageLayer17 != null)
            {
                message = messageLayer17.Message as TLDecryptedMessageService17;
            }

            var decryptedMessage = obj as TLDecryptedMessageService;
            if (decryptedMessage != null)
            {
                message = decryptedMessage;
            }

            return message;
        }

        public static TLDecryptedMessage GetDecryptedMessage(TLObject obj)
        {
            TLDecryptedMessage message = null;
            
            var messageLayer17 = obj as TLDecryptedMessageLayer17;
            if (messageLayer17 != null)
            {
                message = messageLayer17.Message as TLDecryptedMessage17;
            }

            var decryptedMessage = obj as TLDecryptedMessage;
            if (decryptedMessage != null)
            {
                message = decryptedMessage;
            }

            return message;
        }

        public static Telegram.Api.WindowsPhone.Tuple<TLDecryptedMessageService, TLObject> GetDecryptedServiceMessageAndObject(TLDecryptedMessageActionBase action, TLEncryptedChat chat, TLInt currentUserId, ICacheService cacheService)
        {
            var mtProtoService = IoC.Get<IMTProtoService>();

            TLDecryptedMessageService decryptedMessage;
            TLObject decryptedObject;

            var random = new Random();
            var randomId = TLLong.Random();

            var randomBytes = new byte[16];
            random.NextBytes(randomBytes);

            var encryptedChat17 = chat as TLEncryptedChat17;
            if (encryptedChat17 != null)
            {
                var cachedEncryptedChat17 = (TLEncryptedChat17)cacheService.GetEncryptedChat(encryptedChat17.Id);

                var inSeqNo = TLUtils.GetInSeqNo(currentUserId, cachedEncryptedChat17);
                var outSeqNo = TLUtils.GetOutSeqNo(currentUserId, cachedEncryptedChat17);

                cachedEncryptedChat17.RawOutSeqNo = new TLInt(cachedEncryptedChat17.RawOutSeqNo.Value + 1);
                var decryptedMessage17 = new TLDecryptedMessageService17
                {
                    Action = action,
                    RandomId = randomId,
                    RandomBytes = TLString.FromBigEndianData(randomBytes),

                    ChatId = chat.Id,
                    FromId = currentUserId,
                    Out = new TLBool(true),
                    Unread = new TLBool(true),
                    Date = TLUtils.DateToUniversalTimeTLInt(mtProtoService.ClientTicksDelta, DateTime.Now),
                    Status = MessageStatus.Sending,

                    TTL = chat.MessageTTL ?? new TLInt(0),
                    InSeqNo = inSeqNo,
                    OutSeqNo = outSeqNo
                };

                var decryptedMessageLayer17 = new TLDecryptedMessageLayer17();
                decryptedMessageLayer17.Layer = encryptedChat17.Layer;
                decryptedMessageLayer17.InSeqNo = inSeqNo;
                decryptedMessageLayer17.OutSeqNo = outSeqNo;
                decryptedMessageLayer17.RandomBytes = TLString.Empty;
                decryptedMessageLayer17.Message = decryptedMessage17;

                decryptedMessage = decryptedMessage17;
                decryptedObject = decryptedMessageLayer17;
            }
            else
            {
                var decryptedMessage8 = new TLDecryptedMessageService
                {
                    Action = action,
                    RandomId = randomId,
                    RandomBytes = TLString.FromBigEndianData(randomBytes),

                    ChatId = chat.Id,
                    FromId = currentUserId,
                    Out = new TLBool(true),
                    Unread = new TLBool(true),
                    Date = TLUtils.DateToUniversalTimeTLInt(mtProtoService.ClientTicksDelta, DateTime.Now),
                    Status = MessageStatus.Sending,

                    TTL = chat.MessageTTL
                };

                decryptedMessage = decryptedMessage8;
                decryptedObject = decryptedMessage;
            }

            return new Telegram.Api.WindowsPhone.Tuple<TLDecryptedMessageService, TLObject>(decryptedMessage, decryptedObject);
        }

        public Telegram.Api.WindowsPhone.Tuple<TLDecryptedMessageBase, TLObject> GetDecryptedMessageAndObject(TLString text, TLDecryptedMessageMediaBase media, TLEncryptedChat chat, bool delaySeq = false)
        {
            TLDecryptedMessageBase decryptedMessage;
            TLObject decryptedObject;

            var random = new Random();
            var randomId = TLLong.Random();

            var textString = text.ToString();
            var randomBytes = new byte[] { };
            if (string.IsNullOrEmpty(textString)
                || textString.Length < 16)
            {
                randomBytes = new byte[16];
                random.NextBytes(randomBytes);
            }

            var encryptedChat17 = chat as TLEncryptedChat17;
            if (encryptedChat17 != null)
            {
                var cachedEncryptedChat17 = (TLEncryptedChat17) CacheService.GetEncryptedChat(encryptedChat17.Id);

                TLInt inSeqNo;
                TLInt outSeqNo;
                if (!delaySeq)
                {
                    inSeqNo = TLUtils.GetInSeqNo(MTProtoService.CurrentUserId, cachedEncryptedChat17);
                    outSeqNo = TLUtils.GetOutSeqNo(MTProtoService.CurrentUserId, cachedEncryptedChat17);

                    cachedEncryptedChat17.RawOutSeqNo = new TLInt(cachedEncryptedChat17.RawOutSeqNo.Value + 1);
                }
                else
                {
                    inSeqNo = new TLInt(-1);
                    outSeqNo = new TLInt(-1);
                }

                var decryptedMessage17 = new TLDecryptedMessage17
                {
                    Media = media,
                    Message = text,
                    RandomId = randomId,
                    RandomBytes = TLString.FromBigEndianData(randomBytes),

                    ChatId = chat.Id,
                    FromId = new TLInt(StateService.CurrentUserId),
                    Out = new TLBool(true),
                    Unread = new TLBool(true),
                    Date = TLUtils.DateToUniversalTimeTLInt(MTProtoService.ClientTicksDelta, DateTime.Now),
                    Status = MessageStatus.Sending,

                    TTL = chat.MessageTTL ?? new TLInt(0),
                    InSeqNo = inSeqNo,
                    OutSeqNo = outSeqNo
                };

                var decryptedMessageLayer17 = new TLDecryptedMessageLayer17();
                decryptedMessageLayer17.Layer = encryptedChat17.Layer;
                decryptedMessageLayer17.InSeqNo = inSeqNo;
                decryptedMessageLayer17.OutSeqNo = outSeqNo;
                decryptedMessageLayer17.RandomBytes = TLString.Empty;
                decryptedMessageLayer17.Message = decryptedMessage17;

                decryptedMessage = decryptedMessage17;
                decryptedObject = decryptedMessageLayer17;
            }
            else
            {
                var decryptedMessage8 = new TLDecryptedMessage
                {
                    Media = media,
                    Message = text,
                    RandomId = randomId,
                    RandomBytes = TLString.FromBigEndianData(randomBytes),

                    ChatId = chat.Id,
                    FromId = new TLInt(StateService.CurrentUserId),
                    Out = new TLBool(true),
                    Unread = new TLBool(true),
                    Date = TLUtils.DateToUniversalTimeTLInt(MTProtoService.ClientTicksDelta, DateTime.Now),
                    Status = MessageStatus.Sending,

                    TTL = chat.MessageTTL
                };

                decryptedMessage = decryptedMessage8;
                decryptedObject = decryptedMessage;
            }

            return new Telegram.Api.WindowsPhone.Tuple<TLDecryptedMessageBase, TLObject>(decryptedMessage, decryptedObject);
        }

        public static void SendEncryptedMediaInternal(TLEncryptedChat chat, TLObject obj, IMTProtoService mtProtoService, ICacheService cacheService)
        {
            Execute.BeginOnUIThread(() =>
            {
                var message = GetDecryptedMessage(obj);
                if (message == null) return;

                var message17 = message as TLDecryptedMessage17;
                var messageLayer17 = obj as TLDecryptedMessageLayer17;
                var chat17 = chat as TLEncryptedChat17;
                if (chat17 != null
                    && messageLayer17 != null
                    && message17 != null
                    && message17.InSeqNo.Value == -1
                    && message17.OutSeqNo.Value == -1)
                {
                    var inSeqNo = TLUtils.GetInSeqNo(mtProtoService.CurrentUserId, chat17);
                    var outSeqNo = TLUtils.GetOutSeqNo(mtProtoService.CurrentUserId, chat17);

                    message17.InSeqNo = inSeqNo;
                    message17.OutSeqNo = outSeqNo;
                    message17.NotifyOfPropertyChange(() => message17.InSeqNo);
                    message17.NotifyOfPropertyChange(() => message17.OutSeqNo);

                    messageLayer17.InSeqNo = inSeqNo;
                    messageLayer17.OutSeqNo = outSeqNo;

                    chat17.RawOutSeqNo = new TLInt(chat17.RawOutSeqNo.Value + 1);
                }

                //message.Media.UploadingProgress = 0.0;
                mtProtoService.SendEncryptedFileAsync(
                   new TLInputEncryptedChat { AccessHash = chat.AccessHash, ChatId = chat.Id },
                   message.RandomId,
                   TLUtils.EncryptMessage(obj, chat),
                   message.InputFile,
                   result =>
                   {
                       message.Status = MessageStatus.Confirmed;
                       message.NotifyOfPropertyChange(() => message.Status);

                       var media = message.Media;
                       if (media != null)
                       {
                           var oldFile = media.File as TLEncryptedFile;

                           media.File = result.EncryptedFile;
                           if (oldFile != null)
                           {
                               var newFile = media.File as TLEncryptedFile;
                               if (newFile != null)
                               {
                                   newFile.FileName = oldFile.FileName;
                                   newFile.Duration = oldFile.Duration;

                                   var mediaPhoto = media as TLDecryptedMessageMediaPhoto;
                                   if (mediaPhoto != null)
                                   {
                                       var sourceFileName = String.Format("{0}_{1}_{2}.jpg",
                                            oldFile.Id,
                                            oldFile.DCId,
                                            oldFile.AccessHash);

                                       var destinationFileName = String.Format("{0}_{1}_{2}.jpg",
                                            newFile.Id,
                                            newFile.DCId,
                                            newFile.AccessHash);

                                       try
                                       {
                                           using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                                           {
                                               if (store.FileExists(sourceFileName))
                                               {
                                                   store.CopyFile(sourceFileName, destinationFileName);
                                                   store.DeleteFile(sourceFileName);
                                               }
                                           }
                                       }
                                       catch (Exception ex)
                                       {
                                           
                                       }
                                   }
                               }
                           }
                       }

                       cacheService.SyncSendingDecryptedMessage(chat.Id, result.Date, message.RandomId, m => { });
                   },
                   () =>
                   {
                       message.Status = MessageStatus.Confirmed;
                       message.NotifyOfPropertyChange(() => message.Status);
                   },
                   error =>
                   {
                       message.Status = MessageStatus.Failed;
                       message.NotifyOfPropertyChange(() => message.Status);

                       Execute.ShowDebugMessage("messages.sendEncryptedFile error " + error);
                   });
            });
        }

        public static void SendEncryptedService(TLEncryptedChat chat, TLObject obj, IMTProtoService mtProtoService, ICacheService cacheService, Action<TLSentEncryptedMessage> callbak)
        {
            var message = GetDecryptedServiceMessage(obj);
            if (message == null) return;

            cacheService.SyncDecryptedMessage(message, chat,
                cachedMessage =>
                {
                    mtProtoService.SendEncryptedServiceAsync(new TLInputEncryptedChat { AccessHash = chat.AccessHash, ChatId = chat.Id }, message.RandomId, TLUtils.EncryptMessage(obj, chat),
                        result =>
                        {
                            callbak.SafeInvoke(result);

                            message.Status = MessageStatus.Confirmed;
                            message.NotifyOfPropertyChange(() => message.Status);

                            cacheService.SyncSendingDecryptedMessage(chat.Id, result.Date, message.RandomId, m => { });
                        },
                        error =>
                        {
                            message.Status = MessageStatus.Failed;
                            message.NotifyOfPropertyChange(() => message.Status);

                            Execute.ShowDebugMessage("messages.sendServiceEncrypted error " + error);
                        });
                });
        }

        public static void SendEncrypted(TLEncryptedChat chat, TLObject obj, IMTProtoService mtProtoService, ICacheService cacheService)
        {
            var message = GetDecryptedMessage(obj);
            if (message == null) return;

            cacheService.SyncDecryptedMessage(message, chat,
                cachedMessage =>
                {
                    mtProtoService.SendEncryptedAsync(new TLInputEncryptedChat { AccessHash = chat.AccessHash, ChatId = chat.Id }, message.RandomId, TLUtils.EncryptMessage(obj, chat),
                        result => Execute.BeginOnUIThread(() =>
                        {
                            message.Status = MessageStatus.Confirmed;
                            message.NotifyOfPropertyChange(() => message.Status);

                            cacheService.SyncSendingDecryptedMessage(chat.Id, result.Date, message.RandomId, m => { });
                        }),
                        () =>
                        {
                            //message.Date = result.Date;
                            message.Status = MessageStatus.Confirmed;
                            message.NotifyOfPropertyChange(() => message.Status);
                        },
                        error =>
                        {
                            message.Status = MessageStatus.Failed;
                            message.NotifyOfPropertyChange(() => message.Status);

                            Execute.ShowDebugMessage("messages.sendEncrypted error " + error);
                        });
                });
        }

        public void ShowMessagesInfo(int limit = 15, Action<string> callback = null)
        {
            MTProtoService.GetSendingQueueInfoAsync(queueInfo =>
            {
                var info = new StringBuilder();

                info.AppendLine("Queue: ");
                info.AppendLine(queueInfo);

                var dialogMessages = Items.Take(limit);
                info.AppendLine("Dialog: ");
                var count = 0;
                foreach (var dialogMessage in dialogMessages)
                {
                    info.AppendLine("  " + count++ + " " + dialogMessage);
                }

                //dialogMessages = CacheService.GetHistory(new TLInt(StateService.CurrentUserId), TLUtils.InputPeerToPeer(Peer, StateService.CurrentUserId), limit);

                dialogMessages = CacheService.GetDecryptedHistory(Chat.Index);
                info.AppendLine();
                info.AppendLine("Database: ");
                count = 0;
                foreach (var dialogMessage in dialogMessages)
                {
                    info.AppendLine("  " + count++ + " " + dialogMessage);
                }

                var infoString = info.ToString();
                Execute.BeginOnUIThread(() => MessageBox.Show(infoString));

                callback.SafeInvoke(infoString);
            });
        }

        public void OpenCropedMessage(TLDecryptedMessage message)
        {
            StateService.DecryptedMediaMessage = message;
            NavigationService.UriFor<MessageViewerViewModel>().Navigate();
        }
    }
}
