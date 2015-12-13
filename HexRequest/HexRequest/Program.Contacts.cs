using System;
using System.Linq;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using Telegram.Api.Transport;

namespace Telegram.Api
{
    public partial class Program
	{
        private static readonly ITransport Transport = new HttpTransport("http://95.142.192.65:80/api");

        //private static readonly ITransport Transport = new TcpTransport("95.142.192.65", 80); 

        private static readonly RequestHelper RequestHelper = new RequestHelper(Transport);

        public static TLResponse SendGetContactsRequest(int seqNo, TLString hash)
        {
            return RequestHelper.Send("contacts.getContacts", seqNo, () => ComposeGetContactsRequest(hash));
        }

	    private static byte[] ComposeGetContactsRequest(TLString hash)
	    {
            //#22c6aa08
	        var signature = new byte[] {0x08, 0xaa, 0xc6, 0x22};

            TLUtils.WriteLine("Hash: " + hash.ToString());
            TLUtils.WriteLine("Hash: " + BitConverter.ToString(hash.ToBytes()));

	        return signature
                .Concat(hash.ToBytes())
                .ToArray();

	    }
	}
}
