using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TelegramClient.Services;
using TelegramClient.ViewModels.Additional;
#if WP81
using Windows.ApplicationModel.DataTransfer.ShareTarget;
using Windows.Storage.Provider;
using Windows.ApplicationModel.Activation;
#endif
using Caliburn.Micro;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using TelegramClient.ViewModels;
using TelegramClient.ViewModels.Dialogs;
using TelegramClient.Views.Additional;
using TelegramClient.Views.Controls;
using TelegramClient.Views.Dialogs;
#if WP8
using Windows.Storage;
using TelegramClient_Opus;
#endif
namespace TelegramClient
{
    public partial class App : Application
    {
        /// <summary>
        /// Provides easy access to the root frame of the Phone Application.
        /// </summary>
        /// <returns>The root frame of the Phone Application.</returns>
        //public PhoneApplicationFrame RootFrame { get; private set; }

        public static Stopwatch Timer = Stopwatch.StartNew();

        

        /// <summary>
        /// Constructor for the Application object.
        /// </summary>
        public App()
        {
            // Standard Silverlight initialization
            InitializeComponent();

            
            // Show graphics profiling information while debugging.
            if (Debugger.IsAttached)
            {
                // Display the current frame rate counters.
                

                // Show the areas of the app that are being redrawn in each frame.
                //Application.Current.Host.Settings.EnableRedrawRegions = true;

                // Enable non-production analysis visualization mode, 
                // which shows areas of a page that are handed off to GPU with a colored overlay.
                //Application.Current.Host.Settings.EnableCacheVisualization = true;

                // Disable the application idle detection by setting the UserIdleDetectionMode property of the
                // application's PhoneApplicationService object to Disabled.
                // Caution:- Use this under debug mode only. Application that disables user idle detection will continue to run
                // and consume battery power when the user is not using the phone.
                PhoneApplicationService.Current.UserIdleDetectionMode = IdleDetectionMode.Disabled;
            }
            //Current.Host.Settings.EnableRedrawRegions = true;
#if DEBUG
            //Current.Host.Settings.EnableFrameRateCounter = true;
            //Current.Host.Settings.EnableRedrawRegions = true;
#endif
#if WP81
            PhoneApplicationService.Current.Activated += OnActivated;
            PhoneApplicationService.Current.ContractActivated += OnContractActivated;
#endif
//#if WP8
//            ApplicationLifetimeObjects.Add(new XnaAsyncDispatcher(TimeSpan.FromMilliseconds(50)));
//#endif

            UnhandledException += (sender, args) =>
            {
                Telegram.Logs.Log.Write(args.ExceptionObject.ToString());
#if DEBUG
                Deployment.Current.Dispatcher.BeginInvoke(() => MessageBox.Show(args.ExceptionObject.ToString()));
#endif
                args.Handled = true;
            };
        }

        public ChooseFileInfo ChooseFileInfo { get; set; }

#if WP81
        public ShareOperation ShareOperation { get; set; }
#endif

