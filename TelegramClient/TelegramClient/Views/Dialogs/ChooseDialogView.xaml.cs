using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using Microsoft.Phone.Shell;
using Telegram.Api.TL;
using Telegram.Controls.Extensions;
using TelegramClient.Resources;
using TelegramClient.ViewModels.Dialogs;

namespace TelegramClient.Views.Dialogs
{
    public partial class ChooseDialogView
    {
        public ChooseDialogViewModel ViewModel
        {
            get { return DataContext as ChooseDialogViewModel; }
        }

        private readonly ApplicationBarIconButton _searchButton = new ApplicationBarIconButton
        {
            Text = AppResources.Search,
            IconUri = new Uri("/Images/ApplicationBar/appbar.feature.search.rest.png", UriKind.Relative)
        };

        public ChooseDialogView()
        {
            InitializeComponent();

            _searchButton.Click += (sender, args) =>
            {
                ViewModel.Search();
            };

            Loaded += (sender, args) => BuildLocalizedAppBar();
        }

        private void BuildLocalizedAppBar()
        {
            if (ApplicationBar != null) return;

            ApplicationBar = new ApplicationBar();

            ApplicationBar.Buttons.Add(_searchButton);
        }

        private void MainItemGrid_OnTap(object sender, GestureEventArgs e)
        {
            var tapedItem = sender as FrameworkElement;
            if (tapedItem == null) return;

            var dialog = tapedItem.DataContext as TLDialogBase;
            if (dialog == null) return;

            if (!(tapedItem.RenderTransform is CompositeTransform))
            {
                tapedItem.RenderTransform = new CompositeTransform();
            }

            var tapedItemContainer = tapedItem.FindParentOfType<ListBoxItem>();
            if (tapedItemContainer != null)
            {
                tapedItemContainer = tapedItemContainer.FindParentOfType<ListBoxItem>();
            }

            var result = ViewModel.ChooseDialog(dialog);
            if (result)
            {
                ShellView.StartContinuumForwardOutAnimation(tapedItem, tapedItemContainer, false);
            }
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);

            if (!e.Cancel)
            {
                if (e.NavigationMode == NavigationMode.New
                    && e.Uri.ToString().EndsWith("DialogDetailsView.xaml"))
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
            }
        }

        private void NavigationTransition_OnEndTransition(object sender, RoutedEventArgs e)
        {
            ViewModel.ForwardInAnimationComplete();
        }
    }
}