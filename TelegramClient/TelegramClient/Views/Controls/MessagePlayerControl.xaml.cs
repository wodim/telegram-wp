using System;
using System.IO;
using System.IO.IsolatedStorage;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Windows.Storage;
using Windows.System;
using Telegram.Api.Helpers;
using Telegram.Api.TL;

namespace TelegramClient.Views.Controls
{
    public partial class MessagePlayerControl
    {
        private static readonly MediaElement _player = new MediaElement{ AutoPlay = false };

        public static MediaElement Player
        {
            get { return _player; }
        } 

        private string _trackFileName;

        public static readonly DependencyProperty DurationStringProperty = DependencyProperty.Register(
            "DurationString", typeof (string), typeof (MessagePlayerControl), new PropertyMetadata(default(string)));

        public string DurationString
        {
            get { return (string) GetValue(DurationStringProperty); }
            set { SetValue(DurationStringProperty, value); }
        }

        public static readonly DependencyProperty DataContextWatcherProperty = DependencyProperty.Register(
            "DataContextWatcher", typeof (object), typeof (MessagePlayerControl), new PropertyMetadata(default(object), OnDataContextChanged));

        private static void OnDataContextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as MessagePlayerControl;
            if (control != null)
            {
                control.DurationString = GetDurationString(control.DataContext);
            }
        }

        public object DataContextWatcher
        {
            get { return GetValue(DataContextWatcherProperty); }
            set { SetValue(DataContextWatcherProperty, value); }
        }

        public static readonly DependencyProperty NotListenedProperty = DependencyProperty.Register(
            "NotListened", typeof (bool), typeof (MessagePlayerControl), new PropertyMetadata(OnNotListenedChanged));

        private static void OnNotListenedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as MessagePlayerControl;
            if (control == null) return;

            if (e.NewValue is bool)
            {
                var isVisible = (bool)e.NewValue;
                
                control.NotListenedIndicator.Visibility = isVisible
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        public bool NotListened
        {
            get { return (bool) GetValue(NotListenedProperty); }
            set { SetValue(NotListenedProperty, value); }
        }


        public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
            "Progress", typeof (double), typeof (MessagePlayerControl), new PropertyMetadata(default(double), OnProgressChanged));

        private static void OnProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as MessagePlayerControl;
            if (control == null) return;

