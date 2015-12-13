using System.Runtime.Serialization;
using Telegram.Api.TL;

namespace TelegramClient.Models
{
    [DataContract]
    public class Country : TLObject
    {
        [DataMember(Name = "code")]
        public string Code { get; set; }

        [DataMember(Name = "phoneCode")]
        public string PhoneCode { get; set; }

        [DataMember(Name = "name")]
        public string Name { get; set; }

        public string GetKey()
        {
            return Name.Substring(0, 1).ToLowerInvariant();
        } 

    }
}
