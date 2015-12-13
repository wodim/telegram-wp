using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using Caliburn.Micro;
using Microsoft.Devices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using TelegramClient.Helpers;
using TelegramClient.Models;
using TelegramClient.Resources;
using TelegramClient.Services;

namespace TelegramClient.ViewModels.Additional
{
    public class NotificationsViewModel : ViewModelBase
    {
        private Settings _settings;

        public Settings Settings
        {
            get { return _settings; }
            set { SetField(ref _settings, value, () => Settings); }
        }

        private volatile bool _suppressUpdating;

        public NotificationsViewModel(ICacheService cacheService, ICommonErrorHandler errorHandler, IStateService stateService, INavigationService navigationService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator) 
            : base(cacheService, errorHandler, stateService, navigationService, mtProtoService, eventAggregator)
        {
            BeginOnThreadPool(() => StateService.GetNotifySettingsAsync(
                s =>
                {
                    Settings = s;

                    //Settings

                    var sound = StateService.Sounds.FirstOrDefault(x => string.Equals(x, Settings.ContactSound, StringComparison.OrdinalIgnoreCase));
                    Settings.ContactSound = sound ?? StateService.Sounds[0];

                    sound = StateService.Sounds.FirstOrDefault(x => string.Equals(x, Settings.GroupSound, StringComparison.OrdinalIgnoreCase));
                    Settings.GroupSound = sound ?? StateService.Sounds[0];

                    Settings.PropertyChanged += OnSettingsChanged;

                    MTProtoService.GetNotifySettingsAsync(new TLInputNotifyUsers(),
                        result =>
                        {
                            _suppressUpdating = true;

                            var settings = result as TLPeerNotifySettings;
                            if (settings != null)
                            {
                                Settings.ContactAlert = settings.MuteUntil.Value == 0;
                                Settings.ContactMessagePreview = settings.ShowPreviews.Value;

                                sound = StateService.Sounds.FirstOrDefault(x => string.Equals(x, settings.Sound.Value, StringComparison.OrdinalIgnoreCase));
                                Settings.ContactSound = sound ?? StateService.Sounds[0];
                            }

                            _suppressUpdating = false;

                            SaveSettings();
                        });

                    MTProtoService.GetNotifySettingsAsync(new TLInputNotifyChats(),
                        result =>
                        {

                            _suppressUpdating = true;

                            var settings = result as TLPeerNotifySettings;
                            if (settings != null)
                            {
                                Settings.GroupAlert = settings.MuteUntil.Value == 0;
                                Settings.GroupMessagePreview = settings.ShowPreviews.Value;

                                sound = StateService.Sounds.FirstOrDefault(x => string.Equals(x, settings.Sound.Value, StringComparison.OrdinalIgnoreCase));
                                Settings.GroupSound = sound ?? StateService.Sounds[0];
                            }

                            _suppressUpdating = false;

                            SaveSettings();
                        });

                    SaveSettings();
                }));
        }

#if DEBUG
        //~NotificationsViewModel()
        //{
            
        //}
#endif
        private void OnSettingsChanged(object sender, PropertyChangedEventArgs args)
        {
            if (_suppressUpdating) return;

            SaveSettings();

            if (Property.NameEquals(args.PropertyName, () => Settings.InAppVibration)
                && Settings.InAppVibration)
            {
                VibrateController.Default.Start(TimeSpan.FromMilliseconds(300));
            }

            if (Property.NameEquals(args.PropertyName, () => Settings.ContactAlert)
                || Property.NameEquals(args.PropertyName, () => Settings.ContactMessagePreview)
                || Property.NameEquals(args.PropertyName, () => Settings.ContactSound))
            {
                if (Property.NameEquals(args.PropertyName, () => Settings.ContactSound)
                    && !string.IsNullOrEmpty(Settings.ContactSound))
                {
                    PlaySound(Settings.ContactSound);
                }
                IsWorking = true;
                MTProtoService.UpdateNotifySettingsAsync(new TLInputNotifyUsers(), 
                    new TLInputPeerNotifySettings
                    {
                        EventsMask = new TLInt(1),
                        MuteUntil = Settings.ContactAlert ? new TLInt(0) : new TLInt(2000000000),
                        ShowPreviews = new TLBool(Settings.ContactMessagePreview),
                        Sound = new TLString(Settings.ContactSound)
                    },
                    result =>
                    {
                        IsWorking = false;
                    },
                    error =>
                    {
                        IsWorking = false;
                    });

                return;
            }

            if (Property.NameEquals(args.PropertyName, () => Settings.GroupAlert)
                || Property.NameEquals(args.PropertyName, () => Settings.GroupMessagePreview)
                || Property.NameEquals(args.PropertyName, () => Settings.GroupSound))
            {
                if (Property.NameEquals(args.PropertyName, () => Settings.GroupSound)
                    && !string.IsNullOrEmpty(Settings.GroupSound))
                {
                    PlaySound(Settings.GroupSound);
                }
                IsWorking = true;
                MTProtoService.UpdateNotifySettingsAsync(new TLInputNotifyChats(),
                    new TLInputPeerNotifySettings
                    {
                        EventsMask = new TLInt(1),
                        MuteUntil = Settings.GroupAlert ? new TLInt(0) : new TLInt(2000000000),
                        ShowPreviews = new TLBool(Settings.GroupMessagePreview),
                        Sound = new TLString(Settings.GroupSound)
                    },
                    result =>
                    {
                        IsWorking = false;
                    },
                    error =>
                    {
                        IsWorking = false;
                    });

                return;
            }
        }

        public static void PlaySound(string sound)
        {
            var s = "Sounds/" + sound + ".wav";

            if (Telegram.Api.Helpers.Utils.XapContentFileExists(s))
            {
                var stream = TitleContainer.OpenStream(s);
                var effect = SoundEffect.FromStream(stream);

                FrameworkDispatcher.Update();
                effect.Play();
            }
        }

        private void SaveSettings()
        {
            StateService.SaveNotifySettingsAsync(_settings);
        }

        public void Reset()
        {
            var r = MessageBox.Show(AppResources.ResetAllNotificationsMessage, AppResources.Confirm,
                MessageBoxButton.OKCancel);

            if (r != MessageBoxResult.OK) return;

            _suppressUpdating = true;

            Settings.ContactAlert = true;
            Settings.ContactMessagePreview = true;
            Settings.ContactSound = StateService.Sounds[0];

            Settings.GroupAlert = true;
            Settings.GroupMessagePreview = true;
            Settings.GroupSound = StateService.Sounds[0];

            Settings.InAppSound = true;
            Settings.InAppMessagePreview = true;
            Settings.InAppVibration = true;

            _suppressUpdating = false;
            SaveSettings();

            IsWorking = true;
            MTProtoService.ResetNotifySettingsAsync(
                result =>
                {
                    IsWorking = false;
                },
                error =>
                {
                    IsWorking = false;
                });
        }

        protected override void OnDeactivate(bool close)
        {
            base.OnDeactivate(close);

            SaveSettings();
        }

        public void OnBackKeyPress()
        {
            //SaveSettings();

            Settings.PropertyChanged -= OnSettingsChanged;
        }
    }
}