            if (e.NewValue is double)
            {
                bool isVisible;
                var progress = (double)e.NewValue;
                if (Math.Abs(progress) < 0.00001)
                {
                    isVisible = false;
                }
                else
                {
                    isVisible = Math.Abs(progress - 1.0) > 0.00001;
                }

                control.PlayerToggleButton.Visibility = isVisible
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                control.PlayerDownloadButton.Visibility = isVisible
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        public double Progress
        {
            get { return (double) GetValue(ProgressProperty); }
            set { SetValue(ProgressProperty, value); }
        }

        public MessagePlayerControl()
        {
            InitializeComponent();
            
            MessagePlayer_Loaded(null, null);
            //Loaded += MessagePlayer_Loaded;

            SetBinding(DataContextWatcherProperty, new Binding());
        }

        #region Player

        private DispatcherTimer _timer;

        private void MessagePlayer_Loaded(object sender, RoutedEventArgs e)
        {
            //DurationString = GetDurationString(PlayerToggleButton.DataContext);

            PositionIndicator.Value = 0.0;

            _timer = new DispatcherTimer {Interval = TimeSpan.FromSeconds(0.02)};
            _timer.Tick += UpdateState;
            if (Player.CurrentState == MediaElementState.Playing)
            {
                // If audio was already playing when the app was launched, update the UI.
                //if (!_isManipulating)
                {
                    UpdateState(null, null);
                }
            }
        }

        private static string GetDurationString(object dataContext)
        {
            var mediaAudio = dataContext as TLMessageMediaAudio;
            if (mediaAudio != null)
            {
                var audio = mediaAudio.Audio as TLAudio;
                if (audio != null)
                {
                    return audio.DurationString;
                }
            }

            var decryptedMediaAudio = dataContext as TLDecryptedMessageMediaAudio;
            if (decryptedMediaAudio != null)
            {
                return decryptedMediaAudio.DurationString;
            }

            return null;
        }

        private static string GetWavFileName(object dataContext)
        {
            var attachment = dataContext as TLMessageMediaAudio;
            if (attachment != null)
            {
                var audio = attachment.Audio as TLAudio;
                if (audio != null)
                {
                    if (TLString.Equals(audio.MimeType, new TLString("audio/mpeg"), StringComparison.OrdinalIgnoreCase))
                    {
                        Execute.BeginOnThreadPool(async () =>
                        {

                            var audioFileName = audio.GetFileName();
#if WP81
                            try
                            {
                                var documentFile = await ApplicationData.Current.LocalFolder.GetFileAsync(audioFileName);
                                Launcher.LaunchFileAsync(documentFile);
                            }
                            catch (Exception ex)
                            {
                                Execute.ShowDebugMessage("LocalFolder.GetFileAsync docLocal exception \n" + ex);
                            }
#elif WP8
                        var file = await ApplicationData.Current.LocalFolder.GetFileAsync(audioFileName);
                        Launcher.LaunchFileAsync(file);
                        return;
#endif
                        });
                    }
                    return string.Format("audio{0}_{1}.wav", audio.Id, audio.AccessHash);
                }
            }

            var decryptedMediaAudio = dataContext as TLDecryptedMessageMediaAudio;
            if (decryptedMediaAudio != null)
            {
                var file = decryptedMediaAudio.File as TLEncryptedFile;
                if (file != null)
                {
                    return string.Format("audio{0}_{1}.wav", file.Id, file.AccessHash);
                }
            }

            return null;
        }

        private void OnMediaEnded(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            PlayerToggleButton.IsChecked = false;
            if (!_isManipulating)
            {
                PositionIndicator.Value = 0.0;
            }

            if (!_isManipulating)
            {
                DurationString = GetDurationString(PlayerToggleButton.DataContext);
            }
        }

        private void UpdateDurationString(TimeSpan timeSpan)
        {
            if (timeSpan.Hours > 0)
            {
                DurationString = timeSpan.ToString(@"h\:mm\:ss");
            }
            DurationString = timeSpan.ToString(@"m\:ss");
        }

        private void OnMediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
#if DEBUG
            MessageBox.Show(e.ErrorException.ToString());
            _timer.Stop();
            PlayerToggleButton.IsChecked = false;
            PositionIndicator.Value = 0.0;
#endif
        }

        private void OnMediaOpened(object sender, RoutedEventArgs e)
        {
            SetPosition();

            if (PlayerToggleButton.IsChecked == true)
            {
                Player.Play();
                _timer.Start();
            }
        }

        private void SetPosition()
        {
            PositionIndicator.IsEnabled = true;
            var ratio = Player.NaturalDuration.TimeSpan.TotalSeconds/PositionIndicator.Maximum;
            var newValue = PositionIndicator.Value*ratio;
            PositionIndicator.Maximum = Player.NaturalDuration.TimeSpan.TotalSeconds;
            PositionIndicator.SmallChange = PositionIndicator.Maximum/10.0;
            PositionIndicator.LargeChange = PositionIndicator.Maximum/10.0;
            if (PositionIndicator.Value >= PositionIndicator.Maximum)
            {
                newValue = PositionIndicator.Maximum - 0.01;    //фикс, если установлено максимальное значение, то при вызове Play аудио не проигрывается и не вызывается OnMediaEnded. Плеер подвисает на конечной позиции
            }

            if (double.IsNaN(newValue) || double.IsInfinity(newValue))
            {
                newValue = 0.0;
            }

            PositionIndicator.Value = newValue;
            if (Player.CanSeek)
            {
                Player.Position = TimeSpan.FromSeconds(newValue);
            }
        }

