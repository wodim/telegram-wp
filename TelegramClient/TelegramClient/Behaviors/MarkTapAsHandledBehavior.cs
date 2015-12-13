using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interactivity;
using Microsoft.Expression.Interactivity.Media;

namespace TelegramClient.Behaviors
{
    public class HandlingEventTrigger : System.Windows.Interactivity.EventTrigger
    {
        protected override void OnEvent(System.EventArgs eventArgs)
        {
            var routedEventArgs = eventArgs as GestureEventArgs;
            if (routedEventArgs != null)
                routedEventArgs.Handled = true;

            base.OnEvent(eventArgs);
        }
    }

    public class MarkTapAsHandledBehavior : Behavior<Grid>
    {
        protected override void OnAttached()
        {
            AssociatedObject.Tap += AssociatedObjectOnTap;

            base.OnAttached();
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();

            AssociatedObject.Tap -= AssociatedObjectOnTap;
        }

        private void AssociatedObjectOnTap(object sender, GestureEventArgs gestureEventArgs)
        {
            gestureEventArgs.Handled = true;
        }
    }
}
