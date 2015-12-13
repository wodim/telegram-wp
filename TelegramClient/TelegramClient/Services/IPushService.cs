namespace TelegramClient.Services
{
    public interface IPushService
    {
        void RegisterDeviceAsync();
        void UnregisterDeviceAsync(System.Action callback);
    }
}
