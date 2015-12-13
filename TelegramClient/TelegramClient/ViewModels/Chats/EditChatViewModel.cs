using System;
using System.IO;
using System.Linq;
using System.Windows;
using Caliburn.Micro;
using Microsoft.Phone.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Extensions;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.Cache.EventArgs;
using Telegram.Api.Services.FileManager;
using Telegram.Api.TL;
using TelegramClient.Resources;
using TelegramClient.Services;
using TelegramClient.ViewModels.Dialogs;
using Execute = Telegram.Api.Helpers.Execute;
using TaskResult = Microsoft.Phone.Tasks.TaskResult;

namespace TelegramClient.ViewModels.Chats
{
    public class EditChatViewModel : ItemDetailsViewModelBase, Telegram.Api.Aggregator.IHandle<UploadableItem>
    {
        private string _title;

        public string Title
        {
            get { return _title; }
            set { SetField(ref _title, value, () => Title); }
        }

        private string _about;

        public string About
        {
            get { return _about; }
            set { SetField(ref _about, value, () => About); }
        }

        public bool IsChannel { get { return CurrentItem is TLChannel; } }

        public bool IsChannelAdmin
        {
            get
            {
                var channel = CurrentItem as TLChannel;
                return channel != null && channel.Creator;
            }
        }

        private readonly IUploadFileManager _uploadManager;

        //public ObservableCollection<TLUserBase> Items { get; set; } 

        public EditChatViewModel(IUploadFileManager uploadManager, ICacheService cacheService,
            ICommonErrorHandler errorHandler, IStateService stateService, INavigationService navigationService,
            IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator)
            : base(cacheService, errorHandler, stateService, navigationService, mtProtoService, eventAggregator)
        {
            EventAggregator.Subscribe(this);

            _uploadManager = uploadManager;

            CurrentItem = StateService.CurrentChat;
            StateService.CurrentChat = null;
            var chat = CurrentItem as TLChat;
            if (chat != null)
            {
                Title = chat.Title.ToString();
            }
            var broadcastChat = CurrentItem as TLBroadcastChat;
            if (broadcastChat != null)
            {
                Title = broadcastChat.Title.ToString();
            }
            var channel = CurrentItem as TLChannel;
            if (channel != null)
            {
                Title = channel.Title.ToString();
                About = channel.About != null ? channel.About.ToString() : string.Empty;
            }

            //Items = new ObservableCollection<TLUserBase>();
        }


        public void ForwardInAnimationComplete()
        {
            //Items.Clear();

            //var broadcastChat = CurrentItem as TLChannel;
            //if (broadcastChat != null)
            //{
            //    if (StateService.Participant != null)
            //    {
            //        broadcastChat.ManagementIds.Add(StateService.Participant.Id);
            //        StateService.Participant = null;
            //    }

            //    var users = new List<TLUserBase>(broadcastChat.ManagementIds.Count);
            //    var count = 0;
            //    foreach (var participantId in broadcastChat.ManagementIds)
            //    {
            //        var user = CacheService.GetUser(participantId);
            //        if (user != null)
            //        {
            //            user.DeleteActionVisibility = System.Windows.Visibility.Visible;
            //            if (count < 4)
            //            {
            //                Items.Add(user);
            //                count++;
            //            }
            //            else
            //            {
            //                users.Add(user);
            //            }
            //        }
            //    }
            //    users = users.OrderByDescending(x => x.StatusValue).ToList();

            //    UpdateUsers(users, () => { });

            //    return;
            //}
        }

        //private void UpdateUsers(List<TLUserBase> users, System.Action callback)
        //{
        //    const int firstSliceCount = 3;
        //    var secondSlice = new List<TLUserBase>();
        //    for (var i = 0; i < users.Count; i++)
        //    {
        //        if (i < firstSliceCount)
        //        {
        //            Items.Add(users[i]);
        //        }
        //        else
        //        {
        //            secondSlice.Add(users[i]);
        //        }
        //    }

        //    Execute.BeginOnUIThread(() =>
        //    {
        //        foreach (var user in secondSlice)
        //        {
        //            Items.Add(user);
        //        }
        //        callback.SafeInvoke();
        //    });
        //}

