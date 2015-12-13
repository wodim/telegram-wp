using System;
using System.Security.Cryptography;
using System.Windows;
using Caliburn.Micro;
using Telegram.Api.Extensions;
using Telegram.Api.Helpers;
using Telegram.Api.TL;

namespace TelegramClient.ViewModels.Dialogs
{
    public class SecretChatDebugViewModel : PropertyChangedBase
    {
        public Visibility InvokeButtonVisibility
        {
            get
            {
                var chat20 = Chat as TLEncryptedChat20;

                return chat20 != null ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public TLEncryptedChatBase Chat { get; protected set; }

        public string KeyFingerprint
        {
            get
            {
                var chat = Chat as TLEncryptedChat;
                if (chat == null) return null;

                var key = chat.Key;
                if (key == null) return null;
                var keyHash = Telegram.Api.Helpers.Utils.ComputeSHA1(key.Data);

                return "Key: " + BitConverter.ToString(keyHash.SubArray(12, keyHash.Length - 12)).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private readonly System.Action _rekeyAction;

        public SecretChatDebugViewModel(TLEncryptedChatBase chat, System.Action rekeyAction)
        {
            Chat = chat;
            _rekeyAction = rekeyAction;
        }

        public void Invoke()
        {
            _rekeyAction.SafeInvoke();
        }

        public void UpdateChat(TLEncryptedChatBase encryptedChat)
        {
            Chat = encryptedChat;
            NotifyOfPropertyChange(() => KeyFingerprint);
            NotifyOfPropertyChange(() => Chat);
            NotifyOfPropertyChange(() => InvokeButtonVisibility);
        }
    }
}
