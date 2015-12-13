using System.Globalization;
using System.Resources;
using System.Threading;
using System.Windows;
using Telegram.EmojiPanel;
using Telegram.EmojiPanel.Controls.Emoji;
#if WP81
using Windows.UI.ViewManagement;
#endif
using Caliburn.Micro;

namespace TelegramClient.Resources
{
    public class LocalizedStrings : PropertyChangedBase
    {
        private static readonly AppResources _resources = new AppResources();

        public AppResources Resources { get { return _resources; } }

        public void SetLanguage(CultureInfo culture)
        {
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;

            NotifyOfPropertyChange(() => Resources);
        }
    }

    public class ScaledText : PropertyChangedBase
    {
#if WP81
        private UISettings _settings;

        public UISettings Settings
        {
            get
            {
                if (_settings == null)
                {
                    _settings = new UISettings();
                    _settings.TextScaleFactorChanged += OnTextScaleFactorChanged;
                }

                return _settings; 
                
            }
        }

        private void OnTextScaleFactorChanged(UISettings sender, object args)
        {
            BrowserNavigationService.FontScaleFactor = TextScaleFactor;
            NotifyOfPropertyChange(() => TextScaleFactor);
        }

#endif

        public double ApplicationBarHeight
        {
            get
            {
#if WP8
                if (Application.Current.Host.Content.ScaleFactor == 225)
                {
                    return 60.0;
                }
#endif

                return 72.0;
            }
        }

        public double TextScaleFactor
        {
            get
            {
                var textScaleFactor = 1.0;

#if WP81
                textScaleFactor = Settings.TextScaleFactor;
#endif

                return textScaleFactor;
            }
        }

        public double DefaultFontSize
        {
            get
            {
                const double defaultFontSize = 22.667;

#if WP8
                switch (Application.Current.Host.Content.ScaleFactor)
                {
                    case 100:   //Lumia 820
                        return defaultFontSize;
                        break;
                    case 150:   //HTC 8X
                        return 20;
                        break;
                    case 160:   //Lumia 925
                        return 20;
                        break;
                    case 225:   // Lumia 1520, Lumia 930
                        return 17.778;
                        break;
                }
#endif

                return defaultFontSize;
            }
        }

        public double DefaultSystemFontSize
        {
            get
            {
                const double defaultFontSize = 20;

#if WP8
                switch (Application.Current.Host.Content.ScaleFactor)
                {
                    case 100:   //Lumia 820
                        return defaultFontSize;
                        break;
                    case 150:   //HTC 8X
                        return 20;
                        break;
                    case 160:   //Lumia 925
                        return 20;
                        break;
                    case 225:   // Lumia 1520, Lumia 930
                        return 17.778;
                        break;
                }
#endif

                return defaultFontSize;
            }
        }

        public double DefaultSystemIconSize
        {
            get
            {
                const double defaultFontSize = 15;

#if WP8
                switch (Application.Current.Host.Content.ScaleFactor)
                {
                    case 100:   //Lumia 820
                        return defaultFontSize;
                        break;
                    case 150:   //HTC 8X
                        return 15;
                        break;
                    case 160:   //Lumia 925
                        return 15;
                        break;
                    case 225:   // Lumia 1520, Lumia 930
                        return 12;
                        break;
                }
#endif

                return defaultFontSize;
            }
        }
    }
}
