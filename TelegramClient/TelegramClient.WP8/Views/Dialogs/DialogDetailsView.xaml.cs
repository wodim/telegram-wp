using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Threading;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Caliburn.Micro;
using Clarity.Phone.Extensions;
using Telegram.Api.Services;
using Telegram.EmojiPanel.Controls.Emoji;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using Telegram.Api.TL;
using TelegramClient.Controls;
using TelegramClient.Converters;
using TelegramClient.Helpers;
using TelegramClient.Resources;
using TelegramClient.Utils;
using TelegramClient.ViewModels.Dialogs;
using TelegramClient.Views.Controls;
using TelegramClient.Views.Media;
using AudioEventArgs = TelegramClient.Views.Controls.AudioEventArgs;
using GestureEventArgs = System.Windows.Input.GestureEventArgs;

namespace TelegramClient.Views.Dialogs
{
    public partial class DialogDetailsView
    {
        private DialogDetailsViewModel ViewModel
        {
            get { return DataContext as DialogDetailsViewModel; }
        }

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

        private readonly ApplicationBarMenuItem _searchMenuItem = new ApplicationBarMenuItem
        {
            Text = AppResources.Search,
        };

        private readonly ApplicationBarMenuItem _pinToStartMenuItem = new ApplicationBarMenuItem
        {
            Text = AppResources.PinToStart
        };

        private readonly ApplicationBarMenuItem _shareMyContactInfoMenuItem = new ApplicationBarMenuItem
        {
            Text = AppResources.ShareMyContactInfo
        };

        private readonly ApplicationBarMenuItem _helpMenuItem = new ApplicationBarMenuItem
        {
            Text = AppResources.Help
        };

        private readonly ApplicationBarMenuItem _reportSpamMenuItem = new ApplicationBarMenuItem
        {
            Text = AppResources.ReportSpam
        };

        private readonly ApplicationBarMenuItem _debugMenuItem = new ApplicationBarMenuItem
        {
            Text = "debug"
        };

        private EmojiControl _emojiKeyboard;

        private TranslateTransform _frameTransform;

        public static readonly DependencyProperty RootFrameTransformProperty = DependencyProperty.Register(
            "RootFrameTransformProperty", typeof(double), typeof(DialogDetailsView), new PropertyMetadata(OnRootFrameTransformChanged));

        private static void OnRootFrameTransformChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var view = d as DialogDetailsView;
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

        private void CloseEmojiPlaceholder()
        {
            EmojiPlaceholder.Visibility = Visibility.Collapsed;

            if (_emojiKeyboard != null)
            {
                _emojiKeyboard.ReloadStickerSprites();
            }
        }

        private void OpenCommandsPlaceholder()
        {
            ReplyKeyboardButtonImage.Source = new BitmapImage(new Uri("/Images/Dialogs/chat.keyboard-WXGA.png", UriKind.Relative));
            CommandsControl.Visibility = Visibility.Visible;
        }

        private void CloseCommandsPlaceholder()
        {
            ReplyKeyboardButtonImage.Source = ViewModel.ReplyMarkup is TLReplyKeyboardMarkup ? 
                new BitmapImage(new Uri("/Images/Dialogs/chat.customkeyboard-WXGA.png", UriKind.Relative)) : 
                new BitmapImage(new Uri("/Images/Dialogs/chat.commands-WXGA.png", UriKind.Relative));

            CommandsControl.Visibility = Visibility.Collapsed;
        }

        private ImageSource GetReplyKeyboardImageSource()
        {
            if (ViewModel != null)
            {
                var replyMarkup = ViewModel.ReplyMarkup as TLReplyKeyboardMarkup;
                if (replyMarkup != null)
                {
                    if (FocusManager.GetFocusedElement() == InputMessage
                        || EmojiPlaceholder.Visibility == Visibility.Visible)
                    {
                        return new BitmapImage(new Uri("/Images/Dialogs/chat.customkeyboard-WXGA.png", UriKind.Relative));
                    }
                    else
                    {
                        return new BitmapImage(new Uri("/Images/Dialogs/chat.keyboard-WXGA.png", UriKind.Relative));
                    }
                }

                if (ViewModel.HasBots)
                {
                    return new BitmapImage(new Uri("/Images/Dialogs/chat.commands-WXGA.png", UriKind.Relative));
                }
            }

            return null;
        }

        public DialogDetailsView()
        {
            //var stopwatch = Stopwatch.StartNew();
            //Loaded += (sender, args) =>
            //{
            //    MessageBox.Show("elapsed \n" + stopwatch.Elapsed);
            //};

            InitializeComponent();

            //Full HD
            OptimizeFullHD();

            _sendButton.Click += (sender, args) =>
            {
                //_sendButton.IsEnabled = false;
                ViewModel.Send();
            };
            _attachButton.Click += (sender, args) =>
            {
                CloseEmojiPlaceholder();
                CloseCommandsPlaceholder();
                AudioRecorder.Visibility = GetAudioRecorderVisibility(); //Visibility.Visible;
                DialogPhoto.Visibility = Visibility.Visible;
                Title.Visibility = Visibility.Visible;
                ChooseAttachment.Focus();
                ViewModel.Attach();
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
                        _smileButtonPressed = true;
                        EmojiPlaceholder.Opacity = 1.0;
                        EmojiPlaceholder.Height = EmojiControl.PortraitOrientationHeight;

                        if (_emojiKeyboard != null)
                        {
                            _emojiKeyboard.OpenStickerSprites();
                        }

                        Items.Focus();
                    }
                    else
                    {
                        CloseCommandsPlaceholder();
                        CloseEmojiPlaceholder();
                        AudioRecorder.Visibility = GetAudioRecorderVisibility(); //Visibility.Visible;
                        DialogPhoto.Visibility = Visibility.Visible;
                        Title.Visibility = Visibility.Visible;
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
                            EmojiPlaceholder.Opacity = 1.0;
                            if (_emojiKeyboard != null)
                            {
                                _emojiKeyboard.OpenStickerSprites();
                            }

                            ReplyKeyboardButtonImage.Source = GetReplyKeyboardImageSource();
                            AudioRecorder.Visibility = GetAudioRecorderVisibility(); //Visibility.Collapsed;
                            DialogPhoto.Visibility = Visibility.Collapsed;
                            Title.Visibility = Visibility.Collapsed;
                        });
                    });
                }
            };

            _manageButton.Click += (sender, args) => ViewModel.IsSelectionEnabled = true;
            _forwardButton.Click += (sender, args) =>
            {
                var selectedItems = ViewModel.Items.Where(x => x.Index > 0 && x.IsSelected).ToList();
                if (selectedItems.Count == 0) return;

                ViewModel.IsSelectionEnabled = false;

                Telegram.Api.Helpers.Execute.BeginOnUIThread(TimeSpan.FromSeconds(0.55), () =>  // waiting to complete animation
                {
                    CreateBitmapCache();
                    Items.Visibility = Visibility.Collapsed;
                    MessagesCache.Visibility = Visibility.Visible;
                    ViewModel.ForwardMessages(selectedItems);
                });
            };
            _deleteButton.Click += (sender, args) => ViewModel.DeleteMessages();

            _pinToStartMenuItem.Click += (sender, args) => ViewModel.PinToStart();

            _shareMyContactInfoMenuItem.Click += (sender, args) => ViewModel.InvokeUserAction();
            _debugMenuItem.Click += Debug_OnClick;
            _helpMenuItem.Click += Help_OnClick;
            _searchMenuItem.Click += (sender, args) => ViewModel.Search();
            _reportSpamMenuItem.Click += (sender, args) => ViewModel.ReportSpam();

            Loaded += (sender, args) =>
            {
                //MessageBox.Show(string.Format("w{0} h{1} {2}", MessagesCache.ActualWidth, MessagesCache.ActualHeight, MessagesCache.Stretch));
#if LOG_NAVIGATION
                TLUtils.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + "DV Loaded", LogSeverity.Error);
