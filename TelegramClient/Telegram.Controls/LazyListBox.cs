using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using DanielVaughan.WindowsPhone7Unleashed;
using Telegram.Controls.Extensions;

namespace Telegram.Controls
{
    public enum CompressionType { Top, Bottom, Left, Right };

    public class LazyListBox : ListBox
    {
        private const string VerticalCompressionGroup = "VerticalCompression";
        private const string HorizontalCompressionGroup = "HorizontalCompression";
        private const string ScrollStatesGroup = "ScrollStates";
        private const string NoHorizontalCompressionState = "NoHorizontalCompression";
        private const string CompressionRightState = "CompressionRight";
        private const string CompressionLeftState = "CompressionLeft";
        private const string NoVerticalCompressionState = "NoVerticalCompression";
        private const string CompressionTopState = "CompressionTop";
        private const string CompressionBottomState = "CompressionBottom";
        private const string ScrollingState = "Scrolling";

        public double PanelVerticalOffset { get; set; }

        public double PanelViewPortHeight { get; set; }

        private VirtualizingStackPanel _stackPanel;

        private ScrollViewer _scrollViewer;

        public ScrollViewer Scroll
        {
            get { return _scrollViewer; }
        }

        protected bool IsBouncy;

        private bool _isInitialized;

        private readonly DependencyPropertyListener _listener = new DependencyPropertyListener();

        public LazyListBox()
        {

            Loaded += ListBox_Loaded;
            //Unloaded += ListBox_Unloaded;
        }

        private void OnListenerChanged(object sender, BindingChangedEventArgs e)
        {
            if (_prevVerticalOffset >= _scrollViewer.VerticalOffset) return;
            if (_scrollViewer.VerticalOffset == 0.0 && _scrollViewer.ScrollableHeight == 0.0) return;

            _prevVerticalOffset = _scrollViewer.VerticalOffset;
            var atBottom = _scrollViewer.VerticalOffset
                                >= _scrollViewer.ScrollableHeight * CloseToEndPercent;

            if (atBottom)
            {
                RaiseCloseToEnd();
            }
        }

        public void DetachPropertyListener()
        {
            if (_listener == null) return;

            _listener.Detach();
            _listener.Changed -= OnListenerChanged;
        }


        public void StopScrolling()
        {
            //stop scrolling


            var offset = _stackPanel.VerticalOffset;

            if (_scrollViewer != null)
            {
                _scrollViewer.InvalidateScrollInfo();
                _scrollViewer.ScrollToVerticalOffset(offset);
                VisualStateManager.GoToState(_scrollViewer, "NotScrolling", true);
            }
        }

        public void ScrollToBeginning()
        {

            _scrollViewer.ScrollToBeginnig(new Duration(TimeSpan.FromSeconds(0.3)));
        }

        private void ListBox_Unloaded(object sender, RoutedEventArgs e)
        {
            _listener.Detach();
        }

        private void ListBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isInitialized)
                return;
 
            _isInitialized = true;
            
            AddHandler(ManipulationCompletedEvent, new EventHandler<ManipulationCompletedEventArgs>(ListBox_ManipulationCompleted), true);

            _scrollViewer = this.FindChildOfType<ScrollViewer>();
 
