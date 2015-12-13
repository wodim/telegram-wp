using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interactivity;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Phone.Controls;

namespace TelegramClient.Behaviors
{
    public class PanAndZoomBehavior : Behavior<FrameworkElement>
    {
        private const double MinZoom = 1.0;
        private readonly CompositeTransform _old = new CompositeTransform();
        private double _initialScale;
        private GestureListener _listener;



        public PanAndZoomBehavior()
        {
            MaxZoom = 10.0;
        }

        /// <summary>
        /// This does not enforce zoom bounds on setting.
        /// </summary>
        public double MaxZoom { get; set; }

        public static readonly DependencyProperty CanZoomProperty = DependencyProperty.Register(
            "CanZoom", typeof (bool), typeof (PanAndZoomBehavior), new PropertyMetadata(default(bool)));

        public bool CanZoom
        {
            get { return (bool) GetValue(CanZoomProperty); }
            set { SetValue(CanZoomProperty, value); }
        }


        public static readonly DependencyProperty CurrentScaleXProperty = DependencyProperty.Register(
            "CurrentScaleX", typeof (double), typeof (PanAndZoomBehavior), new PropertyMetadata(1.0));

        public double CurrentScaleX
        {
            get { return (double) GetValue(CurrentScaleXProperty); }
            set { SetValue(CurrentScaleXProperty, value); }
        }

        public static readonly DependencyProperty CurrentScaleYProperty = DependencyProperty.Register(
            "CurrentScaleY", typeof (double), typeof (PanAndZoomBehavior), new PropertyMetadata(1.0));

        public double CurrentScaleY
        {
            get { return (double) GetValue(CurrentScaleYProperty); }
            set { SetValue(CurrentScaleYProperty, value); }
        }

        public static readonly DependencyProperty BackgroundPanelProperty = DependencyProperty.Register(
            "BackgroundPanel", typeof (Panel), typeof (PanAndZoomBehavior), new PropertyMetadata(default(Panel)));

        public Panel BackgroundPanel
        {
            get { return (Panel) GetValue(BackgroundPanelProperty); }
            set { SetValue(BackgroundPanelProperty, value); }
        }

        public static readonly DependencyProperty DebugTextProperty = DependencyProperty.Register(
            "DebugText", typeof (TextBlock), typeof (PanAndZoomBehavior), new PropertyMetadata(default(TextBlock)));

        private Orientation? _direction;

        public TextBlock DebugText
        {
            get { return (TextBlock) GetValue(DebugTextProperty); }
            set { SetValue(DebugTextProperty, value); }
        }

        public event EventHandler<FlickGestureEventArgs> Flick;

        protected virtual void RaiseFlick(FlickGestureEventArgs e)
        {
            var handler = Flick;
            if (handler != null) handler(this, e);
        }

        public event EventHandler<DragDeltaGestureEventArgs> DragDelta;

        protected virtual void RaiseDragDelta(DragDeltaGestureEventArgs e)
        {
            var handler = DragDelta;
            if (handler != null) handler(this, e);
        }

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.RenderTransform = new CompositeTransform();
            _listener = GestureService.GetGestureListener(AssociatedObject);
            _listener.PinchDelta += OnPinchDelta;
            _listener.PinchStarted += OnPinchStarted;
            _listener.DragStarted += OnDragStarted;
            _listener.DragDelta += OnDragDelta;
            _listener.DragCompleted += OnDragCompleted;
            _listener.Flick += OnFlick;
            _listener.Tap += OnTap;
            _listener.DoubleTap += OnDoubleTap;
            // wait for the RootVisual to be initialized
            //Dispatcher.BeginInvoke(() =>
            //    ((PhoneApplicationFrame)Application.Current.RootVisual).OrientationChanged += OrientationChanged);
        }

        protected override void OnDetaching()
        {
            //((PhoneApplicationPage)Application.Current.RootVisual).OrientationChanged -= OrientationChanged;
            _listener.Flick -= OnFlick;
            _listener.PinchDelta -= OnPinchDelta;
            _listener.PinchStarted -= OnPinchStarted;
            _listener.DragStarted -= OnDragStarted;
            _listener.DragDelta -= OnDragDelta;
            _listener.DragCompleted -= OnDragCompleted;
            _listener.Tap -= OnTap;
            _listener.DoubleTap -= OnDoubleTap;
            _listener = null;
            base.OnDetaching();
        }

        public event EventHandler<GestureEventArgs> DoubleTap;

        protected virtual void RaiseDoubleTap(GestureEventArgs e)
        {
            var handler = DoubleTap;
            if (handler != null) handler(this, e);
        }

        private void OnDoubleTap(object sender, GestureEventArgs e)
        {
            RaiseDoubleTap(e);   
        }

        public event EventHandler<GestureEventArgs> Tap;