#endif
                if (!ViewModel.StateService.IsEmptyBackground)
                {
                    var color = Colors.White;
                    color.A = 254;
                    SystemTray.ForegroundColor = color;
                }


                //TLUtils.WriteLog("DV Loaded");
                //ViewModel.OnNavigatedTo();
                AudioRecorder.Visibility = GetAudioRecorderVisibility(); 
                ReplyKeyboardButton.Visibility = GetReplyKeyboardButtonVisibility();
                ReplyKeyboardButtonImage.Source = GetReplyKeyboardImageSource();

                SetRootFrameBinding();

                if (ViewModel.With is TLBroadcastChat)
                {
                    _forwardButton.IsEnabled = false;
                }

                RunAnimation();

                Telegram.Api.Helpers.Execute.BeginOnUIThread(TimeSpan.FromSeconds(1.0), () =>
                {
                    if (ViewModel.StateService.FocusOnInputMessage)
                    {
                        ViewModel.StateService.FocusOnInputMessage = false;
                        if (!ViewModel.IsAppBarCommandVisible)
                        {
                            InputMessage.Focus();
                        }
                    }
                });

                if (ViewModel.ChooseAttachment != null)
                {
                    ViewModel.ChooseAttachment.PropertyChanged += OnChooseAttachmentPropertyChanged;
                }
                if (ViewModel.ImageViewer != null)
                {
                    ViewModel.ImageViewer.PropertyChanged += OnImageViewerPropertyChanged;
                }
                if (ViewModel.MultiImageEditor != null)
                {
                    ViewModel.MultiImageEditor.PropertyChanged += OnMultiImageEditorPropertyChanged;
                }
                if (ViewModel.ImageEditor != null)
                {
                    ViewModel.ImageEditor.PropertyChanged += OnImageEditorPropertyChanged;
                }
                if (ViewModel.AnimatedImageViewer != null)
                {
                    ViewModel.AnimatedImageViewer.PropertyChanged += OnAnimatedImageViewerPropertyChanged;
                }
                ViewModel.PropertyChanged += OnViewModelPropertyChanged;
                ViewModel.ScrollToBottom += OnViewModelScrollToBottom;
                ViewModel.ScrollTo += OnViewModelScrollTo;

                BuildLocalizedAppBar();

                AudioRecorder.AudioRecorded += OnAudioRecorded;
                AudioRecorder.RecordStarted += OnRecordStarted;
                AudioRecorder.RecordingAudio += OnRecordingAudio;
                AudioRecorder.RecordCanceled += OnRecordCanceled;
            };

            Unloaded += (sender, args) =>
            {
                
#if LOG_NAVIGATION
                TLUtils.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + "DV Unloaded", LogSeverity.Error);
#endif
                //TLUtils.WriteLog("DV Unloaded");
                //ViewModel.OnNavigatedFrom();

                RemoveRootFrameBinding();

                if (ViewModel.ChooseAttachment != null)
                {
                    ViewModel.ChooseAttachment.PropertyChanged -= OnChooseAttachmentPropertyChanged;
                }
                if (ViewModel.ImageViewer != null)
                {
                    ViewModel.ImageViewer.PropertyChanged -= OnImageViewerPropertyChanged;
                }
                if (ViewModel.MultiImageEditor != null)
                {
                    ViewModel.MultiImageEditor.PropertyChanged -= OnMultiImageEditorPropertyChanged;
                }
                if (ViewModel.ImageEditor != null)
                {
                    ViewModel.ImageEditor.PropertyChanged -= OnImageEditorPropertyChanged;
                }
                if (ViewModel.AnimatedImageViewer != null)
                {
                    ViewModel.AnimatedImageViewer.PropertyChanged -= OnAnimatedImageViewerPropertyChanged;
                }
                ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
                ViewModel.ScrollToBottom -= OnViewModelScrollToBottom;
                ViewModel.ScrollTo -= OnViewModelScrollTo;
                AudioRecorder.AudioRecorded -= OnAudioRecorded;
                AudioRecorder.RecordStarted -= OnRecordStarted;
                AudioRecorder.RecordingAudio -= OnRecordingAudio;
                AudioRecorder.RecordCanceled -= OnRecordCanceled;
            };
        }

        private void Help_OnClick(object sender, System.EventArgs e)
        {
            ViewModel.Help();
        }

        private void Debug_OnClick(object sender, System.EventArgs e)
        {
            var log = new StringBuilder();
            var messages = ViewModel.GetHistory();
            foreach (var message in messages)
            {
                log.AppendLine(message != null ? " " + message : " empty message");
            }

            var mtProtoService = IoC.Get<IMTProtoService>();
            mtProtoService.GetMessagesAsync(new TLVector<TLInt>{new TLInt(272411)}, result =>
            {
                
            });

            MessageBox.Show(log.ToString());
            //ViewModel.AddComments();
            //_stickerSpriteItem = new StickerSpriteItem(1, new List<TLStickerItem>{new TLStickerItem{Document = new TLDocument22{Emoticon = "a"}}}, 100.0, 100.0);
            //_stickerSpriteItem.Load();
            // GC.Collect();
        }

        private void OptimizeFullHD()
        {
            var appBar = new ApplicationBar();
            var appBarDefaultSize = appBar.DefaultSize;

            AppBarCommandPlaceholder.Height = appBarDefaultSize;
            BottomAppBarPlaceholder.Height = new GridLength(appBarDefaultSize);
        }

        private void OnViewModelScrollToBottom(object sender, System.EventArgs e)
        {
            if (ViewModel.Items.Count > 0)
            {
                Items.ScrollToItem(ViewModel.Items[0]);
            }
        }

        private void OnViewModelScrollTo(object sender, ScrollToEventArgs e)
        {
            if (ViewModel.Items.Count > 0)
            {
                Items.ScrollToItem(e.Message);
            }
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
            ReplyKeyboardButton.Visibility = GetReplyKeyboardButtonVisibility();

            ViewModel.AudioTypingManager.CancelTyping();
        }

        private void OnRecordStarted(object sender, System.EventArgs e)
        {
            _visibilityBinding = SaveVisibilityBinding(InputMessage);
            _previousAudioRecorderMinHeight = AudioRecorder.MinHeight;

            AudioRecorder.MinHeight = InputMessage.ActualHeight;
            InputMessage.Visibility = Visibility.Collapsed;
            ReplyKeyboardButton.Visibility = Visibility.Collapsed;
        }

        private void OnRecordingAudio(object sender, System.EventArgs e)
        {
            ViewModel.AudioTypingManager.SetTyping();
        }

        private void OnAudioRecorded(object sender, AudioEventArgs e)
        {
            AudioRecorder.MinHeight = _previousAudioRecorderMinHeight;
            RestoreVisibilityBinding(InputMessage, _visibilityBinding, Visibility.Visible);
            ReplyKeyboardButton.Visibility = GetReplyKeyboardButtonVisibility();

            // чтобы быстро обновить Visibility InputMessage переносим все остальное в фон
            var viewModel = ViewModel;
            Telegram.Api.Helpers.Execute.BeginOnThreadPool(() => viewModel.SendAudio(e));
        }

        private void RunAnimation()
        {
            if (_isForwardInAnimation)
            {
                _isForwardInAnimation = false;

                if (ViewModel.With.Bitmap != null)
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

                var continuumLayoutRootOpacity = new DoubleAnimation
                {
                    From = 0.0,
                    To = 1.0,
                    Duration = TimeSpan.FromSeconds(0.25),
                    EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 6.0 }
                };
                Storyboard.SetTarget(continuumLayoutRootOpacity, LayoutRoot);
                Storyboard.SetTargetProperty(continuumLayoutRootOpacity, new PropertyPath("(UIElement.Opacity)"));
                storyboard.Children.Add(continuumLayoutRootOpacity);

                Deployment.Current.Dispatcher.BeginInvoke(() =>
                {
                    LayoutRoot.Opacity = 1.0;
                    //InputMessage.IsHitTestVisible = false;
                    //InputMessageFocusHolder.Visibility = Visibility.Visible;
                    //_inputMessageDisabled = true;
                    storyboard.Completed += (o, e) =>
                    {
                        MessagesCache.Visibility = Visibility.Collapsed;
                        Items.Visibility = Visibility.Visible;
                        Items.IsHitTestVisible = true;
                        ViewModel.ForwardInAnimationComplete();
                    };
                    storyboard.Begin();
                });
            }
            else if (_isBackwardOutAnimation)
            {
                _isBackwardOutAnimation = false;

                LayoutRoot.CacheMode = new BitmapCache();

                var storyboard = new Storyboard();

                var translateAnimation = new DoubleAnimationUsingKeyFrames();
                translateAnimation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.00), Value = 0.0 });
                translateAnimation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.25), Value = 150.0, EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseIn, Exponent = 6.0 } });
                Storyboard.SetTarget(translateAnimation, LayoutRoot);
                Storyboard.SetTargetProperty(translateAnimation, new PropertyPath("(UIElement.RenderTransform).(CompositeTransform.TranslateY)"));
                storyboard.Children.Add(translateAnimation);

                var opacityAnimation = new DoubleAnimationUsingKeyFrames();
                opacityAnimation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.00), Value = 1.0 });
                opacityAnimation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.15), Value = 1.0 });
                opacityAnimation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.25), Value = 0.0 });
                Storyboard.SetTarget(opacityAnimation, LayoutRoot);
                Storyboard.SetTargetProperty(opacityAnimation, new PropertyPath("(UIElement.Opacity)"));
                storyboard.Children.Add(opacityAnimation);

                storyboard.Begin();
            }
            else if (_isBackwardInAnimation)
            {
                _isBackwardInAnimation = false;

                var storyboard = TelegramTurnstileAnimations.GetAnimation(LayoutRoot, TurnstileTransitionMode.BackwardIn);

                Deployment.Current.Dispatcher.BeginInvoke(() =>
                {
                    LayoutRoot.Opacity = 1.0;
                    MessagesCache.Visibility = Visibility.Visible;
                    Items.Visibility = Visibility.Collapsed;
                    storyboard.Completed += (o, e) =>
                    {
                        MessagesCache.Visibility = Visibility.Collapsed;
                        Items.Visibility = Visibility.Visible;
                        ViewModel.BackwardInAnimationComplete();
                    };
                    storyboard.Begin();
                });
            }
        }

        //~DialogDetailsView()
        //{
            
        //}

        private bool _inputMessageDisabled;
        private bool _focusInputMessage;

        private void OpenPeerDetails_OnTap(object sender, GestureEventArgs args)
        {
            if (ViewModel.With is TLChatForbidden)
            {
                return;
            }

            if (CommandsControl.Visibility == Visibility.Visible)
            {
                CloseCommandsPlaceholder();
                AudioRecorder.Visibility = GetAudioRecorderVisibility(); //Visibility.Visible;
                DialogPhoto.Visibility = Visibility.Visible;
                Title.Visibility = Visibility.Visible;
            }

            if (EmojiPlaceholder.Visibility == Visibility.Visible)
            {
                CloseEmojiPlaceholder();
                AudioRecorder.Visibility = GetAudioRecorderVisibility(); //Visibility.Visible;
                DialogPhoto.Visibility = Visibility.Visible;
                Title.Visibility = Visibility.Visible;
            }

            // чтобы клавиатура успела опуститься
            Telegram.Api.Helpers.Execute.BeginOnUIThread(() =>
            {
                CreateBitmapCache();
                MessagesCache.Visibility = Visibility.Visible;
                Items.Visibility = Visibility.Collapsed;

                ViewModel.OpenPeerDetails();
            });
        }

        private void CreateBitmapCache()
        {
            try
            {
                if (Items.Visibility == Visibility.Visible)
                {
                    var stopwatch = Stopwatch.StartNew();
                    var writeableBitmap = new WriteableBitmap(Items, null);
                    var elapsed1 = stopwatch.Elapsed;
                    ViewModel.With.SetBitmap(writeableBitmap);
                    var elapsed2 = stopwatch.Elapsed;
                    //MessageBox.Show("create bitmap render=" + elapsed1 + " set=" + elapsed2);
                }
            }
            catch (Exception ex)
            {
                Telegram.Api.Helpers.Execute.ShowDebugMessage("WritableBitmap exception " + ex);
            }
        }

        private void MorePanel_OnTap(object sender, GestureEventArgs args)
        {
            var frameworkElement = sender as FrameworkElement;
            if (frameworkElement == null) return;

            var message = frameworkElement.DataContext as TLMessage;
            if (message == null) return;

            if (CommandsControl.Visibility == Visibility.Visible)
            {
                CloseCommandsPlaceholder();
                AudioRecorder.Visibility = GetAudioRecorderVisibility(); //Visibility.Visible;
                DialogPhoto.Visibility = Visibility.Visible;
                Title.Visibility = Visibility.Visible;
            }

            if (EmojiPlaceholder.Visibility == Visibility.Visible)
            {
                CloseEmojiPlaceholder();
                AudioRecorder.Visibility = GetAudioRecorderVisibility(); //Visibility.Visible;
                DialogPhoto.Visibility = Visibility.Visible;
                Title.Visibility = Visibility.Visible;
            }

            // чтобы клавиатура успела опуститься
            Telegram.Api.Helpers.Execute.BeginOnUIThread(() =>
            {
                CreateBitmapCache();
                MessagesCache.Visibility = Visibility.Visible;
                Items.Visibility = Visibility.Collapsed;

                ViewModel.OpenCropedMessage(message);
            });
        }

        private bool _isBackwardOutAnimation;
        private bool _isBackwardInAnimation;
        private bool _isForwardInAnimation;
        private bool _isForwardOutAnimation;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ViewModel.OnNavigatedTo();
