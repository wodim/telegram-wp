using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Caliburn.Micro;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using Telegram.Api.Extensions;
using Telegram.EmojiPanel.Controls.Emoji;
using Telegram.Logs;
using TelegramClient.Helpers;
using TelegramClient.Resources;
using TelegramClient.Services;
using TelegramClient.ViewModels.Media;

namespace TelegramClient.Views.Media
{
    public partial class MultiImageEditorView
    {
        private readonly AppBarButton _doneButton = new AppBarButton
        {
            Text = AppResources.Done,
            IconUri = new Uri("/Images/ApplicationBar/appbar.check.png", UriKind.Relative)
        };

        private readonly AppBarButton _cancelButton = new AppBarButton
        {
            Text = AppResources.Cancel,
            IconUri = new Uri("/Images/ApplicationBar/appbar.cancel.rest.png", UriKind.Relative)
        };

        public MultiImageEditorViewModel ViewModel
        {
            get { return DataContext as MultiImageEditorViewModel; }
        }

        public MultiImageEditorView()
        {
            InitializeComponent();

            Visibility = Visibility.Collapsed;

            BuildLocalizedAppBar();

            OptimizeFullHD();

            Loaded += (o, e) =>
            {
                ViewModel.OnLoaded();
                ViewModel.PropertyChanged += OnViewModelPropertyChanged;
            };
            Unloaded += (o, e) => ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (Property.NameEquals(e.PropertyName, () => ViewModel.IsOpen))
            {
                if (ViewModel.IsOpen)
                {
                    Log.Write("MultiImageEditorView.OnViewModelPropertyChanged BeginOpenStoryboard");
                    BeginOpenStoryboard();
                }
                else
                {
                    BeginCloseStoryboard();
                }
            }
            else if (Property.NameEquals(e.PropertyName, () => ViewModel.IsDoneEnabled))
            {
                _doneButton.IsEnabled = ViewModel.IsDoneEnabled;
            }
        }

        private void OptimizeFullHD()
        {
            return;
#if WP8
            var appBar = ApplicationBar;
            if (appBar == null)
            {
                appBar = new ApplicationBar();
            }

            var appBarDefaultSize = appBar.DefaultSize;
            var appBarDifference = appBarDefaultSize - 72.0;

            ApplicationBarPlaceholder.Height = appBarDefaultSize;
#endif
        }

        private void BuildLocalizedAppBar()
        {
            return;

            if (ApplicationBar != null)
            {
                return;
            }

            ApplicationBar = new ApplicationBar { Opacity = 0.9999, IsVisible = false };
            //ApplicationBar.BackgroundColor = Colors.Black;
            //ApplicationBar.ForegroundColor = Colors.White;
            ApplicationBar.StateChanged += (o, e) =>
            {
                ApplicationBar.Opacity = e.IsMenuVisible ? 0.9999 : 0.0;
            };

            _doneButton.Click += (sender, args) => AppBarAction(ViewModel.Done);
            _doneButton.IsEnabled = false;
            _cancelButton.Click += (sender, args) => AppBarAction(ViewModel.Cancel);

            ApplicationBar.Buttons.Add(_doneButton);
            ApplicationBar.Buttons.Add(_cancelButton);
        }

        private void AppBarAction(System.Action action)
        {
            if (FocusManager.GetFocusedElement() == Caption)
            {
                Items.Focus();
                Telegram.Api.Helpers.Execute.BeginOnUIThread(TimeSpan.FromSeconds(0.20), action.SafeInvoke);
            }
            else
            {
                action.SafeInvoke();
            }
        }

        private void BeginCloseStoryboard()
        {
            SystemTray.IsVisible = true;
            //ApplicationBar.IsVisible = false;

            var duration = TimeSpan.FromSeconds(0.25);
            var easingFunction = new ExponentialEase { EasingMode = EasingMode.EaseIn, Exponent = 5.0 };

            var storyboard = new Storyboard();

            var rootFrameHeight = ((PhoneApplicationFrame)Application.Current.RootVisual).ActualHeight;
            var translateYTo = rootFrameHeight;
            var translateImageAniamtion = new DoubleAnimationUsingKeyFrames();
            translateImageAniamtion.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = duration, Value = translateYTo, EasingFunction = easingFunction });
            Storyboard.SetTarget(translateImageAniamtion, ImagesGrid);
            Storyboard.SetTargetProperty(translateImageAniamtion, new PropertyPath("(UIElement.RenderTransform).(CompositeTransform.TranslateY)"));
            storyboard.Children.Add(translateImageAniamtion);

