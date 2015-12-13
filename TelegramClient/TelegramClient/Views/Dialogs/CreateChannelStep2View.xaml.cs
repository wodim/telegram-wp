using System;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Controls;
using Caliburn.Micro;
using Microsoft.Phone.Shell;
using TelegramClient.Animation.Navigation;
using TelegramClient.Resources;
using TelegramClient.ViewModels.Dialogs;

namespace TelegramClient.Views.Dialogs
{
    public partial class CreateChannelStep2View : IDisposable
    {
        private readonly IDisposable _keyPressSubscription;

        public CreateChannelStep2ViewModel ViewModel
        {
            get { return DataContext as CreateChannelStep2ViewModel; }
        }

        private readonly AppBarButton _nextButton = new AppBarButton
        {
            Text = AppResources.Next,
            IconUri = new Uri("/Images/ApplicationBar/appbar.next.png", UriKind.Relative)
        };

        public CreateChannelStep2View()
        {
            InitializeComponent();

            AnimationContext = LayoutRoot;

            _nextButton.Click += (sender, args) => ViewModel.Next();

            var keyPressEvents = Observable.FromEventPattern<TextChangedEventHandler, TextChangedEventArgs>(
                keh => { UserName.TextChanged += keh; },
                keh => { UserName.TextChanged -= keh; });

            _keyPressSubscription = keyPressEvents
                .Throttle(TimeSpan.FromSeconds(0.3))
                .ObserveOnDispatcher()
                .Subscribe(e => ViewModel.Check());



            Loaded += (sender, args) =>
            {
                BuildLocalizedAppBar();

                ViewModel.EmptyUserName += OnEmptyUserName;
            };

            Unloaded += (sender, args) =>
            {
                ViewModel.EmptyUserName -= OnEmptyUserName;
            };
        }

        private void OnEmptyUserName(object sender, System.EventArgs e)
        {
            UserName.Focus();
        }

        protected override AnimatorHelperBase GetAnimation(AnimationType animationType, Uri toOrFrom)
        {
            if (animationType == AnimationType.NavigateForwardIn
                || animationType == AnimationType.NavigateBackwardIn)
            {
                return new SwivelShowAnimator { RootElement = LayoutRoot };
            }

            return new SwivelHideAnimator { RootElement = LayoutRoot };
        }

        private void BuildLocalizedAppBar()
        {
            if (ApplicationBar != null) return;

            ApplicationBar = new ApplicationBar();
            ApplicationBar.Buttons.Add(_nextButton);
        }

        private void CopyInvite_OnClick(object sender, RoutedEventArgs e)
        {
            ViewModel.CopyInvite();
        }

        public void Dispose()
        {
            _keyPressSubscription.Dispose();
        }

        private void UserName_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            
        }
    }
}