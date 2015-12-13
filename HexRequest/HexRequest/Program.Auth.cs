using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Telegram.Api.Helpers;
using Telegram.Api.TL;

namespace Telegram.Api
{
    public partial class Program
    {
        public static TLResponse SendSignInRequest(int seqNo, TLString phone, TLString phoneCodeHash, TLString phoneCode)
        {
            return RequestHelper.Send("auth.signIn", seqNo, () => ComposeSignInRequest(phone, phoneCodeHash, phoneCode));
        }

        private static TLResponse SendSignUpRequest(int seqNo, string phone, TLString phoneCodeHash, TLString phoneCode, TLString firstName, TLString lastName)
        {
            return RequestHelper.Send("auth.signUp", seqNo, () => ComposeSignUpRequest(phone, phoneCodeHash, phoneCode, firstName, lastName));
        }

        public static TLResponse SendSendCallRequest(int seqNo, TLString phone, TLString phoneHashCode)
        {
            return RequestHelper.Send("auth.sendCall", seqNo, () => ComposeSendCallRequest(phone, phoneHashCode));
        }

        public static TLResponse SendGetConfigRequest(int seqNo)
        {
            //#c4f9186b
            return RequestHelper.Send("help.getConfig", seqNo, () => new byte[] { 0x6b, 0x18, 0xf9, 0xc4 });
        }

        public static TLResponse SendGetNearestDCRequest(int seqNo)
        {
            return RequestHelper.Send("help.getNearestDc", seqNo, () => new byte[] { 0x26, 0x30, 0xb3, 0x1f });   //#1fb33026
        }

        private static TLResponse SendMessageAcknowledgmentsRequest(int seqNo, ICollection<TLLong> ids)
        {
            return RequestHelper.Send("msgs_ack", seqNo, () => ComposeMessageAcknowledgmentRequest(ids));
        }

        public static TLResponse SendSaveDeveloperInfoRequest(int seqNo)
        {
            return RequestHelper.Send("contest.saveDeveloperInfo", seqNo, ComposeSaveDeveloperInfoRequest);
        }

        private static TLResponse SendCheckPhoneRequest(int seqNo, string phone)
        {
            return RequestHelper.Send("auth.checkPhone", seqNo, () => ComposeCheckPhoneRequest(phone));
        }

        public static TLResponse SendPingRequest(int seqNo)
        {
            return RequestHelper.Send("ping", seqNo, ComposePingRequest);
        }


        public static TLResponse SendSendCodeRequest(int seqNo, TLString phone, TLSmsType type)
        {
            return RequestHelper.Send("auth.sendCode", seqNo, () => ComposeSendCodeRequest(phone, type, Constants.ApiId, new TLString(Constants.ApiHash)));
        }

        private static byte[] ComposeSignInRequest(TLString phone, TLString phoneCodeHash, TLString phoneCode)
        {
            TLUtils.WriteLine("--Compose SignInAsync request--");
            //#bcd51581
            var signature = new byte[] { 0x81, 0x15, 0xd5, 0xbc };

            TLUtils.WriteLine("Phone: " + phone.ToString());
            TLUtils.WriteLine("Phone serialized: " + BitConverter.ToString(phone.ToBytes()));

            TLUtils.WriteLine("PhoneCodeHash: " + phoneCodeHash.ToString());
            TLUtils.WriteLine("PhoneCodeHash serialized: " + BitConverter.ToString(phoneCodeHash.ToBytes()));

            TLUtils.WriteLine("PhoneCode: " + phoneCode.ToString());
            TLUtils.WriteLine("PhoneCode serialized: " + BitConverter.ToString(phoneCode.ToBytes()));

            return signature
                .Concat(phone.ToBytes())
                .Concat(phoneCodeHash.ToBytes())
                .Concat(phoneCode.ToBytes())
                .ToArray();
        }

