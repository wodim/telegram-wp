using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using Windows.System;
using Microsoft.Phone.Info;
using TelegramClient.Controls;
#if WP8
using TelegramClient_WebP.LibWebP;
using Windows.Phone.PersonalInformation;
#endif
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using TelegramClient.Utils;
#if WP81
using Windows.ApplicationModel.Background;
using Windows.Storage.Pickers;
#endif
using Caliburn.Micro;
using Microsoft.Phone.Shell;
using Telegram.Api.TL;
using TelegramClient.Resources;
using TelegramClient.Services;
using TelegramClient.ViewModels;
using TelegramClient.ViewModels.Contacts;
using TelegramClient.ViewModels.Dialogs;

namespace TelegramClient.Views
{
    public partial class ShellView
    {
        private ShellViewModel ViewModel { get { return DataContext as ShellViewModel; } }

        private bool _firstRun = true;

        private readonly ApplicationBarIconButton _addContactButton = new ApplicationBarIconButton
        {
            Text = AppResources.ComposeMessage,
            IconUri = new Uri("/Images/ApplicationBar/appbar.add.rest.png", UriKind.Relative)
        };

        private readonly ApplicationBarIconButton _searchButton = new ApplicationBarIconButton
        {
            Text = AppResources.Search,
            IconUri = new Uri("/Images/ApplicationBar/appbar.feature.search.rest.png", UriKind.Relative)
        };

        private readonly ApplicationBarMenuItem _refreshMenuItem = new ApplicationBarMenuItem
        {
            Text = AppResources.Refresh
        };

        private readonly ApplicationBarMenuItem _settingsItem = new ApplicationBarMenuItem
        {
            Text = AppResources.Settings,
        };

        private readonly ApplicationBarMenuItem _reviewItem = new ApplicationBarMenuItem
        {
            Text = AppResources.Review,
        };

        private readonly ApplicationBarMenuItem _aboutItem = new ApplicationBarMenuItem
        {
            Text = AppResources.About,
        };

#if DEBUG
        private readonly ApplicationBarMenuItem _openConfig = new ApplicationBarMenuItem
        {
            Text = "Open config",
        };

        private readonly ApplicationBarMenuItem _getCurrentPacketInfoItem = new ApplicationBarMenuItem
        {
            Text = "Info",
        };

        private readonly ApplicationBarMenuItem _showRegistrationLogItem = new ApplicationBarMenuItem
        {
            Text = "Log",
        };
#endif
        private readonly ApplicationBarMenuItem _importContactsItem = new ApplicationBarMenuItem
        {
            Text = "import contacts",
        };

