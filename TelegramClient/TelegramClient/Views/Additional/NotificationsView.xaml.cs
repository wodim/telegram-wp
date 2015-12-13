using System;
using System.Windows.Navigation;
using TelegramClient.Animation.Navigation;

namespace TelegramClient.Views.Additional
{
    public partial class NotificationsView
    {
        public NotificationsView()
        {
            InitializeComponent();

            //AnimationContext = LayoutRoot;
        }

        //protected override AnimatorHelperBase GetAnimation(AnimationType animationType, Uri toOrFrom)
        //{
        //    if (toOrFrom.OriginalString.Contains("ListPickerPage.xaml"))
        //    {
        //        return null;
        //    }

        //    return base.GetAnimation(animationType, toOrFrom);
        //}
    }
}