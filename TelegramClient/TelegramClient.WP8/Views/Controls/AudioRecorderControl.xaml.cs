using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Windows.Storage;
using Caliburn.Micro;
using Microsoft.Devices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.FileManager;
using Telegram.Api.TL;
using TelegramClient.Services;
using TelegramClient_Opus;
using Action = System.Action;
using Execute = Telegram.Api.Helpers.Execute;

namespace TelegramClient.Views.Controls
{
    public partial class AudioRecorderControl
    {
        public bool UploadFileDuringRecording { get; set; }

        public string RecorderImageSource
        {
            get
            {
                var currentBackground = IoC.Get<IStateService>().CurrentBackground;
                if (currentBackground != null && currentBackground.Name != "Empty")
                {
                    return "/Images/Audio/microphone.light.png";
                }

                var isLightTheme = (Visibility) Application.Current.Resources["PhoneLightThemeVisibility"] == Visibility.Visible;

                if (isLightTheme)
                {
                    return "/Images/Audio/microphone.dark.png";
                }

                return "/Images/Audio/microphone.light.png";
            }
        }

        private bool _isSliding;
        private readonly Microphone _microphone;
        private readonly byte[] _buffer;
        private TimeSpan _duration;
        private TimeSpan _recordedDuration;
        private DateTime _startTime;
        private volatile bool _stopRequested;
        private volatile bool _cancelRequested;
        private MemoryStream _stream;
        private readonly XnaAsyncDispatcher _asyncDispatcher;
        private string _fileName = "audio.mp3";

        private WindowsPhoneRuntimeComponent _component;

        protected WindowsPhoneRuntimeComponent Component
        {
            get
            {
                if (DesignerProperties.IsInDesignTool) return null;

                _component = _component ?? new WindowsPhoneRuntimeComponent();

                return _component;
            }
        }

        private void OnTimerTick()
        {
            Duration.Text = (DateTime.Now - _startTime).ToString(@"mm\:ss");
        }

        public AudioRecorderControl()
        {
            InitializeComponent();

            _asyncDispatcher = new XnaAsyncDispatcher(TimeSpan.FromMilliseconds(33), OnTimerTick);
            _microphone = Microphone.Default;

            if (_microphone == null)
            {
                RecordButton.Visibility = Visibility.Collapsed;
                Visibility = Visibility.Collapsed;
                IsHitTestVisible = false;
                return;
            }

            var rate = _microphone.SampleRate;
            _microphone.BufferDuration = TimeSpan.FromMilliseconds(240);
            _duration = _microphone.BufferDuration;
            _buffer = new byte[_microphone.GetSampleSizeInBytes(_microphone.BufferDuration)];

            Loaded += (o, e) =>
            {
                _microphone.BufferReady += Microphone_OnBufferReady;
            };
            Unloaded += (o, e) =>
            {
                _microphone.BufferReady -= Microphone_OnBufferReady;
            };
        }

        private long _uploadingLength;
        private volatile bool _isPartReady;
        private TLLong _fileId;
        private readonly List<UploadablePart> _uploadableParts = new List<UploadablePart>();

        private void Microphone_OnBufferReady(object sender, System.EventArgs e)
        {
            if (Component == null) return;

            var dataLength = _microphone.GetData(_buffer);
            const int frameLength = 1920;
            var partsCount = dataLength / frameLength;
            _stream.Write(_buffer, 0, _buffer.Length);
            for (var i = 0; i < partsCount; i++)
            {
                var count = frameLength * (i + 1) > _buffer.Length ? _buffer.Length - frameLength * i : frameLength;
                var result = Component.WriteFrame(_buffer.SubArray(frameLength * i, count), count);
            }

            if (_stopRequested || _cancelRequested)
            {
                _microphone.Stop();
                _asyncDispatcher.StopService();
                Component.StopRecord();

                if (UploadFileDuringRecording)
                {
                    UploadAudioFileAsync(true);
                }

                if (_stopRequested)
                {
                    if ((DateTime.Now - _startTime).TotalMilliseconds < 1000.0)
                    {
                        _stopRequested = false;
                        _cancelRequested = false;
                        HintStoryboard.Begin();
                        return;
                    }

                    RaiseAudioRecorded(_stream, (DateTime.Now - _startTime).TotalSeconds, _fileName, _fileId, _uploadableParts);
                    return;
                }

                if (_cancelRequested)
                {
                    RaiseRecordCanceled();
                    return;
                }
            }
            else
            {
                var now = DateTime.Now;
                if (!_lastTypingTime.HasValue
                    || _lastTypingTime.Value.AddSeconds(1.0) < now)
                {
                    _lastTypingTime = DateTime.Now;
                    RaiseRecordingAudio();
                }

                if (UploadFileDuringRecording)
                {
                    UploadAudioFileAsync(false);
                }
            }
        }

