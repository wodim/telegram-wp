using System.Runtime.Serialization;

namespace TelegramClient.Models
{
    [DataContract]
    public class InAppNotifications
    {
        [DataMember(Name = "V")]
        public bool InAppVibration { get; set; }

        [DataMember(Name = "S")]
        public bool InAppSound { get; set; }

        [DataMember(Name = "P")]
        public bool InAppMessagePreview { get; set; }
    }
}
