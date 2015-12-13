using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Telegram.Api.Helpers;
using Telegram.Api.TL;

namespace Telegram.Api
{
    
    class Tests
    {
        public static void DateTests()
        {
            var now = DateTime.Now;
            var nowLong = Utils.DateTimeToUnixTimestamp(now);
            var unitxTime = (now - new DateTime(1970, 1, 1).ToLocalTime()).Ticks;
            Console.WriteLine("ticks " + unitxTime + " " + BitConverter.ToString(BitConverter.GetBytes(unitxTime)));
            Console.WriteLine("Double: " + nowLong + " " + BitConverter.ToString(BitConverter.GetBytes(nowLong)));
            Console.WriteLine("Long: " + (long)nowLong + " " + BitConverter.ToString(BitConverter.GetBytes((long)nowLong)));

            var t = (long) (nowLong*4294967296);
            Console.WriteLine(t);
            Console.WriteLine(BitConverter.ToString(BitConverter.GetBytes(t)));
        }

        public static void TestBytesToHexString()
        {
            var bytesString = "7E-43-4F-4E-54-45-53-54-5F-46-49-4E-49-53-48-45-44-3A-20-53-6F-72-72-79-2C-20-63-6F-6E-74-65-73-74-20-68-61-73-20-61-6C-72-65-61-64-79-20-66-69-6E-69-73-68-65-64-2E-20-59-6F-75-20-63-61-6E-20-63-68-65-63-6B-20-72-65-73-75-6C-74-73-20-68-65-72-65-3A-20-68-74-74-70-3A-2F-2F-64-65-76-2E-73-74-65-6C-2E-63-6F-6D-2F-63-6F-6E-74-65-73-74-2F-72-6F-75-6E-64-31-2D-72-65-73-75-6C-74-73-2E-00";
            //bytesString = new string(bytesString.ToArray());
            var bytes = Utils.StringToByteArray(bytesString.Replace("-", string.Empty));
            int position = 0;
            var tlString = new TLString().FromBytes(bytes, ref position);

            var str = tlString.ToString();
        }

        private static byte[] TestComposeData()
        {
            var pqInnerData = new byte[] { 0x83, 0xc9, 0x5a, 0xec };

            var pq = new byte[] { 0x08, 0x17, 0xED, 0x48, 0x94, 0x1A, 0x08, 0xF9, 0x81, 0x00, 0x00, 0x00 };
            var pBytes = new byte[] { 0x04, 0x49, 0x4C, 0x55, 0x3B, 0x00, 0x00, 0x00 };
            var qBytes = new byte[] { 0x04, 0x53, 0x91, 0x10, 0x73, 0x00, 0x00, 0x00 };

            var nonce = new byte[] { 0x3E, 0x05, 0x49, 0x82, 0x8C, 0xCA, 0x27, 0xE9, 0x66, 0xB3, 0x01, 0xA4, 0x8F, 0xEC, 0xE2, 0xFC };
            var serverNonce = new byte[] { 0xA5, 0xCF, 0x4D, 0x33, 0xF4, 0xA1, 0x1E, 0xA8, 0x77, 0xBA, 0x4A, 0xA5, 0x73, 0x90, 0x73, 0x30 };

            var newNonce = new byte[]{0x31, 0x1C, 0x85, 0xDB, 0x23, 0x4A, 0xA2, 0x64, 0x0A, 0xFC, 0x4A, 0x76, 0xA7, 0x35, 0xCF, 0x5B, 
                                      0x1F, 0x0F, 0xD6, 0x8B, 0xD1, 0x7F, 0xA1, 0x81, 0xE1, 0x22, 0x9A, 0xD8, 0x67, 0xCC, 0x02, 0x4D};


            return pqInnerData.Reverse().ToArray()
                .Concat(pq).ToArray()
                .Concat(pBytes).ToArray()
                .Concat(qBytes).ToArray()
                .Concat(nonce).ToArray()
                .Concat(serverNonce).ToArray()
                .Concat(newNonce).ToArray();
        }