        private static byte[] ComposeSignUpRequest(string phone, TLString phoneCodeHash, TLString phoneCode, TLString firstName, TLString lastName)
        {
            TLUtils.WriteLine("--Compose SignUp request--");
            //#1b067634x
            var signature = new byte[] { 0x34, 0x76, 0x06, 0x1b };

            var phoneNumberBytes = Encoding.UTF8.GetBytes(phone);
            var phoneNumberStr = TLString.FromBigEndianData(phoneNumberBytes.ToArray());
            TLUtils.WriteLine("Phone: " + BitConverter.ToString(phoneNumberBytes));
            TLUtils.WriteLine("Phone serialized: " + BitConverter.ToString(phoneNumberStr.ToBytes()));

            TLUtils.WriteLine("PhoneCodeHash: " + phoneCodeHash.ToString());
            TLUtils.WriteLine("PhoneCodeHash serialized: " + BitConverter.ToString(phoneCodeHash.ToBytes()));

            TLUtils.WriteLine("PhoneCode: " + phoneCode.ToString());
            TLUtils.WriteLine("PhoneCode serialized: " + BitConverter.ToString(phoneCode.ToBytes()));

            TLUtils.WriteLine("FirstName: " + firstName.ToString());
            TLUtils.WriteLine("FirstName serialized: " + BitConverter.ToString(firstName.ToBytes()));

            TLUtils.WriteLine("LastName: " + lastName.ToString());
            TLUtils.WriteLine("LastName serialized: " + BitConverter.ToString(lastName.ToBytes()));

            return signature
                .Concat(phoneNumberStr.ToBytes())
                .Concat(phoneCodeHash.ToBytes())
                .Concat(phoneCode.ToBytes())
                .Concat(firstName.ToBytes())
                .Concat(lastName.ToBytes())
                .ToArray();
        }

        private static byte[] ComposeSendCallRequest(TLString phone, TLString phoneCodeHash)
        {
            TLUtils.WriteLine("--Compose SendCallAsync request--");

            var signature = new byte[] { 0x64, 0x15, 0xc5, 0x03 };

            TLUtils.WriteLine("Phone: " + phone);
            TLUtils.WriteLine("Phone serialized: " + BitConverter.ToString(phone.ToBytes()));

            TLUtils.WriteLine("PhoneCodeHash: " + phoneCodeHash.ToString());
            TLUtils.WriteLine("PhoneCodeHash serialized: " + BitConverter.ToString(phoneCodeHash.ToBytes()));

            return signature
                .Concat(phone.ToBytes())
                .Concat(phoneCodeHash.ToBytes())
                .ToArray();
        }

        private static byte[] ComposeSaveDeveloperInfoRequest()
        {
            TLUtils.WriteLine("--Compose save developer info--");

            var saveDeveloperInfoBytes = new byte[] { 0x95, 0x6e, 0x5f, 0x9a };

            Int32 vkID = 210427;
            var vkIDBytes = BitConverter.GetBytes(vkID);
            TLUtils.WriteLine("VK ID");
            TLUtils.WriteLine(BitConverter.ToString(vkIDBytes));

            var name = "Evgeny Nadymov";
            var nameBytes = Encoding.UTF8.GetBytes(name); // little endian
            var nameStr = TLString.FromBigEndianData(nameBytes.ToArray());
            TLUtils.WriteLine("Name");
            TLUtils.WriteLine(BitConverter.ToString(nameBytes));
            TLUtils.WriteLine("Name serialized");
            TLUtils.WriteLine(BitConverter.ToString(nameStr.ToBytes()));

            var phoneNumber = "+79052636554";
            var phoneNumberBytes = Encoding.UTF8.GetBytes(phoneNumber);
            var phoneNumberStr = TLString.FromBigEndianData(phoneNumberBytes.ToArray());
            TLUtils.WriteLine("Phone");
            TLUtils.WriteLine(BitConverter.ToString(phoneNumberBytes));
            TLUtils.WriteLine("Phone serialized");
            TLUtils.WriteLine(BitConverter.ToString(phoneNumberStr.ToBytes()));


            Int32 age = 25;
            var ageBytes = BitConverter.GetBytes(age);
            TLUtils.WriteLine("Age");
            TLUtils.WriteLine(BitConverter.ToString(ageBytes));


            var city = "SPb";
            var cityBytes = Encoding.UTF8.GetBytes(city);
            var cityStr = TLString.FromBigEndianData(cityBytes.ToArray());
            TLUtils.WriteLine("City");
            TLUtils.WriteLine(BitConverter.ToString(cityBytes));
            TLUtils.WriteLine("City serialized");
            TLUtils.WriteLine(BitConverter.ToString(cityStr.ToBytes()));

            TLUtils.WriteLine("---------------------------------");

            return saveDeveloperInfoBytes
                .Concat(vkIDBytes)
                .Concat(nameStr.ToBytes())
                .Concat(phoneNumberStr.ToBytes())
                .Concat(ageBytes)
                .Concat(cityStr.ToBytes())
                .ToArray();
        }


