using System;
using System.Diagnostics;
#if WP81 && WNS_PUSH_SERVICE
using Windows.Networking.PushNotifications;
#endif
using Microsoft.Phone.Notification;
using Telegram.Api.Extensions;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.TL;

namespace TelegramClient.Services
{
#if WP81 && WNS_PUSH_SERVICE
    public class WNSPushService : PushServiceBase
    {
        protected override TLInt TokenType
        {
            get { return new TLInt(Constants.WNSTokenType); }
        }

        private PushNotificationChannel _pushChannel;

        public WNSPushService(IMTProtoService service) : base(service)
        {
            LoadOrCreateChannelAsync();
        }

        private void LoadOrCreateChannelAsync(Action callback = null)
        {
            Execute.BeginOnThreadPool(async () =>
            {
                try
                {
                    var pushChannel = HttpNotificationChannel.Find(Constants.ToastNotificationChannelName);

                    if (pushChannel != null)
                    {
                        pushChannel.UnbindToShellTile();
                        pushChannel.UnbindToShellToast();
                    }
                }
                catch (Exception ex)
                {
                    Execute.ShowDebugMessage("WNSPushService.LoadOrCreateChannelAsync ex " + ex);
                }

                Telegram.Logs.Log.Write("WNSPushService start creating channel");
                _pushChannel = await PushNotificationChannelManager.CreatePushNotificationChannelForApplicationAsync();
                _pushChannel.PushNotificationReceived += OnPushNotificationReceived;
                Telegram.Logs.Log.Write("WNSPushService stop creating channel");

                callback.SafeInvoke();
            });
        }

        private void OnPushNotificationReceived(PushNotificationChannel sender, PushNotificationReceivedEventArgs args)
        {
            //Execute.ShowDebugMessage("WNSPushService.OnPushNotificationReceived " + args);
        }

        protected override string GetPushChannelUri()
        {
            return _pushChannel != null ? _pushChannel.Uri : null;
        }
    }
#endif

    public class PushService : PushServiceBase
    {
        protected override TLInt TokenType
        {
            get { return new TLInt(Constants.MPNSTokenType); }
        }

        protected override string GetPushChannelUri()
        {
            return _pushChannel != null && _pushChannel.ChannelUri != null ? _pushChannel.ChannelUri.ToString() : null;
        }

        private HttpNotificationChannel _pushChannel;

        public PushService(IMTProtoService service) : base(service)
        {
            LoadOrCreateChannelAsync();
        }

        private void LoadOrCreateChannelAsync(Action callback = null)
        {
            Execute.BeginOnThreadPool(() =>
            {
                _pushChannel = HttpNotificationChannel.Find(Constants.ToastNotificationChannelName);

                if (_pushChannel == null)
                {
                    _pushChannel = new HttpNotificationChannel(Constants.ToastNotificationChannelName);
                    _pushChannel.HttpNotificationReceived += OnHttpNotificationReceived;
                    _pushChannel.ChannelUriUpdated += OnChannelUriUpdated;
                    _pushChannel.ErrorOccurred += OnErrorOccurred;
                    _pushChannel.ShellToastNotificationReceived += OnShellToastNotificationReceived;

                    try
                    {
                        _pushChannel.Open();
                    }
                    catch (Exception e)
                    {
                        TLUtils.WriteException(e);
                    }

                    if (_pushChannel != null && _pushChannel.ChannelUri != null)
                    {
                        Debug.WriteLine(_pushChannel.ChannelUri.ToString());
                    }
                    _pushChannel.BindToShellToast();
                    _pushChannel.BindToShellTile();
                }
                else
                {
                    _pushChannel.HttpNotificationReceived += OnHttpNotificationReceived;
                    _pushChannel.ChannelUriUpdated += OnChannelUriUpdated;
                    _pushChannel.ErrorOccurred += OnErrorOccurred;
                    _pushChannel.ShellToastNotificationReceived += OnShellToastNotificationReceived;

                    if (!_pushChannel.IsShellTileBound)
                    {
                        _pushChannel.BindToShellToast();
                    }
                    if (!_pushChannel.IsShellTileBound)
                    {
                        _pushChannel.BindToShellTile();
                    }
                }
                callback.SafeInvoke();
            });
        }


        private void OnHttpNotificationReceived(object sender, HttpNotificationEventArgs e)
        {

        }

        private void OnShellToastNotificationReceived(object sender, NotificationEventArgs e)
        {
            
        }

        private void OnErrorOccurred(object sender, NotificationChannelErrorEventArgs e)
        {
            var message = string.Format("A push notification {0} error occurred.  {1} ({2}) {3}", e.ErrorType, e.Message, e.ErrorCode, e.ErrorAdditionalData);
            Execute.ShowDebugMessage(message);
            TLUtils.WriteLine(message);

            LoadOrCreateChannelAsync(RegisterDeviceAsync);
        }

        private void OnChannelUriUpdated(object sender, NotificationChannelUriEventArgs e)
        {
            Debug.WriteLine(e.ChannelUri.ToString());
            TLUtils.WriteLine(String.Format("Channel Uri is {0}", e.ChannelUri));

            RegisterDeviceAsync();
        }
    }
}