        public void Done()
        {
            if (IsWorking) return;

            var chat = CurrentItem as TLChat;
            if (chat != null)
            {
                IsWorking = true;
                MTProtoService.EditChatTitleAsync(chat.Id, new TLString(Title),
                    statedMessage =>
                    {
                        IsWorking = false;

                        var updates = statedMessage as TLUpdates;
                        if (updates != null)
                        {
                            var updateNewMessage = updates.Updates.FirstOrDefault(x => x is TLUpdateNewMessage) as TLUpdateNewMessage;
                            if (updateNewMessage != null)
                            {
                                EventAggregator.Publish(updateNewMessage.Message);
                                BeginOnUIThread(() => NavigationService.GoBack());
                            }
                        }

                    },
                    error =>
                    {
                        Execute.ShowDebugMessage("messages.editChatTitle error " + error);

                        IsWorking = false;
                        BeginOnUIThread(() => NavigationService.GoBack());
                    });
                return;
            }

            var channel = CurrentItem as TLChannel;
            if (channel != null)
            {
                EditChannelAboutAsync(channel, new TLString(About),
                    () => EditChannelTitleAsync(channel, new TLString(Title), 
                        () => NavigationService.GoBack()));

                return;
            }

            var broadcastChat = CurrentItem as TLBroadcastChat;
            if (broadcastChat != null)
            {
                broadcastChat.Title = new TLString(Title);
                CacheService.SyncBroadcast(broadcastChat, result => BeginOnUIThread(() => NavigationService.GoBack()));
                return;
            }
        }

        public void DeleteChannel()
        {
            var channel = CurrentItem as TLChannel;
            if (channel == null || !channel.Creator) return;

            var confirmation = MessageBox.Show(AppResources.DeleteChannelConfirmation, AppResources.Confirm, MessageBoxButton.OKCancel);
            if (confirmation != MessageBoxResult.OK) return;

            IsWorking = true;
            MTProtoService.DeleteChannelAsync(channel, 
                result => Execute.BeginOnUIThread(() =>
                {
                    IsWorking = false;

                    ContinueDeleteChannel(channel);
                }),
                error => Execute.BeginOnUIThread(() =>
                {
                    Execute.ShowDebugMessage("channels.deleteChannel error " + error);

                    IsWorking = false;

                    if (error.CodeEquals(ErrorCode.BAD_REQUEST)
                        && error.TypeEquals(ErrorType.CHANNEL_PRIVATE))
                    {
                        ContinueDeleteChannel(channel);
                    }
                }));
        }

        private void ContinueDeleteChannel(TLChannel channel)
        {
            TLDialogBase dialog = CacheService.GetDialog(new TLPeerChannel {Id = channel.Id});
            if (dialog == null)
            {
                dialog = DialogsViewModel.GetChannel(channel.Id);
            }
            
            if (dialog != null)
            {
                CacheService.DeleteDialog(dialog);
                DialogsViewModel.UnpinFromStart(dialog);
                EventAggregator.Publish(new DialogRemovedEventArgs(dialog));
            }

            NavigationService.RemoveBackEntry();
            NavigationService.RemoveBackEntry();
            NavigationService.GoBack();
        }

        public void EditChannelTitleAsync(TLChannel channel, TLString title, System.Action callback)
        {
            if (TLString.Equals(title, channel.Title, StringComparison.Ordinal))
            {
                callback.SafeInvoke();
                return;
            }

            IsWorking = true;
            MTProtoService.EditTitleAsync(channel, title,
                result => Execute.BeginOnUIThread(() =>
                {
                    IsWorking = false;

                    var updates = result as TLUpdates;
                    if (updates != null)
                    {
                        var updateNewMessage = updates.Updates.FirstOrDefault(x => x is TLUpdateNewChannelMessage) as TLUpdateNewChannelMessage;
                        if (updateNewMessage != null)
                        {
                            EventAggregator.Publish(updateNewMessage.Message);
                            
                        }
                    } 

                    callback.SafeInvoke();
                }),
                error => Execute.BeginOnUIThread(() =>
                {
                    Execute.ShowDebugMessage("channels.editTitle error " + error);

                    IsWorking = false;

                    if (error.CodeEquals(ErrorCode.BAD_REQUEST) 
                        && error.TypeEquals(ErrorType.CHAT_NOT_MODIFIED))
                    {

                    } 
                    callback.SafeInvoke();
                }));
        }