        private void OnActivated(object sender, ActivatedEventArgs e)
        {
            
        }

#if WP8
        public static IReadOnlyCollection<StorageFile> Photos { get; set; } 
#endif

#if WP81
        private void OnContractActivated(object sender, IActivatedEventArgs e)
        {
            var saveArgs = e as FileSavePickerContinuationEventArgs;
            if (saveArgs != null)
            {
                object from;
                if (saveArgs.ContinuationData != null
                    && saveArgs.ContinuationData.TryGetValue("From", out from))
                {
                    if (string.Equals(from, "DialogDetailsView"))
                    {
                        Telegram.Api.Helpers.Execute.BeginOnThreadPool(() => DialogDetailsViewModel.SaveFile(saveArgs.File));

                        return;
                    }
                }
            }

            var args = e as FileOpenPickerContinuationEventArgs;
            if (args != null)
            {
                object from;
                if (args.ContinuationData != null
                    && args.ContinuationData.TryGetValue("From", out from))
                {
                    if (string.Equals(from, "DialogDetailsView"))
                    {
                        var contentControl = RootVisual as ContentControl;
                        if (contentControl != null)
                        {
                            var dialogDetailsView = contentControl.Content as DialogDetailsView;
                            if (dialogDetailsView != null)
                            {
                                var dialogDetailsViewModel = dialogDetailsView.DataContext as DialogDetailsViewModel;
                                if (dialogDetailsViewModel != null)
                                {
                                    object type;
                                    if (!args.ContinuationData.TryGetValue("Type", out type))
                                    {
                                        type = "Document";
                                    }

                                    if (string.Equals(type, "Video"))
                                    {
                                        var file = args.Files.FirstOrDefault();
                                        Telegram.Api.Helpers.Execute.BeginOnThreadPool(() => dialogDetailsViewModel.EditVideo(file));
                                    }
                                    else if (string.Equals(type, "Image"))
                                    {
                                        var file = args.Files.FirstOrDefault();
                                        if (file != null)
                                        {
#if MULTIPLE_PHOTOS
                                            Photos = args.Files;
                                            return;
#endif

#if WP81
                                            Telegram.Api.Helpers.Execute.BeginOnThreadPool(async () =>
                                            {
                                                var randomStream = await file.OpenReadAsync();

                                                await ChooseAttachmentViewModel.Handle(IoC.Get<IStateService>(), randomStream, file.Name);

                                                //MessageBox.Show("OnContractActivated after handle");
                                                dialogDetailsViewModel.BackwardInAnimationComplete();
                                            });
#else
                                            Telegram.Api.Helpers.Execute.BeginOnThreadPool(async () =>
                                            {
                                                var randomStream = await file.OpenReadAsync();
                                                var chosenPhoto = randomStream.AsStreamForRead();

                                                //MessageBox.Show("OnContractActivated stream");
                                                Telegram.Api.Helpers.Execute.BeginOnUIThread(() =>
                                                {
                                                    ChooseAttachmentViewModel.Handle(IoC.Get<IStateService>(), chosenPhoto, file.Name);

                                                    //MessageBox.Show("OnContractActivated after handle");
                                                    dialogDetailsViewModel.BackwardInAnimationComplete();
                                                });
                                            });
#endif


                                        }
                                    }
                                    else
                                    {
                                        var file = args.Files.FirstOrDefault();
                                        Telegram.Api.Helpers.Execute.BeginOnThreadPool(() => dialogDetailsViewModel.SendDocument(file));
                                    }

                                    return;
                                }
                            }
                        }
                    }
                    else if (string.Equals(from, "SecretDialogDetailsView"))
                    {
                        var contentControl = RootVisual as ContentControl;
                        if (contentControl != null)
                        {
                            var dialogDetailsView = contentControl.Content as SecretDialogDetailsView;
                            if (dialogDetailsView != null)
                            {
                                var dialogDetailsViewModel = dialogDetailsView.DataContext as SecretDialogDetailsViewModel;
                                if (dialogDetailsViewModel != null)
                                {
                                    object type;
                                    if (!args.ContinuationData.TryGetValue("Type", out type))
                                    {
                                        type = "Document";
                                    }

                                    //if (string.Equals(type, "Video"))
                                    //{
                                    //    var file = args.Files.FirstOrDefault();
                                    //    Telegram.Api.Helpers.Execute.BeginOnThreadPool(() => dialogDetailsViewModel.EditVideo(file));
                                    //}
                                    //else
                                    {
                                        var file = args.Files.FirstOrDefault();
                                        Telegram.Api.Helpers.Execute.BeginOnThreadPool(() => dialogDetailsViewModel.SendDocument(file));
                                    }

                                    
                                    if (string.Equals(type, "Image"))
                                    {
                                        var file = args.Files.FirstOrDefault();
                                        if (file != null)
                                        {
#if WP81
                                            Telegram.Api.Helpers.Execute.BeginOnThreadPool(async () =>
                                            {
                                                var randomStream = await file.OpenReadAsync();
                                                await ChooseAttachmentViewModel.Handle(IoC.Get<IStateService>(), randomStream, file.Name);
                                                dialogDetailsViewModel.OnBackwardInAnimationComplete();
                                            });
#else
                                            Telegram.Api.Helpers.Execute.BeginOnThreadPool(async () =>
                                            {
                                                var randomStream = await file.OpenReadAsync();
                                                var chosenPhoto = randomStream.AsStreamForRead();

                                                Telegram.Api.Helpers.Execute.BeginOnUIThread(() =>
                                                {
                                                    ChooseAttachmentViewModel.Handle(IoC.Get<IStateService>(), chosenPhoto, file.Name);
                                                });
                                            });
#endif
                                        }
                                    }
                                    else
                                    {
                                        var file = args.Files.FirstOrDefault();
                                        Telegram.Api.Helpers.Execute.BeginOnThreadPool(() => dialogDetailsViewModel.SendDocument(file));
                                    }

                                    return;
                                }
                            }
                        }
                    }
                }
            }
        }
#endif
    }

    public class ChooseFileInfo
    {
        public DateTime Time { get; set; }

        public ChooseFileInfo(DateTime time)
        {
            Time = time;
        }
    }
}