        private DateTime? _lastTypingTime;

        private void UploadAudioFileAsync(bool isLastPart)
        {
            Execute.BeginOnThreadPool(() =>
            {
                if (!_isPartReady) return;

                _isPartReady = false;

                var uploadablePart = GetUploadablePart(_fileName, _uploadingLength, _uploadableParts.Count, isLastPart);
                if (uploadablePart == null)
                {
                    _isPartReady = true;
                    return;
                }

                _uploadableParts.Add(uploadablePart);
                _uploadingLength += uploadablePart.Count;

                //Execute.BeginOnUIThread(() => VibrateController.Default.Start(TimeSpan.FromSeconds(0.02)));

                var mtProtoService = IoC.Get<IMTProtoService>();
                mtProtoService.SaveFilePartAsync(_fileId, uploadablePart.FilePart,
                    TLString.FromBigEndianData(uploadablePart.Bytes),
                    result =>
                    {
                        if (result.Value)
                        {
                            uploadablePart.Status = PartStatus.Processed;
                        }
                    },
                    error => Execute.ShowDebugMessage("upload.saveFilePart error " + error));

                _isPartReady = true;
            });
        }

        private static UploadablePart GetUploadablePart(string fileName, long position, int partId, bool isLastPart = false)
        {
            var fullFilePath = ApplicationData.Current.LocalFolder.Path + "\\" + fileName;
            var fi = new FileInfo(fullFilePath);
            if (!fi.Exists)
            {
                return null;
            }

            const int minPartLength = 1024;
            const int maxPartLength = 16 * 1024;

            var recordingLength = fi.Length - position;
            if (!isLastPart && recordingLength < minPartLength)
            {
                return null;
            }

            var subpartsCount = (int)recordingLength / minPartLength;
            var uploadingBufferSize = 0;
            if (isLastPart)
            {
                if (recordingLength > 0)
                {
                    uploadingBufferSize = Math.Min(maxPartLength, (int)recordingLength);
                }
            }
            else
            {
                uploadingBufferSize = Math.Min(maxPartLength, subpartsCount * minPartLength);
            }
            if (uploadingBufferSize == 0)
            {
                return null;
            }

            var uploadingBuffer = new byte[uploadingBufferSize];

            try
            {
                using (var fileStream = File.Open(fullFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    fileStream.Position = position;
                    fileStream.Read(uploadingBuffer, 0, uploadingBufferSize);
                }
            }
            catch (Exception ex)
            {
                Execute.ShowDebugMessage("read file " + fullFilePath + " exception " + ex);
                return null;
            }

            return new UploadablePart(null, new TLInt(partId), uploadingBuffer, position, uploadingBufferSize);
        }

        private void RecordButton_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsHitTestVisible) return;
            if (Component == null) return;
            if (_microphone == null) return;

            _fileId = TLLong.Random();
            _fileName = _fileId.Value + ".mp3";
            _isPartReady = true;
            _uploadingLength = 0;
            _uploadableParts.Clear();

            _isSliding = true;
            _stopRequested = false;
            _cancelRequested = false;

            RaiseRecordStarted();

            Duration.Text = "00:00";
            SliderTransform.X = 0.0;
            Slider.Visibility = Visibility.Visible;
            TimerPanel.Visibility = Visibility.Visible;
            Component.StartRecord(ApplicationData.Current.LocalFolder.Path + "\\" + _fileName);

            _stream = new MemoryStream();
            _startTime = DateTime.Now; 
            VibrateController.Default.Start(TimeSpan.FromMilliseconds(25));
            _asyncDispatcher.StartService(null);
            _microphone.Start();