        public void EditChannelAboutAsync(TLChannel channel, TLString about, System.Action callback)
        {
            if (TLString.Equals(about, channel.About, StringComparison.Ordinal))
            {
                callback.SafeInvoke();
                return;
            }

            IsWorking = true;
            MTProtoService.EditAboutAsync(channel, about,
                statedMessage => Execute.BeginOnUIThread(() =>
                {
                    IsWorking = false;

                    channel.About = about;
                    CacheService.Commit();

                    callback.SafeInvoke();
                }),
                error => Execute.BeginOnUIThread(() =>
                {
                    Execute.ShowDebugMessage("channels.editAbout error " + error);

                    IsWorking = false;

                    if (error.CodeEquals(ErrorCode.BAD_REQUEST) 
                        && error.TypeEquals(ErrorType.CHAT_ABOUT_NOT_MODIFIED))
                    {

                    }
                    callback.SafeInvoke();
                }));
        }

        public void ReplacePhoto()
        {
            if (CurrentItem is TLChat || CurrentItem is TLChannel)
            {
                EditChatActions.EditPhoto(photo =>
                {
                    var fileId = TLLong.Random();
                    IsWorking = true;
                    _uploadManager.UploadFile(fileId, CurrentItem, photo);
                });
            }
        }

        public void DeletePhoto()
        {
            var channel = CurrentItem as TLChannel;
            if (channel != null)
            {
                //IsWorking = true;
                MTProtoService.EditPhotoAsync(channel, new TLInputChatPhotoEmpty(),
                    statedMessage => Execute.BeginOnUIThread(() =>
                    {
                        //IsWorking = false;
                        var updates = statedMessage as TLUpdates;
                        if (updates != null)
                        {
                            var updateNewMessage = updates.Updates.FirstOrDefault(x => x is TLUpdateNewChannelMessage) as TLUpdateNewChannelMessage;
                            if (updateNewMessage != null)
                            {
                                EventAggregator.Publish(updateNewMessage.Message);
                            }
                        }
                    }),
                    error => Execute.BeginOnUIThread(() =>
                    {
                        //IsWorking = false;
                        Execute.ShowDebugMessage("channels.editPhoto error " + error);
                    }));
            }

            var chat = CurrentItem as TLChat;
            if (chat != null)
            {
                //IsWorking = true;
                MTProtoService.EditChatPhotoAsync(chat.Id, new TLInputChatPhotoEmpty(),
                    statedMessage => Execute.BeginOnUIThread(() =>
                    {
                        //IsWorking = false;
                        var updates = statedMessage as TLUpdates;
                        if (updates != null)
                        {
                            var updateNewMessage = updates.Updates.FirstOrDefault(x => x is TLUpdateNewMessage) as TLUpdateNewMessage;
                            if (updateNewMessage != null)
                            {
                                EventAggregator.Publish(updateNewMessage.Message);
                            }
                        }
                    }),
                    error => Execute.BeginOnUIThread(() =>
                    {
                        //IsWorking = false;
                        Execute.ShowDebugMessage("messages.editChatPhoto error " + error);
                    }));
            }
        }

        public void AddManager()
        {
            var channel = CurrentItem as TLChannel;
            if (channel == null || channel.IsForbidden) return;

            StateService.IsInviteVisible = false;
            StateService.CurrentChat = channel;
            //StateService.RemovedUsers = Items;
            StateService.RequestForwardingCount = false;
            NavigationService.UriFor<AddChatParticipantViewModel>().Navigate();
        }

        public void Cancel()
        {
            NavigationService.GoBack();
        }

        public void Handle(UploadableItem item)
        {
            if (item.Owner == CurrentItem)
            {
                IsWorking = false;
            }
        }
    }

    public static class EditChatActions
    {
        public static void EditPhoto(Action<byte[]> callback)
        {
            var photoChooserTask = new PhotoChooserTask
            {
                ShowCamera = true,
                PixelHeight = 800,
                PixelWidth = 800
            };

            photoChooserTask.Completed += (o, e) =>
            {
                if (e.TaskResult == TaskResult.OK)
                {
                    byte[] bytes;
                    var sourceStream = e.ChosenPhoto;
                    using (var memoryStream = new MemoryStream())
                    {
                        sourceStream.CopyTo(memoryStream);
                        bytes = memoryStream.ToArray();
                    }
                    callback.SafeInvoke(bytes);
                }
            };

            photoChooserTask.Show();
        }
    }
}
