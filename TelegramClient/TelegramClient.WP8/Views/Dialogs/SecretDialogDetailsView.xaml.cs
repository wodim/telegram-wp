using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using Caliburn.Micro;
using Microsoft.Phone.Shell;
using Telegram.Api.TL;
using Telegram.EmojiPanel.Controls.Emoji;
using TelegramClient.Helpers;
using TelegramClient.Resources;
using TelegramClient.ViewModels.Dialogs;
using TelegramClient.Views.Controls;
using TelegramClient.Views.Media;
using GestureEventArgs = System.Windows.Input.GestureEventArgs;

namespace TelegramClient.Views.Dialogs
{
    public partial class SecretDialogDetailsView
    {
        public SecretDialogDetailsViewModel ViewModel
        {
            get { return DataContext as SecretDialogDetailsViewModel; }
        }

        private readonly Stopwatch _timer;

        #region ApplicationBar
        private readonly AppBarButton _sendButton = new AppBarButton
        {
            Text = AppResources.Send,
            IconUri = new Uri("/Images/ApplicationBar/appbar.send.text.rest.png", UriKind.Relative)
        };

        private readonly AppBarButton _attachButton = new AppBarButton
        {
            Text = AppResources.Attach,
            IconUri = new Uri("/Images/ApplicationBar/appbar.attach.png", UriKind.Relative)
        };

        private readonly AppBarButton _smileButton = new AppBarButton
        {
            Text = AppResources.Emoji,
            //IsEnabled = false,
            IconUri = new Uri("/Images/ApplicationBar/appbar.smile.png", UriKind.Relative)
        };

        private readonly AppBarButton _manageButton = new AppBarButton
        {
            Text = AppResources.Manage,
            IsEnabled = true,
            IconUri = new Uri("/Images/ApplicationBar/appbar.manage.rest.png", UriKind.Relative)
        };

        private readonly AppBarButton _forwardButton = new AppBarButton
        {
            Text = AppResources.Forward,
            IsEnabled = true,
            IconUri = new Uri("/Images/ApplicationBar/appbar.forwardmessage.png", UriKind.Relative)
        };

        private readonly AppBarButton _deleteButton = new AppBarButton
        {
            Text = AppResources.Delete,
            IsEnabled = true,
            IconUri = new Uri("/Images/ApplicationBar/appbar.delete.png", UriKind.Relative)
        };

        private bool _firstRun = true;

        private void BuildLocalizedAppBar()
        {
            if (!_firstRun) return;
            _firstRun = false;

            ApplicationBar = new ApplicationBar();
            ApplicationBar.Opacity = 0.99;

            ApplicationBar.Buttons.Add(_sendButton);
            ApplicationBar.Buttons.Add(_attachButton);
            ApplicationBar.Buttons.Add(_smileButton);
            ApplicationBar.Buttons.Add(_manageButton);

            _sendButton.IsEnabled = ViewModel.CanSend;
            ApplicationBar.IsVisible = ViewModel.IsApplicationBarVisible && !ViewModel.IsChooseAttachmentOpen;
        }
        #endregion

        private EmojiControl _emojiKeyboard;

        private TranslateTransform _frameTransform;

        public static readonly DependencyProperty RootFrameTransformProperty = DependencyProperty.Register(
            "RootFrameTransformProperty", typeof(double), typeof(SecretDialogDetailsView), new PropertyMetadata(OnRootFrameTransformChanged));

        private static void OnRootFrameTransformChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var view = d as SecretDialogDetailsView;
            if (view != null)
            {
                view._frameTransform.Y = 0;
            }
        }

        public double RootFrameTransform
        {
            get { return (double)GetValue(RootFrameTransformProperty); }
            set { SetValue(RootFrameTransformProperty, value); }
        }

        private void SetRootFrameBinding()
        {
            var frame = (Frame)Application.Current.RootVisual;
            _frameTransform = ((TranslateTransform)((TransformGroup)frame.RenderTransform).Children[0]);
            var binding = new Binding("Y")
            {
                Source = _frameTransform
            };
            SetBinding(RootFrameTransformProperty, binding);
        }

        private void RemoveRootFrameBinding()
        {
            ClearValue(RootFrameTransformProperty);
        }