        private static byte[] ComposePingRequest()
        {
            var random = new Random();
            TLUtils.WriteLine("--Compose ping request--");
            //#7abe77ec
            var signature = new byte[] { 0xec, 0x77, 0xbe, 0x7a };

            var pingId = new byte[8];
            random.NextBytes(pingId);
            TLUtils.WriteLine("PingId: " + BitConverter.ToString(pingId));

            return signature
                .Concat(pingId)
                .ToArray();
        }

        private static byte[] ComposeMessageAcknowledgmentRequest(ICollection<TLLong> ids)
        {
            var acknoledgmentSignature = new byte[] { 0x59, 0xb4, 0xd6, 0x62 };
            // Vector long 0xc734a64e
            var vectorSignature = new byte[] { 0x15, 0xc4, 0xb5, 0x1c };
            //var vectorSignature = new byte[] { 0x4e, 0xa6, 0x34, 0xc7 };
            var vectorLength = BitConverter.GetBytes(ids.Count);
            var bytes = new byte[] { };
            bytes = ids.Aggregate(bytes, (current, id) => current.Concat(BitConverter.GetBytes(id.Value)).ToArray());

            TLUtils.WriteLine("--Acknowledgments--");
            foreach (var id in ids)
            {
                TLUtils.WriteLine(BitConverter.ToString(BitConverter.GetBytes(id.Value)));
            }

            return acknoledgmentSignature
                .Concat(vectorSignature)
                .Concat(vectorLength)
                .Concat(bytes).ToArray();
        }



        private static byte[] ComposeSendCodeRequest(TLString phone, TLSmsType type, int apiId, TLString apiHash)
        {
            TLUtils.WriteLine("--Compose TLSendCode request--");

            var signature = new byte[] { 0x72, 0xf3, 0x6f, 0xd1 };

            TLUtils.WriteLine("Phone: " + phone.ToString());
            TLUtils.WriteLine("Phone serialized: " + BitConverter.ToString(phone.ToBytes()));

            TLUtils.WriteLine("ApiHash: " + apiHash.ToString());
            TLUtils.WriteLine("ApiHash serialized: " + BitConverter.ToString(apiHash.ToBytes()));

            return signature
                .Concat(phone.ToBytes())
                .Concat(BitConverter.GetBytes((int)type))
                .Concat(BitConverter.GetBytes(apiId))
                .Concat(apiHash.ToBytes())
                .ToArray();
        }

        private static byte[] ComposeCheckPhoneRequest(string phoneNumber)
        {
            TLUtils.WriteLine("--Compose CheckPhone request--");

            // revert documentation order
            var signature = new byte[] { 0xfb, 0x1d, 0xe5, 0x6f };
            var phoneNumberBytes = Encoding.UTF8.GetBytes(phoneNumber);
            var phoneNumberStr = TLString.FromBigEndianData(phoneNumberBytes.ToArray());
            TLUtils.WriteLine("Phone");
            TLUtils.WriteLine(BitConverter.ToString(phoneNumberBytes));
            TLUtils.WriteLine("Phone serialized");
            TLUtils.WriteLine(BitConverter.ToString(phoneNumberStr.ToBytes()));

            TLUtils.WriteLine("---------------------------------");

            return signature
                .Concat(phoneNumberStr.ToBytes())
                .ToArray();
        }
    }
}