        public static void TestAuthKeySHA1()
        {
            //var data = TestComposeData();

            SHA1 sha = new SHA1CryptoServiceProvider();
            // This is one implementation of the abstract class SHA1.
            

            var authKey = "80-24-4B-1C-B3-98-B6-D3-D6-0F-6A-54-7B-16-F2-24-B5-C8-39-5A-E9-EA-06-03-68-91-57-9A-CF-78-16-97-3C-F0-3E-77-8F-25-5D-D3-F1-AC-8E-70-A7-AA-9C-27-AA-C6-07-07-0C-5B-01-A1-A1-C2-F2-E0-BA-A0-8A-CE-A8-17-44-E0-81-25-7F-4F-3B-08-2D-CD-9C-47-6F-80-36-63-51-8A-DF-AE-6A-84-50-18-E9-2E-54-E4-01-68-B8-86-BD-C7-AD-7E-13-40-BF-BB-93-E0-BC-A7-A3-71-43-CB-D6-8A-42-00-15-C5-11-29-8C-F4-DD-B2-CE-92-86-93-67-28-B7-2C-E8-DC-83-94-10-C0-AC-FF-AF-2B-23-3C-E8-5A-B3-C6-FE-85-4F-BB-AC-ED-67-11-CB-27-F1-A4-77-23-7E-EE-12-CE-28-1A-6B-00-33-31-91-3E-26-9E-77-B6-DA-27-B6-A8-C6-92-5C-51-08-30-F2-30-89-15-59-5A-5D-1A-55-F0-87-ED-7B-D3-26-B7-92-46-B8-73-4F-14-F9-01-F0-9F-41-8B-05-84-C6-6A-F7-E1-7B-68-AA-DC-AD-6F-A1-F5-9C-14-DA-CA-05-00-37-6C-24-60-66-AE-19-E3-96-14-64-74-78-C2-D3-D3-38-90";

            var data = Utils.StringToByteArray(authKey.Replace("-", string.Empty));
            var sha1 = sha.ComputeHash(data);
            var stringSHA1 = BitConverter.ToString(sha1);
            TLUtils.WriteLine(stringSHA1);
        }

        static void TestSHA1()
        {
            var data = TestComposeData();

            SHA1 sha = new SHA1CryptoServiceProvider();
            // This is one implementation of the abstract class SHA1.
            var sha1 = sha.ComputeHash(data);
 
            var stringSHA1 = BitConverter.ToString(sha1);

        }

        public static void TestCalculateCRC32()
        {
            //var str = "user id:int first_name:string last_name:string = User";
            var str2 =
                "p_q_inner_data pq:string p:string q:string nonce:int128 server_nonce:int128 new_nonce:int256 = P_Q_inner_data"; //#83c95aec
            byte[] utf16Bytes = Encoding.Unicode.GetBytes(str2);
            byte[] utf8Bytes = Encoding.Convert(Encoding.Unicode, Encoding.UTF8, utf16Bytes);
            //var crc = new CRC32();

            //var hash = crc.ComputeHash(utf8Bytes);

            var hash2 = Utils.CalculateCRC32(str2);
            var hash = Utils.CalculateCRC32(utf8Bytes);

            TLUtils.WriteLine("Test CRC: expected            " + "83c95aec");
            TLUtils.WriteLine("Test CRC: actual from string  " + BitConverter.ToString(hash2));
            TLUtils.WriteLine("Test CRC: actual from bytes   " + BitConverter.ToString(hash));

            const string message = "message msg_id:long seqno:int bytes:int body:MessageData = TLMessage";
            const string user = "user id:int first_name:string last_name:string = User";
            const string container = "msg_container messages:vector message = TLMessageContainer";
            TLUtils.WriteLine("Test CRC: user    " + BitConverter.ToString(Utils.CalculateCRC32(user)));
            TLUtils.WriteLine("Test CRC: message " + BitConverter.ToString(Utils.CalculateCRC32(message)));
            TLUtils.WriteLine("Test CRC: messageContainer " + BitConverter.ToString(Utils.CalculateCRC32(container)));
        }

        //public static void TestPQ()
        //{
        //    var data = new byte[] { 0x08, 0x17, 0xED, 0x48, 0x94, 0x1A, 0x08, 0xF9, 0x81, 0x00, 0x00, 0x00 };
        //    TLUtils.WriteLine("Initial data: " + BitConverter.ToString(data));

        //    var str = TLString.Parse(data);
        //    TLUtils.WriteLine("Hex string data: " + BitConverter.ToString(str.Data));