            StartRecordingStoryboard.Begin();
        }

        private void RecordButton_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            StopRecording();
        }

        public event EventHandler<AudioEventArgs> AudioRecorded;

        protected virtual void RaiseAudioRecorded(MemoryStream stream, double duration, string fileName, TLLong fileId, IList<UploadablePart> parts)
        {
            var handler = AudioRecorded;
            if (handler != null) handler(this, new AudioEventArgs(stream, duration, fileName, fileId, parts));
        }

        public event EventHandler<System.EventArgs> RecordCanceled;

        protected virtual void RaiseRecordCanceled()
        {
            var handler = RecordCanceled;
            if (handler != null) handler(this, System.EventArgs.Empty);
        }

        public event EventHandler<System.EventArgs> RecordStarted;

        protected virtual void RaiseRecordStarted()
        {
            var handler = RecordStarted;
            if (handler != null) handler(this, System.EventArgs.Empty);
        }

        public event EventHandler<System.EventArgs> RecordingAudio;

        protected virtual void RaiseRecordingAudio()
        {
            var handler = RecordingAudio;
            if (handler != null) handler(this, System.EventArgs.Empty);
        }

        private void CancelRecording()
        {
            Slider.Visibility = Visibility.Collapsed;
            TimerPanel.Visibility = Visibility.Collapsed;

            if (!_stopRequested)
            {
                _cancelRequested = true;
            }
            _isSliding = false;
            _lastTypingTime = null;
        }

        private void StopRecording()
        {
            VibrateController.Default.Start(TimeSpan.FromMilliseconds(25));

            Slider.Visibility = Visibility.Collapsed;
            TimerPanel.Visibility = Visibility.Collapsed;
            _stopRequested = true;
            _isSliding = false;
            _lastTypingTime = null;
        }

        private void HintStoryboard_OnCompleted(object sender, System.EventArgs e)
        {
            RaiseRecordCanceled();
        }

        private void LayoutRoot_OnManipulationDelta(object sender, ManipulationDeltaEventArgs e)
        {
            if (!_isSliding) return;

            SliderTransform.X += e.DeltaManipulation.Translation.X;

            if (SliderTransform.X < 0)
            {
                SliderTransform.X = 0;
            }

            if (SliderTransform.X > 200)
            {
                //SliderTransform.X = 0;
                _isSliding = false;

                CancelRecordingStoryboard.Begin();
            }
        }

        private void LayoutRoot_OnManipulationCompleted(object sender, ManipulationCompletedEventArgs e)
        {
            if (!_cancelRequested && Hint.Visibility == Visibility.Collapsed)
            {
                StopRecording();
            }
        }

        private void CancelRecordingStoryboard_OnCompleted(object sender, System.EventArgs e)
        {
            CancelRecording();
        }
    }

    public class XnaAsyncDispatcher : IApplicationService
    {

        private readonly DispatcherTimer _timer;
        private readonly Action _tickAction;
        public XnaAsyncDispatcher(TimeSpan dispatchInterval, Action tickAction = null)
        {
            FrameworkDispatcher.Update();
            _timer = new DispatcherTimer();
            _timer.Tick += TimerTick;
            _timer.Interval = dispatchInterval;

            _tickAction = tickAction;
        }
        public void StartService(ApplicationServiceContext context)
        {
            _timer.Start();
        }

        public void StopService()
        {
            _timer.Stop();
        }

        private void TimerTick(object sender, System.EventArgs eventArgs)
        {
            try
            {
                FrameworkDispatcher.Update();
            }
            catch (Exception e)
            {
#if DEBUG
                MessageBox.Show(e.ToString());
#endif
            }
            if (_tickAction != null) _tickAction();
        }
    }

    public class AudioEventArgs : System.EventArgs
    {
        public MemoryStream PcmStream { get; set; }

        public string OggFileName { get; set; }

        public double Duration { get; set; }

        public TLLong FileId { get; set; }

        public IList<UploadablePart> Parts { get; set; }

        public AudioEventArgs(MemoryStream stream, double duration, string fileName, TLLong fileId, IList<UploadablePart> parts)
        {
            PcmStream = stream;
            Duration = duration;
            OggFileName = fileName;
            FileId = fileId;
            Parts = parts;
        }
    }
}
