using System;
using TelegramClient.Animation.Navigation;

namespace TelegramClient.Views.Chats
{
    public partial class ChannelIntroView
    {
        public ChannelIntroView()
        {
            InitializeComponent();

            AnimationContext = LayoutRoot;
        }

        protected override AnimatorHelperBase GetAnimation(AnimationType animationType, Uri toOrFrom)
        {
            if (animationType == AnimationType.NavigateForwardIn
                || animationType == AnimationType.NavigateBackwardIn)
            {
                return new SwivelShowAnimator { RootElement = LayoutRoot };
            }

            return new SwivelHideAnimator { RootElement = LayoutRoot };
        }
    }
}