        private void UpdateState(object sender, System.EventArgs e)
        {
            if (Player.Source != null)
            {
                if (!_isManipulating)
                {
                    try
                    {
                        PositionIndicator.Value = Player.Position.TotalSeconds;
                        if (PositionIndicator.Value > 0.0)
                        {
                            UpdateDurationString(Player.Position);
                        }
                        //else if (PositionIndicator.Value >= PositionIndicator.Maximum)
                        //{
                        //    OnMediaEnded(sender, new RoutedEventArgs());
                        //}
                    }
                    catch (Exception ex)
                    {

                    }
                }
            }
        }

        private void BindToPlayer()
        {
            Player.MediaOpened += OnMediaOpened;
            Player.MediaEnded += OnMediaEnded;
            Player.MediaFailed += OnMediaFailed;
            Player.Tag = this;
        }

        private void UnbindFromPlayer(MessagePlayerControl control)
        {
            Player.MediaOpened -= control.OnMediaOpened;
            Player.MediaEnded -= control.OnMediaEnded;
            Player.MediaFailed -= control.OnMediaFailed;
            Player.Tag = null;
        }

        private void ResetPlayer()
        {
            var playerControl = Player.Tag as MessagePlayerControl;
            if (playerControl != null)
            {
                playerControl._timer.Stop();
                playerControl.PlayerToggleButton.IsChecked = false;
                playerControl.PositionIndicator.Value = 0.0;
                playerControl.DurationString = GetDurationString(playerControl.PlayerToggleButton.DataContext);

                UnbindFromPlayer(playerControl);
            }

            BindToPlayer();
        }

        private void PlayerToggleButton_Click(object sender, RoutedEventArgs routedEventArgs)
        {
            if (Player.Tag != this)
            {
                ResetPlayer();
                Player.Tag = this;
            }

            var wavFileName = GetWavFileName(PlayerToggleButton.DataContext);
            if (string.IsNullOrEmpty(wavFileName)) return;

            if (PlayerToggleButton.IsChecked == true)
            {

                if (Player.Source == null
                    || Path.GetFileName(Player.Source.OriginalString) != wavFileName)
                {
                    Player.Source = null;
                    _trackFileName = wavFileName;

                    using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                    {
                        if (!store.FileExists(wavFileName))
                        {
                            PlayerToggleButton.IsChecked = false;
                            return;
                        }

                        using (var wavFile = store.OpenFile(wavFileName, FileMode.Open, FileAccess.Read))
                        {
                            Player.SetSource(wavFile);
                        }
                    }
                }
                else
                {
                    _trackFileName = wavFileName;

                    SetPosition();

                    Player.Play();
                    _timer.Start();
                }

            }
            else
            {
                Player.Pause();
                _timer.Stop();
            }
        }

        #endregion

        private bool _isManipulating;

        private void Slider_ManipulationStarted(object sender, ManipulationStartedEventArgs e)
        {
            if (!Player.CanSeek
                || Path.GetFileName(Player.Source.LocalPath) != _trackFileName)
            {
                e.Handled = true;
                return;
            }

            _isManipulating = true;
        }

        private void Slider_ManipulationCompleted(object sender, ManipulationCompletedEventArgs e)
        {
            var value = PositionIndicator.Value;
            _isManipulating = false;


            if (Player.Source != null
                && Player.CanSeek
                && Path.GetFileName(Player.Source.LocalPath) == _trackFileName)
            {
                if (value >= PositionIndicator.Maximum)
                {
                    value = PositionIndicator.Maximum - 0.01;
                }

                Player.Position = TimeSpan.FromSeconds(value);
                UpdateDurationString(Player.Position);
            }
            else
            {
                PositionIndicator.Value = 0.0;
            }
        }

        private void Slider_ManipulationDelta(object sender, ManipulationDeltaEventArgs e)
        {
            var timeSpan = TimeSpan.FromSeconds(PositionIndicator.Value);
            UpdateDurationString(timeSpan);
        }

        public static void Stop()
        {
            var mpc = Player.Tag as MessagePlayerControl;
            if (mpc == null) return;
            mpc.OnMediaEnded(mpc, new RoutedEventArgs());           
        }
    }
}
