using Telegram.Api.TL;
using TelegramClient.Services;

namespace TelegramClient.ViewModels.Dialogs
{
    public class MessageViewerViewModel
    {
        public IMessage Message { get; set; }

        private readonly IStateService _stateService;

        public MessageViewerViewModel(IStateService stateService)
        {
            _stateService = stateService;

            if (_stateService.MediaMessage != null)
            {
                Message = _stateService.MediaMessage;
                _stateService.MediaMessage = null;
            }

            if (_stateService.DecryptedMediaMessage != null)
            {
                Message = _stateService.DecryptedMediaMessage;
                _stateService.DecryptedMediaMessage = null;
            }
        }
    }
}
