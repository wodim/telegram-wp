using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Navigation;
using Caliburn.Micro;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using TelegramClient.Services;
using TelegramClient.ViewModels.Additional;
using TelegramClient.Views.Additional;

namespace TelegramClient.Controls
{
    public class TelegramTransitionFrame : TransitionFrame
    {
        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
            "Title", typeof (string), typeof (TelegramTransitionFrame), new PropertyMetadata(default(string)));

        public string Title
        {
            get { return (string) GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }

        private UIElement _blockingProgress;

        private Border _clientArea;

        private StackPanel _blockingPanel;

        private LockscreenView _passcodePanel;

        public TelegramTransitionFrame()
        {
            DefaultStyleKey = typeof(TelegramTransitionFrame);

            Loaded += OnLoaded;

            Navigating += OnNavigating;
        }

        private void OnNavigating(object sender, NavigatingCancelEventArgs e)
        {
            if (_passcodePanel != null
                && _passcodePanel.Visibility == Visibility.Visible
                && e.IsCancelable)
            {
                e.Cancel = true;
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _blockingProgress = GetTemplateChild("BlockingProgress") as Border;
            _clientArea = GetTemplateChild("ClientArea") as Border;
            _blockingPanel = GetTemplateChild("BlockingPanel") as StackPanel;
            _passcodePanel = GetTemplateChild("PasscodePanel") as LockscreenView;

            if (PasscodeUtils.IsLockscreenRequired)
            {
                OpenLockscreen();
            }
        }

        private TranslateTransform _frameTransform;

        public static readonly DependencyProperty RootFrameTransformProperty = DependencyProperty.Register(
            "RootFrameTransformProperty", typeof(double), typeof(TelegramTransitionFrame), new PropertyMetadata(OnRootFrameTransformChanged));

        private static void OnRootFrameTransformChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var view = d as TelegramTransitionFrame;
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

        private IList<object> _buttons = new List<object>();
        private IList<object> _menuItems = new List<object>();

        private bool _removeApplicationBar;
        private double _previousOpacity;
        private Color _previousColor;
        private bool _isSystemTrayVisible;

        public bool IsPasscodeActive
        {
            get { return _passcodePanel != null && _passcodePanel.Visibility == Visibility.Visible; }
        }

        private bool _stateExists;

        public void OpenLockscreen()
        {
            if (_passcodePanel != null && _clientArea != null)
            {
                if (_passcodePanel.DataContext == null)
                {
                    var viewModel = new LockscreenViewModel();
                    _passcodePanel.DataContext = viewModel;
                    viewModel.PasscodeIncorrect += _passcodePanel.OnPasscodeIncorrect;
                }
                _passcodePanel.Visibility = Visibility.Visible;
                var page = Content as PhoneApplicationPage;
                if (page != null)
                {
                    _passcodePanel.ParentPage = page;
                    SetRootFrameBinding();                    
                    page.IsHitTestVisible = false;

                    if (!_stateExists)
                    {
                        _stateExists = true;
                        _isSystemTrayVisible = SystemTray.IsVisible;
                        SystemTray.IsVisible = false;
                        if (page.ApplicationBar != null)
                        {
                            if (_buttons.Count == 0)
                            {
                                foreach (var button in page.ApplicationBar.Buttons)
                                {
                                    _buttons.Add(button);
                                }
                            }

                            if (_menuItems.Count == 0)
                            {
                                foreach (var menuItem in page.ApplicationBar.MenuItems)
                                {
                                    _menuItems.Add(menuItem);
                                }
                            }
                            //page.ApplicationBar.IsVisible = false;
                            page.ApplicationBar.Buttons.Clear();
                            page.ApplicationBar.MenuItems.Clear();
                        }
                        else
                        {
                            page.ApplicationBar = new ApplicationBar();
                            //page.ApplicationBar.IsVisible = false;
                            _removeApplicationBar = true;
                        }
                        _previousColor = page.ApplicationBar.BackgroundColor;
                        _previousOpacity = page.ApplicationBar.Opacity;
                        page.ApplicationBar.Opacity = 1.0;
                        page.ApplicationBar.BackgroundColor = Colors.Transparent;
                    }
                }
                _passcodePanel.FocusPasscode();
            }
        }

        public void CloseLockscreen()
        {
            if (_passcodePanel != null && _clientArea != null)
            {
                _passcodePanel.Visibility = Visibility.Collapsed;

                var page = Content as PhoneApplicationPage;
                if (page != null)
                {
                    RemoveRootFrameBinding();
                    page.IsHitTestVisible = true;
                    _stateExists = false;
                    SystemTray.IsVisible = _isSystemTrayVisible;
                    if (_removeApplicationBar)
                    {
                        page.ApplicationBar = null;
                        _removeApplicationBar = false;
                    }

                    if (page.ApplicationBar != null)
                    {
                        page.ApplicationBar.Buttons.Clear();
                        page.ApplicationBar.MenuItems.Clear();
                        foreach (var button in _buttons)
                        {
                            page.ApplicationBar.Buttons.Add(button);
                        }
                        foreach (var menuItem in _menuItems)
                        {
                            page.ApplicationBar.MenuItems.Add(menuItem);
                        }

                        _buttons.Clear();
                        _menuItems.Clear();

                        page.ApplicationBar.BackgroundColor = _previousColor;
                        page.ApplicationBar.Opacity = _previousOpacity;
                        //page.ApplicationBar.IsVisible = true;
                    }
                }
            }
        }

        public void OpenBlockingProgress()
        {
            if (_blockingProgress != null && _clientArea != null)
            {
                _clientArea.IsHitTestVisible = false;
                _blockingProgress.Visibility = Visibility.Visible;
                _blockingPanel.Visibility = Visibility.Visible;
            }
        }

        public void CloseBlockingProgress()
        {
            if (_blockingProgress != null && _clientArea != null)
            {
                _clientArea.IsHitTestVisible = true;
                _blockingProgress.Visibility = Visibility.Collapsed;
                _blockingPanel.Visibility = Visibility.Collapsed;
            }
        }

        public bool IsBlockingProgressOpen()
        {
            return _blockingProgress != null && _blockingProgress.Visibility == Visibility.Visible;
        }
    }
}