#if LOG_NAVIGATION
            TLUtils.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + string.Format("DV OnNavigatedTo Mode={0} Uri={1}", e.NavigationMode, e.Uri), LogSeverity.Error);
#endif
            //TLUtils.WriteLog("DV OnNavigatedTo");
            MediaControl.Content = MessagePlayerControl.Player;

            if (e.NavigationMode == NavigationMode.New)
            {
                LayoutRoot.Opacity = 0.0;
                _isForwardInAnimation = true;
            }
            else if (e.NavigationMode == NavigationMode.Back)
            {
                if (!_fromExternalUri)
                {
                    LayoutRoot.Opacity = 0.0;
                    _isBackwardInAnimation = true;
                }
                else
                {
                    ViewModel.BackwardInAnimationComplete();
                }
                _fromExternalUri = false;
            }
            else if (e.NavigationMode == NavigationMode.Forward && e.Uri != ExternalUri)
            {
                _isForwardOutAnimation = true;
            }

            base.OnNavigatedTo(e);
        }

        private static readonly Uri ExternalUri = new Uri(@"app://external/");

        private bool _fromExternalUri;
        private bool _suppressNavigation;

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            if (CancelNavigatingFrom(e)) return;
            
#if LOG_NAVIGATION
            TLUtils.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + string.Format("DV OnNavigatingFrom Mode={0} Uri={1}", e.NavigationMode, e.Uri), LogSeverity.Error);
