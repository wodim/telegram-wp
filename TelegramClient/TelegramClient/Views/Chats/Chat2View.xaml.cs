using System.Diagnostics;
using System.Windows;

namespace TelegramClient.Views.Chats
{
    public partial class Chat2View
    {
        public static readonly DependencyProperty TimerProperty = DependencyProperty.Register(
            "Timer", typeof(string), typeof(Chat2View), new PropertyMetadata(default(string)));

        public string Timer
        {
            get { return (string) GetValue(TimerProperty); }
            set { SetValue(TimerProperty, value); }
        }

        public Chat2View()
        {
            var timer = Stopwatch.StartNew();
            InitializeComponent();

            Loaded += (sender, args) =>
            {
                Timer = timer.Elapsed.ToString();
            };
        }
    }
}