        //    var pq = BitConverter.ToUInt64(str.Data.ToArray(), 0);
        //    TLUtils.WriteLine("pq: " + pq);

        //    var tuple = Program.GetPQ(pq);
        //    TLUtils.WriteLine("p: " + tuple.Item1);
        //    var pStr = TLString.FromUInt64(tuple.Item1);
        //    TLUtils.WriteLine("p bytes: " + BitConverter.ToString(pStr.ToBytes(8)));

        //    var qStr = TLString.FromUInt64(tuple.Item2);
        //    TLUtils.WriteLine("q: " + tuple.Item2);
        //    TLUtils.WriteLine("q bytes: " + BitConverter.ToString(qStr.ToBytes(8)));
        //}

        //public static void TestRSA()
        //{
        //    const string exptectedString = "1f f5 b5 6b e3 98 03 aa ea db b7 e5 fe 7e a3 59" +
        //                                   "19 d9 2f 6e e2 2f a0 5e 7e f8 e7 ba bf a4 69 61" +
        //                                   "e6 ce a7 df 9c 01 fb a8 87 5c 26 ec 3a 56 c4 52" +
        //                                   "fd 01 24 15 34 f1 a8 56 0b b9 90 e5 9c 44 ea 1d" +
        //                                   "46 7f dc 56 07 4b 6f 1e 54 cd ad 6a a1 82 ba 97" +
        //                                   "c9 e2 eb 1e 96 99 98 2c 35 f3 56 f7 b4 61 4b fb" +
        //                                   "be 39 98 ff dd ae 0d 6d 4a ef d8 f4 21 3b f5 8f" +
        //                                   "b2 d7 ad 22 d2 30 c3 d2 87 7c 6c c9 58 e5 9d 81" +
        //                                   "da e2 1e f3 5a 2a dc 14 87 90 81 8b f9 ac c9 cd" +
        //                                   "f6 fd 31 bd fe 4d 0e b0 43 44 ec 8d f8 72 a9 35" +
        //                                   "8d 70 15 a3 7a bc f0 70 6d c1 87 d2 83 12 fe c9" +
        //                                   "65 ee 29 81 39 1c 7f 24 77 ff 80 ba ce 18 76 5d" +
        //                                   "99 8e 85 1b 10 26 1f e9 81 38 11 df 4c 3d 0a 39" +
        //                                   "fb 90 e7 22 61 20 6b 10 06 47 49 2e bb 53 22 2d" +
        //                                   "df 51 c1 15 ee 4b 94 93 48 29 93 2a c3 e4 a6 62" +
        //                                   "23 8f ff f6 f3 a4 d4 f6 7e 7e b1 2e 2b 0b 53 1d";

        //    const string bytesString = "10 62 28 d5 a5 7c 70 c3 59 b1 67 2c 06 0d 05 0e" +
        //                               "b1 45 de 20 ec 5a c9 83 08 15 3e cf ed c9 ae 40" +
        //                               "df 00 00 00 04 3e 58 2c 87 00 00 00 04 57 3c f6" +
        //                               "e9 00 00 00 de de ec 75 e1 23 0b 3f c5 b4 f9 18" +
        //                               "74 8b 53 7a fb 1c 89 6d 3e 18 f4 c7 8a 45 a8 99" +
        //                               "eb 05 ff 50 0c 48 2f 00 c4 b0 81 f4 b5 b6 f2 e5" +
        //                               "e7 ab 54 d2 cc bb c6 9f 87 b3 58 7f 6c 33 d6 d0" +
        //                               "ce f3 36 4c 86 15 06 9c 10 5d 06 91 64 86 d6 63" +
        //                               "4a 83 49 78 80 4f 1b 26 43 1e 37 96 81 57 a5 6d" +
        //                               "64 2e 4e 74 ed 82 c8 a8 2d db 0e 4e c1 f8 74 d9" +
        //                               "bf d4 5a 87 11 36 ce 7b e8 8b 68 84 08 90 52 79" +
        //                               "63 c2 b1 0a ba 48 6f 0b 33 31 cb 28 a6 82 39 86" +
        //                               "38 39 13 6c d9 f7 d8 4c 76 52 5a f6 11 b5 60 d6" +
        //                               "a5 48 cd 85 54 06 cf 7b 03 d8 41 5e d4 91 ef c1" +
        //                               "7a fb fe a4 1a 1f b1 26 9e c5 8e 3e c7 0b 88 1e" +
        //                               "0f 00 46 c1 77 c4 ab 21 3f 99 7a de 80 0f 32";

