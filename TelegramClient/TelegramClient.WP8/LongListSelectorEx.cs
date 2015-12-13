using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Caliburn.Micro;
using Microsoft.Phone.Controls;
using Telegram.Api.TL;

namespace TelegramClient
{
    public class LongListSelectorEx : LongListSelector
    {
        public static readonly DependencyProperty IsSelectionEnabledProperty = DependencyProperty.Register(
            "IsSelectionEnabled", typeof (bool), typeof (LongListSelectorEx), new PropertyMetadata(OnIsSelectionEnabledChanged));

        private static void OnIsSelectionEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var lls = (LongListSelectorEx) d;

            var projection = lls.Projection as PlaneProjection;
            var upDown = projection != null && projection.RotationZ == 180.0;
            var x = upDown ? -60.0 : 60.0;

            var viewport = lls.Viewport;
            if ((bool)e.NewValue)
            {
                var storyboard = new Storyboard();
                if (!(viewport.RenderTransform is CompositeTransform))
                {
                    viewport.RenderTransform = new CompositeTransform();
                }

                if (viewport.CacheMode == null)
                {
                    viewport.CacheMode = new BitmapCache();
                }

                var translateXAnimation = new DoubleAnimationUsingKeyFrames();
                translateXAnimation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.0), Value = 0.0 });
                translateXAnimation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.55), Value = x, EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 5.0 } });
                Storyboard.SetTarget(translateXAnimation, viewport);
                Storyboard.SetTargetProperty(translateXAnimation, new PropertyPath("(UIElement.RenderTransform).(CompositeTransform.TranslateX)"));
                storyboard.Children.Add(translateXAnimation);

                //storyboard.Begin();
                Deployment.Current.Dispatcher.BeginInvoke(storyboard.Begin);
            }
            else
            {
                var storyboard = new Storyboard();
                if (!(viewport.RenderTransform is CompositeTransform))
                {
                    viewport.RenderTransform = new CompositeTransform();
                }

                var translateXAnimation = new DoubleAnimationUsingKeyFrames();
                translateXAnimation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.0), Value = x });
                translateXAnimation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.55), Value = 0.0, EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 5.0 } });
                Storyboard.SetTarget(translateXAnimation, viewport);
                Storyboard.SetTargetProperty(translateXAnimation, new PropertyPath("(UIElement.RenderTransform).(CompositeTransform.TranslateX)"));
                storyboard.Children.Add(translateXAnimation);

                storyboard.Begin();
                //Deployment.Current.Dispatcher.BeginInvoke(() => storyboard.Begin());
            }
        }

        public bool IsSelectionEnabled
        {
            get { return (bool) GetValue(IsSelectionEnabledProperty); }
            set { SetValue(IsSelectionEnabledProperty, value); }
        }

        public static readonly DependencyProperty IsFirstSliceLoadedProperty = DependencyProperty.Register(
            "IsFirstSliceLoaded", typeof(bool), typeof(LongListSelectorEx), new PropertyMetadata(true));

        public bool IsFirstSliceLoaded
        {
            get { return (bool)GetValue(IsFirstSliceLoadedProperty); }
            set { SetValue(IsFirstSliceLoadedProperty, value); }
        }

        public int MeasureOverrideCount { get; set; }

        protected override Size MeasureOverride(Size availableSize)
        {
            try
            {
                MeasureOverrideCount++;
                return base.MeasureOverride(availableSize);
            }
            catch (ArgumentException e)
            {
                return base.MeasureOverride(availableSize);
            }
        }

        private int _knob = 1;

        public int Knob
        {
            get { return _knob; }
            set { _knob = value; }
        }

        public ScrollBar VerticalScrollBar { get; protected set; }

        public ViewportControl Viewport { get; protected set; }

        public LongListSelectorEx()
        {
            ItemRealized += OnItemRealized;
        }

        public override void OnApplyTemplate()
        {
            Viewport = (ViewportControl)GetTemplateChild("ViewportControl");

            Viewport.ViewportChanged += OnViewportChanged;

            base.OnApplyTemplate();
        }

        private void OnViewportChanged(object sender, ViewportChangedEventArgs e)
        {
            if (Viewport.Bounds.Y - Viewport.Viewport.Y == 0.0)
            {
                RaiseBegin();
            }

            if ((Viewport.Bounds.Height + Viewport.Bounds.Y) >= ActualHeight
                && (Viewport.Bounds.Height + Viewport.Bounds.Y) == (Viewport.Viewport.Height + Viewport.Viewport.Y))
            {
                //Telegram.Api.Helpers.Execute.ShowDebugMessage("CloseToEnd ActualHeight=" + ActualHeight + " Height+Y=" + (Viewport.Bounds.Height + Viewport.Bounds.Y));
                RaiseCloseToEnd();
            }
        }

        public bool IsHoldingScrollingPosition
        {
            get { return ListHeader != null; }
        }

        public void HoldScrollingPosition()
        {
            ListHeader = null;
        }

        public void UnholdScrollingPosition()
        {
            ListHeader = new Border { Visibility = Visibility.Collapsed };
        }

        public void ScrollToItem(object item)
        {
            if (Viewport.Bounds.Y - Viewport.Viewport.Y != 0.0)
            {
                //MessageBox.Show("ScrollToItem");
                ScrollTo(item);
            }
        }

        public event EventHandler CloseToEnd;

        protected virtual void RaiseCloseToEnd()
        {
            var handler = CloseToEnd;
            if (handler != null) handler(this, System.EventArgs.Empty);
        }

        public event EventHandler CloseToBegin;

        protected virtual void RaiseCloseToBegin()
        {
            var handler = CloseToBegin;
            if (handler != null) handler(this, System.EventArgs.Empty);
        }

        public event EventHandler Begin;

        protected virtual void RaiseBegin()
        {
            var handler = Begin;
            if (handler != null) handler(this, System.EventArgs.Empty);
        }


        public static readonly DependencyProperty DownButtonVisibilityProperty = DependencyProperty.Register(
            "DownButtonVisibility", typeof(Visibility), typeof(LongListSelectorEx), new PropertyMetadata(Visibility.Collapsed));

        public Visibility DownButtonVisibility
        {
            get { return (Visibility)GetValue(DownButtonVisibilityProperty); }
            set { SetValue(DownButtonVisibilityProperty, value); }
        }

        public static readonly DependencyProperty PrevIndexProperty = DependencyProperty.Register(
            "PrevIndex", typeof(int), typeof(LongListSelectorEx), new PropertyMetadata(default(int)));

        public int PrevIndex
        {
            get { return (int)GetValue(PrevIndexProperty); }
            set { SetValue(PrevIndexProperty, value); }
        }

        public static readonly DependencyProperty IndexProperty = DependencyProperty.Register(
            "Index", typeof(int), typeof(LongListSelectorEx), new PropertyMetadata(default(int)));

        public int Index
        {
            get { return (int)GetValue(IndexProperty); }
            set { SetValue(IndexProperty, value); }
        }

        private void OnItemRealized(object sender, ItemRealizationEventArgs e)
        {
            //MessageBox.Show("ItemRealized");
            //OnViewportChanged(sender, new ViewportChangedEventArgs());

            var longListSelector = this;

            var item = e.Container.Content;


            var items = longListSelector.ItemsSource;
            var index = items.IndexOf(item);

            //if (items.Count >= Knob
            //    && e.Container.Content.Equals(longListSelector.ItemsSource[longListSelector.ItemsSource.Count - Knob]))
            //{
            //    InvokeActions(null);
            //}
            if (index > 20
                && IsFirstSliceLoaded
                && PrevIndex > index)
            {
                DownButtonVisibility = Visibility.Visible;
            }
            else
            {
                DownButtonVisibility = Visibility.Collapsed;
                //_prevIndex = 0;
            }

            PrevIndex = index;

            if (items.Count - index <= Knob)
            {
                if (ManipulationState != ManipulationState.Idle)
                {
                    RaiseCloseToEnd();
                    return;
                }
            }

            if (index <= Knob)
            {
                if (ManipulationState != ManipulationState.Idle)
                {
                    RaiseCloseToBegin();
                    return;
                }
            }


            if (LayoutMode == LongListSelectorLayoutMode.List)
            {
                var message = e.Container.Content as TLMessageBase;
                if (message == null) return;

                if (!message._isAnimated)
                {
                    e.Container.Opacity = 1.0;
                    return;
                }

                message._isAnimated = false;

                if (Visibility == Visibility.Collapsed) return;

                e.Container.Opacity = 0.0;



                if (message.Index == 160952)
                {
                    TLUtils.WriteLine("e.Container.Opacity="+e.Container.Opacity + " Tag=" + e.Container.Tag + " Parent=" + e.Container.Parent, LogSeverity.Error);
                }

                if (e.Container.Tag != null && (bool) e.Container.Tag)
                {
                    StartLoadingAnimation(e.Container);
                    return;
                }
                e.Container.Loaded += OnContainerLoaded;
                e.Container.Unloaded += OnContainerUnloaded;
            }
        }

        private void OnContainerUnloaded(object sender, RoutedEventArgs e)
        {
            var container = (ContentPresenter)sender;
            container.Tag = false;
            container.Unloaded -= OnContainerUnloaded;
        }

        private void OnContainerLoaded(object sender, RoutedEventArgs e)
        {
            var container = (ContentPresenter)sender;
            container.Tag = true;
            container.Loaded -= OnContainerLoaded;
            
            StartLoadingAnimation(container);
        }

        private void StartLoadingAnimation(ContentPresenter container)
        {
            var message = container.Content as TLMessageBase;
            if (message!= null
                && message.Index == 160952)
            {
                TLUtils.WriteLine("startAnimation", LogSeverity.Error);
            }

            container.CacheMode = new BitmapCache();
            var storyboard = new Storyboard();
            var opacityAnimation = new DoubleAnimation { To = 1.0, Duration = TimeSpan.FromSeconds(1.0) };
            Storyboard.SetTarget(opacityAnimation, container);
            Storyboard.SetTargetProperty(opacityAnimation, new PropertyPath("(UIElement.Opacity)"));
            storyboard.Children.Add(opacityAnimation);

            Deployment.Current.Dispatcher.BeginInvoke(storyboard.Begin);
        }
    }
}