        public ShellView()
        {
            InitializeComponent();

            OptimizeFullHD();

            _addContactButton.Click += (sender, args) => ViewModel.Add();
            _searchButton.Click += (sender, args) => ViewModel.Search();

            _refreshMenuItem.Click += (sender, args) => ViewModel.RefreshItems();
            _settingsItem.Click += (sender, args) => ViewModel.OpenSettings();
            _reviewItem.Click += (sender, args) => ViewModel.Review();
            _aboutItem.Click += (sender, args) => ViewModel.About();
            _importContactsItem.Click += (o, e) => { };//ViewModel.ImportContactsAsync();

            Items.SelectionChanged += (sender, args) =>
            {
                if (ApplicationBar == null) return;

                var contacts = Items.SelectedItem as ContactsViewModel;

                if (contacts != null)
                {
                    _addContactButton.Text = AppResources.Add;
                    return;
                }

                var dialogs = Items.SelectedItem as DialogsViewModel;

                if (dialogs != null)
                {
                    _addContactButton.Text = AppResources.ComposeMessage;
                    return;
                }
            };

            Loaded += (sender, args) =>
            {
                //MessageBox.Show("ShellViewModel.Loaded");
#if WP81
                NavigationService.PauseOnBack = true;
#endif

                var result = RunAnimation((o, e) => ReturnItemsVisibility());
                if (!result)
                {
                    Execute.BeginOnUIThread(ReturnItemsVisibility);
                }

                ViewModel.OnAnimationComplete();
                if (!_firstRun)
                {
                    return;
                }

                _firstRun = false;
            };

#if WP81
            Telegram.Api.Helpers.Execute.BeginOnUIThread(TimeSpan.FromSeconds(2.0), async () =>
            {
                foreach (var backgroundTaskRegistration in Windows.ApplicationModel.Background.BackgroundTaskRegistration.AllTasks.Values)
                {
                    //Telegram.Logs.Log.Write("::Unregister background task " + backgroundTaskRegistration.Name);
                    backgroundTaskRegistration.Unregister(true);
                }

                var result = await Windows.ApplicationModel.Background.BackgroundExecutionManager.RequestAccessAsync();

                if (result == BackgroundAccessStatus.AllowedMayUseActiveRealTimeConnectivity)
                {
                    var builder = new BackgroundTaskBuilder();
                    builder.Name = Constants.PushNotificationsBackgroundTaskName;
                    builder.TaskEntryPoint = "TelegramClient.Tasks.PushNotificationsBackgroundTask";
                    builder.SetTrigger(new PushNotificationTrigger());
                    builder.Register();
                    //Telegram.Logs.Log.Write("::Register background task " + builder.Name);

                    var builder2 = new BackgroundTaskBuilder();
                    builder2.Name = Constants.MessageSchedulerBackgroundTaskName;
                    builder2.TaskEntryPoint = "TelegramClient.Tasks.MessageSchedulerBackgroundTask";
                    builder2.SetTrigger(new SystemTrigger(SystemTriggerType.InternetAvailable, false));
                    //builder2.AddCondition(new SystemCondition(SystemConditionType.InternetAvailable));
                    builder2.Register();
                    //Telegram.Logs.Log.Write("::Register background task " + builder2.Name);

                    var builder3 = new BackgroundTaskBuilder();
                    builder3.Name = Constants.TimerMessageSchedulerBackgroundTaskName;
                    builder3.TaskEntryPoint = "TelegramClient.Tasks.MessageSchedulerBackgroundTask";
                    builder3.SetTrigger(new TimeTrigger(15, false));
                    builder3.AddCondition(new SystemCondition(SystemConditionType.InternetAvailable));
                    builder3.Register();
                    //Telegram.Logs.Log.Write("::Register background task " + builder3.Name);

                    var builder4 = new BackgroundTaskBuilder();
                    builder4.Name = Constants.BackgroundDifferenceLoaderTaskName;
                    builder4.TaskEntryPoint = "TelegramClient.Tasks.BackgroundDifferenceLoader";
                    builder4.SetTrigger(new PushNotificationTrigger());
                    builder4.Register();
                    //Telegram.Logs.Log.Write("::Register background task " + builder4.Name);
                }
                else
                {
                    Telegram.Logs.Log.Write("::Background tasks are disabled result=" + result);
                    var messageBoxResult = MessageBox.Show(AppResources.BackgroudnTaskDisabledAlert, AppResources.Warning, MessageBoxButton.OKCancel);
                    if (messageBoxResult == MessageBoxResult.OK)
                    {
                        await Launcher.LaunchUriAsync(new Uri("ms-settings-power://"));
                    }
                }
            });
#endif

            BuildLocalizedAppBar();
        }

#if WP81
        private void OnBackgroundTaskCompleted(BackgroundTaskRegistration sender, BackgroundTaskCompletedEventArgs args)
        {
            
        }
#endif

        private void OptimizeFullHD()
        {
#if WP8
            var isFullHD = Application.Current.Host.Content.ScaleFactor == 225;
            //if (!isFullHD) return;
#endif            
            Items.HeaderTemplate = (DataTemplate)Application.Current.Resources["FullHDPivotHeaderTemplate"];
        }

        private void ReturnItemsVisibility()
        {
            foreach (ViewModelBase item in Items.Items)
            {
                item.Visibility = Visibility.Visible;
            }
        }

        private bool _isBackwardInAnimation;