        protected virtual void RaiseTap(GestureEventArgs e)
        {
            var handler = Tap;
            if (handler != null) handler(this, e);
        }

        private void OnTap(object sender, GestureEventArgs e)
        {
            RaiseTap(e);
        }

        public event EventHandler<DragCompletedGestureEventArgs> Close;

        protected virtual void RaiseClose(DragCompletedGestureEventArgs e)
        {
            var handler = Close;
            if (handler != null) handler(this, e);
        }

        private void OnDragCompleted(object sender, DragCompletedGestureEventArgs e)
        {
            if (CurrentScaleX != 1.0)
            {
                return;
            }

            var frameworkElement = sender as FrameworkElement;
            var transform = frameworkElement.RenderTransform as CompositeTransform;

            if (DebugText != null)
            {
                DebugText.Text = string.Format("DragCompleted TranslateX={0} TranslateY={1}", transform.TranslateX, transform.TranslateY);
            }

            if (_direction == Orientation.Vertical)
            {
                if (Math.Abs(transform.TranslateY) > 100)
                {
                    RaiseClose(e);
                    return;
                }
                else
                {
                    var storyboard = new Storyboard();

                    var translateAnimation = new DoubleAnimationUsingKeyFrames();
                    translateAnimation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.25), Value = 0.0, EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 6.0 } });
                    Storyboard.SetTarget(translateAnimation, frameworkElement);
                    Storyboard.SetTargetProperty(translateAnimation, new PropertyPath("(UIElement.RenderTransform).(CompositeTransform.TranslateY)"));
                    storyboard.Children.Add(translateAnimation);

                    storyboard.Begin();

                    if (BackgroundPanel != null)
                    {
                        BackgroundPanel.Opacity = 1.0;
                        //BackgroundPanel.Background = new SolidColorBrush(Colors.Black);
                    }
                }
            }
            //else if (_direction == Orientation.Horizontal && Math.Abs(transform.TranslateX) < 150)
            //{
            //    var storyboard = new Storyboard();

            //    var translateAnimation = new DoubleAnimationUsingKeyFrames();
            //    translateAnimation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.15), Value = 0.0, EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 6.0 } });
            //    Storyboard.SetTarget(translateAnimation, img);
            //    Storyboard.SetTargetProperty(translateAnimation, new PropertyPath("(UIElement.RenderTransform).(CompositeTransform.TranslateX)"));
            //    storyboard.Children.Add(translateAnimation);

            //    storyboard.Begin();
            //}


            _direction = null;
        }

        private void OnFlick(object sender, FlickGestureEventArgs e)
        {
            RaiseFlick(e);
        }



        private void OnDragStarted(object sender, DragStartedGestureEventArgs e)
        {
            _direction = e.Direction;
        }

        private void OnPinchDelta(object sender, PinchGestureEventArgs e)
        {
            if (!CanZoom) return;

            var frameworkElement = sender as FrameworkElement;
            var transform = frameworkElement.RenderTransform as CompositeTransform;
            var a = transform.Transform(e.GetPosition(frameworkElement, 0)); // we need the points to be relative to the current transform
            var b = transform.Transform(e.GetPosition(frameworkElement, 1));

            var scale = new CompositeTransform
            {
                CenterX = (a.X + b.X) / 2,
                CenterY = (a.Y + b.Y) / 2,
                ScaleX = Clamp(e.DistanceRatio * _initialScale / _old.ScaleX,
                    MinZoom / _old.ScaleX,
                    MaxZoom / _old.ScaleX),
                ScaleY = Clamp(e.DistanceRatio * _initialScale / _old.ScaleY,
                    MinZoom / _old.ScaleY,
                    MaxZoom / _old.ScaleY)
            };

            ConstrainToParentBounds(frameworkElement, scale);

            transform = ComposeScaleTranslate(transform, scale);
            frameworkElement.RenderTransform = transform;

            _old.CenterX = transform.CenterX;
            _old.CenterY = transform.CenterY;
            _old.TranslateX = transform.TranslateX;
            _old.TranslateY = transform.TranslateY;
            _old.ScaleX = transform.ScaleX;
            _old.ScaleY = transform.ScaleY;

            CurrentScaleX = _old.ScaleX;
            CurrentScaleY = _old.ScaleY;
        }

        private void OnPinchStarted(object sender, PinchStartedGestureEventArgs e)
        {
            if (!CanZoom) return;

            var img = sender as FrameworkElement;
            var transform = img.RenderTransform as CompositeTransform;

            _old.CenterX = transform.CenterX;
            _old.CenterY = transform.CenterY;
            _old.TranslateX = transform.TranslateX;
            _old.TranslateY = transform.TranslateY;
            _old.ScaleX = transform.ScaleX;
            _old.ScaleY = transform.ScaleY;
            _initialScale = transform.ScaleX;
        }

        private void OnDragDelta(object sender, DragDeltaGestureEventArgs e)
        {
            RaiseDragDelta(e);

            //if (CurrentScaleX == 1.0) return;


            //DebugText.Text = string.Format("DragDelta X={0} Y={1} Direction={2}", e.HorizontalChange, e.VerticalChange, e.Direction);

            // Translation is done as the last operation, so no need to move the operation up in composition order
            var img = sender as FrameworkElement;
            if (img == null) return;
            var transform = img.RenderTransform as CompositeTransform;
            if (transform == null) return;

            CompositeTransform translate = null;
            if (CurrentScaleX == 1.0)
            {
                if (_direction == Orientation.Vertical)
                {
                    translate = new CompositeTransform
                    {
                        TranslateX = 0,
                        TranslateY = e.VerticalChange
                    };

                    if (BackgroundPanel != null)
                    {
                        var rootFrameHeight = ((PhoneApplicationFrame)Application.Current.RootVisual).ActualHeight;
                        var deltaY = Math.Abs(translate.TranslateY + translate.ScaleY * transform.TranslateY + (translate.ScaleY - 1) * (transform.CenterY - translate.CenterY));
                        var opacity = (rootFrameHeight - deltaY) / rootFrameHeight;
                        var backgroundBrush = (SolidColorBrush)BackgroundPanel.Background;
                        var backgroundColor = backgroundBrush.Color;
                        backgroundColor.A = (byte)(opacity * byte.MaxValue);

                        BackgroundPanel.Opacity = opacity;
                        //BackgroundPanel.Background = new SolidColorBrush(backgroundColor);
                    }

                    img.RenderTransform = ComposeScaleTranslate(transform, translate);
                }
                else if (_direction == Orientation.Horizontal)
                {
                    //if (CurrentScaleX == 1.0)
                    //{
                    //    var translate = new CompositeTransform
                    //    {
                    //        TranslateX = e.HorizontalChange,
                    //        TranslateY = 0
                    //    };

                    //    img.RenderTransform = ComposeScaleTranslate(transform, translate);
                    //}
                }
            }
            else
            {
                if (!CanZoom) return;

                translate = new CompositeTransform
                {
                    TranslateX = e.HorizontalChange,
                    TranslateY = e.VerticalChange
                };

                ConstrainToParentBounds(img, translate);

                img.RenderTransform = ComposeScaleTranslate(transform, translate);
            }
        }

        private static void ConstrainToParentBounds(FrameworkElement elm, CompositeTransform transform)
        {
            var p = (FrameworkElement)elm.Parent;
            var canvas = p.TransformToVisual(elm).TransformBounds(new Rect(0, 0, p.ActualWidth, p.ActualHeight));
            // Now compute the new viewport relative to the previous
            var newViewport = transform.TransformBounds(new Rect(0, 0, elm.ActualWidth, elm.ActualHeight));

            var top = newViewport.Top - canvas.Top;
            var bottom = canvas.Bottom - newViewport.Bottom;
            var left = newViewport.Left - canvas.Left;
            var right = canvas.Right - newViewport.Right;

            if (top > 0)
                if (top + bottom > 0)
                    transform.TranslateY += (bottom - top) / 2;
                else
                    transform.TranslateY -= top;
            else if (bottom > 0)
                if (top + bottom > 0)
                    transform.TranslateY += (bottom - top) / 2;
                else
                    transform.TranslateY += bottom;

            if (left > 0)
                if (left + right > 0)
                    transform.TranslateX += (right - left) / 2;
                else
                    transform.TranslateX -= left;
            else if (right > 0)
                if (left + right > 0)
                    transform.TranslateX += (right - left) / 2;
                else
                    transform.TranslateX += right;
        }

        public static CompositeTransform ComposeScaleTranslate(CompositeTransform fst, CompositeTransform snd)
        {
            // See http://stackoverflow.com/a/19439099/388010 on why this works
            var compositTransform =  new CompositeTransform
            {
                ScaleX = fst.ScaleX * snd.ScaleX,
                ScaleY = fst.ScaleY * snd.ScaleY,
                CenterX = fst.CenterX,
                CenterY = fst.CenterY,
                TranslateX = snd.TranslateX + snd.ScaleX * fst.TranslateX + (snd.ScaleX - 1) * (fst.CenterX - snd.CenterX),
                TranslateY = snd.TranslateY + snd.ScaleY * fst.TranslateY + (snd.ScaleY - 1) * (fst.CenterY - snd.CenterY),
            };

            return compositTransform;
        }

        private static double Clamp(double val, double min, double max)
        {
            return val > min ? val < max ? val : max : min;
        }

        private void OrientationChanged(object sender, OrientationChangedEventArgs e)
        {
            // Handling orientation change is a heck more involved than I initially thought
            AssociatedObject.RenderTransform = new CompositeTransform();
        }
    }
}
