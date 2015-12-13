using Telegram.Api.TL;

namespace TelegramClient.EventArgs
{
    public class UpdateChatTitleEventArgs
    {
        public TLChatBase Chat { get; set; }

        public string Title { get; set; }

        public UpdateChatTitleEventArgs(TLChatBase chat, string title)
        {
            Chat = chat;
            Title = title;
        }
    }
}