        private bool RunAnimation(EventHandler callback)
        {
            if (_isBackwardInAnimation)
            {
                _isBackwardInAnimation = false;

                if (_nextUri != null 
                    && _nextUri.ToString().Contains("DialogDetailsView.xaml"))
                {
                    var storyboard = new Storyboard();

                    var translateAnimation = new DoubleAnimationUsingKeyFrames();
                    translateAnimation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.00), Value = 150.0 });
                    translateAnimation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.35), Value = 0.0, EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 6.0 } });
                    Storyboard.SetTarget(translateAnimation, LayoutRoot);
                    Storyboard.SetTargetProperty(translateAnimation, new PropertyPath("(UIElement.RenderTransform).(CompositeTransform.TranslateY)"));
                    storyboard.Children.Add(translateAnimation);

                    //LayoutRoot.Opacity = 1.0;
                    LayoutRoot.Opacity = 0.0;
                    var opacityAnimation = new DoubleAnimationUsingKeyFrames();
                    opacityAnimation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.00), Value = 1.0 });
                    opacityAnimation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.35), Value = 1.0, EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 6.0 } });
                    Storyboard.SetTarget(opacityAnimation, LayoutRoot);
                    Storyboard.SetTargetProperty(opacityAnimation, new PropertyPath("(UIElement.Opacity)"));
                    storyboard.Children.Add(opacityAnimation);
                    if (callback != null)
                    {
                        storyboard.Completed += callback;
                    }

                    Deployment.Current.Dispatcher.BeginInvoke(storyboard.Begin);
                    return true;
                }
                
                if (_nextUri != null 
                    && (_nextUri.ToString().EndsWith("AboutView.xaml") 
                        || _nextUri.ToString().EndsWith("SettingsView.xaml")))
                {
                    var storyboard = TelegramTurnstileAnimations.BackwardIn(LayoutRoot);
                    if (callback != null)
                    {
                        storyboard.Completed += callback;
                    }
                    //var rotationAnimation = new DoubleAnimationUsingKeyFrames();
                    //rotationAnimation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.00), Value = 105.0 });
                    //rotationAnimation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.35), Value = 0.0, EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 6.0 } });
                    //Storyboard.SetTarget(rotationAnimation, LayoutRoot);
                    //Storyboard.SetTargetProperty(rotationAnimation, new PropertyPath("(UIElement.Projection).(PlaneProjection.RotationY)"));
                    //storyboard.Children.Add(rotationAnimation);
                    //if (callback != null)
                    //{
                    //    storyboard.Completed += callback;
                    //}

                    LayoutRoot.Opacity = 1.0;

                    Deployment.Current.Dispatcher.BeginInvoke(storyboard.Begin);
                    return true;
                }
                else
                {
                    ((CompositeTransform)LayoutRoot.RenderTransform).TranslateY = 0.0;
                    LayoutRoot.Opacity = 1.0;
                    return false;
                }
            }
            else
            {
                ((CompositeTransform)LayoutRoot.RenderTransform).TranslateY = 0.0;
                LayoutRoot.Opacity = 1.0;
                return false;
            }

            return false;
        }

#if DEBUG
        private void GetCurrentPacketInfoItemOnClick(object sender, System.EventArgs eventArgs)
        {
            ViewModel.GetCurrentPacketInfo();
        }
