using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Telegram.Controls.Helpers;

namespace Telegram.Controls
{
    public class WatermarkedTextBox : TextBox
    {
        public static readonly DependencyProperty TextScaleFactorProperty = DependencyProperty.Register(
            "TextScaleFactor", typeof(double), typeof(WatermarkedTextBox), new PropertyMetadata(1.0, OnTextScaleFactorChanged));

        private static void OnTextScaleFactorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var textBox = (WatermarkedTextBox)d;
            if (textBox != null && textBox._contentElement != null)
            {
                textBox.FontSize = textBox._defaultFontSize * (double)e.NewValue;
            }
        }

        private double _defaultFontSize;

        public double TextScaleFactor
        {
            get { return (double)GetValue(TextScaleFactorProperty); }
            set { SetValue(TextScaleFactorProperty, value); }
        }


        private ContentControl _watermarkContent;

        private ContentControl _contentElement;

        public static readonly DependencyProperty WatermarkForegroundProperty = DependencyProperty.Register(
            "WatermarkForeground", typeof (Brush), typeof (WatermarkedTextBox), new PropertyMetadata(default(Brush)));

        public Brush WatermarkForeground
        {
            get { return (Brush) GetValue(WatermarkForegroundProperty); }
            set { SetValue(WatermarkForegroundProperty, value); }
        }

        public static readonly DependencyProperty WatermarkProperty =
            DependencyProperty.Register("Watermark", typeof(object), typeof(WatermarkedTextBox), new PropertyMetadata(OnWatermarkPropertyChanged));

        public static readonly DependencyProperty WatermarkStyleProperty =
            DependencyProperty.Register("WatermarkStyle", typeof(Style), typeof(WatermarkedTextBox), null);

        public Style WatermarkStyle
        {
            get { return GetValue(WatermarkStyleProperty) as Style; }
            set { SetValue(WatermarkStyleProperty, value); }
        }

        public object Watermark
        {
            get { return GetValue(WatermarkProperty); }
            set { SetValue(WatermarkProperty, value); }
        }

        private readonly DependencyPropertyChangedListener _listener;

        public WatermarkedTextBox()
        {
            DefaultStyleKey = typeof(WatermarkedTextBox);

            _listener = DependencyPropertyChangedListener.Create(this, "Text");
            _listener.ValueChanged += OnTextChanged;
        }

        private void OnTextChanged(object sender, DependencyPropertyValueChangedEventArgs args)
        {
            if (_watermarkContent != null)
            {
                _watermarkContent.Opacity = !string.IsNullOrEmpty(Text) ? 0.0 : 0.5;
            }
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _defaultFontSize = FontSize;
            if (TextScaleFactor > 1.0)
            {
                FontSize = _defaultFontSize*TextScaleFactor;
            }

            _watermarkContent = GetTemplateChild("WatermarkContent") as ContentControl;
            _contentElement = GetTemplateChild("ContentElement") as ContentControl;
            if (_watermarkContent != null)
            {
                DetermineWatermarkContentVisibility();
            }
        }

        protected override void OnGotFocus(RoutedEventArgs e)
        {
            if (_watermarkContent != null && string.IsNullOrEmpty(Text))
            {
                _watermarkContent.Opacity = 0.0;
            }
            base.OnGotFocus(e);
        }

        protected override void OnLostFocus(RoutedEventArgs e)
        {
            if (_watermarkContent != null && string.IsNullOrEmpty(Text))
            {
                _watermarkContent.Opacity = 0.5;
            }
            base.OnLostFocus(e);
        }

        private static void OnWatermarkPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            var watermarkTextBox = sender as WatermarkedTextBox;
            if (watermarkTextBox != null && watermarkTextBox._watermarkContent != null)
            {
                watermarkTextBox.DetermineWatermarkContentVisibility();
            }
        }

        private void DetermineWatermarkContentVisibility()
        {
            _watermarkContent.Opacity = string.IsNullOrEmpty(Text) ? 0.5 : 0.0;
        }
    }
}
