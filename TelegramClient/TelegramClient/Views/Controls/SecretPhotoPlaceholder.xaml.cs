using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Telegram.Api.TL;
using TelegramClient.Resources;

namespace TelegramClient.Views.Controls
{
    public partial class SecretPhotoPlaceholder
    {
        private const double OpenDelaySeconds = 0.15;

        public static readonly DependencyProperty TTLParamsProperty = DependencyProperty.Register(
            "TTLParams", typeof (TTLParams), typeof (SecretPhotoPlaceholder), new PropertyMetadata(default(TTLParams), OnTTLParamsChanged));

        public TTLParams TTLParams
        {
            get { return (TTLParams) GetValue(TTLParamsProperty); }
            set { SetValue(TTLParamsProperty, value); }
        }

        private static void OnTTLParamsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var secretPhotoPlaceholder = (SecretPhotoPlaceholder)d;
            var oldTTLParams = e.OldValue as TTLParams;
            var newTTLParams = e.NewValue as TTLParams;

            if (newTTLParams != null)
            {
                if (newTTLParams.IsStarted)
                {
                    if (newTTLParams.Out)
                    {
                        secretPhotoPlaceholder.GasIcon.Visibility = Visibility.Collapsed;
                        secretPhotoPlaceholder.CheckIcon.Visibility = Visibility.Visible;

                        return;
                    }

                    if (secretPhotoPlaceholder.TimerStoryboard.GetCurrentState() == ClockState.Active)
                    {
                        return;
                    }

                    var progressAnimation = secretPhotoPlaceholder.TimerProgressAnimation;
                    var elapsed = (DateTime.Now - newTTLParams.StartTime).TotalSeconds;

                    var remaining = newTTLParams.Total - elapsed;
                    if (remaining > 0)
                    {
                        progressAnimation.From = remaining / newTTLParams.Total * 359.0;
                        progressAnimation.Duration = TimeSpan.FromSeconds(remaining);
                    }

                    secretPhotoPlaceholder.TimerStoryboard.Begin();
                }
                else
                {
                    secretPhotoPlaceholder.GasIcon.Visibility = Visibility.Visible;
                    secretPhotoPlaceholder.CheckIcon.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                secretPhotoPlaceholder.GasIcon.Visibility = Visibility.Visible;
                secretPhotoPlaceholder.CheckIcon.Visibility = Visibility.Collapsed;
            }
        }

        public SecretPhotoPlaceholder()
        {
            InitializeComponent();
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(0.03);
            _timer.Tick += OnTimerTick;

            Unloaded += (sender, args) =>
            {
                _timer.Stop();
                //TimerStoryboard.Stop();
            };
        }

        public event EventHandler StartTimer;

        protected virtual void RaiseStartTimer()
        {
            var handler = StartTimer;
            if (handler != null) handler(this, System.EventArgs.Empty);
        }

        private void OnTimerTick(object sender, System.EventArgs e)
        {
            if (_leftButtonDownTime.HasValue)
            {
                if ((DateTime.Now - _leftButtonDownTime.Value).TotalSeconds > OpenDelaySeconds)
                {
                    _timer.Stop();
                    _leftButtonDownTime = null;

                    //StartTimerAnimation();
                    RaiseStartTimer();
                }
            }
            else
            {
                _timer.Stop();
            }
        }

        //private void StartTimerAnimation()
        //{
            
        //}

        public event EventHandler Elapsed;

        protected virtual void RaiseElapsed()
        {
            var handler = Elapsed;
            if (handler != null) handler(this, System.EventArgs.Empty);
        }

        private void TimerStoryboard_OnCompleted(object sender, System.EventArgs e)
        {
            RaiseElapsed();
            Telegram.Api.Helpers.Execute.BeginOnUIThread(TimeSpan.FromSeconds(0.2), () =>
            {
                GasIcon.Visibility = Visibility.Visible;
                CheckIcon.Visibility = Visibility.Collapsed;
            });
        }

        private DateTime? _leftButtonDownTime;

        private readonly DispatcherTimer _timer;

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            _leftButtonDownTime = DateTime.Now;
            _timer.Start();

            base.OnMouseLeftButtonDown(e);
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if (_leftButtonDownTime.HasValue && (DateTime.Now - _leftButtonDownTime.Value).TotalSeconds < OpenDelaySeconds)
            {
                MessageBox.Show(AppResources.TapAndHoldToView);
            }
            base.OnMouseLeftButtonUp(e);
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            _leftButtonDownTime = null;

            base.OnMouseLeave(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            _leftButtonDownTime = null;

            base.OnMouseMove(e);
        }
    }
}
