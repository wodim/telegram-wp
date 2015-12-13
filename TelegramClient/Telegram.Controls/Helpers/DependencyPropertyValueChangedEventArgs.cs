using System;

namespace Telegram.Controls.Helpers
{
    /// <summary>
    /// Provides data for a DependencyPropertyChangedListener ValueChanged implementation.
    /// </summary>
    public class DependencyPropertyValueChangedEventArgs : EventArgs
    {
        internal DependencyPropertyValueChangedEventArgs(object oldValue, object newValue)
        {
            OldValue = oldValue;
            NewValue = newValue;
        }

        /// <summary>
        ///  Gets the value of the property before the change.
        /// </summary>
        public object OldValue { get; private set; }

        /// <summary>
        /// Gets the value of the property after the change.
        /// </summary>
        public object NewValue { get; private set; }
    }
}
