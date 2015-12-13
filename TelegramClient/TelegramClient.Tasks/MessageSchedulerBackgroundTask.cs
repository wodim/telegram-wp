using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Telegram.Api;
using Telegram.Api.Aggregator;
using Telegram.Api.Extensions;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.Connection;
using Telegram.Api.Services.DeviceInfo;
using Telegram.Api.Services.Updates;
using Telegram.Api.TL;
using Telegram.Api.Transport;

namespace TelegramClient.Tasks
{
    public sealed class MessageSchedulerBackgroundTask : IBackgroundTask
    {
        private readonly Mutex _appOpenMutex = new Mutex(false, Constants.TelegramMessengerMutexName);
        
        private bool _logEnabled = true;

        private void Log(string message, Action callback = null)
        {
            if (!_logEnabled) return;

            Telegram.Logs.Log.Write(string.Format("::MessageSchedulerBackgroundTask {0} {1}", _id, message), callback.SafeInvoke);
#if DEBUG
            PushUtils.AddToast("scheduler", message, string.Empty, string.Empty, null, null);
#endif
        }

        private static readonly int _id = new Random().Next(999);

        private readonly object _initConnectionSyncRoot = new object();

        private TLInitConnection GetInitConnection()
        {
            return TLUtils.OpenObjectFromMTProtoFile<TLInitConnection>(_initConnectionSyncRoot, Constants.InitConnectionFileName) ??
                new TLInitConnection
                {
                    DeviceModel = new TLString("unknown"),
                    AppVersion = new TLString("background task"),
                    SystemVersion = new TLString("8.10.0.0")
                };
        }

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            Telegram.Logs.Log.WriteSync = true;

            taskInstance.Canceled += OnTaskCanceled;
            var deferral = taskInstance.GetDeferral();

            var stopwatch = Stopwatch.StartNew();
            var task = taskInstance.Task;
            var name = task != null ? task.Name : null;
            Log("start " + name);
            if (!_appOpenMutex.WaitOne(0))
            {
                Log("cancel", deferral.Complete);

                return;
            }
            _appOpenMutex.ReleaseMutex();
            Log("release mutex");
            var isAuthorized = SettingsHelper.GetValue<bool>(Constants.IsAuthorizedKey);
            if (!isAuthorized)
            {
                Log("cancel isAuthorized=false", deferral.Complete);

                return;
            }
            Log("isAuthorized=true");

            var deviceInfoService = new DeviceInfoService(GetInitConnection(), true, "MessageSchedulerBackgroundTask", _id);
            var eventAggregator = new TelegramEventAggregator();
            var cacheService = new InMemoryCacheService(eventAggregator);
            var updatesService = new UpdatesService(cacheService, eventAggregator);
            var transportService = new TransportService();
            var connectionService = new ConnectionService(deviceInfoService);

            var manualResetEvent = new ManualResetEvent(false);
            Log("before init");
            var requestsToRemove = new List<TLObject>();
            var mtProtoService = new MTProtoService(deviceInfoService, updatesService, cacheService, transportService, connectionService);
            mtProtoService.Initialized +=  async (o, e) =>
            {
                Log("init completed");

                var actionInfos = mtProtoService.GetActionInfoFromFile();
                var count = actionInfos != null ? actionInfos.Count : 0;
                Log("send count=" + count);

                if (actionInfos != null
                    && actionInfos.Count > 0)
                {
                    var actionsString = new StringBuilder();
                    foreach (var info in actionInfos)
                    {
                        actionsString.AppendLine(info.ToString());
                    }
                    Log(actionsString.ToString());

                    var sendMessageActions = new List<TLObject>();
                    const int maxActionCount = 10;
                    var currentCount = 0;
                    foreach (var actionInfo in actionInfos)
                    {
                        if (TLUtils.IsValidAction(actionInfo.Action) && currentCount < maxActionCount)
                        {
                            currentCount++;
                            sendMessageActions.Add(actionInfo.Action);
                        }
                    }

                    if (sendMessageActions.Count > 0)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(3.0));

                        mtProtoService.SendActionsAsync(sendMessageActions,
                            (request, result) => // will be invoked for each sent action
                            {
                                requestsToRemove.Add(request);
                                var sendingMessages = mtProtoService.SendingMessages;
                                Log("send completed count=" + sendingMessages, () =>
                                {
                                    if (sendingMessages == 0)
                                    {
                                        _clearActionInfoFile = true;

                                        manualResetEvent.Set();
                                    }
                                });
                            },
                            error =>
                            {
                                Log(string.Format("send error={0}\n{1}", error, error.Exception),
                                    async () =>
                                    {
                                        await Task.Delay(TimeSpan.FromSeconds(1.0));
                                        manualResetEvent.Set();
                                    });
                            });
                    }
                    else
                    {
                        manualResetEvent.Set();
                    }
                }
                else
                {
                    manualResetEvent.Set();
                }
            };
            mtProtoService.InitializationFailed += (o, e) =>
            {
                Log("init failed");

                manualResetEvent.Set();
            };
            mtProtoService.Initialize();
#if DEBUG
            manualResetEvent.WaitOne();
#else
            manualResetEvent.WaitOne(15000);
#endif
            if (_clearActionInfoFile)
            {
                Log("clear");
                mtProtoService.RemoveActionInfoFromFile(requestsToRemove);
            }

            Log("stop " + stopwatch.Elapsed, deferral.Complete);
        }

        private void OnTaskCanceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            Log(string.Format("cancel reason={0}", reason));
        }

        private bool _clearActionInfoFile;
    }
}
