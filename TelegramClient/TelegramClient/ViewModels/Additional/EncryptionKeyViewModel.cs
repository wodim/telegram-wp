using System.Diagnostics;
using System.Security.Cryptography;
using System.Windows.Media.Imaging;
using Caliburn.Micro;
using Telegram.Api.TL;
using TelegramClient.Resources;
using TelegramClient.Services;

namespace TelegramClient.ViewModels.Additional
{
    public class EncryptionKeyViewModel  : PropertyChangedBase
    {
        private readonly TLString _key;
        private readonly TLUserBase _contact;

        public string VisualizationTime { get; set; }

        public WriteableBitmap Bitmap { get; set; }

        public string EncryptionKeyDescription1
        {
            get { return string.Format(AppResources.EncryptionKeyDescription1, _contact.FirstName); }
        }

        public string EncryptionKeyDescription2
        {
            get { return string.Format(AppResources.EncryptionKeyDescription2, _contact.FirstName); }
        }

        public EncryptionKeyViewModel(IStateService stateService)
        {
            _key = stateService.CurrentKey;
            stateService.CurrentKey = null;

            _contact = stateService.CurrentContact;
            stateService.CurrentContact = null;

            var timer = Stopwatch.StartNew();
            Bitmap = CreateKeyBitmap();
            VisualizationTime = timer.Elapsed.ToString();
        }

        public void AnimationComplete()
        {
            
            //NotifyOfPropertyChange(() => VisualizationTime);
            //NotifyOfPropertyChange(() => Visualization);
        }

        public static void DrawFilledRectangle(WriteableBitmap bmp, int x1, int y1, int x2, int y2, int color)
        {
            // Use refs for faster access (really important!) speeds up a lot!
            int w = bmp.PixelWidth;
            int h = bmp.PixelHeight;
            int[] pixels = bmp.Pixels;

            // Check boundaries
            if (x1 < 0) { x1 = 0; }
            if (y1 < 0) { y1 = 0; }
            if (x2 < 0) { x2 = 0; }
            if (y2 < 0) { y2 = 0; }
            if (x1 >= w) { x1 = w - 1; }
            if (y1 >= h) { y1 = h - 1; }
            if (x2 >= w) { x2 = w - 1; }
            if (y2 >= h) { y2 = h - 1; }

            int i = y1 * w;
            for (int y = y1; y < y2; y++)
            {
                int i2 = i + x1;
                while (i2 < i + x2)
                {
                    pixels[i2++] = color;
                }
                i += w;
            }
        }

        private WriteableBitmap CreateKeyBitmap()
        {
            var length = 40;
            var bitmap = new WriteableBitmap(8 * length, 8 * length);
            var data = _key.Data;
            var hash = Telegram.Api.Helpers.Utils.ComputeSHA1(data);
            var colors = new []{
                    0xffffffff,
                    0xffd5e6f3,
                    0xff2d5775,
                    0xff2f99c9};
            for (int i = 0; i < 64; i++) 
            {
                int index = (hash[i / 4] >> (2 * (i % 4))) & 0x3;
                var x = i % 8;
                var y = i / 8;
                DrawFilledRectangle(bitmap, x * length, y * length, (x + 1) * length, (y + 1) * length, (int)colors[index]);
            }
            return bitmap;
        }
    }
}
