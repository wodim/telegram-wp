using Telegram.Api.TL;

namespace TelegramClient.ViewModels.Dialogs
{
    public partial class DialogDetailsViewModel
    {
        private void SendContact(TLUserBase contact)
        {
            if (TLString.IsNullOrEmpty(contact.Phone))
            {
                var username = contact as IUserName;
                if (username != null && !TLString.IsNullOrEmpty(username.UserName))
                {
                    _text = string.Format(Constants.UsernameLinkPlaceholder, username.UserName);
                    Send();

                    return;
                }

                return;
            }

            var media = new TLMessageMediaContact
            {
                UserId = contact.Id,
                FirstName = contact.FirstName,
                LastName = contact.LastName,
                PhoneNumber = contact.Phone
            };

            var message = GetMessage(TLString.Empty, media);

            BeginOnUIThread(() =>
            {
                var previousMessage = InsertSendingMessage(message);
                IsEmptyDialog = Items.Count == 0 && LazyItems.Count == 0;

                BeginOnThreadPool(() =>
                CacheService.SyncSendingMessage(
                    message, previousMessage,
                    TLUtils.InputPeerToPeer(Peer, StateService.CurrentUserId),
                    SendContactInternal));
            });
        }

        private void SendContactInternal(TLMessageBase message)
        {
            var message25 = message as TLMessage25;
            if (message25 == null) return;

            var mediaContact = message25.Media as TLMessageMediaContact;
            if (mediaContact == null) return;

            var inputMediaContact = new TLInputMediaContact
            {
                FirstName = mediaContact.FirstName,
                LastName = mediaContact.LastName,
                PhoneNumber = mediaContact.PhoneNumber
            };

            message25.InputMedia = inputMediaContact;

            ShellViewModel.SendMediaInternal(message25, MTProtoService, StateService, CacheService);
        }
    }
}
