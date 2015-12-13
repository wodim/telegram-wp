using Telegram.Api.TL;

namespace TelegramClient.ViewModels.Dialogs
{
    public partial class SecretDialogDetailsViewModel
    {
        private void SendGeoPoint(TLGeoPoint geoPoint)
        {
            var chat = Chat as TLEncryptedChat;
            if (chat == null) return;

            var decryptedTuple = GetDecryptedMessageAndObject(TLString.Empty, new TLDecryptedMessageMediaGeoPoint { Lat = geoPoint.Lat, Long = geoPoint.Long }, chat);

            Items.Insert(0, decryptedTuple.Item1);
            RaiseScrollToBottom();
            NotifyOfPropertyChange(() => DescriptionVisibility);

            SendEncrypted(chat, decryptedTuple.Item2, MTProtoService, CacheService);
        }
    }
}
