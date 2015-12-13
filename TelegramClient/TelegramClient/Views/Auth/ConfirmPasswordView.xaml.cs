using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;
using Telegram.Api.TL;
using TelegramClient.ViewModels.Auth;
using Execute = Telegram.Api.Helpers.Execute;

namespace TelegramClient.Views.Auth
{
    public partial class ConfirmPasswordView
    {
        public ConfirmPasswordViewModel ViewModel
        {
            get { return DataContext as ConfirmPasswordViewModel; }
        }

        public ConfirmPasswordView()
        {
            InitializeComponent();

            Loaded += (sender, args) => Execute.BeginOnUIThread(TimeSpan.FromSeconds(0.3), () => CodePasswordBox.Focus());
        }

        private void Passcode_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ViewModel.Confirm();
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
#if LOG_REGISTRATION
            TLUtils.WriteLog("ConfirmPasswordView.OnNavigatedTo");
#endif

            base.OnNavigatedTo(e);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
#if LOG_REGISTRATION
            TLUtils.WriteLog("ConfirmPasswordView.OnNavigatedFrom");
#endif

            base.OnNavigatedFrom(e);
        }
    }
}