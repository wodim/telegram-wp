using System.Linq;
using Telegram.Api.Helpers;
using Telegram.Api.TL;

namespace Telegram.Api
{
    public partial class Program
	{
        /// <summary>
        /// Возвращает список диалогов текущего пользователя.
        /// </summary>
        /// <param name="seqNo">    </param>
        /// <param name="offset">   Количество элементов списка, которое необходимо пропустить </param>
        /// <param name="maxId">    Если передано положительное значение, метод вернет только диалоги с идентификаторами меньше заданного (offset игнорируется) </param>
        /// <param name="limit">    Количество элементов списка, которое необходимо вернуть </param>
        /// <returns></returns>
        public static TLResponse SendGetDialogsRequest(int seqNo, TLInt offset, TLInt maxId, TLInt limit)
        {
            return RequestHelper.Send("messages.getDialogs", seqNo, () => ComposeGetDialogsRequest(offset, maxId, limit));
        }

        private static byte[] ComposeGetDialogsRequest(TLInt offset, TLInt maxId, TLInt limit)
        {
            //#eccf1df6
            var signature = new byte[] { 0xf6, 0x1d, 0xcf, 0xec };

            TLUtils.WriteLine("Offset: " + offset);
            TLUtils.WriteLine("MaxId: " + maxId);
            TLUtils.WriteLine("Limit: " + limit);

            return signature
                .Concat(offset.ToBytes())
                .Concat(maxId.ToBytes())
                .Concat(limit.ToBytes())
                .ToArray();
        }

        /// <summary>
        /// Возвращает сообщения из истории переписки внутри одного диалога.
        /// </summary>
        /// <param name="seqNo"></param>
        /// <param name="peerBase">     Пользователь или чат, истории переписки с которым интересует</param>
        /// <param name="offset">   Количество элементов списка, которое необходимо пропустить</param>
        /// <param name="maxId">    Если передано положительное значение, метод вернет только сообщения с идентификаторами меньше заданного</param>
        /// <param name="limit">    Количество элементов списка, которое необходимо вернуть</param>
        /// <returns></returns>
        private static TLResponse SendGetHistoryRequest(int seqNo, TLInputPeerBase peerBase, TLInt offset, TLInt maxId, TLInt limit)
        {
            return RequestHelper.Send("messages.getHistory", seqNo, () => ComposeGetHistoryRequest(peerBase, offset, maxId, limit));
        }

        private static byte[] ComposeGetHistoryRequest(TLInputPeerBase peerBase, TLInt offset, TLInt maxId, TLInt limit)
        {
            //#92a1df2f
            var signature = new byte[] { 0x2f, 0xdf, 0xa1, 0x92 };

            TLUtils.WriteLine("Peer: " + peerBase);
            TLUtils.WriteLine("Offset: " + offset);
            TLUtils.WriteLine("MaxId: " + maxId);
            TLUtils.WriteLine("Limit: " + limit);

            return signature
                .Concat(peerBase.ToBytes())
                .Concat(offset.ToBytes())
                .Concat(maxId.ToBytes())
                .Concat(limit.ToBytes())
                .ToArray();
        }

        /// <summary>
        /// Выполняет отправку текстового сообщения.
        /// </summary>
        /// <param name="seqNo"></param>
        /// <param name="peerBase">     Пользователь или чат, куда будет отправлено сообщение</param>
        /// <param name="message">  Текст сообщения</param>
        /// <param name="randomId"> Уникальный клиентский идентификатор сообщений, необходимый для исключения повторной отправки сообщения</param>
        /// <returns></returns>
        private static TLResponse SendSendMessageRequest(int seqNo, TLInputPeerBase peerBase, TLString message, TLLong randomId)
        {
            return RequestHelper.Send("messages.sendMessage", seqNo, () => ComposeSendMessageRequest(peerBase, message, randomId));
        }

        private static byte[] ComposeSendMessageRequest(TLInputPeerBase peerBase, TLString message, TLLong randomId)
        {
            //#4cde0aab
            var signature = new byte[] { 0xab, 0x0a, 0xde, 0x4c };

            TLUtils.WriteLine("Peer: " + peerBase);
            TLUtils.WriteLine("Message: " + message);
            TLUtils.WriteLine("RandomId: " + randomId);

            return signature
                .Concat(peerBase.ToBytes())
                .Concat(message.ToBytes())
                .Concat(randomId.ToBytes())
                .ToArray();
        }
	}
}
