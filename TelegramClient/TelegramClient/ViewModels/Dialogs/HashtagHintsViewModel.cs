using System.Collections.ObjectModel;
using Telegram.Api.TL;

namespace TelegramClient.ViewModels.Dialogs
{
    public class HashtagHintsViewModel
    {
        public ObservableCollection<TLHashtagItem> Hints { get; protected set; }

        public HashtagHintsViewModel()
        {
            Hints = new ObservableCollection<TLHashtagItem>();
        }
    }
}
