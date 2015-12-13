using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using Caliburn.Micro;
using Telegram.Api.TL;
using TelegramClient.Services;

namespace TelegramClient.Helpers.TemplateSelectors
{
    public class ItemsPanelTemplateSelector : IValueConverter
    {
        public ItemsPanelTemplate NormalMemoryDeviceTemplate { get; set; }

        public ItemsPanelTemplate LowMemoryDeviceTemplate { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var extendedDeviceInfoService = IoC.Get<IExtendedDeviceInfoService>();

            if (extendedDeviceInfoService.IsLowMemoryDevice)
            {
                TLUtils.WritePerformance("  Select ItemsPanel for Low Memory Device");
                return LowMemoryDeviceTemplate;
            }

            TLUtils.WritePerformance("  Select ItemsPanel for Normal Memory Device");
            return NormalMemoryDeviceTemplate;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