        public SecretDialogDetailsView()
        {
            _timer = System.Diagnostics.Stopwatch.StartNew();

            InitializeComponent();

            //Full HD
            OptimizeFullHD();

            _sendButton.Click += (sender, args) => ViewModel.Send();
            _attachButton.Click += (sender, args) =>
            {
                EmojiPlaceholder.Visibility = Visibility.Collapsed;
                AudioRecorder.Visibility = GetAudioRecorderVisibility();    //Visibility.Visible;
                Title.Visibility = Visibility.Visible;
                DialogPhoto.Visibility = Visibility.Visible;

                Self.Focus();
                ViewModel.Attach();
            };
            _manageButton.Click += (sender, args) => SwitchToSelectionMode();
            _deleteButton.Click += (sender, args) =>
            {
                ViewModel.DeleteMessages();
                SwitchToNormalMode();
            };
            _smileButton.Click += (sender, args) =>
            {
                if (_emojiKeyboard == null)
                {
                    _emojiKeyboard = EmojiControl.GetInstance();

                    _emojiKeyboard.BindTextBox(InputMessage, true);
                    _emojiKeyboard.StickerSelected += OnStickerSelected;
                    EmojiPlaceholder.Content = _emojiKeyboard;
                    _emojiKeyboard.IsOpen = true;
                }

                if (EmojiPlaceholder.Visibility == Visibility.Visible)
                {
                    if (InputMessage == FocusManager.GetFocusedElement())
                    {
                        Items.Focus();
                        _smileButtonPressed = true;
                        EmojiPlaceholder.Opacity = 1.0;
                        EmojiPlaceholder.Height = EmojiControl.PortraitOrientationHeight;
                    }
                    else
                    {
                        EmojiPlaceholder.Visibility = Visibility.Collapsed;
                        AudioRecorder.Visibility = GetAudioRecorderVisibility();    //Visibility.Visible;
                        Title.Visibility = Visibility.Visible;
                        DialogPhoto.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    var awaitKeyboardDown = false;
                    if (InputMessage == FocusManager.GetFocusedElement())
                    {
                        awaitKeyboardDown = true;
                        Items.Focus();
                    }

                    Telegram.Api.Helpers.Execute.BeginOnThreadPool(() =>
                    {
                        if (awaitKeyboardDown)
                        {
                            Thread.Sleep(400);
                        }
                        Telegram.Api.Helpers.Execute.BeginOnUIThread(() =>
                        {
                            EmojiPlaceholder.Visibility = Visibility.Visible;
                            AudioRecorder.Visibility = Visibility.Collapsed;
                            Title.Visibility = Visibility.Collapsed;
                            DialogPhoto.Visibility = Visibility.Collapsed;
                        });
                    });
                }
            };

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OptimizeFullHD()
        {
            var appBar = new ApplicationBar();
            var appBarDefaultSize = appBar.DefaultSize;

            WaitingBar.Height = appBarDefaultSize;
        }

        private void SwitchToSelectionMode()
        {
            Items.Focus();
            ViewModel.IsSelectionEnabled = true;
            EmojiPlaceholder.Visibility = Visibility.Collapsed;
            DialogPhoto.Visibility = Visibility.Visible;
            Title.Visibility = Visibility.Visible;

            ApplicationBar.Buttons.Clear();
            ApplicationBar.Buttons.Add(_deleteButton);

            var isGroupActionEnabled = ViewModel.IsGroupActionEnabled;

            _deleteButton.IsEnabled = isGroupActionEnabled;
        }

        private void SwitchToNormalMode()
        {
            ViewModel.IsSelectionEnabled = false;

            ApplicationBar.Buttons.Clear();

            ApplicationBar.Buttons.Clear();
            ApplicationBar.Buttons.Add(_sendButton);
            ApplicationBar.Buttons.Add(_attachButton);
            ApplicationBar.Buttons.Add(_smileButton);
            ApplicationBar.Buttons.Add(_manageButton);
        }

        public static readonly DependencyProperty StopwatchProperty =
            DependencyProperty.Register("Stopwatch", typeof(string), typeof(SecretDialogDetailsView), new PropertyMetadata(default(string)));

        public string Stopwatch
        {
            get { return (string)GetValue(StopwatchProperty); }
            set { SetValue(StopwatchProperty, value); }
        }

        private void OnLoaded(object sender, RoutedEventArgs routedEventArgs)
        {
            if (!ViewModel.StateService.IsEmptyBackground)
            {
                var color = Colors.White;
                color.A = 254;
                SystemTray.ForegroundColor = color;
            }

            SetRootFrameBinding();

            AudioRecorder.AudioRecorded += OnAudioRecorded;
            AudioRecorder.RecordStarted += OnRecordStarted;
            AudioRecorder.RecordCanceled += OnRecordCanceled;

            ViewModel.PropertyChanged += OnViewModelPropertyChanged;
            ViewModel.ScrollToBottom += OnViewModelScrollToBottom;

            if (ViewModel.IsApplicationBarVisible)
            {
                BuildLocalizedAppBar();
            }
            else if (ApplicationBar != null)
            {
                ApplicationBar.IsVisible = ViewModel.IsApplicationBarVisible && !ViewModel.IsChooseAttachmentOpen;
            }

            RunAnimation();
            Stopwatch = _timer.Elapsed.ToString();
        }

        private void OnViewModelScrollToBottom(object sender, System.EventArgs e)
        {
            if (ViewModel.Items.Count > 0)
            {
                Items.ScrollToItem(ViewModel.Items[0]);
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            RemoveRootFrameBinding();

            AudioRecorder.AudioRecorded -= OnAudioRecorded;
            AudioRecorder.RecordStarted -= OnRecordStarted;
            AudioRecorder.RecordCanceled -= OnRecordCanceled;

            ViewModel.ScrollToBottom -= OnViewModelScrollToBottom;
            ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        private Binding _visibilityBinding;

        private double _previousAudioRecorderMinHeight;

        private static Binding SaveVisibilityBinding(FrameworkElement element)
        {
            var visibilityExpression = element.GetBindingExpression(VisibilityProperty);
            if (visibilityExpression != null)
            {
                return visibilityExpression.ParentBinding;
            }

            return null;
        }

        private static void RestoreVisibilityBinding(FrameworkElement element, Binding binding, Visibility defaultValue)
        {
            if (binding != null)
            {
                element.SetBinding(VisibilityProperty, binding);
            }
            else
            {
                element.Visibility = defaultValue;
            }
        }

        private void OnRecordCanceled(object sender, System.EventArgs e)
        {
            AudioRecorder.MinHeight = _previousAudioRecorderMinHeight;
            RestoreVisibilityBinding(InputMessage, _visibilityBinding, Visibility.Visible);
        }

        private void OnRecordStarted(object sender, System.EventArgs e)
        {
            _visibilityBinding = SaveVisibilityBinding(InputMessage);
            _previousAudioRecorderMinHeight = AudioRecorder.MinHeight;

            AudioRecorder.MinHeight = InputMessage.ActualHeight;
            InputMessage.Visibility = Visibility.Collapsed;
        }

        private void OnAudioRecorded(object sender, AudioEventArgs e)
        {
            AudioRecorder.MinHeight = _previousAudioRecorderMinHeight;
            RestoreVisibilityBinding(InputMessage, _visibilityBinding, Visibility.Visible);

            // чтобы быстро обновить Visibility InputMessage переносим все остальное в фон
            var viewModel = ViewModel;
            Telegram.Api.Helpers.Execute.BeginOnThreadPool(() => viewModel.SendAudio(e));
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (Property.NameEquals(e.PropertyName, () => ViewModel.IsGroupActionEnabled))
            {
                var isGroupActionEnabled = ViewModel.IsGroupActionEnabled;

                _deleteButton.IsEnabled = isGroupActionEnabled;
            }
            else if (Property.NameEquals(e.PropertyName, () => ViewModel.IsApplicationBarVisible)
                && ViewModel.IsApplicationBarVisible)
            {
                BuildLocalizedAppBar();
            }
            else if (Property.NameEquals(e.PropertyName, () => ViewModel.ChooseAttachment)
               && ViewModel.ChooseAttachment != null)
            {
                ViewModel.ChooseAttachment.PropertyChanged += OnChooseAttachmentPropertyChanged;
            }
            else if (Property.NameEquals(e.PropertyName, () => ViewModel.ImageViewer)
               && ViewModel.ImageViewer != null)
            {
                ViewModel.ImageViewer.PropertyChanged += OnImageViewerPropertyChanged;
            }
            else if (Property.NameEquals(e.PropertyName, () => ViewModel.IsApplicationBarVisible))
            {
                ApplicationBar.IsVisible = ViewModel.IsApplicationBarVisible && !ViewModel.IsChooseAttachmentOpen;
            }
            else if (Property.NameEquals(e.PropertyName, () => ViewModel.CanSend))
            {
                _sendButton.IsEnabled = ViewModel.CanSend;
            }
        }

        private IApplicationBar _prevApplicationBar;

        private void OnImageViewerPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (Property.NameEquals(e.PropertyName, () => ViewModel.ImageViewer.IsOpen))
            {
                Items.IsHitTestVisible = !ViewModel.ChooseAttachment.IsOpen && (ViewModel.ImageViewer == null || !ViewModel.ImageViewer.IsOpen);

                if (ViewModel.ImageViewer != null
                    && ViewModel.ImageViewer.IsOpen)
                {
                    _prevApplicationBar = ApplicationBar;
                    ApplicationBar = ((DecryptedImageViewerView)ImageViewer.Content).ApplicationBar;
                }
                else
                {
                    if (_prevApplicationBar != null)
                    {
                        ApplicationBar = _prevApplicationBar;
                    }
                }
            }
        }

        private void OnChooseAttachmentPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (Property.NameEquals(e.PropertyName, () => ViewModel.ChooseAttachment.IsOpen))
            {
                Items.IsHitTestVisible = !ViewModel.ChooseAttachment.IsOpen && (ViewModel.ImageViewer == null || !ViewModel.ImageViewer.IsOpen);
            }
        }

        private bool _isForwardInAnimation;
        private bool _isBackwardInAnimation;
        private bool _fromExternalUri;
        private readonly Uri _externalUri = new Uri(@"app://external/");

        private void RunAnimation(Uri uri = null)
        {
            if (_isForwardInAnimation)
            {
                _isForwardInAnimation = false;

                if (ViewModel.Chat.Bitmap != null)
                {
                    Items.Visibility = Visibility.Collapsed;
                    Items.IsHitTestVisible = false;
                }

                var storyboard = new Storyboard();
                if (ViewModel != null
                    && ViewModel.StateService.AnimateTitle)
                {
                    ViewModel.StateService.AnimateTitle = false;

                    var continuumElementX = new DoubleAnimationUsingKeyFrames();
                    continuumElementX.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.0), Value = 130.0 });
                    continuumElementX.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.25), Value = 0.0, EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 3.0 } });
                    Storyboard.SetTarget(continuumElementX, Title);
                    Storyboard.SetTargetProperty(continuumElementX, new PropertyPath("(UIElement.RenderTransform).(CompositeTransform.TranslateX)"));
                    storyboard.Children.Add(continuumElementX);

                    var continuumElementY = new DoubleAnimationUsingKeyFrames();
                    continuumElementY.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.0), Value = -40.0 });
                    continuumElementY.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.25), Value = 0.0 });
                    Storyboard.SetTarget(continuumElementY, Title);
                    Storyboard.SetTargetProperty(continuumElementY, new PropertyPath("(UIElement.RenderTransform).(CompositeTransform.TranslateY)"));
                    storyboard.Children.Add(continuumElementY);
                }

                var continuumLayoutRootY = new DoubleAnimationUsingKeyFrames();
                continuumLayoutRootY.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.0), Value = 150.0 });
                continuumLayoutRootY.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.25), Value = 0.0, EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 3.0 } });
                Storyboard.SetTarget(continuumLayoutRootY, LayoutRoot);
                Storyboard.SetTargetProperty(continuumLayoutRootY, new PropertyPath("(UIElement.RenderTransform).(CompositeTransform.TranslateY)"));
                storyboard.Children.Add(continuumLayoutRootY);

                var continuumLayoutRootOpacity = new DoubleAnimation { From = 0.0, To = 1.0, Duration = TimeSpan.FromSeconds(0.25), EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 6.0 } };
                Storyboard.SetTarget(continuumLayoutRootOpacity, LayoutRoot);
                Storyboard.SetTargetProperty(continuumLayoutRootOpacity, new PropertyPath("(UIElement.Opacity)"));
                storyboard.Children.Add(continuumLayoutRootOpacity);

                Deployment.Current.Dispatcher.BeginInvoke(() =>
                {
                    LayoutRoot.Opacity = 1.0;
                    storyboard.Completed += (o, e) =>
                    {

                        //Items.Opacity = 0.0;
                        Items.Visibility = Visibility.Visible;
                        Items.IsHitTestVisible = true;
                        MessagesCache.Visibility = Visibility.Collapsed;
                        ViewModel.OnForwardInAnimationComplete();
                        //Deployment.Current.Dispatcher.BeginInvoke(() => CloseCacheStoryboard.Begin());
                    };
                    storyboard.Begin();
                });
            }
            else if (_isBackwardOutAnimation)
            {
                _isBackwardOutAnimation = false;

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
        }

        private bool _suppressCancel = true;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ViewModel.OnNavigatedTo();
            MediaControl.Content = MessagePlayerControl.Player;

            // этот код выполняется до того, как происходит отрисовка экрана
            // нельзя ставить сюда долгие операции
            if (e.NavigationMode == NavigationMode.New)
            {
                LayoutRoot.Opacity = 0.0;
                _isForwardInAnimation = true;
            }
            else if (e.NavigationMode == NavigationMode.Back)
            {
                if (_fromExternalUri)
                {
                    ThreadPool.QueueUserWorkItem(state =>
                    {
                        Thread.Sleep(500);
                        Deployment.Current.Dispatcher.BeginInvoke(() => ViewModel.OnBackwardInAnimationComplete());
                    });
                }
                else
                {
                    _isBackwardInAnimation = true;
                }
                _fromExternalUri = false;
            }

            base.OnNavigatedTo(e);
        }

        private void OnStickerSelected(object sender, StickerSelectedEventArgs e)
        {
            if (e.Sticker == null) return;

            var document22 = e.Sticker.Document as TLDocument22;
            if (document22 == null) return;

            ViewModel.SendSticker(document22);
        }

        private bool _isBackwardOutAnimation;
        private bool _isForwardOutAnimation;

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            if (_emojiKeyboard != null)
            {
                // Destroy EmojiControl
                _emojiKeyboard.IsOpen = false;
                _emojiKeyboard.UnbindTextBox();
                _emojiKeyboard.StickerSelected -= OnStickerSelected;
                EmojiPlaceholder.Content = null; // Remove from view
                EmojiPlaceholder.Visibility = Visibility.Collapsed;
                AudioRecorder.Visibility = GetAudioRecorderVisibility();    //Visibility.Visible;
                Title.Visibility = Visibility.Visible;
                DialogPhoto.Visibility = Visibility.Visible;
                _emojiKeyboard = null;
            }

            base.OnNavigatingFrom(e);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel.OnNavigatedFrom();

            MediaControl.Content = null;
            MessagePlayerControl.Stop();

            if (e.Uri == _externalUri)
            {
                _fromExternalUri = true;
            }
            else
            {
                _fromExternalUri = false;
            }

            base.OnNavigatedFrom(e);
        }

        protected override void OnBackKeyPress(CancelEventArgs e)
        {
            if (!NavigationService.BackStack.Any())
            {
                e.Cancel = true;
                ViewModel.NavigateToShellViewModel();

                return;
            }

            base.OnBackKeyPress(e);
        }

        private void SecretDialogDetailsView_OnBackKeyPress(object sender, CancelEventArgs e)
        {
            if (ViewModel.ImageViewer != null
                && ViewModel.ImageViewer.IsOpen)
            {
                ViewModel.ImageViewer.CloseViewer();
                e.Cancel = true;

                return;
            }

            if (ViewModel.IsSelectionEnabled)
            {
                SwitchToNormalMode();
                e.Cancel = true;

                return;
            }

            if (EmojiPlaceholder.Visibility == Visibility.Visible)
            {
                EmojiPlaceholder.Visibility = Visibility.Collapsed;
                AudioRecorder.Visibility = GetAudioRecorderVisibility();    //Visibility.Visible;
                Title.Visibility = Visibility.Visible;
                DialogPhoto.Visibility = Visibility.Visible;
                e.Cancel = true;
                return;
            }

            if (ViewModel.ChooseAttachment != null
                && ViewModel.ChooseAttachment.IsOpen)
            {
                e.Cancel = true;
                ViewModel.ChooseAttachment.Close();
                return;
            }

            _isBackwardOutAnimation = true;

            try
            {
                if (Items.Visibility == Visibility.Visible)
                {
                    var writeableBitmap = new WriteableBitmap(Items, null);
                    ViewModel.Chat.SetBitmap(writeableBitmap);
                }
            }
            catch (Exception ex)
            {
                Telegram.Api.Helpers.Execute.ShowDebugMessage("WritableBitmap exception " + ex);
            }
            MessagesCache.Visibility = Visibility.Visible;
            Items.Visibility = Visibility.Collapsed;

            RunAnimation();
        }

        private void InputMessage_OnGotFocus(object sender, RoutedEventArgs e)
        {
            AudioRecorder.Visibility = GetAudioRecorderVisibility();    // Visibility.Collapsed;
            Title.Visibility = Visibility.Collapsed;
            DialogPhoto.Visibility = Visibility.Collapsed;

            EmojiPlaceholder.Visibility = Visibility.Visible;
            EmojiPlaceholder.Opacity = 0.0;
            EmojiPlaceholder.Height = EmojiControl.PortraitOrientationHeight;
            var storyboard = new Storyboard();
            var opacityAnimation = new DoubleAnimationUsingKeyFrames();
            opacityAnimation.KeyFrames.Add(new DiscreteDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.0), Value = 0.0 });
            opacityAnimation.KeyFrames.Add(new DiscreteDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.75), Value = 1.0 });
            Storyboard.SetTarget(opacityAnimation, EmojiPlaceholder);
            Storyboard.SetTargetProperty(opacityAnimation, new PropertyPath("Opacity"));
            storyboard.Children.Add(opacityAnimation);
            storyboard.Begin();

            if (ViewModel.Items.Count == 0)
            {
                Description.Visibility = Visibility.Collapsed;
            }
            //WaitingBar.Visibility = Visibility.Collapsed;
        }

        private bool _smileButtonPressed;

        private void InputMessage_OnLostFocus(object sender, RoutedEventArgs e)
        {
            if (!_smileButtonPressed)
            {
                if (EmojiPlaceholder.Visibility == Visibility.Visible)
                {
                    EmojiPlaceholder.Visibility = Visibility.Collapsed;
                }
            }
            _smileButtonPressed = false;

            if (EmojiPlaceholder.Visibility == Visibility.Collapsed)
            {
                AudioRecorder.Visibility = GetAudioRecorderVisibility(); //Visibility.Visible;
                DialogPhoto.Visibility = Visibility.Visible;
                Title.Visibility = Visibility.Visible;
            }

            if (ViewModel.Items.Count == 0)
            {
                Description.Visibility = Visibility.Visible;
            }
            //WaitingBar.Visibility = Visibility.Visible;
        }

        private void NavigationTransition_OnEndTransition(object sender, RoutedEventArgs e)
        {
            if (_isBackwardInAnimation)
            {
                _isBackwardInAnimation = false;

                Items.Visibility = Visibility.Visible;
                MessagesCache.Visibility = Visibility.Collapsed;
                ViewModel.OnBackwardInAnimationComplete();
            }
        }

        private void UIElement_OnHold(object sender, GestureEventArgs e)
        {

        }

        private void InputMessage_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (ViewModel.StateService.SendByEnter)
                {
                    ViewModel.Send();
                    e.Handled = true;
                }
            }
        }

        private void NavigationOutTransition_OnEndTransition(object sender, RoutedEventArgs e)
        {
            //Items.Visibility = Visibility.Collapsed;
        }

        private void SecretPhotoPlaceholder_OnElapsed(object sender, System.EventArgs e)
        {
            var control = sender as FrameworkElement;
            if (control == null) return;
            ViewModel.DeleteMessage(control.DataContext as TLDecryptedMessageMediaPhoto);
            SecretImageViewer.Visibility = Visibility.Collapsed;
            //MessageBox.Show("Elapsed");
        }

        private void SecretPhotoPlaceholder_OnStartTimer(object sender, System.EventArgs e)
        {
            var uielement = sender as FrameworkElement;
            if (uielement != null)
            {
                var decryptedMessage = uielement.DataContext as TLDecryptedMessageMediaPhoto;
                if (decryptedMessage != null)
                {
                    var result = ViewModel.OpenSecretPhoto(decryptedMessage);
                    if (result)
                    {
                        SecretImageViewer.Visibility = Visibility.Visible;
                        ApplicationBar.IsVisible = false;
                    }
                }
            }
        }

        private void UIElement_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
