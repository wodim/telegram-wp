using System.Collections.ObjectModel;
using Telegram.Api.TL;

namespace TelegramClient.ViewModels.Dialogs
{
    public class UsernameHintsViewModel
    {
        public ObservableCollection<TLUserBase> Hints { get; protected set; }

        public UsernameHintsViewModel()
        {
            Hints = new ObservableCollection<TLUserBase>();
        }
    }
}
