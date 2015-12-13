using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.Updates;
using Telegram.Api.TL;
using Telegram.Api.Transport;

namespace Telegram.Api
{
    public partial class Program
    {


        
        static void Main(string[] args)
        {
            var manualResetEvent = new ManualResetEvent(false);
            ////var transport = new HttpTransport(string.Empty);
            ////transport.SetAddress("http://95.142.192.65/api", 80, () => manualResetEvent.Set());
            //var transport = new TcpTransport();
            //transport.SetAddress("95.142.192.65", 80, () => manualResetEvent.Set());
            //manualResetEvent.WaitOne();
            

            //IAuthorizationHelper authHelper = new AuthorizationHelper(Transport);
            //authHelper.InitAsync(res =>{});
            manualResetEvent.Reset();

            var cacheService = new InMemoryCacheService(null);
            var updatesService = new UpdatesService(cacheService, null);

            MTProtoService.Salt = new TLLong(0);
            var service = new MTProtoService(updatesService, cacheService, new TransportService());
            service.Initialized += (o, e) => manualResetEvent.Set();

            manualResetEvent.WaitOne();
            var phoneNumber = new TLString("79996610000");
            service.SendCodeAsync(phoneNumber, TLSmsType.Code,
                sentCode =>
                {
                    var phoneCodeHash = sentCode.PhoneCodeHash;
                    service.SignInAsync(phoneNumber, phoneCodeHash, new TLString("11111"),
                        authorization =>
                        {
                            

                            //TLUtils.WriteLine("Auth: " + TLUtils.MessageIdString(authorization.Expires));
                            service.GetDialogsAsync(new TLInt(0), new TLInt(0), new TLInt(int.MaxValue),
                                        dialogs =>
                                        {

                                        },
                                        er2 =>
                                        {

                                        });

                            //service.GetContactsAsync(
                            //    new TLString(""),
                            //    contactsResult =>
                            //    {
                            //        var contacts = contactsResult as TLContacts;
                            //        if (contacts != null)
                            //        {
                            //            var contact = contacts.Contacts[0];

                            //            service.GetHistoryAsync(new TLInputPeerContact{UserId = contact.UserId}, new TLInt(0), new TLInt(0), new TLInt(int.MaxValue),
                            //                messagesResult =>
                            //                {
                                                
                            //                },
                            //                er2 => { });
                            //        }
                            //    },
                            //    er =>
                            //    {
                            //    });
                        });
                });

            Console.ReadKey();
        }
    }
}