#endif

        private void BuildLocalizedAppBar()
        {
            if (ApplicationBar != null) return;

            ApplicationBar = new ApplicationBar();
            
            ApplicationBar.Buttons.Add(_addContactButton);
            ApplicationBar.Buttons.Add(_searchButton);

            ApplicationBar.MenuItems.Add(_refreshMenuItem);
            ApplicationBar.MenuItems.Add(_settingsItem);
            ApplicationBar.MenuItems.Add(_reviewItem);
            ApplicationBar.MenuItems.Add(_aboutItem);
//#if DEBUG
//            ApplicationBar.MenuItems.Add(_importContactsItem);
//            ApplicationBar.MenuItems.Add(_openConfig);
//            ApplicationBar.MenuItems.Add(_getCurrentPacketInfoItem);
//            ApplicationBar.MenuItems.Add(_showRegistrationLogItem);
//#endif
        }

        private static readonly Uri ExternalUri = new Uri(@"app://external/");

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            if (e.Uri != ExternalUri)
            {
                var selectedIndex = Items.SelectedIndex;
                for (var i = 0; i < Items.Items.Count; i++)
                {
                    if (selectedIndex != i)
                    {
                        ((ViewModelBase)Items.Items[i]).Visibility = Visibility.Collapsed;
                    }
                }
            }

            base.OnNavigatedFrom(e);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {

            //MessageBox.Show("ShellViewModel.OnNavigatedTo");

            if (e.NavigationMode == NavigationMode.Back)
            {
                //if (e.EndsWith("DialogDetailsView.xaml"))
                {
                    //LayoutRoot.Opacity = 0.0;
                    _isBackwardInAnimation = true;
                }
            }

            if (e.NavigationMode == NavigationMode.New)
            {
                // share photo 
                string fileId;
                if (NavigationContext.QueryString.TryGetValue("FileId", out fileId))
                {
                    IoC.Get<IStateService>().FileId = fileId;
                    while (NavigationService.RemoveBackEntry() != null) { }
                }
            }

            if (_lastTapedItem != null)
            {
                var transform = _lastTapedItem.RenderTransform as CompositeTransform;
                if (transform != null)
                {
                    transform.TranslateX = 0.0;
                    transform.TranslateY = 0.0;
                }
                _lastTapedItem.Opacity = 1.0;
            }

            base.OnNavigatedTo(e);
        }

        private static FrameworkElement _lastTapedItem;
        private Uri _nextUri;

        public static Storyboard StartContinuumForwardOutAnimation(FrameworkElement tapedItem, FrameworkElement tapedItemContainer = null, bool saveLastTapedItem = true)
        {
            if (saveLastTapedItem)
            {
                _lastTapedItem = tapedItem;
                _lastTapedItem.CacheMode = new BitmapCache();
            }

            var storyboard = new Storyboard();

            var timeline = new DoubleAnimationUsingKeyFrames();
            timeline.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.0), Value = 0.0 });
            timeline.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.25), Value = 73.0 });
            Storyboard.SetTarget(timeline, tapedItem);
            Storyboard.SetTargetProperty(timeline, new PropertyPath("(UIElement.RenderTransform).(CompositeTransform.TranslateY)"));
            storyboard.Children.Add(timeline);

            var timeline2 = new DoubleAnimationUsingKeyFrames();
            timeline2.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.0), Value = 0.0 });
            timeline2.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.25), Value = 425.0, EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseIn, Exponent = 5.0 } });
            Storyboard.SetTarget(timeline2, tapedItem);
            Storyboard.SetTargetProperty(timeline2, new PropertyPath("(UIElement.RenderTransform).(CompositeTransform.TranslateX)"));
            storyboard.Children.Add(timeline2);

            var timeline3 = new DoubleAnimationUsingKeyFrames();
            timeline3.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.0), Value = 1.0 });
            timeline3.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.2), Value = 1.0 });
            timeline3.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.25), Value = 0.0 });
            Storyboard.SetTarget(timeline3, tapedItem);
            Storyboard.SetTargetProperty(timeline3, new PropertyPath("(UIElement.Opacity)"));
            storyboard.Children.Add(timeline3);

            if (tapedItemContainer != null)
            {
                var timeline4 = new ObjectAnimationUsingKeyFrames();
                timeline4.KeyFrames.Add(new DiscreteObjectKeyFrame { KeyTime = TimeSpan.FromSeconds(0.0), Value = 999.0 });
                timeline4.KeyFrames.Add(new DiscreteObjectKeyFrame { KeyTime = TimeSpan.FromSeconds(0.25), Value = 0.0 });
                Storyboard.SetTarget(timeline4, tapedItemContainer);
                Storyboard.SetTargetProperty(timeline4, new PropertyPath("(Canvas.ZIndex)"));
                storyboard.Children.Add(timeline4);
            }

            storyboard.Begin();

            return storyboard;
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            //var tempUri = HttpUtility.UrlDecode(e.Uri.ToString());

            //if (tempUri.Contains("encodedLaunchUri=tg://resolve"))
            //{
            //    e.Cancel = true;

            //    var uriParams = TelegramUriMapper.ParseQueryString(tempUri);
            //    if (tempUri.Contains("domain"))
            //    {
            //        IoC.Get<IStateService>().Domain = uriParams["domain"];
            //        //IoC.Get<IStateService>().RemoveBackEntries = true;
            //        //IoC.Get<IStateService>().NavigateToDialogDetails = true;
            //        NavigationService.Navigate(new Uri("/Views/Dialogs/DialogDetailsView.xaml", UriKind.Relative));
            //        return;
            //    }
            //}



            base.OnNavigatingFrom(e);

            


            _nextUri = e.Uri;

            if (!e.Cancel)
            {
                if (e.Uri.ToString().Contains("DialogDetailsView.xaml"))
                {
                    var storyboard = new Storyboard();

                    var translateAnimation = new DoubleAnimationUsingKeyFrames();
                    translateAnimation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.00), Value = 0.0 });
                    translateAnimation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.25), Value = 150.0, EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseIn, Exponent = 6.0 } });
                    Storyboard.SetTarget(translateAnimation, LayoutRoot);
                    Storyboard.SetTargetProperty(translateAnimation, new PropertyPath("(UIElement.RenderTransform).(CompositeTransform.TranslateY)"));
                    storyboard.Children.Add(translateAnimation);

                    var opacityAnimation = new DoubleAnimationUsingKeyFrames();
                    opacityAnimation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.00), Value = 1.0 });
                    opacityAnimation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.25), Value = 0.0, EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseIn, Exponent = 6.0 } });
                    Storyboard.SetTarget(opacityAnimation, LayoutRoot);
                    Storyboard.SetTargetProperty(opacityAnimation, new PropertyPath("(UIElement.Opacity)"));
                    storyboard.Children.Add(opacityAnimation);

                    storyboard.Begin();
                }
                else if (e.Uri.ToString().EndsWith("AboutView.xaml") 
                    || e.Uri.ToString().EndsWith("SettingsView.xaml"))
                {
                    var storyboard = TelegramTurnstileAnimations.ForwardOut(LayoutRoot);

                    //var translateAnimation = new DoubleAnimationUsingKeyFrames();
                    //translateAnimation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.00), Value = 0.0 });
                    //translateAnimation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.25), Value = 105.0, EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseIn, Exponent = 6.0 } });
                    //Storyboard.SetTarget(translateAnimation, LayoutRoot);
                    //Storyboard.SetTargetProperty(translateAnimation, new PropertyPath("(UIElement.Projection).(PlaneProjection.RotationY)"));
                    //storyboard.Children.Add(translateAnimation);

                    //var opacityAnimation = new DoubleAnimationUsingKeyFrames();
                    //opacityAnimation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.00), Value = 1.0 });
                    //opacityAnimation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.25), Value = 0.0, EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseIn, Exponent = 6.0 } });
                    //Storyboard.SetTarget(opacityAnimation, LayoutRoot);
                    //Storyboard.SetTargetProperty(opacityAnimation, new PropertyPath("(UIElement.Opacity)"));
                    //storyboard.Children.Add(opacityAnimation);

                    storyboard.Begin();
                }
                
            }
        }

        private void ImageBrush_OnImageOpened(object sender, RoutedEventArgs e)
        {
            PasscodeIcon.Opacity = 1.0;
        }

        private void FrameworkElement_OnLoaded(object sender, RoutedEventArgs e)
        {
            var textBlock = (TextBlock) sender;
            if (textBlock != null)
            {
                MessageBox.Show(textBlock.ActualHeight.ToString());
            }
        }
    }
}