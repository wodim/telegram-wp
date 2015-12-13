using System.ComponentModel;
using System.Windows.Controls;
using ImageTools.Controls;
using ImageTools.IO.Gif;
using TelegramClient.Helpers;
using TelegramClient.ViewModels.Media;

namespace TelegramClient.Views.Media
{
    public class AnimatedImageEx : AnimatedImage
    {
        private Image _image;

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            BindXaml();
            //OnSourceChanged();
        }

        private void BindXaml()
        {
            _image = GetTemplateChild("Image") as Image;
        }

        protected override void OnSourceChanged()
        {
            if (Source == null)
            {
                if (_image != null)
                {
                    _image.Source = null;
                }
            }

            base.OnSourceChanged();
        }
    }

    public partial class AnimatedImageViewerView
    {
        public AnimatedImageViewerViewModel ViewModel
        {
            get { return DataContext as AnimatedImageViewerViewModel; }
        }

        public AnimatedImageViewerView()
        {
            ImageTools.IO.Decoders.AddDecoder<GifDecoder>();
            InitializeComponent();

            Loaded += (sender, args) =>
            {
                ViewModel.PropertyChanged += OnViewModelProprtyChanged;
            };

            Unloaded += (sender, args) =>
            {
                ViewModel.PropertyChanged -= OnViewModelProprtyChanged;
                AnimatedImage.Stop();
            };
        }

        private void OnViewModelProprtyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (Property.NameEquals(e.PropertyName, () => ViewModel.IsOpen))
            {
                if (!ViewModel.IsOpen)
                {
                    AnimatedImage.Stop();
                }
            }
        }
    }
}