#endif
            //TLUtils.WriteLog("DV OnNavigatingFrom");
            if (_emojiKeyboard != null)
            {
                // Destroy EmojiControl
                _emojiKeyboard.IsOpen = false;
                _emojiKeyboard.UnbindTextBox();
                _emojiKeyboard.StickerSelected -= OnStickerSelected;
                EmojiPlaceholder.Content = null; // Remove from view
                CloseEmojiPlaceholder();
                AudioRecorder.Visibility = GetAudioRecorderVisibility(); //Visibility.Visible;
                DialogPhoto.Visibility = Visibility.Visible;
                Title.Visibility = Visibility.Visible;
                _emojiKeyboard = null;
            }

            if (CommandsControl.Visibility == Visibility.Visible)
            {
                CloseCommandsPlaceholder();
                AudioRecorder.Visibility = GetAudioRecorderVisibility(); //Visibility.Visible;
                DialogPhoto.Visibility = Visibility.Visible;
                Title.Visibility = Visibility.Visible;
            }

            if (e.Uri.OriginalString.EndsWith("VideoCaptureView.xaml")
                || e.Uri.OriginalString.EndsWith("MapView.xaml")
                || e.Uri.OriginalString.EndsWith("ShareContactView.xaml")
                || e.Uri.OriginalString.EndsWith("ContactView.xaml")
                || e.Uri.OriginalString.EndsWith("ChatView.xaml")
                || e.Uri.OriginalString.EndsWith("Chat2View.xaml"))
            {
                //if (_suppressNavigation)
                //{
                //    _suppressNavigation = false;
                //}
                //else
                //{
                //    var storyboard = new Storyboard();
                //    var layoutRoot = new DoubleAnimationUsingKeyFrames();
                //    layoutRoot.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.0), Value = 0.0 });
                //    layoutRoot.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.35), Value = 90.0, EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseIn, Exponent = 6.0 } });
                //    Storyboard.SetTarget(layoutRoot, LayoutRoot);
                //    Storyboard.SetTargetProperty(layoutRoot, new PropertyPath("(UIElement.Projection).(PlaneProjection.RotationY)"));
                //    storyboard.Children.Add(layoutRoot);
                //    storyboard.Completed += (sender, args) =>
                //    {
                //        NavigationService.Navigate(e.Uri);
                //    };
                //    storyboard.Begin();
                //    _suppressNavigation = true;
                //    e.Cancel = true;
                //}
            }
            
            if (e.Uri.OriginalString.EndsWith("EditVideoView.xaml")
                || e.Uri.OriginalString.EndsWith("MapView.xaml")
                || e.Uri.OriginalString.EndsWith("ContactView.xaml")
                || e.Uri.OriginalString.EndsWith("ChatView.xaml")
                || e.Uri.OriginalString.EndsWith("ProfilePhotoViewerView.xaml")
                || e.Uri.OriginalString.EndsWith("SearchShellView.xaml")
                || e.Uri.OriginalString.EndsWith("ChooseDialogView.xaml")
                )
            {
                CreateBitmapCache();
                Items.Visibility = Visibility.Collapsed;
                MessagesCache.Visibility = Visibility.Visible;
                //e.Cancel = true;
                //Deployment.Current.Dispatcher.BeginInvoke(() => NavigationService.Navigate(e.Uri));
                //return;
            }
            else
            {
                //TransitionService.SetNavigationOutTransition(Self, OutTransition);
            }

            base.OnNavigatingFrom(e);
        }

        private bool CancelNavigatingFrom(NavigatingCancelEventArgs e)
        {
            if (e.Uri.OriginalString.StartsWith("/Views/ShellView.xaml"))
            {
                if (e.Uri.OriginalString.Contains("from_id"))
                {
                    var user = ViewModel.With as TLUserBase;
                    if (user != null)
                    {
                        try
                        {
                            var uriParams = TelegramUriMapper.ParseQueryString(e.Uri.OriginalString);
                            var fromId = Convert.ToInt32(uriParams["from_id"]);
                            if (user.Index == fromId)
                            {
                                e.Cancel = true;
                                return true;
                            }
                        }
                        catch (Exception ex)
                        {
                        }
                    }
                }

                if (e.Uri.OriginalString.Contains("chat_id"))
                {
                    var chat = ViewModel.With as TLChatBase;
                    if (chat != null)
                    {
                        try
                        {
                            var uriParams = TelegramUriMapper.ParseQueryString(e.Uri.OriginalString);
                            var chatId = Convert.ToInt32(uriParams["chat_id"]);
                            if (chat.Index == chatId)
                            {
                                e.Cancel = true;
                                return true;
                            }
                        }
                        catch (Exception ex)
                        {
                        }
                    }
                }
            }
            return false;
        }

        private void OnStickerSelected(object sender, StickerSelectedEventArgs e)
        {
            if (e.Sticker == null) return;

            var document22 = e.Sticker.Document as TLDocument22;
            if (document22 == null) return;

            ViewModel.SendSticker(document22);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            //TLUtils.WriteLog("DV OnNavigatedFrom");
            ViewModel.OnNavigatedFrom();
#if LOG_NAVIGATION
            TLUtils.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + string.Format("DV OnNavigatedFrom Mode={0} Uri={1}", e.NavigationMode, e.Uri), LogSeverity.Error);
