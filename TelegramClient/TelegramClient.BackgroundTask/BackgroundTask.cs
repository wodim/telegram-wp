using Windows.ApplicationModel.Background;
using Windows.Networking.PushNotifications;

namespace TelegramClient.Tasks
{
    public class BackgroundTask : IBackgroundTask
    {
        public void Run(IBackgroundTaskInstance taskInstance)
        {
            var notification = (RawNotification) taskInstance.TriggerDetails;
            var content = notification.Content;
        }
    }
}
