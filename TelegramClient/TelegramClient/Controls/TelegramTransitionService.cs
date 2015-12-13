// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Windows;

namespace TelegramClient.Controls
{
    /// <summary>
    /// Provides attached properties for navigation
    /// <see cref="T:Microsoft.Phone.Controls.ITransition"/>s.
    /// </summary>
    /// <QualityBand>Preview</QualityBand>
    public static class TelegramTransitionService
    {
        /// <summary>
        /// The
        /// <see cref="T:System.Windows.DependencyProperty"/>
        /// for the in <see cref="T:Microsoft.Phone.Controls.ITransition"/>s.
        /// </summary>
        public static readonly DependencyProperty NavigationInTransitionProperty =
            DependencyProperty.RegisterAttached("NavigationInTransition", typeof(TelegramNavigationInTransition), typeof(TelegramTransitionService), null);

        /// <summary>
        /// The
        /// <see cref="T:System.Windows.DependencyProperty"/>
        /// for the in <see cref="T:Microsoft.Phone.Controls.ITransition"/>s.
        /// </summary>
        public static readonly DependencyProperty NavigationOutTransitionProperty =
            DependencyProperty.RegisterAttached("NavigationOutTransition", typeof(TelegramNavigationOutTransition), typeof(TelegramTransitionService), null);

        /// <summary>
        /// Gets the
        /// <see cref="T:Microsoft.Phone.Controls.NavigationTransition"/>s
        /// of
        /// <see cref="M:Microsoft.Phone.Controls.TransitionService.NavigationInTransitionProperty"/>
        /// for a
        /// <see cref="T:System.Windows.UIElement"/>.
        /// </summary>
        /// <param name="element">The <see cref="T:System.Windows.UIElement"/>.</param>
        /// <returns>The </returns>
        public static TelegramNavigationInTransition GetNavigationInTransition(UIElement element)
        {
            if (element == null)
            {
                throw new ArgumentNullException("element");
            }
            return (TelegramNavigationInTransition)element.GetValue(NavigationInTransitionProperty);
        }

        /// <summary>
        /// Gets the
        /// <see cref="T:Microsoft.Phone.Controls.NavigationTransition"/>s
        /// of
        /// <see cref="M:Microsoft.Phone.Controls.TransitionService.NavigationOutTransitionProperty"/>
        /// for a
        /// <see cref="T:System.Windows.UIElement"/>.
        /// </summary>
        /// <param name="element">The <see cref="T:System.Windows.UIElement"/>.</param>
        /// <returns>The </returns>
        public static TelegramNavigationOutTransition GetNavigationOutTransition(UIElement element)
        {
            if (element == null)
            {
                throw new ArgumentNullException("element");
            }
            return (TelegramNavigationOutTransition)element.GetValue(NavigationOutTransitionProperty);
        }

        /// <summary>
        /// Sets a
        /// <see cref="T:Microsoft.Phone.Controls.NavigationTransition"/>
        /// to
        /// <see cref="M:Microsoft.Phone.Controls.TransitionService.NavigationInTransitionProperty"/>
        /// for a
        /// <see cref="T:System.Windows.UIElement"/>.
        /// </summary>
        /// <param name="element">The <see cref="T:System.Windows.UIElement"/>.</param>
        /// <param name="value">The <see cref="T:Microsoft.Phone.Controls.NavigationTransition"/>.</param>
        /// <returns>The </returns>
        public static void SetNavigationInTransition(UIElement element, TelegramNavigationInTransition value)
        {
            if (element == null)
            {
                throw new ArgumentNullException("element");
            }
            element.SetValue(NavigationInTransitionProperty, value);
        }

        /// <summary>
        /// Sets a
        /// <see cref="T:Microsoft.Phone.Controls.NavigationTransition"/>s
        /// to
        /// <see cref="M:Microsoft.Phone.Controls.TransitionService.NavigationOutTransitionProperty"/>
        /// for a
        /// <see cref="T:System.Windows.UIElement"/>.
        /// </summary>
        /// <param name="element">The <see cref="T:System.Windows.UIElement"/>.</param>
        /// <param name="value">The <see cref="T:Microsoft.Phone.Controls.NavigationTransition"/>.</param>
        /// <returns>The </returns>
        public static void SetNavigationOutTransition(UIElement element, TelegramNavigationOutTransition value)
        {
            if (element == null)
            {
                throw new ArgumentNullException("element");
            }
            element.SetValue(NavigationOutTransitionProperty, value);
        }
    }
}