#endif
            MediaControl.Content = null;

            _fromExternalUri = e.Uri == ExternalUri;

            MessagePlayerControl.Stop();

            base.OnNavigatedFrom(e);
        }

        private void OnChooseAttachmentPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (Property.NameEquals(e.PropertyName, () => ViewModel.ChooseAttachment.IsOpen))
            {
                Items.IsHitTestVisible = !ViewModel.ChooseAttachment.IsOpen && (ViewModel.ImageViewer == null || !ViewModel.ImageViewer.IsOpen);

                return;
                // ApplicationBar скрывается в коде ChooseAttachmentView
                //if (ViewModel.ChooseAttachment.IsOpen)
                //{
                //    _prevApplicationBar = ApplicationBar;
                //    ApplicationBar = ((ChooseAttachmentView)ChooseAttachment.Content).ApplicationBar;
                //}
                //else
                //{
                //    if (_prevApplicationBar != null)
                //    {
                //        ApplicationBar = _prevApplicationBar;
                //    }
                //}
            }
        }

        private IApplicationBar _prevApplicationBar;

        private void OnImageViewerPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (Property.NameEquals(e.PropertyName, () => ViewModel.ImageViewer.IsOpen))
            {
                Items.IsHitTestVisible = !ViewModel.ImageViewer.IsOpen && (ViewModel.ChooseAttachment == null || !ViewModel.ChooseAttachment.IsOpen);

                if (ViewModel.ImageViewer.IsOpen)
                {
                    _prevApplicationBar = ApplicationBar;
                    ApplicationBar = ((ImageViewerView)ImageViewer.Content).ApplicationBar;
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

        private void OnMultiImageEditorPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (Property.NameEquals(e.PropertyName, () => ViewModel.MultiImageEditor.IsOpen))
            {
                Items.IsHitTestVisible = !ViewModel.MultiImageEditor.IsOpen && (ViewModel.ChooseAttachment == null || !ViewModel.ChooseAttachment.IsOpen);

                if (ViewModel.MultiImageEditor.IsOpen)
                {
                    _prevApplicationBar = ApplicationBar;
                    ApplicationBar = ((MultiImageEditorView)MultiImageEditor.Content).ApplicationBar;
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

        private void OnImageEditorPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (Property.NameEquals(e.PropertyName, () => ViewModel.ImageEditor.IsOpen))
            {
                Items.IsHitTestVisible = !ViewModel.ImageEditor.IsOpen && (ViewModel.ChooseAttachment == null || !ViewModel.ChooseAttachment.IsOpen);

                if (ViewModel.ImageEditor.IsOpen)
                {
                    _prevApplicationBar = ApplicationBar;
                    ApplicationBar = ((ImageEditorView)ImageEditor.Content).ApplicationBar;
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

        private void OnAnimatedImageViewerPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (Property.NameEquals(e.PropertyName, () => ViewModel.AnimatedImageViewer.IsOpen))
            {
                Items.IsHitTestVisible = !ViewModel.AnimatedImageViewer.IsOpen && (ViewModel.ChooseAttachment == null || !ViewModel.ChooseAttachment.IsOpen);

                if (ViewModel.AnimatedImageViewer.IsOpen)
                {
                    _prevApplicationBar = ApplicationBar;
                    ApplicationBar = ((AnimatedImageViewerView)AnimatedImageViewer.Content).ApplicationBar;
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

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (Property.NameEquals(e.PropertyName, () => ViewModel.HasBots))
            {
                ReplyKeyboardButton.Visibility = GetReplyKeyboardButtonVisibility();
                ReplyKeyboardButtonImage.Source = GetReplyKeyboardImageSource();
            }
            if (Property.NameEquals(e.PropertyName, () => ViewModel.ReplyMarkup))
            {
                ReplyKeyboardButton.Visibility = GetReplyKeyboardButtonVisibility();
                ReplyKeyboardButtonImage.Source = GetReplyKeyboardImageSource();

                var replyMarkup = ViewModel.ReplyMarkup as TLReplyKeyboardMarkup;
                if (replyMarkup != null)
                {
                    if (!replyMarkup.HasResponse)
                    {
                        OpenCommandsPlaceholder();

                        if (!ViewModel.IsAppBarCommandVisible)
                        {
                            AudioRecorder.Visibility = GetAudioRecorderVisibility();
                            DialogPhoto.Visibility = Visibility.Collapsed;
                            Title.Visibility = Visibility.Collapsed;
                        }
                    }
                    else
                    {
                        CloseCommandsPlaceholder();
                    }
                }
            }
            else if (Property.NameEquals(e.PropertyName, () => ViewModel.IsGroupActionEnabled))
            {
                var isGroupActionEnabled = ViewModel.IsGroupActionEnabled;

                _forwardButton.IsEnabled = isGroupActionEnabled;
                _deleteButton.IsEnabled = isGroupActionEnabled;
            }
            else if (Property.NameEquals(e.PropertyName, () => ViewModel.ScrollToBottomVisibility))
            {
                if (ViewModel.ScrollToBottomVisibility == Visibility.Visible)
                {
                    ShowScrollToBottomButton();
                }
                else
                {
                    HideScrollToBottomButton();
                }
            }
            else if (Property.NameEquals(e.PropertyName, () => ViewModel.Reply))
            {
                if (ViewModel.Reply != null)
                {
                    if (ViewModel.Reply is TLMessagesContainter)
                    {
                        return;
                    }

                    var message31 = ViewModel.Reply as TLMessage31;
                    if (message31 != null && message31.ReplyMarkup != null)
                    {
                        return;
                    }

                    InputMessage.Focus();
                }
            }
            else if (Property.NameEquals(e.PropertyName, () => ViewModel.HoldScrollingPosition))
            {
                if (ViewModel.HoldScrollingPosition)
                {
                    Items.HoldScrollingPosition();
                }
                else
                {
                    Items.UnholdScrollingPosition();
                }
            }
            else if (Property.NameEquals(e.PropertyName, () => ViewModel.IsSelectionEnabled))
            {
                if (ViewModel.IsSelectionEnabled)
                {
                    SwitchToSelectionMode();
                }
                else
                {
                    SwitchToNormalMode();
                }
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
            else if (Property.NameEquals(e.PropertyName, () => ViewModel.MultiImageEditor)
                && ViewModel.MultiImageEditor != null)
            {
                ViewModel.MultiImageEditor.PropertyChanged += OnMultiImageEditorPropertyChanged;
            }
            else if (Property.NameEquals(e.PropertyName, () => ViewModel.ImageEditor)
                && ViewModel.ImageEditor != null)
            {
                ViewModel.ImageEditor.PropertyChanged += OnImageEditorPropertyChanged;
            }
            else if (Property.NameEquals(e.PropertyName, () => ViewModel.AnimatedImageViewer)
            && ViewModel.AnimatedImageViewer != null)
            {
                ViewModel.AnimatedImageViewer.PropertyChanged += OnAnimatedImageViewerPropertyChanged;
            }
            else if (Property.NameEquals(e.PropertyName, () => ViewModel.With))
            {
                AudioRecorder.Visibility = GetAudioRecorderVisibility();
                ViewModel.ChangeUserAction();
                if (ApplicationBar != null)
                {
                    ApplicationBar.IsVisible = !ViewModel.IsAppBarCommandVisible && !ViewModel.IsChooseAttachmentOpen;
                }
                ChangeShareInfo();
            }
            else if (Property.NameEquals(e.PropertyName, () => ViewModel.CanSend))
            {
                _sendButton.IsEnabled = ViewModel.CanSend;
            }
        }

        private void ChangeShareInfo()
        {
            if (ApplicationBar == null) return;

            if (ViewModel.With is TLUserForeign)
            {
                ApplicationBar.MenuItems.Remove(_shareMyContactInfoMenuItem);
                ApplicationBar.MenuItems.Insert(0, _shareMyContactInfoMenuItem);
            }
            else
            {
                ApplicationBar.MenuItems.Remove(_shareMyContactInfoMenuItem);
            }
        }

        private void SwitchToSelectionMode()
        {
            //ViewModel.IsSelectionEnabled = true;
            Items.Focus();
            CloseEmojiPlaceholder();
            CloseCommandsPlaceholder();
            AudioRecorder.Visibility = GetAudioRecorderVisibility(); //Visibility.Visible;
            DialogPhoto.Visibility = Visibility.Visible;
            Title.Visibility = Visibility.Visible;


            ApplicationBar.Buttons.Clear();
            ApplicationBar.Buttons.Add(_forwardButton);

            var channel = ViewModel.With as TLChannel;
            if (channel == null || channel.Creator)
            {
                ApplicationBar.Buttons.Add(_deleteButton);
            }

            var isGroupActionEnabled = ViewModel.IsGroupActionEnabled;

            _forwardButton.IsEnabled = isGroupActionEnabled;
            _deleteButton.IsEnabled = isGroupActionEnabled;
        }

        private void SwitchToNormalMode()
        {
            //ViewModel.IsSelectionEnabled = false;

            ApplicationBar.Buttons.Clear();

            ApplicationBar.Buttons.Clear();
            ApplicationBar.Buttons.Add(_sendButton);
            ApplicationBar.Buttons.Add(_attachButton);
            ApplicationBar.Buttons.Add(_smileButton);
            ApplicationBar.Buttons.Add(_manageButton);
        }

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
            var broadcast = ViewModel.With as TLBroadcastChat;
            var channel = ViewModel.With as TLChannel;
            if (broadcast == null)
            {
                ApplicationBar.MenuItems.Add(_reportSpamMenuItem);
            }
            if (broadcast == null || (channel != null && channel.IsMegaGroup))
            {
                ApplicationBar.MenuItems.Add(_searchMenuItem);
            }
            var user = ViewModel.With as TLUser;
            if (user != null && user.IsBot)
            {
                ApplicationBar.MenuItems.Add(_helpMenuItem);
            }
            ApplicationBar.MenuItems.Add(_pinToStartMenuItem);
            
            if (ViewModel.With is TLUserForeign)
            {
                ApplicationBar.MenuItems.Add(_shareMyContactInfoMenuItem);
            }
#if DEBUG
            ApplicationBar.MenuItems.Add(_debugMenuItem);
#endif

            _sendButton.IsEnabled = ViewModel.CanSend;
            ApplicationBar.IsVisible = !ViewModel.IsAppBarCommandVisible && !ViewModel.IsChooseAttachmentOpen;
        }


#if DEBUG
        ///<summary>
        ///Add a finalizer to check for memory leaks
        ///</summary>
        //~DialogDetailsView()
        //{
        //    TLUtils.WritePerformance("++DialogDetailsView dstr");
        //}
#endif

        private void DialogDetailsView_OnBackKeyPress(object sender, CancelEventArgs e)
        {
            if (_lastMessagePrompt != null
                && _lastMessagePrompt.IsOpen)
            {
                _lastMessagePrompt.Hide();
                e.Cancel = true;

                return;
            }

            if (ViewModel == null) return;

            if (ViewModel.SearchMessages != null
                && ViewModel.SearchMessages.IsOpen)
            {
                ViewModel.SearchMessages.Close();
                e.Cancel = true;

                return;
            }

            if (ViewModel.MultiImageEditor != null
                && ViewModel.MultiImageEditor.IsOpen)
            {
                ViewModel.MultiImageEditor.CloseEditor();
                e.Cancel = true;

                return;
            }


            if (ViewModel.ImageEditor != null
                && ViewModel.ImageEditor.IsOpen)
            {
                ViewModel.ImageEditor.CloseEditor();
                e.Cancel = true;

                return;
            }

            if (ViewModel.ImageViewer != null
                && ViewModel.ImageViewer.IsOpen)
            {
                ViewModel.ImageViewer.CloseViewer();
                e.Cancel = true;

                return;
            }

            if (ViewModel.AnimatedImageViewer != null
                && ViewModel.AnimatedImageViewer.IsOpen)
            {
                ViewModel.AnimatedImageViewer.CloseViewer();
                e.Cancel = true;

                return;
            }

            if (_lastContextMenu != null && _lastContextMenu.IsOpen)
            {
                _lastContextMenu.IsOpen = false;
                e.Cancel = true;
                return;
            }

            if (EmojiPlaceholder.Visibility == Visibility.Visible
                || CommandsControl.Visibility == Visibility.Visible)
            {
                CloseEmojiPlaceholder();
                CloseCommandsPlaceholder();
                AudioRecorder.Visibility = GetAudioRecorderVisibility(); //Visibility.Visible;
                DialogPhoto.Visibility = Visibility.Visible;
                Title.Visibility = Visibility.Visible;
                e.Cancel = true;
                return;
            }

            if (ViewModel.ChooseAttachment != null
                && ViewModel.ChooseAttachment.IsOpen)
            {
                ViewModel.ChooseAttachment.Close();
                e.Cancel = true;

                return;
            }

            if (ViewModel.IsSelectionEnabled)
            {
                ViewModel.IsSelectionEnabled = false;
                ApplicationBar.IsVisible = !ViewModel.IsAppBarCommandVisible;
                e.Cancel = true;

                return;
            }

            if (!NavigationService.BackStack.Any())
            {
                e.Cancel = true;
                ViewModel.NavigateToShellViewModel();

                return;
            }

            _isBackwardOutAnimation = true;

            CreateBitmapCache();
            MessagesCache.Visibility = Visibility.Visible;
            Items.Visibility = Visibility.Collapsed;

            RunAnimation();

            ViewModel.CancelDownloading();
        }

        private void UIElement_OnHold(object sender, GestureEventArgs e)
        {
            e.Handled = true;
        }

        private void ScrollButton_OnClick(object sender, RoutedEventArgs e)
        {
            ViewModel.ScrollToBottomVisibility = Visibility.Collapsed;

            Telegram.Api.Helpers.Execute.BeginOnUIThread(() =>
            {
                if (ViewModel.Items.Count > 0)
                {
                    ViewModel.ProcessScroll();
                }
            });
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

        private ContextMenu _lastContextMenu;

        private void ContextMenu_OnOpened(object sender, RoutedEventArgs e)
        {
            //var menu = sender as ContextMenu;
            //if (menu != null)
            //{
            //    menu.Items
            //}

            //MessageBox.Show("ContextMenu.Opened");

            _lastContextMenu = sender as ContextMenu;
        }

        private void InputMessage_OnGotFocus(object sender, RoutedEventArgs e)
        {
            ReplyKeyboardButtonImage.Source = GetReplyKeyboardImageSource();
            AudioRecorder.Visibility = GetAudioRecorderVisibility(); //Visibility.Collapsed;
            DialogPhoto.Visibility = Visibility.Collapsed;
            Title.Visibility = Visibility.Collapsed;
            EmojiPlaceholder.Visibility = Visibility.Visible;
            EmojiPlaceholder.Opacity = 0.0;
            EmojiPlaceholder.Height = EmojiControl.PortraitOrientationHeight;
            //var storyboard = new Storyboard();
            //var opacityAnimation = new DoubleAnimationUsingKeyFrames();
            //opacityAnimation.KeyFrames.Add(new DiscreteDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.0), Value = 0.0 });
            //opacityAnimation.KeyFrames.Add(new DiscreteDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.75), Value = 1.0 });
            //Storyboard.SetTarget(opacityAnimation, EmojiPlaceholder);
            //Storyboard.SetTargetProperty(opacityAnimation, new PropertyPath("Opacity"));
            //storyboard.Children.Add(opacityAnimation);
            //storyboard.Begin();
        }

        private bool _smileButtonPressed;

        private void InputMessage_OnLostFocus(object sender, RoutedEventArgs e)
        {
            if (!_smileButtonPressed)
            {
                if (EmojiPlaceholder.Visibility == Visibility.Visible)
                {
                    CloseEmojiPlaceholder();
                }
                if (CommandsControl.Visibility == Visibility.Visible)
                {
                    CloseCommandsPlaceholder();
                }
            }
            _smileButtonPressed = false;

            if (EmojiPlaceholder.Visibility == Visibility.Collapsed)
            {
                AudioRecorder.Visibility = GetAudioRecorderVisibility(); //Visibility.Visible;
                DialogPhoto.Visibility = Visibility.Visible;
                Title.Visibility = Visibility.Visible;
            }
        }

        private bool _once = true;

        private void Items_OnManipulationStarted(object sender, ManipulationStartedEventArgs e)
        {
            if (ViewModel.SliceLoaded) return;

            ViewModel.LoadNextSlice();
            ViewModel.LoadPreviousSlice();
        }

        private void InputMessage_OnTap(object sender, MouseButtonEventArgs mouseButtonEventArgs)
        {
            if (_inputMessageDisabled)
            {
                _focusInputMessage = true;
            }
        }

        private DispatcherTimer _stickerTimer;
        private string _stickerTimerText;
        private TLStickerPack _stickerPack; 

        private string _previousStickerText;

        private void InputMessage_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            AudioRecorder.Visibility = GetAudioRecorderVisibility();
            ReplyKeyboardButton.Visibility = GetReplyKeyboardButtonVisibility();


            var currentStickerText = InputMessage.Text.Trim();
            var stickerPack = ViewModel.GetStickerPack(currentStickerText);

            if (stickerPack != null)
            {
                if (string.Equals(_previousStickerText, currentStickerText, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _previousStickerText = currentStickerText;

                if (OpenStickersStoryboard.GetCurrentState() == ClockState.Active
                    || (StickersPanel.Visibility == Visibility.Visible
                        && StickersPanel.Opacity == 1.0
                        && StickersPanelTransform.TranslateY == 0.0))
                {
                    return;
                }

                if (_stickerTimer == null)
                {
                    _stickerTimer = new DispatcherTimer();
                    _stickerTimer.Tick += OnStickerTimerTick;
                    _stickerTimer.Interval = TimeSpan.FromSeconds(0.25);
                }

                _stickerTimer.Stop();

                _stickerPack = stickerPack;
                _stickerTimerText = currentStickerText;
                _stickerTimer.Start();
            }
            else
            {
                _previousStickerText = null;
                StickersPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void OnStickerTimerTick(object sender, System.EventArgs e)
        {
            _stickerTimer.Stop();
            var currentStickerText = InputMessage.Text.Trim();
            if (currentStickerText == _stickerTimerText)
            {
                const int stickersChunkCount = 5;

                var stickers = new List<TLStickerItem>(_stickerPack.Documents.Count);
                for (var i = 0; i < _stickerPack.Documents.Count; i++)
                {
                    var sticker = ViewModel.Stickers.Documents.FirstOrDefault(x => x.Id.Value == _stickerPack.Documents[i].Value);
                    if (sticker != null)
                    {
                        stickers.Add(new TLStickerItem { Document = sticker });
                    }
                }

                _stickers = new ObservableCollection<TLStickerItem>(stickers.Take(stickersChunkCount));

                Stickers.ItemsSource = _stickers;
                _delayedStickers = stickers.Skip(stickersChunkCount).ToList();

                StickersPanel.Visibility = Visibility.Visible;
                StickersPanel.Opacity = 0.0;

                Execute.BeginOnUIThread(() => OpenStickersStoryboard.Begin());
            }
        }

        private void UIElement_OnTap(object sender, GestureEventArgs e)
        {
            var frameworkElement = sender as FrameworkElement;
            if (frameworkElement == null) return;

            var stickerItem = frameworkElement.DataContext as TLStickerItem;
            if (stickerItem == null) return;

            var document22 = stickerItem.Document as TLDocument22;
            if (document22 == null) return;

            ViewModel.SendSticker(document22);
        }

        private ObservableCollection<TLStickerItem> _stickers; 
        private IList<TLStickerItem> _delayedStickers;
        
        private void OpenStickersStoryboard_OnCompleted(object sender, System.EventArgs e)
        {
            if (_delayedStickers != null && _stickers != null)
            {
                for (var i = 0; i < _delayedStickers.Count; i++)
                {
                    _stickers.Add(_delayedStickers[i]);
                }
                _delayedStickers = null;
            }
        }

        private Visibility GetReplyKeyboardButtonVisibility()
        {
            if (InputMessage.Text.Length > 0)
            {
                return Visibility.Collapsed;
            }

            if (ViewModel != null)
            {
                var replyMarkup = ViewModel.ReplyMarkup as TLReplyKeyboardMarkup;
                if (replyMarkup == null)
                {
                    if (!ViewModel.HasBots)
                    {
                        return Visibility.Collapsed;
                    }
                }
            }

            return Visibility.Visible;
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

            if (CommandsControl.Visibility == Visibility.Visible)
            {
                return Visibility.Collapsed;
            }

            if (ViewModel != null)
            {
                var chatForbidden = ViewModel.With as TLChatForbidden;
                var chat = ViewModel.With as TLChat;

                var isForbidden = chatForbidden != null || (chat != null && chat.Left.Value);
                if (isForbidden)
                {
                    return Visibility.Collapsed;
                }
            }

            return Visibility.Visible;
        }

        private void MainItemGrid2_OnLoaded(object sender, RoutedEventArgs e)
        {
            return;

            var grid = sender as Grid;
            if (grid != null)
            {
                grid.Opacity = 0.0;

                var storyboard = new Storyboard();
                var opacityAnimation = new DoubleAnimation { To = 1, Duration = TimeSpan.FromSeconds(.3) };
                storyboard.Children.Add(opacityAnimation);
                Storyboard.SetTarget(storyboard, grid);
                Storyboard.SetTargetProperty(storyboard, new PropertyPath("(UIElement.Opacity)"));

                storyboard.Begin();
            }
        }

        private void CommandHint_OnTap(object sender, GestureEventArgs e)
        {
            InputMessage.Focus();

            var frameworkElement = e.OriginalSource as FrameworkElement;
            if (frameworkElement != null)
            {
                var botCommand = frameworkElement.DataContext as TLBotCommand;
                if (botCommand != null)
                {
                    //var index = 0;
                    //for (var i = InputMessage.Text.Length - 1; i >= 0; i--)
                    //{
                    //    if (InputMessage.Text[i] == '/')
                    //    {
                    //        index = i;
                    //        break;
                    //    }
                    //}

                    var command = !ViewModel.IsSingleBot
                        ? string.Format("{0}@{1}", botCommand.Command, ((IUserName)botCommand.Bot).UserName)
                        : botCommand.Command.ToString();

                    //InputMessage.Text = string.Format("{0}{1}", InputMessage.Text.Substring(0, index + 1), command);
                    //InputMessage.SelectionStart = InputMessage.Text.Length;
                    //InputMessage.SelectionLength = 0;
                    InputMessage.Text = string.Empty;
                    Execute.BeginOnUIThread(() => ViewModel.Send(new TLString("/" + command)));
                }
            }
        }

        private void UsernameHint_OnTap(object sender, GestureEventArgs e)
        {
            InputMessage.Focus();

            var frameworkElement = e.OriginalSource as FrameworkElement;
            if (frameworkElement != null)
            {
                var user = frameworkElement.DataContext as IUserName;
                if (user != null)
                {
                    var index = 0;
                    for (var i = InputMessage.Text.Length - 1; i >= 0; i--)
                    {
                        if (InputMessage.Text[i] == '@')
                        {
                            index = i;
                            break;
                        }
                    }

                    InputMessage.Text = string.Format("{0}{1} ", InputMessage.Text.Substring(0, index + 1), user.UserName);
                    InputMessage.SelectionStart = InputMessage.Text.Length;
                    InputMessage.SelectionLength = 0;
                }
            }
        }

        private void HashtagHint_OnTap(object sender, GestureEventArgs e)
        {
            InputMessage.Focus();

            var frameworkElement = e.OriginalSource as FrameworkElement;
            if (frameworkElement != null)
            {
                var hashtag = frameworkElement.DataContext as TLHashtagItem;
                if (hashtag != null)
                {
                    var index = 0;
                    for (var i = InputMessage.Text.Length - 1; i >= 0; i--)
                    {
                        if (InputMessage.Text[i] == '#')
                        {
                            index = i;
                            break;
                        }
                    }

                    InputMessage.Text = string.Format("{0}{1} ", InputMessage.Text.Substring(0, index + 1), hashtag.Hashtag);
                    InputMessage.SelectionStart = InputMessage.Text.Length;
                    InputMessage.SelectionLength = 0;
                }
            }
        }

        private void UsernameHints_OnManipulationStarted(object sender, ManipulationStartedEventArgs e)
        {
            ViewModel.ContinueUsernameHints();
        }

        private void CommandHints_OnManipulationStarted(object sender, ManipulationStartedEventArgs e)
        {
            ViewModel.ContinueCommandHints();
        }

        private void HashtagHints_OnManipulationStarted(object sender, ManipulationStartedEventArgs e)
        {
            ViewModel.ContinueHashtagHints();
        }

        private void Items_OnBegin(object sender, System.EventArgs e)
        {
            if (Items.Viewport.Bounds.Y == 0.0) return;

            if (ViewModel != null)
            {
                ViewModel.ScrollToBottomVisibility = Visibility.Collapsed;
            }
        }

        private void HideScrollToBottomButton()
        {
            if (ScrollButton.Visibility == Visibility.Collapsed) return;

            //ScrollToBottomButton.Visibility = Visibility.Collapsed;
            //return;

            var storyboard = new Storyboard();
            var continuumScrollToBottomButton = new DoubleAnimationUsingKeyFrames();
            continuumScrollToBottomButton.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.0), Value = 0.0 });
            continuumScrollToBottomButton.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.25), Value = 150.0, EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 3.0 } });
            Storyboard.SetTarget(continuumScrollToBottomButton, ScrollButton);
            Storyboard.SetTargetProperty(continuumScrollToBottomButton, new PropertyPath("(UIElement.RenderTransform).(CompositeTransform.TranslateY)"));
            storyboard.Children.Add(continuumScrollToBottomButton);

            var continuumLayoutRootOpacity = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = TimeSpan.FromSeconds(0.25),
                EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 6.0 }
            };
            Storyboard.SetTarget(continuumLayoutRootOpacity, ScrollButton);
            Storyboard.SetTargetProperty(continuumLayoutRootOpacity, new PropertyPath("(UIElement.Opacity)"));
            storyboard.Children.Add(continuumLayoutRootOpacity);
            storyboard.Completed += (sender, args) =>
            {
                ScrollButton.Visibility = Visibility.Collapsed;
            };

            storyboard.Begin();
            //Telegram.Api.Helpers.Execute.BeginOnUIThread(() => storyboard.Begin());
        }

        private void ShowScrollToBottomButton()
        {
            if (ScrollButton.Visibility == Visibility.Visible) return;

            ScrollButton.Visibility = Visibility.Visible;
            ScrollButton.Opacity = 0.0;

            var storyboard = new Storyboard();
            var continuumScrollToBottomButton = new DoubleAnimationUsingKeyFrames();
            continuumScrollToBottomButton.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.0), Value = 150.0 });
            continuumScrollToBottomButton.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.25), Value = 0.0, EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 3.0 } });
            Storyboard.SetTarget(continuumScrollToBottomButton, ScrollButton);
            Storyboard.SetTargetProperty(continuumScrollToBottomButton, new PropertyPath("(UIElement.RenderTransform).(CompositeTransform.TranslateY)"));
            storyboard.Children.Add(continuumScrollToBottomButton);

            var continuumLayoutRootOpacity = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = TimeSpan.FromSeconds(0.25),
                EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 6.0 }
            };
            Storyboard.SetTarget(continuumLayoutRootOpacity, ScrollButton);
            Storyboard.SetTargetProperty(continuumLayoutRootOpacity, new PropertyPath("(UIElement.Opacity)"));
            storyboard.Children.Add(continuumLayoutRootOpacity);

            storyboard.Begin();
            //Telegram.Api.Helpers.Execute.BeginOnUIThread(() => storyboard.Begin());
        }

        private void FrameworkElement_OnLoaded(object sender, RoutedEventArgs e)
        {
            return;
            var image = (Image) sender;
            MessageBox.Show(string.Format("{0} {1}", image.ActualHeight, image.ActualWidth));
        }


        private static Storyboard _storyboard;
        private static UIElement _element;

        public static readonly DependencyProperty AnimatedVisibilityProperty =
            DependencyProperty.RegisterAttached("AnimatedVisibility", typeof(bool), typeof(DialogDetailsView),
                new PropertyMetadata(OnAnimagedVisibilityChanged));

        private StickerSpriteItem _stickerSpriteItem;

        private static void OnAnimagedVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var element = d as UIElement;
            if (element != null)
            {
                if (_storyboard != null)
                {
                    
                    _storyboard.Stop();
                }
                if (_element != null)
                {
                    _element.Opacity = 0.0;
                    _element.Visibility = Visibility.Collapsed;
                }

                if ((bool) e.NewValue)
                {

                    element.Opacity = 1.0;
                    element.Visibility = Visibility.Visible;
                }
                else
                {
                    var storyboard = new Storyboard();
                    var continuumLayoutRootOpacity = new DoubleAnimation
                    {
                        From = 1.0,
                        To = 0.0,
                        Duration = TimeSpan.FromSeconds(1.0),
                        //EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 6.0 }
                    };
                    Storyboard.SetTarget(continuumLayoutRootOpacity, element);
                    Storyboard.SetTargetProperty(continuumLayoutRootOpacity, new PropertyPath("(UIElement.Opacity)"));
                    storyboard.Children.Add(continuumLayoutRootOpacity);

                    storyboard.Begin();
                    storyboard.Completed += (sender, args) =>
                    {
                        _storyboard = null;
                        _element = null;
                    };

                    _storyboard = storyboard;
                    _element = element;
                }
            }
        }


        public static bool GetAnimatedVisibility(UIElement element)
        {
            return (bool) element.GetValue(AnimatedVisibilityProperty);
        }

        public static void SetAnimatedVisibility(UIElement element, bool value)
        {
            element.SetValue(AnimatedVisibilityProperty, value);
        }

        private void HashtagHintsPanel_OnHold(object sender, GestureEventArgs e)
        {
            ViewModel.ClearHashtags();
        }

        private void CommandsControl_OnButtonClick(object sender, KeyboardButtonEventArgs e)
        {
            ViewModel.Send(e.Button);

            if (ViewModel.ReplyMarkup != null
                && ViewModel.ReplyMarkup.IsSingleUse)
            {
                //Telegram.Api.Helpers.Execute.ShowDebugMessage("TLReplyKeyboardMarkup.IsSingleUse=true");
                ReplyKeyboardButtonImage.Source = new BitmapImage(new Uri("/Images/Dialogs/chat.customkeyboard-WXGA.png", UriKind.Relative));
                InputMessage.Focus();
            }
        }

        private void ReplyKeyboardButton_OnClick(object sender, RoutedEventArgs e)
        {
            var replyKeyboard = ViewModel.ReplyMarkup as TLReplyKeyboardMarkup;

            if (ViewModel.HasBots && replyKeyboard == null)
            {
                InputMessage.Text = "/";
                InputMessage.SelectionStart = 1;
                InputMessage.Focus();

                return;
            }
            
            if (_emojiKeyboard != null
                && EmojiPlaceholder.Visibility == Visibility.Visible)
            {
                
                if (replyKeyboard != null)
                {
                    OpenCommandsPlaceholder();
                    CloseEmojiPlaceholder();

                    return;
                }
            }

            if (CommandsControl.Visibility == Visibility.Visible)
            {
                if (_emojiKeyboard != null
                    && EmojiPlaceholder.Visibility == Visibility.Visible)
                {
                    CloseCommandsPlaceholder();
                    AudioRecorder.Visibility = GetAudioRecorderVisibility();
                    DialogPhoto.Visibility = Visibility.Visible;
                    Title.Visibility = Visibility.Visible;
                }
                else
                {
                    ReplyKeyboardButtonImage.Source = GetReplyKeyboardImageSource();
                    InputMessage.Focus();
                }
            }
            else
            {
                if (replyKeyboard != null)
                {
                    OpenCommandsPlaceholder();
                    AudioRecorder.Visibility = GetAudioRecorderVisibility();
                    DialogPhoto.Visibility = Visibility.Collapsed;
                    Title.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void AddToStickers_OnLoaded(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            if (menuItem != null)
            {
                var message = menuItem.DataContext as TLMessage;
                if (message != null && message.IsSticker())
                {
                    var mediaDocument = message.Media as TLMessageMediaDocument;
                    if (mediaDocument == null) return;

                    var document = mediaDocument.Document as TLDocument22;
                    if (document != null)
                    {
                        var inputStickerSet = document.StickerSet;
                        if (inputStickerSet != null)
                        {
                            var allStickers = ViewModel.Stickers as TLAllStickers29;
                            if (allStickers != null)
                            {
                                var set =
                                    allStickers.Sets.FirstOrDefault(
                                        x => x.Id.Value.ToString() == inputStickerSet.Name.ToString());
                                if (set != null)
                                {
                                    menuItem.Visibility = Visibility.Collapsed;
                                }
                                else
                                {
                                    menuItem.Visibility = Visibility.Visible;
                                }
                            }
                        }
                    }
                }
            }
        }

        private void ImageBrush_OnImageOpened(object sender, RoutedEventArgs e)
        {
            //BroadcastIcon.Opacity = 1.0;
        }

        private void ChannelBroadcastButton_OnClick(object sender, RoutedEventArgs e)
        {
            ViewModel.IsChannelMessage = !ViewModel.IsChannelMessage;
        }

        private void StickerContextMenu_OnLoaded(object sender, RoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element != null)
            {
                element.Visibility = Visibility.Visible;

                //var message = element.DataContext as TLMessage40;
                //var channel = ViewModel.With as TLChannel;
                //element.Visibility = channel != null && channel.IsMegaGroup ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        private void ServiceMessageContextMenu_OnLoaded(object sender, RoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element != null)
            {
                var message = element.DataContext as TLMessage40;
                var channel = ViewModel.With as TLChannel;
                element.Visibility = channel != null && channel.IsMegaGroup ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        private void DeleteMenuItem_OnLoaded(object sender, RoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element != null)
            {
                var message = element.DataContext as TLMessage40;
                if (message != null && message.Out.Value)
                {
                    element.Visibility = Visibility.Visible;
                    return;
                }

                var channel = ViewModel.With as TLChannel;
                if (channel != null 
                    && (channel.Creator || channel.IsEditor))
                {
                    element.Visibility = Visibility.Visible;
                    return;
                }

                element.Visibility = Visibility.Collapsed;
            }
        }

        private void ReplyMenuItem_OnLoaded(object sender, RoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element == null) return;

            element.Visibility = ViewModel.IsAppBarCommandVisible ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ForwardMenuItem_OnLoaded(object sender, RoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element == null) return;

            var broadcast = ViewModel.With as TLBroadcastChat;
            var channel = ViewModel.With as TLChannel;
            if (broadcast != null
                && channel == null)
            {
                element.Visibility = Visibility.Collapsed;
                return;
            }

            element.Visibility = Visibility.Visible;
        }

        private void MoreMenuItem_OnLoaded(object sender, RoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element == null) return;

            element.Visibility = ViewModel.IsAppBarCommandVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        private void MoreMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            ViewModel.IsSelectionEnabled = true;
            ApplicationBar.IsVisible = true;
        }

        private void ReplyMessage_OnLoaded(object sender, RoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element == null) return;

            element.Visibility = Visibility.Collapsed;

            var channel = ViewModel.With as TLChannel;
            if (channel != null && channel.MigratedFromChatId != null)
            {
                var message = element.DataContext as TLMessageCommon;
                if (message != null)
                {
                    if (message.ToId is TLPeerChat)
                    {
                        element.Visibility = message.ToId.Id.Value == channel.MigratedFromChatId.Value ? Visibility.Collapsed : Visibility.Visible;
                    }
                }
            }
        }

        private void DeleteMessage_OnLoaded(object sender, RoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element == null) return;

            element.Visibility = Visibility.Visible;
            //element.Visibility = Visibility.Collapsed;

            //var channel = ViewModel.With as TLChannel;
            //if (channel != null && channel.MigratedFromChatId != null)
            //{
            //    var message = element.DataContext as TLMessageCommon;
            //    if (message != null)
            //    {
            //        if (message.ToId is TLPeerChat)
            //        {
            //            element.Visibility = message.ToId.Id.Value == channel.MigratedFromChatId.Value ? Visibility.Collapsed : Visibility.Visible;
            //        }
            //    }
            //}
        }

        private void ForwardMessage_OnLoaded(object sender, RoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element == null) return;
            if (ViewModel == null) return;

            element.Visibility = ViewModel.IsBroadcast && !ViewModel.IsChannel ? Visibility.Collapsed : Visibility.Visible;
        }

        private void CopyMessage_OnLoaded(object sender, RoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element == null) return;

            element.Visibility = Visibility.Collapsed;

            var message = element.DataContext as TLMessage;
            if (message != null)
            {
                element.Visibility = TLString.IsNullOrEmpty(message.Message) ? Visibility.Collapsed : Visibility.Visible;
            }
        }
    }

    public class TLStickerItem : TLObject
    {
        public TLDocumentBase Document { get; set; }

        public TLStickerItem Self
        {
            get { return this; }
        }
    }

    //public class ScrollPreserver : DependencyObject
    //{
    //    public static readonly DependencyProperty PreserveScrollProperty =
    //        DependencyProperty.RegisterAttached("PreserveScroll", 
    //            typeof(bool),
    //            typeof(ScrollPreserver), 
    //            new PropertyMetadata(new PropertyChangedCallback(OnScrollGroupChanged)));

    //    public static bool GetPreserveScroll(DependencyObject invoker)
    //    {
    //        return (bool)invoker.GetValue(PreserveScrollProperty);
    //    }

    //    public static void SetPreserveScroll(DependencyObject invoker, bool value)
    //    {
    //        invoker.SetValue(PreserveScrollProperty, value);
    //    }

    //    private static Dictionary<ScrollViewer, bool> scrollViewers_States = new Dictionary<ScrollViewer, bool>();

    //    private static void OnScrollGroupChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    //    {

    //        ScrollViewer scrollViewer = d as ScrollViewer;
    //        if (scrollViewer != null && (bool)e.NewValue == true)
    //        {
    //            if (!scrollViewers_States.ContainsKey(scrollViewer))
    //            {
    //                scrollViewer.cha += new ScrollChangedEventHandler(scrollViewer_ScrollChanged);
    //                scrollViewers_States.Add(scrollViewer, false);
    //            }
    //        }
    //    }

    //    static void scrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    //    {
    //        if (scrollViewers_States[sender as ScrollViewer])
    //            (sender as ScrollViewer).ScrollToVerticalOffset(e.VerticalOffset + e.ExtentHeightChange);

    //        scrollViewers_States[sender as ScrollViewer] = e.VerticalOffset != 0;
    //    }
    //}
}