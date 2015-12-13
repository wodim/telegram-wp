using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Telegram.Api.TL;
using Telegram.EmojiPanel;
using Telegram.EmojiPanel.Controls.Emoji;

namespace TelegramClient.Views.Dialogs
{
    public partial class CommandsControl
    {
        public static readonly DependencyProperty ReplyMarkupProperty = DependencyProperty.Register(
            "ReplyMarkup", typeof (TLReplyKeyboardBase), typeof (CommandsControl), new PropertyMetadata(OnReplyMarkupChanged));

        public TLReplyKeyboardBase ReplyMarkup
        {
            get { return (TLReplyKeyboardBase)GetValue(ReplyMarkupProperty); }
            set { SetValue(ReplyMarkupProperty, value); }
        }
        private static void OnReplyMarkupChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var commandsControl = (CommandsControl) d;
            commandsControl.UpdateMarkup((TLReplyKeyboardBase) e.NewValue);
        }

        private void UpdateMarkup(TLReplyKeyboardBase replyKeyboardBase)
        {
            ButtonRows.Children.Clear();

            var replyMarkup = replyKeyboardBase as TLReplyKeyboardMarkup;
            if (replyMarkup == null)
            {
                Visibility = Visibility.Collapsed;
                return;
            }

            var height = EmojiControl.PortraitOrientationHeight;
            var buttonRowsHeight = height - ButtonRows.Margin.Top - ButtonRows.Margin.Bottom;
            var buttonMargin = 3.0;
            var buttonHeight = 78.0;    // without margin
            if (!replyMarkup.IsResizable
                && buttonHeight*replyMarkup.Rows.Count < buttonRowsHeight)
            {
                buttonHeight = buttonRowsHeight / replyMarkup.Rows.Count - 2 * buttonMargin;
            }

            foreach (var buttonRow in replyMarkup.Rows)
            {
                var grid = new Grid();

                for (var i = 0; i < buttonRow.Buttons.Count; i++)
                {
                    var button = CreateButton(buttonRow.Buttons[i], buttonHeight, buttonMargin);
                    Grid.SetColumn(button, i);

                    grid.ColumnDefinitions.Add(new ColumnDefinition());
                    grid.Children.Add(button);
                }

                ButtonRows.Children.Add(grid);
            }

            LayoutRoot.MaxHeight = height;
            if (replyMarkup.IsResizable)
            {
                LayoutRoot.ClearValue(HeightProperty);
            }
            else
            {
                LayoutRoot.Height = height;
            }
            Visibility = Visibility.Visible;
            ScrollViewer.VerticalScrollBarVisibility = buttonHeight * replyMarkup.Rows.Count > buttonRowsHeight
                ? ScrollBarVisibility.Auto
                : ScrollBarVisibility.Disabled;
        }

        private FrameworkElement CreateButton(TLKeyboardButton keyboardButton, double height, double margin)
        {
            var isLightTheme = (Visibility)Application.Current.Resources["PhoneLightThemeVisibility"] == Visibility.Visible;
            var background = isLightTheme ? (Brush)Resources["ButtonLightBackground"] : (Brush)Resources["ButtonBackground"];

            var text = keyboardButton.Text.ToString();
            var textBox = new TelegramRichTextBox { Text = text, MaxHeight = height, Margin = new Thickness(0.0, -4.0, 0.0, 0.0), FontSize = 22, FontFamily = new FontFamily("Segoe WP Semibold") };
            BrowserNavigationService.SetSuppressParsing(textBox, true);

            var button = new Button();
            button.Style = (Style)Resources["CommandButtonStyle"];
            button.Height = height;
            button.Margin = new Thickness(margin);
            button.Background = background;

            button.Content = textBox;
            button.DataContext = keyboardButton;
            button.Click += OnButtonClick;

            return button;
        }

        private void OnButtonClick(object sender, RoutedEventArgs e)
        {
            var border = sender as FrameworkElement;
            if (border != null)
            {
                var keyboardButton = border.DataContext as TLKeyboardButton;
                if (keyboardButton != null)
                {
                    RaiseButtonClick(new KeyboardButtonEventArgs { Button = keyboardButton });
                }
            }
        }

        public event EventHandler<KeyboardButtonEventArgs> ButtonClick;

        protected virtual void RaiseButtonClick(KeyboardButtonEventArgs e)
        {
            var handler = ButtonClick;
            if (handler != null) handler(this, e);
        }

        public CommandsControl()
        {
            InitializeComponent();
        }
    }

    public class KeyboardButtonEventArgs : System.EventArgs
    {
        public TLKeyboardButton Button { get; set; }
    }
}
