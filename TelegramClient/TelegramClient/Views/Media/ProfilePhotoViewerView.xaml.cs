using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using TelegramClient.Helpers;
using TelegramClient.Resources;
using TelegramClient.ViewModels.Media;

namespace TelegramClient.Views.Media
{
    public partial class ProfilePhotoViewerView
    {
        public ProfilePhotoViewerViewModel ViewModel
        {
            get { return DataContext as ProfilePhotoViewerViewModel; }
        }

        private readonly ApplicationBarMenuItem _savePhotoMenuItem = new ApplicationBarMenuItem { Text = AppResources.Save };

        private readonly ApplicationBarMenuItem _deletePhotoMenuItem = new ApplicationBarMenuItem { Text = AppResources.Delete };

        private readonly ApplicationBarMenuItem _sharePhotoMenuItem = new ApplicationBarMenuItem { Text = AppResources.Share + "..." };

        private readonly ApplicationBarMenuItem _setPhotoMenuItem = new ApplicationBarMenuItem { Text = AppResources.Set, IsEnabled = false };

        private bool _runOnce;

        public ProfilePhotoViewerView()
        {
            InitializeComponent();

            Visibility = Visibility.Collapsed;

            _savePhotoMenuItem.Click += (o, e) => ViewModel.SavePhoto();
            _deletePhotoMenuItem.Click += (o, e) => ViewModel.DeletePhoto();
            _sharePhotoMenuItem.Click += (o, e) => ViewModel.SharePhoto();
            _setPhotoMenuItem.Click += (o, e) => ViewModel.SetPhoto();

            BuildLocalizedAppBar();

            _runOnce = true;

            Loaded += (o, e) =>
            {

                if (_runOnce && ViewModel.CurrentItem != null)
                {
                    _runOnce = false;
                    BeginOpenStoryboard();
                }

                ViewModel.PropertyChanged += OnViewModelPropertyChanged;
            };

            Unloaded += (o, e) =>
            {
                ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            };
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (Property.NameEquals(e.PropertyName, () => ViewModel.CurrentItem))
            {
                _setPhotoMenuItem.IsEnabled = ViewModel.CanSlideRight;

                if (ApplicationBar != null)
                {
                    ApplicationBar.MenuItems.Remove(_deletePhotoMenuItem);
                    if (!ViewModel.CanSlideRight)
                    {
                        ApplicationBar.MenuItems.Insert(1, _deletePhotoMenuItem);
                    }
                }
            } 
            else if (Property.NameEquals(e.PropertyName, () => ViewModel.IsOpen))
            {
                if (ViewModel.IsOpen)
                {
                    BeginOpenStoryboard();
                }
                else
                {
                    BeginCloseStoryboard();
                }
            }
        }

        private void BuildLocalizedAppBar()
        {
            if (ApplicationBar == null)
            {
                ApplicationBar = new ApplicationBar { Opacity = 0.3 };
                ApplicationBar.BackgroundColor = Colors.Black;
                ApplicationBar.ForegroundColor = Colors.White;
                ApplicationBar.StateChanged += (o, e) => { ApplicationBar.Opacity = e.IsMenuVisible ? 0.9999 : 0.3; };
            }

            ApplicationBar.MenuItems.Add(_savePhotoMenuItem);
            ApplicationBar.MenuItems.Add(_deletePhotoMenuItem);
#if WP8
            ApplicationBar.MenuItems.Add(_sharePhotoMenuItem);
#endif
            ApplicationBar.MenuItems.Add(_setPhotoMenuItem);
        }

        private void GestureListener_OnFlick(object sender, FlickGestureEventArgs e)
        {
            if (PanAndZoom == null) return;
            if (PanAndZoom.CurrentScaleX != 1.0) return;

            if (e.HorizontalVelocity > 0 && (e.Angle > 330 || e.Angle < 30))
            {
                if (ViewModel.CanSlideRight)
                {
                    var storyboard = (Storyboard)Resources["SlideRightAnimation"];
                    storyboard.Begin();
                }
            }
            else if (e.HorizontalVelocity < 0 && (e.Angle > 150 && e.Angle < 210))
            {
                if (ViewModel.CanSlideLeft)
                {
                    var storyboard = (Storyboard)Resources["SlideLeftAnimation"];
                    storyboard.Begin();
                }
            }

            e.Handled = true;
        }

