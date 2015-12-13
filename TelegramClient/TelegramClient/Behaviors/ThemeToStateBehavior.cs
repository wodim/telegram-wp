using System.Windows;
using System.Windows.Controls;
using System.Windows.Interactivity;

namespace TelegramClient.Behaviors
{
    public class ThemeToStateBehavior : Behavior<Control>
    {
        protected override void OnAttached()
        {
            base.OnAttached();
            var isLightTheme = (Visibility)Application.Current.Resources["PhoneLightThemeVisibility"] == Visibility.Visible;

            Dispatcher.BeginInvoke(() =>  VisualStateManager.GoToState(AssociatedObject, isLightTheme ? LightState : DarkState, true));
        }

        [CustomPropertyValueEditor(CustomPropertyValueEditor.StateName)]
        public string LightState { get; set; }

        [CustomPropertyValueEditor(CustomPropertyValueEditor.StateName)]
        public string DarkState { get; set; }
    }
}
