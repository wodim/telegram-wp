using System.Collections.ObjectModel;
using Telegram.Api.TL;

namespace TelegramClient.ViewModels.Dialogs
{
    public class CommandHintsViewModel
    {
        public ObservableCollection<TLBotCommand> Hints { get; protected set; }

        private readonly TLObject _with;

        public CommandHintsViewModel(TLObject with)
        {
            _with = with;

            Hints = new ObservableCollection<TLBotCommand>();
        }
    }
}
