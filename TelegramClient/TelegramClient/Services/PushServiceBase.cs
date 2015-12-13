using System;
using Telegram.Api.Extensions;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.TL;

namespace TelegramClient.Services
{
    public abstract class PushServiceBase : IPushService
    {
        protected abstract TLInt TokenType { get; }

        protected readonly IMTProtoService Service;

        protected PushServiceBase(IMTProtoService service)
        {
            Service = service;
        }

        protected abstract string GetPushChannelUri();

        private string _lastRegisteredUri;

        private readonly object _lastRegisteredUriRoot = new object();

        public virtual void RegisterDeviceAsync()
        {
            Execute.BeginOnThreadPool(() =>
            {
                var channelUri = GetPushChannelUri();

                if (string.IsNullOrEmpty(channelUri))
                {
                    return;
                }

                var isAuthorized = SettingsHelper.GetValue<bool>(Constants.IsAuthorizedKey);
                if (!isAuthorized)
                {
                    return;
                }

                lock (_lastRegisteredUriRoot)
                {
                    if (string.Equals(_lastRegisteredUri, channelUri))
                    {
                        return;
                    }

                    _lastRegisteredUri = channelUri;
                }

                Service.RegisterDeviceAsync(
                    TokenType,
                    new TLString(_lastRegisteredUri),
                    result =>
                    {
                        //Execute.ShowDebugMessage("account.registerDevice result " + result);
                        TLUtils.WriteLine("account.registerDevice result " + result);
                    },
                    error =>
                    {
                        //Execute.ShowDebugMessage("account.registerDevice error " + error);
                        //TLUtils.WriteLine("account.registerDevice error " + error);
                    });
            });
        }

        public virtual void UnregisterDeviceAsync(Action callback)
        {
            Execute.BeginOnThreadPool(() =>
            {
                var channelUri = GetPushChannelUri();

                if (string.IsNullOrEmpty(channelUri))
                {
                    callback.SafeInvoke();
                    return;
                }

                Service.UnregisterDeviceAsync(
                    new TLString(channelUri),
                    result =>
                    {
                        TLUtils.WriteLine("account.unregisterDevice result " + result);
                        callback.SafeInvoke();
                    },
                    error =>
                    {
                        Execute.ShowDebugMessage("account.unregisterDevice error " + error);
                        TLUtils.WriteLine("account.unregisterDevice error " + error);
                        callback.SafeInvoke();
                    });
            });
        }
    }
}