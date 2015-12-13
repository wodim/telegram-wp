using System;
using Caliburn.Micro;
using Telegram.Api.TL;

namespace TelegramClient.ViewModels.Dialogs
{
    public class UserActionViewModel : PropertyChangedBase
    {
        public TLUserBase User { get; protected set; }

        public UserActionViewModel(TLUserBase user)
        {
            User = user;
        }

        public static bool IsRequired(TLObject obj)
        {
            var userBase = obj as TLUserBase;

            return 
                userBase is TLUserRequest
                && !userBase.RemoveUserAction && userBase.Index != 777000;
        }

        public void SetUser(TLUserBase user)
        {
            User = user;
            NotifyOfPropertyChange(() => User);
        }

        public event EventHandler InvokeUserAction;

        protected virtual void RaiseInvokeUserAction()
        {
            var handler = InvokeUserAction;
            if (handler != null) handler(this, System.EventArgs.Empty);
        }

        public event EventHandler InvokeUserAction2;

        protected virtual void RaiseInvokeUserAction2()
        {
            var handler = InvokeUserAction2;
            if (handler != null) handler(this, System.EventArgs.Empty);
        }

        public void Invoke()
        {
            RaiseInvokeUserAction();
        }

        public void Invoke2()
        {
            RaiseInvokeUserAction2();
        }

        public void Remove()
        {
            if (User == null) return;

            User.RemoveUserAction = true;
        }
    }
}