        //    var bytes = Utils.StringToByteArray(bytesString.Replace(" ", string.Empty));
        //    var rsa = Program.GetRSABytes(bytes);
        //    TLUtils.WriteLine("RSA actual " + rsa.Length);
        //    TLUtils.WriteLine(BitConverter.ToString(rsa));
        //    TLUtils.WriteLine("RSA expected");
        //    TLUtils.WriteLine(exptectedString);
        //}

        public static void TestAES()
        {
            TLUtils.WriteLine("---------------------------------------");
            TLUtils.WriteLine("--Test Aes ----------------------------");
            TLUtils.WriteLine("---------------------------------------");
            var key = Utils.StringToByteArray("000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F");
            TLUtils.WriteLine("Key " + BitConverter.ToString(key));
            var iv = Utils.StringToByteArray("000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F");
            TLUtils.WriteLine("IV " + BitConverter.ToString(iv));
            var inData = Utils.StringToByteArray("0000000000000000000000000000000000000000000000000000000000000000");
            TLUtils.WriteLine("Data " + BitConverter.ToString(inData));

            // encryption
            using (var rijAlg = new RijndaelManaged{Mode = CipherMode.ECB, Padding = PaddingMode.None})
            {
                //rijAlg.Key = key;
                rijAlg.GenerateIV();

                byte[] encrypted = null;
                List<byte> decrypted = null;


                // Create a decrytor to perform the stream transform.
                ICryptoTransform encryptor = rijAlg.CreateEncryptor(key, rijAlg.IV);


                // Create the streams used for encryption. 
                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        using (BinaryWriter swEncrypt = new BinaryWriter(csEncrypt))
                        {

                            //Write all data to the stream.
                            swEncrypt.Write(inData);
                        }
                        encrypted = msEncrypt.ToArray();
                        TLUtils.WriteLine("Actual encrypted " + BitConverter.ToString(encrypted));
                    }
                }

                // Create a decrytor to perform the stream transform.
                ICryptoTransform decryptor = rijAlg.CreateDecryptor(key, rijAlg.IV);

