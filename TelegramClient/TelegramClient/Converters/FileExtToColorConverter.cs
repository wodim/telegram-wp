using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Telegram.Api.TL;

namespace TelegramClient.Converters
{
    public class FileExtToColorConverter : IValueConverter
    {
        public Brush Yellow { get; set; }

        public Brush Green { get; set; }

        public Brush Red { get; set; }

        public Brush Blue { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var fileExtColors = new []{Blue, Green, Red, Yellow}; 

            var document = value as TLDocument;
            if (document == null) return fileExtColors[0];

            var name = document.FileName.ToString();

            if (name.Length != 0)
            {
                int color = -1;
                if (name.EndsWith(".doc") || name.EndsWith(".txt") || name.EndsWith(".psd"))
                {
                    color = 0;
                }
                else if (name.EndsWith(".xls") || name.EndsWith(".csv"))
                {
                    color = 1;
                }
                else if (name.EndsWith(".pdf") || name.EndsWith(".ppt") || name.EndsWith(".key"))
                {
                    color = 2;
                }
                else if (name.EndsWith(".zip") || name.EndsWith(".rar") || name.EndsWith(".ai") || name.EndsWith(".mp3") || name.EndsWith(".mov") || name.EndsWith(".avi"))
                {
                    color = 3;
                }
                if (color == -1)
                {
                    int idx;
                    var ext = (idx = name.LastIndexOf(".", StringComparison.Ordinal)) == -1 ? "" : name.Substring(idx + 1);
                    if (ext.Length != 0)
                    {
                        color = ext[0] % fileExtColors.Length;
                    }
                    else
                    {
                        color = name[0] % fileExtColors.Length;
                    }
                }
                return fileExtColors[color];
            }
            return fileExtColors[0];
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