            if (_scrollViewer != null)
            {
                _stackPanel = _scrollViewer.FindChildOfType<VirtualizingStackPanel>();

                // Visual States are always on the first child of the control template 
                var element = VisualTreeHelper.GetChild(_scrollViewer, 0) as FrameworkElement;
                if (element != null)
                {
                    var verticalGroup = FindVisualState(element, VerticalCompressionGroup);
                    var horizontalGroup = FindVisualState(element, HorizontalCompressionGroup);
                    var scrollStatesGroup = FindVisualState(element, ScrollStatesGroup); 

                    if (verticalGroup != null)
                        verticalGroup.CurrentStateChanging += VerticalGroup_CurrentStateChanging;
                    if (horizontalGroup != null)
                        horizontalGroup.CurrentStateChanging += HorizontalGroup_CurrentStateChanging;
                    if (scrollStatesGroup != null)
                        scrollStatesGroup.CurrentStateChanging += ScrollStateGroup_CurrentStateChanging;
                }



                _listener.Changed += OnListenerChanged;
                Binding binding = new Binding("VerticalOffset") { Source = _scrollViewer };
                _listener.Attach(_scrollViewer, binding);
            }
        }

        private double _closeToEndPercent = 0.7;

        public double CloseToEndPercent
        {
            get { return _closeToEndPercent; }
            set { _closeToEndPercent = value; }
        }

        private double _prevVerticalOffset;

        public event EventHandler<EventArgs> CloseToEnd;

        protected virtual void RaiseCloseToEnd()
        {
            var handler = CloseToEnd;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void ScrollStateGroup_CurrentStateChanging(object sender, VisualStateChangedEventArgs e)
        {
            IsScrolling = (e.NewState.Name == ScrollingState);

        }

        /// <summary>
        /// The event people can subscribe to
        /// </summary>
        public event EventHandler<ScrollingStateChangedEventArgs> ScrollingStateChanged;

        /// <summary>
        /// DependencyProperty that backs the <see cref="IsScrolling"/> property
        /// </summary>
        public static readonly DependencyProperty IsScrollingProperty = DependencyProperty.Register(
            "IsScrolling",
            typeof(bool),
            typeof(LazyListBox),
            new PropertyMetadata(false, IsScrollingPropertyChanged));

        /// <summary>
        /// Whether the list is currently scrolling or not
        /// </summary>
        public bool IsScrolling
        {
            get { return (bool)GetValue(IsScrollingProperty); }
            set { SetValue(IsScrollingProperty, value); }
        }

        /// <summary>
        /// Handler for when the IsScrolling dependency property changes
        /// </summary>
        /// <param name="source">The object that has the property</param>
        /// <param name="e">Args</param>
        static void IsScrollingPropertyChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
        {
            var listbox = source as LazyListBox;
            if (listbox == null) return;

            // Call the virtual notification method for anyone who derives from this class
            var scrollingArgs = new ScrollingStateChangedEventArgs((bool)e.OldValue, (bool)e.NewValue);

            // Raise the event, if anyone is listening to it
            var handler = listbox.ScrollingStateChanged;
            if (handler != null)
                handler(listbox, scrollingArgs);
        }

        #region Compression
        public event EventHandler<CompressionEventArgs> Compression;
 
        private void HorizontalGroup_CurrentStateChanging(object sender, VisualStateChangedEventArgs e)
        {
            if (e.NewState.Name == CompressionLeftState)
            {
                IsBouncy = true;
                if (Compression != null)
                    Compression(this, new CompressionEventArgs(CompressionType.Left));
            }

            if (e.NewState.Name == CompressionRightState)
            {
                IsBouncy = true;
                if (Compression != null)
                    Compression(this, new CompressionEventArgs(CompressionType.Right));
            }
            if (e.NewState.Name == NoHorizontalCompressionState)
            {
                IsBouncy = false;
            }
        }
 
        private void VerticalGroup_CurrentStateChanging(object sender, VisualStateChangedEventArgs e)
        {
            if (e.NewState.Name == CompressionTopState)
            {
                IsBouncy = true;
                if (Compression != null)
                    Compression(this, new CompressionEventArgs(CompressionType.Top));
            }
            if (e.NewState.Name == CompressionBottomState)
            {
                IsBouncy = true;
                if(Compression!=null)
                    Compression(this, new CompressionEventArgs(CompressionType.Bottom));
            }
            if (e.NewState.Name == NoVerticalCompressionState)
                IsBouncy = false;
        }
 
        private void ListBox_ManipulationCompleted(object sender, ManipulationCompletedEventArgs e)
        {
            if (IsBouncy)
                IsBouncy = false;
        }
 
        private static VisualStateGroup FindVisualState(FrameworkElement element, string stateName)
        {
            if (element == null)
                return null;
 
            var groups = VisualStateManager.GetVisualStateGroups(element);
            return groups.Cast<VisualStateGroup>().FirstOrDefault(group => group.Name == stateName);
        }
        #endregion
    }

    /// <summary>
    /// Event args for the <see cref="LazyListBox.ScrollingStateChanged"/> event
    /// </summary>
    public class ScrollingStateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Old scrolling value
        /// </summary>
        public bool OldValue { get; private set; }

        /// <summary>
        /// New scrolling value
        /// </summary>
        public bool NewValue { get; private set; }

        /// <summary>
        /// Create a new instance of the event args
        /// </summary>
        /// <param name="oldValue">Old scrolling value</param>
        /// <param name="newValue">New scrolling value</param>
        public ScrollingStateChangedEventArgs(bool oldValue, bool newValue)
        {
            OldValue = oldValue;
            NewValue = newValue;
        }
    }
 
    public class CompressionEventArgs : EventArgs
    {
        public CompressionType Type { get; protected set; }
 
        public CompressionEventArgs(CompressionType type)
        {
            Type = type;
        }
    }
}
