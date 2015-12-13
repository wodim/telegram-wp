using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using TelegramClient.ViewModels.Additional;

namespace TelegramClient.Views.Additional
{
    public partial class ChangePasswordView
    {
        public ChangePasswordViewModel ViewModel
        {
            get { return DataContext as ChangePasswordViewModel; }
        }

        public ChangePasswordView()
        {
            InitializeComponent();

            Loaded += (sender, args) =>
            {
                Telegram.Api.Helpers.Execute.BeginOnUIThread(() => PasscodeBox.Focus());
            };
        }

        private void Passcode_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ConfirmOrCompletePasscode();
            }
            else if (e.Key == Key.Back)
            {
                if (ConfirmPasscodeBox.IsFocused
                    && ConfirmPasscodeBox.Length == 0)
                {
                    PasscodeBox.Focus();
                }
            }
        }

        private void ConfirmOrCompletePasscode()
        {
            if (PasscodeBox.IsFocused)
            {
                if (PasscodeBox.Length > 0)
                {
                    ConfirmPasscodeBox.Focus();
                }
            }
            else if (ConfirmPasscodeBox.IsFocused)
            {
                ViewModel.ChangePassword();
            }
        }
    }
}