                // Create the streams used for decryption. 
                using (MemoryStream msDecrypt = new MemoryStream(encrypted))
                {
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (BinaryReader srDecrypt = new BinaryReader(csDecrypt))
                        {
                            decrypted = new List<byte>();

                            byte[] chunk;

                            chunk = srDecrypt.ReadBytes(16);
                            while (chunk.Length > 0)
                            {
                                decrypted = decrypted.Concat(chunk).ToList();
                                chunk = srDecrypt.ReadBytes(16);
                            }

                            // Read the decrypted bytes from the decrypting stream 
                            // and place them in a string.
                            //decrypted = srDecrypt.ReadBytes(int.MaxValue);
                            TLUtils.WriteLine("Actual decrypted " + BitConverter.ToString(decrypted.ToArray()));
                        }
                    }
                }

            }


        }

        public static void TestAesIge()
        {
            TLUtils.WriteLine("---------------------------------------");
            TLUtils.WriteLine("--Test Aes Ige 1-----------------------");
            TLUtils.WriteLine("---------------------------------------");
            var key = Utils.StringToByteArray("000102030405060708090A0B0C0D0E0F");
            TLUtils.WriteLine("Key " + BitConverter.ToString(key));
            var iv = Utils.StringToByteArray("000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F");
            TLUtils.WriteLine("IV " + BitConverter.ToString(iv));
            var inData = Utils.StringToByteArray("0000000000000000000000000000000000000000000000000000000000000000");
            TLUtils.WriteLine("Data " + BitConverter.ToString(inData));
            const string expected = "1A8519A6 557BE652 E9DA8E43 DA4EF445 3CF456B4 CA488AA3 83C79C98 B34797CB";
            TLUtils.WriteLine("Expected         " + expected);
            var encrypted = Utils.AesIge(inData, key, iv, true);
            var decrypted = Utils.AesIge(encrypted, key, iv, false);
            TLUtils.WriteLine("Actual encrypted " + BitConverter.ToString(encrypted));
            TLUtils.WriteLine("Actual decrypted " + BitConverter.ToString(decrypted));

            TLUtils.WriteLine("---------------------------------------");
            TLUtils.WriteLine("--Test Aes Ige 2-----------------------");
            TLUtils.WriteLine("---------------------------------------");
            key = Utils.StringToByteArray("5468697320697320616E20696D706C65");
            TLUtils.WriteLine("Key " + BitConverter.ToString(key));
            iv = Utils.StringToByteArray("6D656E746174696F6E206F6620494745206D6F646520666F72204F70656E5353");
            TLUtils.WriteLine("IV " + BitConverter.ToString(iv));
            inData = Utils.StringToByteArray("99706487A1CDE613BC6DE0B6F24B1C7AA448C8B9C3403E3467A8CAD89340F53B");
            TLUtils.WriteLine("Data " + BitConverter.ToString(inData));
            const string expected2 = "4C2E204C 65742773 20686F70 65204265 6E20676F 74206974 20726967 6874210A";
            TLUtils.WriteLine("Expected         " + expected2);
            encrypted = Utils.AesIge(inData, key, iv, true);
            decrypted = Utils.AesIge(encrypted, key, iv, false);
            TLUtils.WriteLine("Actual encrypted " + BitConverter.ToString(encrypted));
            TLUtils.WriteLine("Actual decrypted " + BitConverter.ToString(decrypted));
        }

        //public static void TestGetAesIVKey()
        //{
        //    TLUtils.WriteLine("---------------------------------------");
        //    TLUtils.WriteLine("--Test Aes iv and  key-----------------");
        //    TLUtils.WriteLine("---------------------------------------");
        //    var serverNonce = Utils.StringToByteArray("A5CF4D33F4A11EA877BA4AA573907330");
        //    TLUtils.WriteLine("ServerNonce " + BitConverter.ToString(serverNonce));
        //    var newNonce = Utils.StringToByteArray("311C85DB234AA2640AFC4A76A735CF5B1F0FD68BD17FA181E1229AD867CC024D");
        //    TLUtils.WriteLine("NewNonce " + BitConverter.ToString(newNonce));

        //    const string expectedKey = "F011280887C7BB01DF0FC4E17830E0B91FBB8BE4B2267CB985AE25F33B527253";   
        //    const string expectedIV = "3212D579EE35452ED23E0D0C92841AA7D31B2E9BDEF2151E80D15860311C85DB";
            
        //    var result = Program.GetAesKeyIV(serverNonce, newNonce);

        //    var actualKey = BitConverter.ToString(result.Item1).Replace("-", string.Empty);
        //    var actualIV = BitConverter.ToString(result.Item2).Replace("-", string.Empty);
        //    TLUtils.WriteLine("Actual key   " + actualKey);
        //    TLUtils.WriteLine("Expected key " + expectedKey);
        //    TLUtils.WriteLine("Actual IV   " + actualIV);
        //    TLUtils.WriteLine("Expected IV " + expectedIV);
        //}

        //public static void TestGetG_B()
        //{
        //    TLUtils.WriteLine("---------------------------------------");
        //    TLUtils.WriteLine("--Test get g_b-------------------------");
        //    TLUtils.WriteLine("---------------------------------------");

        //    var ansverBytes = Utils.StringToByteArray("BA0D89B53E0549828CCA27E966B301A48FECE2FCA5CF4D33F4A11EA877BA4AA57390733002000000FE000100C71CAEB9C6B1C9048E6C522F70F13F73980D40238E3E21C14934D037563D930F48198A0AA7C14058229493D22530F4DBFA336F6E0AC925139543AED44CCE7C3720FD51F69458705AC68CD4FE6B6B13ABDC9746512969328454F18FAF8C595F642477FE96BB2A941D5BCD1D4AC8CC49880708FA9B378E3C4F3A9060BEE67CF9A4A4A695811051907E162753B56B0F6B410DBA74D8A84B2A14B3144E0EF1284754FD17ED950D5965B4B9DD46582DB1178D169C6BC465B0D6FF9CA3928FEF5B9AE4E418FC15E83EBEA0F87FA9FF5EED70050DED2849F47BF959D956850CE929851F0D8115F635B105EE2E4E15D04B2454BF6F4FADF034B10403119CD8E3B92FCC5BFE000100262AABA621CC4DF587DC94CF8252258C0B9337DFB47545A49CDD5C9B8EAE7236C6CADC40B24E88590F1CC2CC762EBF1CF11DCC0B393CAAD6CEE4EE5848001C73ACBB1D127E4CB93072AA3D1C8151B6FB6AA6124B7CD782EAF981BDCFCE9D7A00E423BD9D194E8AF78EF6501F415522E44522281C79D906DDB79C72E9C63D83FB2A940FF779DFB5F2FD786FB4AD71C9F08CF48758E534E9815F634F1E3A80A5E1C2AF210C5AB762755AD4B2126DFA61A77FA9DA967D65DFD0AFB5CDF26C4D4E1A88B180F4E0D0B45BA1484F95CB2712B50BF3F5968D9D55C99C0FB9FB67BFF56D7D4481B634514FBA3488C4CDA2FC0659990E8E868B28632875A9AA703BCDCE8FCB7AE551");
        //    var answer = Answer.Parse(ansverBytes);
        //    var gBytes = answer.G;
        //    var dhPrimeBytes = answer.DHPrime;
        //    var bBytes = Utils.StringToByteArray("6F620AFA575C9233EB4C014110A7BCAF49464F798A18A0981FEA1E05E8DA67D9681E0FD6DF0EDF0272AE3492451A84502F2EFC0DA18741A5FB80BD82296919A70FAA6D07CBBBCA2037EA7D3E327B61D585ED3373EE0553A91CBD29B01FA9A89D479CA53D57BDE3A76FBD922A923A0A38B922C1D0701F53FF52D7EA9217080163A64901E766EB6A0F20BC391B64B9D1DD2CD13A7D0C946A3A7DF8CEC9E2236446F646C42CFE2B60A2A8D776E56C8D7519B08B88ED0970E10D12A8C9E355D765F2B7BBB7B4CA9360083435523CB0D57D2B106FD14F94B4EEE79D8AC131CA56AD389C84FE279716F8124A543337FB9EA3D988EC5FA63D90A4BA3970E7A39E5C0DE5");

        //    var g_b = Program.GetG_B(bBytes, gBytes, dhPrimeBytes);

        //    var expectedG_B = "73700E7BFC7AEEC828EB8E0DCC04D09A 0DD56A1B4B35F72F0B55FCE7DB7EBB72 D7C33C5D4AA59E1C74D09B01AE536B31 8CFED436AFDB15FE9EB4C70D7F0CB14E 46DBBDE9053A64304361EB358A9BB32E 9D5C2843FE87248B89C3F066A7D5876D 61657ACC52B0D81CD683B2A0FA93E8AD AB20377877F3BC3369BBF57B10F5B589 E65A9C27490F30A0C70FFCFD3453F5B3 79C1B9727A573CFFDCA8D23C721B135B 92E529B1CDD2F7ABD4F34DAC4BE1EEAF 60993DDE8ED45890E4F47C26F2C0B2E0 37BB502739C8824F2A99E2B1E7E41658 3417CC79A8807A4BDAC6A5E9805D4F61 86C37D66F6988C9F9C752896F3D34D25 529263FAF2670A09B2A59CE35264511F";

        //    TLUtils.WriteLine("Expected g_b");
        //    TLUtils.WriteLine(expectedG_B.Replace(" ", "\n"));
        //    TLUtils.WriteLine("Actual b_g");
        //    TLUtils.WriteLine(BitConverter.ToString(g_b));
        //}

        public static void CRC32Test()
        {
            const string user = "user id:int first_name:string last_name:string = User";
            const string message = "message msg_id:long seqno:int bytes:int body:MessageData = TLMessage;";
            const string userEmpty = "userStatusEmpty = UserStatus";

            TLUtils.WriteLine(BitConverter.ToString(Utils.CalculateCRC32(userEmpty)));
        }

        //public static void TestGetAuthKey()
        //{
        //    TLUtils.WriteLine("---------------------------------------");
        //    TLUtils.WriteLine("--Test authKey-------------------------");
        //    TLUtils.WriteLine("---------------------------------------");

        //    var ansverBytes = Utils.StringToByteArray("BA0D89B53E0549828CCA27E966B301A48FECE2FCA5CF4D33F4A11EA877BA4AA57390733002000000FE000100C71CAEB9C6B1C9048E6C522F70F13F73980D40238E3E21C14934D037563D930F48198A0AA7C14058229493D22530F4DBFA336F6E0AC925139543AED44CCE7C3720FD51F69458705AC68CD4FE6B6B13ABDC9746512969328454F18FAF8C595F642477FE96BB2A941D5BCD1D4AC8CC49880708FA9B378E3C4F3A9060BEE67CF9A4A4A695811051907E162753B56B0F6B410DBA74D8A84B2A14B3144E0EF1284754FD17ED950D5965B4B9DD46582DB1178D169C6BC465B0D6FF9CA3928FEF5B9AE4E418FC15E83EBEA0F87FA9FF5EED70050DED2849F47BF959D956850CE929851F0D8115F635B105EE2E4E15D04B2454BF6F4FADF034B10403119CD8E3B92FCC5BFE000100262AABA621CC4DF587DC94CF8252258C0B9337DFB47545A49CDD5C9B8EAE7236C6CADC40B24E88590F1CC2CC762EBF1CF11DCC0B393CAAD6CEE4EE5848001C73ACBB1D127E4CB93072AA3D1C8151B6FB6AA6124B7CD782EAF981BDCFCE9D7A00E423BD9D194E8AF78EF6501F415522E44522281C79D906DDB79C72E9C63D83FB2A940FF779DFB5F2FD786FB4AD71C9F08CF48758E534E9815F634F1E3A80A5E1C2AF210C5AB762755AD4B2126DFA61A77FA9DA967D65DFD0AFB5CDF26C4D4E1A88B180F4E0D0B45BA1484F95CB2712B50BF3F5968D9D55C99C0FB9FB67BFF56D7D4481B634514FBA3488C4CDA2FC0659990E8E868B28632875A9AA703BCDCE8FCB7AE551");
        //    var answer = Answer.Parse(ansverBytes);
        //    var g_aBytes = answer.G_A;
        //    var dhPrimeBytes = answer.DHPrime;
        //    var bBytes = Utils.StringToByteArray("6F620AFA575C9233EB4C014110A7BCAF49464F798A18A0981FEA1E05E8DA67D9681E0FD6DF0EDF0272AE3492451A84502F2EFC0DA18741A5FB80BD82296919A70FAA6D07CBBBCA2037EA7D3E327B61D585ED3373EE0553A91CBD29B01FA9A89D479CA53D57BDE3A76FBD922A923A0A38B922C1D0701F53FF52D7EA9217080163A64901E766EB6A0F20BC391B64B9D1DD2CD13A7D0C946A3A7DF8CEC9E2236446F646C42CFE2B60A2A8D776E56C8D7519B08B88ED0970E10D12A8C9E355D765F2B7BBB7B4CA9360083435523CB0D57D2B106FD14F94B4EEE79D8AC131CA56AD389C84FE279716F8124A543337FB9EA3D988EC5FA63D90A4BA3970E7A39E5C0DE5");

        //    var authKey = Program.GetAuthKey(bBytes, g_aBytes, dhPrimeBytes);

        //    var expectedAuthKey = "AB96E207C631300986F30EF97DF55E179E63C112675F0CE502EE76D74BBEE6CBD1E95772818881E9F2FF54BD52C258787474F6A7BEA61EABE49D1D01D55F64FC07BC31685716EC8FB46FEACF9502E42CFD6B9F45A08E90AA5C2B5933AC767CBE1CD50D8E64F89727CA4A1A5D32C0DB80A9FCDBDDD4F8D5A1E774198F1A4299F927C484FEEC395F29647E43C3243986F93609E23538C21871DF50E00070B3B6A8FA9BC15628E8B43FF977409A61CEEC5A21CF7DFB5A4CC28F5257BC30CD8F2FB92FBF21E28924065F50E0BBD5E11A420300E2C136B80E9826C6C5609B5371B7850AA628323B6422F3A94F6DFDE4C3DC1EA60F7E11EE63122B3F39CBD1A8430157";

        //    TLUtils.WriteLine("Expected authKey");
        //    TLUtils.WriteLine(expectedAuthKey);
        //    TLUtils.WriteLine("Actual authKey");
        //    TLUtils.WriteLine(BitConverter.ToString(authKey));
        //}
    }
}
