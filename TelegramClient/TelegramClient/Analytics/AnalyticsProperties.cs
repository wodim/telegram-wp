using System;
using System.Globalization;
using System.IO.IsolatedStorage;
using Coding4Fun.Toolkit.Controls.Common;
using Microsoft.Phone.Info;

namespace TelegramClient.Analytics
{
    public static class AnalyticsProperties
    {
        //public static string DeviceId
        //{
        //    get
        //    {
        //        var value = (byte[])DeviceExtendedProperties.GetValue("DeviceUniqueId");
        //        return Convert.ToBase64String(value);
        //    }
        //}

        public static string LaunchCount
        {
            get
            {
                var settings = IsolatedStorageSettings.ApplicationSettings;

                if (settings != null)
                {
                    if (settings.Contains("LaunchCount"))
                    {
                        return ((int)settings["LaunchCount"] + 1).ToString(CultureInfo.InvariantCulture);
                    }
                }

                return "1";
            }
        }

        public static string DeviceManufacturer
        {
            get { return DeviceExtendedProperties.GetValue("DeviceManufacturer").ToString(); }
        }

        public static string DeviceType
        {
            get { return DeviceExtendedProperties.GetValue("DeviceName").ToString(); }
        }

        public static string Device
        {
            get { return string.Format("{0} - {1}", DeviceManufacturer, DeviceType); }
        }

        public static string OsVersion
        {
            get { return string.Format("WP {0}", Environment.OSVersion.Version); }
        }

        public static string ApplicationVersion
        {
            get { return PhoneHelper.GetAppAttribute("Version").Replace(".0.0", ""); }
        }
    }
}