//            var uielement = sender as FrameworkElement;
//            if (uielement != null)
//            {
//                var decryptedMessage = uielement.DataContext as TLDecryptedMessage17;
//                if (decryptedMessage != null)
//                {
//                    var result = ViewModel.OpenSecretPhoto(decryptedMessage);
//                    if (result)
//                    {
//                        SecretImageViewer.Visibility = Visibility.Visible;
//                        ApplicationBar.IsVisible = false;
//                    }
//                }
//            }
        }

        private void SecretImageViewer_OnMouseLeave(object sender, MouseEventArgs e)
        {
            SecretImageViewer.Visibility = Visibility.Collapsed;
            ApplicationBar.IsVisible = true;
        }

        private void SecretImageViewer_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            SecretImageViewer.Visibility = Visibility.Collapsed;
            ApplicationBar.IsVisible = true;
        }

        private void DeleteMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element == null) return;

            ViewModel.DeleteMessage(element.DataContext as TLDecryptedMessage);
        }

        private void CopyMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element == null) return;

            ViewModel.CopyMessage(element.DataContext as TLDecryptedMessage);
        }

        private void Items_OnManipulationStarted(object sender, ManipulationStartedEventArgs e)
        {
            if (ViewModel.SliceLoaded) return;

            ViewModel.LoadNextSlice();
        }

        private Visibility GetAudioRecorderVisibility()
        {
            if (FocusManager.GetFocusedElement() == InputMessage)
            {
                return Visibility.Collapsed;
            }

            if (InputMessage.Text.Length > 0)
            {
                return Visibility.Collapsed;
            }

            if (EmojiPlaceholder.Visibility == Visibility.Visible)
            {
                return Visibility.Collapsed;
            }

            //if (ViewModel != null)
            //{
            //    var chatForbidden = ViewModel.With as TLChatForbidden;
            //    var chat = ViewModel.With as TLChat;

            //    var isForbidden = chatForbidden != null || (chat != null && chat.Left.Value);
            //    if (isForbidden)
            //    {
            //        return Visibility.Collapsed;
            //    }
            //}

            return Visibility.Visible;
        }

        private void InputMessage_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            AudioRecorder.Visibility = GetAudioRecorderVisibility();
        }

        private void MorePanel_OnTap(object sender, GestureEventArgs e)
        {
            var frameworkElement = sender as FrameworkElement;
            if (frameworkElement == null) return;

            var message = frameworkElement.DataContext as TLDecryptedMessage;
            if (message == null) return;

            if (EmojiPlaceholder.Visibility == Visibility.Visible)
            {
                EmojiPlaceholder.Visibility = Visibility.Collapsed;
                AudioRecorder.Visibility = GetAudioRecorderVisibility(); //Visibility.Visible;
                DialogPhoto.Visibility = Visibility.Visible;
                Title.Visibility = Visibility.Visible;
            }

            // чтобы клавиатура успела опуститься
            Telegram.Api.Helpers.Execute.BeginOnUIThread(() =>
            {
                //try
                //{
                //    if (Items.Visibility == Visibility.Visible)
                //    {
                //        var writeableBitmap = new WriteableBitmap(Items, null);
                //        ViewModel.With.Bitmap = writeableBitmap;
                //    }
                //}
                //catch (Exception ex)
                //{
                //    Telegram.Api.Helpers.Execute.ShowDebugMessage("WritableBitmap exception " + ex);
                //}

                //MessagesCache.Visibility = Visibility.Visible;
                //Items.Visibility = Visibility.Collapsed;

                ViewModel.OpenCropedMessage(message);
            });
        }
    }
}