        private void SlideRightAnimation_OnCompleted(object sender, System.EventArgs e)
        {
            ViewModel.SlideRight();

            var storyboard = FadeInFromLeftAnimation;
            Deployment.Current.Dispatcher.BeginInvoke(() => storyboard.Begin());
        }

        private void SlideLeftAnimation_OnCompleted(object sender, System.EventArgs e)
        {
            ViewModel.SlideLeft();

            var storyboard = FadeInFromRightAnimation;
            Deployment.Current.Dispatcher.BeginInvoke(() => storyboard.Begin());
        }

        private CloseDirection? _direction;

        private void BeginCloseStoryboard()
        {
            SystemTray.IsVisible = true;
            ApplicationBar.IsVisible = false;

            var direction = _direction ?? CloseDirection.Down;
            var duration = _direction != null ? TimeSpan.FromSeconds(0.15) : TimeSpan.FromSeconds(0.25);
            var easingFunction = _direction != null ? null : new ExponentialEase { EasingMode = EasingMode.EaseIn, Exponent = 5.0 };

            var storyboard = new Storyboard();

            var rootFrameHeight = ((PhoneApplicationFrame)Application.Current.RootVisual).ActualHeight;
            var translateYTo = ImagesGrid.ActualHeight / 2 + rootFrameHeight / 2;
            var translateImageAniamtion = new DoubleAnimationUsingKeyFrames();
            translateImageAniamtion.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = duration, Value = direction == CloseDirection.Down ? translateYTo : -translateYTo, EasingFunction = easingFunction });
            Storyboard.SetTarget(translateImageAniamtion, ImagesGrid);
            Storyboard.SetTargetProperty(translateImageAniamtion, new PropertyPath("(UIElement.RenderTransform).(CompositeTransform.TranslateY)"));
            storyboard.Children.Add(translateImageAniamtion);

            var opacityImageAniamtion = new DoubleAnimationUsingKeyFrames();
            opacityImageAniamtion.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = duration, Value = 0 });
            Storyboard.SetTarget(opacityImageAniamtion, BackgroundBorder);
            Storyboard.SetTargetProperty(opacityImageAniamtion, new PropertyPath("Opacity"));
            storyboard.Children.Add(opacityImageAniamtion);

            storyboard.Begin();
            storyboard.Completed += (o, args) =>
            {
                Visibility = Visibility.Collapsed;
                _direction = null;
            };

            //CloseApplicationPanelAnimation.Begin();
        }

        private void BeginOpenStoryboard()
        {
            SystemTray.IsVisible = false;
            ApplicationBar.IsVisible = true;
            //PanAndZoom.CurrentScaleX = 1.0;
            //PanAndZoom.CurrentScaleY = 1.0;

            var transparentBlack = Colors.Black;
            transparentBlack.A = 0;

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

            storyboard.Completed += (sender, args) =>
            {
                //Deployment.Current.Dispatcher.BeginInvoke(() => OpenApplicationPanelAnimation.Begin());
            };
            Deployment.Current.Dispatcher.BeginInvoke(storyboard.Begin);
        }

        private bool _handled;

        private void PanAndZoom_OnTap(object sender, GestureEventArgs e)
        {
            if (_handled)
            {
                _handled = false;
                return;
            }

            if (ApplicationBar != null)
            {
                ApplicationBar.IsVisible = !ApplicationBar.IsVisible;
            }

            //var animation = ApplicationPanel.Visibility == Visibility.Visible
            //    ? CloseApplicationPanelAnimation
            //    : OpenApplicationPanelAnimation;

            //animation.Begin();
        }

        private void PanAndZoom_OnClose(object sender, DragCompletedGestureEventArgs e)
        {
            _direction = e.VerticalChange > 0 ? CloseDirection.Down : CloseDirection.Up;

            ViewModel.CloseViewer();
        }
    }
}