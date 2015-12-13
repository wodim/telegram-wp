using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Caliburn.Micro;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;

namespace TelegramClient.Views.Dialogs
{
    public partial class FastDialogDetailsView : PhoneApplicationPage
    {
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

        private List<string> _logs = new List<string>(); 

        public FastDialogDetailsView()
        {
            InitializeComponent();

            Loaded += (sender, args) =>
            {
                var elapsed = _stopwatch.Elapsed;
                _logs.Add("OnLoaded elapsed=" + elapsed);

                Logs.Text = string.Join(Environment.NewLine, _logs);
                //MessageBox.Show(string.Join(Environment.NewLine, _logs));
            };
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            var elapsed = _stopwatch.Elapsed;
            _logs.Add("OnNavigatedTo elapsed=" + elapsed);
        }
    }
}