            var opacityImageAniamtion = new DoubleAnimationUsingKeyFrames();
            opacityImageAniamtion.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.4), Value = 0 });
            Storyboard.SetTarget(opacityImageAniamtion, BackgroundBorder);
            Storyboard.SetTargetProperty(opacityImageAniamtion, new PropertyPath("Opacity"));
            storyboard.Children.Add(opacityImageAniamtion);

            var translateBarAniamtion = new DoubleAnimationUsingKeyFrames();
            translateBarAniamtion.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.15), Value = 0.0 });
            translateBarAniamtion.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.4), Value = translateYTo, EasingFunction = easingFunction });
            Storyboard.SetTarget(translateBarAniamtion, Bar);
            Storyboard.SetTargetProperty(translateBarAniamtion, new PropertyPath("(UIElement.RenderTransform).(CompositeTransform.TranslateY)"));
            storyboard.Children.Add(translateBarAniamtion);

            storyboard.Begin();
            storyboard.Completed += (o, args) =>
            {
                Visibility = Visibility.Collapsed;
            };
        }

        private void BeginOpenStoryboard()
        {
            SystemTray.IsVisible = false;
            //ApplicationBar.IsVisible = true;

            var transparentBlack = Colors.Black;
            transparentBlack.A = 0;

            CaptionWatermark.Visibility = Visibility.Visible;
            Visibility = Visibility.Visible;
            ImagesGrid.Opacity = 1.0;
            ImagesGrid.RenderTransform = new CompositeTransform();
            BackgroundBorder.Opacity = 1.0;


            var duration = TimeSpan.FromSeconds(0.25);
            var easingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 5.0 };

            var storyboard = new Storyboard();

            var rootFrameHeight = ((PhoneApplicationFrame)Application.Current.RootVisual).ActualHeight;
            var translateYTo = rootFrameHeight;

            ((CompositeTransform)ImagesGrid.RenderTransform).TranslateY = translateYTo;
            var translateImageAniamtion = new DoubleAnimationUsingKeyFrames();
            translateImageAniamtion.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.0), Value = translateYTo });
            translateImageAniamtion.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = duration, Value = 0.0, EasingFunction = easingFunction });
            Storyboard.SetTarget(translateImageAniamtion, ImagesGrid);
            Storyboard.SetTargetProperty(translateImageAniamtion, new PropertyPath("(UIElement.RenderTransform).(CompositeTransform.TranslateY)"));
            storyboard.Children.Add(translateImageAniamtion);

            var translateBarAniamtion = new DoubleAnimationUsingKeyFrames();
            translateBarAniamtion.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.15), Value = translateYTo });
            translateBarAniamtion.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.4), Value = 0.0, EasingFunction = easingFunction });
            Storyboard.SetTarget(translateBarAniamtion, Bar);
            Storyboard.SetTargetProperty(translateBarAniamtion, new PropertyPath("(UIElement.RenderTransform).(CompositeTransform.TranslateY)"));
            storyboard.Children.Add(translateBarAniamtion);

            storyboard.Completed += (sender, args) => ViewModel.OpenAnimationComplete();
            Deployment.Current.Dispatcher.BeginInvoke(storyboard.Begin);
        }

        private void Caption_OnGotFocus(object sender, RoutedEventArgs e)
        {
            var height = GetKeyboardHeightDifference();
            CaptionWatermark.Visibility = Visibility.Collapsed;
            KeyboardPlaceholder.Height = EmojiControl.PortraitOrientationHeight - ApplicationBarPlaceholder.ActualHeight;
            ImagesGrid.Margin = new Thickness(0.0, 0.0, 0.0, -height);

            var easingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 5.0 };
            var storyboard = new Storyboard();
            var translateImageAniamtion = new DoubleAnimationUsingKeyFrames();
            translateImageAniamtion.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.0), Value = 0.0 });
            translateImageAniamtion.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.25), Value = -height / 2.0, EasingFunction = easingFunction });
            Storyboard.SetTarget(translateImageAniamtion, ImagesGrid);
            Storyboard.SetTargetProperty(translateImageAniamtion, new PropertyPath("(UIElement.RenderTransform).(CompositeTransform.TranslateY)"));
            storyboard.Children.Add(translateImageAniamtion);

            Deployment.Current.Dispatcher.BeginInvoke(storyboard.Begin);
        }

        private double GetKeyboardHeightDifference()
        {
            var heightDifference = EmojiControl.PortraitOrientationHeight - Items.ActualHeight - ApplicationBarPlaceholder.ActualHeight;

            return heightDifference;
        }

        private void Caption_OnLostFocus(object sender, RoutedEventArgs e)
        {
            var height = GetKeyboardHeightDifference();
            CaptionWatermark.Visibility = string.IsNullOrEmpty(Caption.Text) ? Visibility.Visible : Visibility.Collapsed;
            KeyboardPlaceholder.Height = 0.0;
            ImagesGrid.Margin = new Thickness(0.0);

            var easingFunction = new ExponentialEase { EasingMode = EasingMode.EaseIn, Exponent = 5.0 };
            var storyboard = new Storyboard();
            var translateImageAniamtion = new DoubleAnimationUsingKeyFrames();
            translateImageAniamtion.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.0), Value = -height / 2.0 });
            translateImageAniamtion.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.20), Value = 0.0, EasingFunction = easingFunction });
            Storyboard.SetTarget(translateImageAniamtion, ImagesGrid);
            Storyboard.SetTargetProperty(translateImageAniamtion, new PropertyPath("(UIElement.RenderTransform).(CompositeTransform.TranslateY)"));
            storyboard.Children.Add(translateImageAniamtion);

            Deployment.Current.Dispatcher.BeginInvoke(storyboard.Begin);
        }

        private void Caption_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            CaptionWatermark.Visibility = string.IsNullOrEmpty(Caption.Text) && FocusManager.GetFocusedElement() != Caption ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Caption_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (ViewModel.Items.Count == 1)
                {
                    ViewModel.Done();
                }
                else
                {
                    Items.Focus();
                }
            }
        }

        private void Image_OnImageOpened(object sender, RoutedEventArgs e)
        {
            var image = (Image) sender;
            image.Opacity = 0.0;
            var storyboard = new Storyboard();

            var opacityImageAniamtion = new DoubleAnimationUsingKeyFrames();
            opacityImageAniamtion.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.5), Value = 1.0 });
            Storyboard.SetTarget(opacityImageAniamtion, image);
            Storyboard.SetTargetProperty(opacityImageAniamtion, new PropertyPath("Opacity"));
            storyboard.Children.Add(opacityImageAniamtion);

            storyboard.Begin();
        }

        private static List<Telegram.Api.WindowsPhone.Tuple<PhotoFile, Image>> _imagesCache = new List<Telegram.Api.WindowsPhone.Tuple<PhotoFile, Image>>();

        private void FrameworkElement_OnLoaded(object sender, RoutedEventArgs e)
        {
            var image = (Image) sender;
            var photoFile = image.DataContext as PhotoFile;
            _imagesCache.Add(new Telegram.Api.WindowsPhone.Tuple<PhotoFile, Image>(photoFile, image));
        }

        public static void ImageOpened(PhotoFile photoFile)
        {
            var tuple = _imagesCache.LastOrDefault(x => x.Item1 == photoFile);
            _imagesCache.Remove(tuple);
            if (tuple != null)
            {
                var image = tuple.Item2;
                image.Opacity = 0.0;
                var storyboard = new Storyboard();

                var opacityImageAniamtion = new DoubleAnimationUsingKeyFrames();
                opacityImageAniamtion.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.5), Value = 1.0 });
                Storyboard.SetTarget(opacityImageAniamtion, image);
                Storyboard.SetTargetProperty(opacityImageAniamtion, new PropertyPath("Opacity"));
                storyboard.Children.Add(opacityImageAniamtion);

                storyboard.Begin();
            }
        }

        private void ContextMenu_OnLoaded(object sender, RoutedEventArgs e)
        {
            var menu = (ContextMenu) sender;
            menu.Visibility = ViewModel.Items.FirstOrDefault(x => x.Message != null) != null ? Visibility.Visible : Visibility.Collapsed;
        }

        private void MenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            DeleteCurrentItem();
        }

        private void Delete_OnClick(object sender, RoutedEventArgs e)
        {
            DeleteCurrentItem();
        }

        private void DeleteCurrentItem()
        {
            var photoFile = ViewModel.CurrentItem;
            if (photoFile != null)
            {
                if (photoFile.IsButton) return;
                
                var index = ViewModel.Items.IndexOf(photoFile);
                if (index == -1) return;

                var container = Items.ItemContainerGenerator.ContainerFromItem(photoFile);
                if (container != null)
                {
                    var storyboard = new Storyboard();

                    var opacityImageAniamtion = new DoubleAnimationUsingKeyFrames();
                    opacityImageAniamtion.KeyFrames.Add(new EasingDoubleKeyFrame
                    {
                        KeyTime = TimeSpan.FromSeconds(0.25),
                        Value = 0.0
                    });
                    Storyboard.SetTarget(opacityImageAniamtion, container);
                    Storyboard.SetTargetProperty(opacityImageAniamtion, new PropertyPath("Opacity"));
                    storyboard.Children.Add(opacityImageAniamtion);

                    storyboard.Begin();
                    storyboard.Completed += (o, args) => ViewModel.Delete(photoFile);
                }
            }
        }
    }
}
