using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Caliburn.Micro;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Info;
using Microsoft.Phone.Shell;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Telegram.Controls.VirtualizedView;
using Telegram.EmojiPanel.Controls.Utilites;
using TelegramClient.Converters;
using TelegramClient.Services;
using TelegramClient.Views.Dialogs;
using Binding = System.Windows.Data.Binding;
using GestureEventArgs = System.Windows.Input.GestureEventArgs;
using Execute = Telegram.Api.Helpers.Execute;

namespace Telegram.EmojiPanel.Controls.Emoji
{
    public class IsOpenedEventArgs : EventArgs
    {
        public bool IsOpened { get; set; }
    }

    public partial class EmojiControl
    {
        public static readonly DependencyProperty IsStickersPanelVisibleProperty = DependencyProperty.Register(
            "IsStickersPanelVisible", typeof (bool), typeof (EmojiControl), new PropertyMetadata(default(bool)));

        public bool IsStickersPanelVisible
        {
            get { return (bool) GetValue(IsStickersPanelVisibleProperty); }
            set { SetValue(IsStickersPanelVisibleProperty, value); }
        }

        private List<VListItemBase> _category1Sprites;
        private List<VListItemBase> _category2Sprites;
        private List<VListItemBase> _category3Sprites;
        private List<VListItemBase> _category4Sprites;
        private List<VListItemBase> _category5Sprites;

        private List<VListItemBase> _categoryRecentStickers;
        private List<List<VListItemBase>> _stickers;

        public event EventHandler<IsOpenedEventArgs> IsOpenedChanged;

        private void RaiseIsOpenedChanged(bool isOpened)
        {
            var eventHandler = IsOpenedChanged;

            if (eventHandler != null)
            {
                eventHandler(this, new IsOpenedEventArgs { IsOpened = isOpened });
            }
        }

        public TextBox TextBoxTarget { get; set; }

        private const int FirstStickerSliceCount = 3;

        private const int AlbumOrientationHeight = 328;

        public const int PortraitOrientationHeight100 = 408;

        public const int PortraitOrientationHeight112 = 408;

        public const int PortraitOrientationHeight112Software = 400;

        public const int PortraitOrientationHeight150 = 408;

        public const int PortraitOrientationHeight150Software = 400;

        public const int PortraitOrientationHeight160 = 408;

        public const int PortraitOrientationHeight225 = 332;

        public static int PortraitOrientationHeight
        {
            get
            {
#if WP8
                var appBar = new ApplicationBar();
                switch (Application.Current.Host.Content.ScaleFactor)
                {
                    case 100:   //Lumia 820             WVGA    480x800
                        return PortraitOrientationHeight100;
                        break;
                    case 112:   //Lumia 535             qHD     540x960
                        // Software buttons //Lumia 535
                        if (appBar.DefaultSize == 67.0)
                        {
                            return PortraitOrientationHeight112Software;
                        }
                        
                        return PortraitOrientationHeight112;
                        break;
                    case 150:   //HTC 8X, 730, 830      720p    720x1280
                        //Software buttons  //Lumia 730
                        if (appBar.DefaultSize == 67.0)
                        {
                            return PortraitOrientationHeight150Software;
                        }

                        return PortraitOrientationHeight150;
                        break;
                    case 160:   //Lumia 925, 1020       WXGA    768x1280
                        return PortraitOrientationHeight160;
                        break;
                    case 225:   // Lumia 1520, 930      1020p   1080x1920  
                        
                        var deviceName = DeviceStatus.DeviceName;
                        if (!string.IsNullOrEmpty(deviceName))
                        {
                            deviceName = deviceName.Replace("-", string.Empty).ToLowerInvariant();

                            //Lumia 1520    6 inch 1020p
                            if (deviceName.StartsWith("rm937")
                                || deviceName.StartsWith("rm938")
                                || deviceName.StartsWith("rm939")
                                || deviceName.StartsWith("rm940"))
                            {
                                return PortraitOrientationHeight225;
                            }
                        }

                        //Lumia 930 other 1020p
                        return PortraitOrientationHeight100;
                        break;
                }
#endif

                return PortraitOrientationHeight100;
            }
        }

        private bool _isOpen;
        private bool _isPortrait = true;
        private bool _isTextBoxTargetFocused;
        private bool _isBlocked; // Block IsOpen during animation
        private int _currentCategory;
        private bool _wasRendered;
        private readonly TranslateTransform _frameTransform;
        private static EmojiControl _instance;

        public static EmojiControl GetInstance()
        {
            return _instance ?? (_instance = new EmojiControl());
        }

        public static readonly DependencyProperty RootFrameTransformProperty = DependencyProperty.Register(
                "RootFrameTransform",
                typeof(double),
                typeof(EmojiControl),
                new PropertyMetadata(OnRootFrameTransformChanged));

        public EmojiControl()
        {
            InitializeComponent();

            //var frame = (Frame)Application.Current.RootVisual;
            //_frameTransform = ((TranslateTransform)((TransformGroup)frame.RenderTransform).Children[0]);
            //var binding = new Binding("Y")
            //{
            //    Source = _frameTransform
            //};
            //SetBinding(RootFrameTransformProperty, binding);

            VirtPanel.InitializeWithScrollViewer(CSV);
            VirtPanel.ScrollPositionChanged += VirtPanelOnScrollPositionChanged;
            //SizeChanged += OnSizeChanged;
            OnSizeChanged(null, null);

            LoadButtons();
            CurrentCategory = 0;
        }

        public void BindTextBox(TextBox textBox, bool isStickersPanelVisible = false)
        {
            TextBoxTarget = textBox;
            UpdateButtons(isStickersPanelVisible);
            textBox.GotFocus += TextBoxOnGotFocus;
            textBox.LostFocus += TextBoxOnLostFocus;
        }

        public void UnbindTextBox()
        {
            TextBoxTarget.GotFocus -= TextBoxOnGotFocus;
            TextBoxTarget.LostFocus -= TextBoxOnLostFocus;
            TextBoxTarget = null;
        }

        public bool IsOpen
        {
            get
            {
                return !_isTextBoxTargetFocused && _isOpen;
            }
            set
            {
                // Dont hide EmojiControl when keyboard is shown (or to be shown)
                if (!_isTextBoxTargetFocused && _isOpen == value || _isBlocked) return;

                if (value)
                {
                    Open();
                }
                else
                {
                    Hide();
                }


                RaiseIsOpenedChanged(value);
            }
        }

        private void Open()
        {
            _isOpen = true;

            TextBoxTarget.Dispatcher.BeginInvoke(() => VisualStateManager.GoToState(TextBoxTarget, "Focused", false));



            //var frame = (PhoneApplicationFrame)Application.Current.RootVisual;
            EmojiContainer.Visibility = Visibility.Visible;
            ButtonsGrid.Visibility = Visibility.Visible;
            StickersGrid.Visibility = Visibility.Collapsed;
            Deployment.Current.Dispatcher.BeginInvoke(() => LoadCategory(0));

            //frame.BackKeyPress += OnBackKeyPress;

            //if (!(EmojiContainer.RenderTransform is TranslateTransform))
            //    EmojiContainer.RenderTransform = new TranslateTransform();
            //var transform = (TranslateTransform)EmojiContainer.RenderTransform;

            var offset = _isPortrait ? PortraitOrientationHeight : AlbumOrientationHeight;
            EmojiContainer.Height = offset;

            //var from = 0;

            //if (_frameTransform.Y < 0) // Keyboard is in view
            //{
            //    from = (int)_frameTransform.Y;
            //    //_frameTransform.Y = -offset;
            //    //transform.Y = offset;// -72;
            //}
            //transform.Y = offset;// -72

            //if (from == offset) return;

            //frame.IsHitTestVisible = false;
            //_isBlocked = true;

            //var storyboard = new Storyboard();
            //var doubleTransformFrame = new DoubleAnimation
            //{
            //    From = from,
            //    To = -offset,
            //    Duration = TimeSpan.FromMilliseconds(440),
            //    EasingFunction = new ExponentialEase
            //    {
            //        EasingMode = EasingMode.EaseOut,
            //        Exponent = 6
            //    }
            //};
            //storyboard.Children.Add(doubleTransformFrame);
            //Storyboard.SetTarget(doubleTransformFrame, _frameTransform);
            //Storyboard.SetTargetProperty(doubleTransformFrame, new PropertyPath("Y"));

            //EmojiContainer.Dispatcher.BeginInvoke(async () =>
            //{
            //    storyboard.Begin();

            //    if (_frameTransform.Y < 0) // Keyboard is in view
            //    {
            //        Focus();
            //        TextBoxTarget.Dispatcher.BeginInvoke(() // no effect without dispatcher
            //            => VisualStateManager.GoToState(TextBoxTarget, "Focused", false));
            //    }

            //    if (_wasRendered) return;
            //    await Task.Delay(50);
            //    LoadCategory(0);
            //});

            //storyboard.Completed += (sender, args) =>
            //{
            //    frame.IsHitTestVisible = true;
            //    _isBlocked = false;
            //};
        }

        private void Hide()
        {
            _isOpen = false;

            VisualStateManager.GoToState(TextBoxTarget, "Unfocused", false);
            EmojiContainer.Visibility = Visibility.Collapsed;


            //var frame = (PhoneApplicationFrame)Application.Current.RootVisual;
            //frame.BackKeyPress -= OnBackKeyPress;

            //if (_isTextBoxTargetFocused)
            //{
            //    _frameTransform.Y = 0;

            //    EmojiContainer.Visibility = Visibility.Collapsed;

            //    return;
            //}

            //VisualStateManager.GoToState(TextBoxTarget, "Unfocused", false);

            //frame.IsHitTestVisible = false;
            //_isBlocked = true;

            //var transform = (TranslateTransform)EmojiContainer.RenderTransform;

            //var storyboard = new Storyboard();
            //var doubleTransformFrame = new DoubleAnimation
            //{
            //    From = -transform.Y,
            //    To = 0,
            //    Duration = TimeSpan.FromMilliseconds(440),
            //    EasingFunction = new ExponentialEase
            //    {
            //        EasingMode = EasingMode.EaseOut,
            //        Exponent = 6
            //    }
            //};
            //storyboard.Children.Add(doubleTransformFrame);
            //Storyboard.SetTarget(doubleTransformFrame, _frameTransform);
            //Storyboard.SetTargetProperty(doubleTransformFrame, new PropertyPath("Y"));
            //storyboard.Begin();

            //storyboard.Completed += (sender, args) =>
            //{
            //    EmojiContainer.Visibility = Visibility.Collapsed;

            //    frame.IsHitTestVisible = true;
            //    _isBlocked = false;
            //    transform.Y = 0;
            //};

        }

        #region _isTextBoxTargetFocused listeners
        private void TextBoxOnGotFocus(object sender, RoutedEventArgs routedEventArgs)
        {
            _isTextBoxTargetFocused = true;
        }
        private void TextBoxOnLostFocus(object sender, RoutedEventArgs routedEventArgs)
        {
            _isTextBoxTargetFocused = false;
        }
        #endregion

        /// <summary>
        /// Hide instance on pressing hardware Back button. Fires only when instance is opened.
        /// </summary>
        private void OnBackKeyPress(object sender, CancelEventArgs cancelEventArgs)
        {
            IsOpen = false;
            cancelEventArgs.Cancel = true;
        }

        /// <summary>
        /// Clear current highlight on scroll
        /// </summary>
        private static void VirtPanelOnScrollPositionChanged(object sender, MyVirtualizingPanel.ScrollPositionChangedEventAgrs scrollPositionChangedEventAgrs)
        {
            EmojiSpriteItem.ClearCurrentHighlight();
        }

        /// <summary>
        /// Changes tabs in UI and _currentCategory property
        /// </summary>
        public int CurrentCategory
        {
            get { return _currentCategory; }
            set
            {
                var previousCategory = GetCategoryButtonByIndex(_currentCategory);
                var nextCategory = GetCategoryButtonByIndex(value);

                if (previousCategory != null)
                    previousCategory.Background = ButtonBackground;

                nextCategory.Background = (Brush)Application.Current.Resources["PhoneAccentBrush"];
                _currentCategory = value;
            }
        }

        public void RemoveStickerSet(TLInputStickerSetBase stickerSet)
        {
            var setId = stickerSet.Name;
            if (_stickerSets.ContainsKey(setId))
            {
                _stickerSets.Remove(setId);

                var stateService = IoC.Get<IStateService>();
                stateService.GetAllStickersAsync(cachedStickers =>
                {
                    var allStickers29 = cachedStickers as TLAllStickers29;
                    if (allStickers29 != null)
                    {
                        List<TLDocument22> recentlyUsed;
                        if (_stickerSets.TryGetValue(@"tlg/recentlyUsed", out recentlyUsed))
                        {
                            var recentlyUsedId = new Dictionary<long, long>();
                            for (var i = 0; i < recentlyUsed.Count; i++)
                            {
                                if (recentlyUsed[i].StickerSet.Name == setId)
                                {
                                    recentlyUsedId[recentlyUsed[i].Id.Value] = recentlyUsed[i].Id.Value;
                                    recentlyUsed.RemoveAt(i--);

                                    _reloadStickerSprites = true;
                                }
                            }

                            for (var i = 0; i < allStickers29.RecentlyUsed.Count; i++)
                            {
                                var recentlyUsedSticker = allStickers29.RecentlyUsed[i];
                                if (recentlyUsedId.ContainsKey(recentlyUsedSticker.Id.Value))
                                {
                                    allStickers29.RecentlyUsed.RemoveAt(i--);
                                }
                            }
                        }
                    }

                    stateService.SaveAllStickersAsync(cachedStickers);

                    UpdateStickersPanel(StickerCategoryIndex);
                });
            }

            UpdateAllStickersAsync();
        }

        private void UpdateAllStickersAsync()
        {
            var mtProtoService = IoC.Get<IMTProtoService>();
            var stateService = IoC.Get<IStateService>();
            var eventAggregator = IoC.Get<ITelegramEventAggregator>();

            stateService.GetAllStickersAsync(cachedStickers =>
            {
                mtProtoService.GetAllStickersAsync(TLString.Empty,
                    result => Execute.BeginOnUIThread(() =>
                    {
                        var allStickers = result as TLAllStickers29;
                        if (allStickers != null)
                        {
                            var cachedStickers29 = cachedStickers as TLAllStickers29;
                            if (cachedStickers29 != null)
                            {
                                allStickers.IsDefaultSetVisible = cachedStickers29.IsDefaultSetVisible;
                                allStickers.RecentlyUsed = cachedStickers29.RecentlyUsed;
                                allStickers.Date = TLUtils.DateToUniversalTimeTLInt(0, DateTime.Now);
                            }

                            stateService.SaveAllStickersAsync(allStickers);

                            Execute.BeginOnThreadPool(() => eventAggregator.Publish(result));
                        }
                    }),
                    error => Execute.BeginOnUIThread(() =>
                    {
                        Execute.ShowDebugMessage("messages.getAllStickers error " + error);
                    }));
            });
        }

        public void AddStickerSet(TLMessagesStickerSet stickerSet)
        {
            var setId = stickerSet.Set.Id.ToString();
            if (_stickerSets.Count > 0
                && !_stickerSets.ContainsKey(setId))
            {
                var stickers = new List<TLDocument22>();
                foreach (var document in stickerSet.Documents)
                {
                    var document22 = document as TLDocument22;
                    if (document22 != null)
                    {
                        stickers.Add(document22);
                    }
                }

                _stickerSets[setId] = stickers;

                UpdateStickersPanel(_currentCategory);
            }

            UpdateAllStickersAsync();
        }

        private void UpdateStickersPanel(int index)
        {
            StickersPanel.Children.Clear();
            StickersPanel.Children.Add(_emojiButton);
            StickersPanel.Children.Add(_recentStickersButton);

            var isLightTheme = (Visibility)Application.Current.Resources["PhoneLightThemeVisibility"] == Visibility.Visible;
            var buttonStyleResourceKey = isLightTheme ? "CategoryButtonLightThemeStyle" : "CategoryButtonDarkThemeStyle";
            var buttonStyle = (Style)Resources[buttonStyleResourceKey];

            _stickerSetButtons.Clear();
            foreach (var stickerSet in _stickerSets)
            {
                if (stickerSet.Key != @"tlg/recentlyUsed")
                {
                    var image = new Image { Width = 53.0, Height = 53.0, DataContext = new TLStickerItem { Document = stickerSet.Value.FirstOrDefault() } };
                    var binding = new Binding("Self")
                    {
                        Converter = new DefaultPhotoConverter(),
                        ConverterParameter = 64.0
                    };
                    image.SetBinding(Image.SourceProperty, binding);

                    var button = new Button{ Width = 78.0, Height = 78.0, ClickMode = ClickMode.Release };
                    button.Style = buttonStyle;
                    button.Content = image;
                    button.DataContext = stickerSet.Key;
                    var set = stickerSet;
                    button.Click += (o, e) => LoadStickerSet(set.Key, set.Value);
                    StickersPanel.Children.Add(button);

                    _stickerSetButtons.Add(button);
                }
            }

            Execute.BeginOnUIThread(() =>
            {

                if (index > EmojiCategoryIndex || index == StickerCategoryIndex)
                {
                    RecentStickersButtonOnClick(null, null);
                }

                return;
            });
        }

        private void LoadStickerSet(string key, List<TLDocument22> stickerSet)
        {
            var recentlyUsedKey = @"tlg/recentlyUsed";
            var stickerPerRow = 5;

            var setId = key;
            List<VListItemBase> sprites;
            if (!_stickerSetSprites.TryGetValue(setId, out sprites)
                || (key == recentlyUsedKey && _reloadStickerSprites))
            {
                _reloadStickerSprites = false;
                sprites = new List<VListItemBase>();
                var stickers = new List<TLStickerItem>();
                for (var i = 1; i <= stickerSet.Count; i++)
                {
                    stickers.Add(new TLStickerItem { Document = stickerSet[i - 1] });

                    if (i % stickerPerRow == 0 || i == stickerSet.Count)
                    {
                        var item = new StickerSpriteItem(stickerPerRow, stickers, 90.0, 472.0);
                        item.StickerSelected += OnStickerSelected;
                        sprites.Add(item);
                        stickers.Clear();
                    }
                }

                _stickerSetSprites[setId] = sprites;
            }

            if (key == recentlyUsedKey)
            {
                _categoryRecentStickers = sprites;
            }


            var firstSlice = new List<VListItemBase>();
            var secondSlice = new List<VListItemBase>();

            for (var i = 0; i < sprites.Count; i++)
            {
                if (i < FirstStickerSliceCount)
                {
                    firstSlice.Add(sprites[i]);
                }
                else
                {
                    secondSlice.Add(sprites[i]);
                }
            }

            VirtPanel.ClearItems();
            VirtPanel.AddItems(firstSlice);
            Execute.BeginOnUIThread(TimeSpan.FromSeconds(0.1), () =>
            {
                VirtPanel.AddItems(secondSlice);
            });

            if (key == recentlyUsedKey)
            {
                CurrentCategory = RecentStickersCategoryIndex;
            }
            else
            {
                var index = 0;
                foreach (var button in _stickerSetButtons)
                {
                    var stickerSetKey = button.DataContext as string;
                    if (stickerSetKey == key) break;
                    index++;
                }

                CurrentCategory = RecentStickersCategoryIndex + index + 1;
            }
        }

        private readonly Dictionary<string, List<TLDocument22>> _stickerSets = new Dictionary<string, List<TLDocument22>>();
        private readonly Dictionary<string, List<VListItemBase>> _stickerSetSprites = new Dictionary<string, List<VListItemBase>>();

        public void LoadCategory(int index)
        {
            VirtPanel.ClearItems();

            if (_currentCategory == RecentsCategoryIndex)
                UnloadRecents();

            if (index == RecentsCategoryIndex)
            {
                LoadRecents();
                return;
            }

            List<VListItemBase> sprites = null;

            switch (index)
            {
                case 0:
                    sprites = _category1Sprites;
                    break;
                case 1:
                    sprites = _category2Sprites;
                    break;
                case 2:
                    sprites = _category3Sprites;
                    break;
                case 3:
                    sprites = _category4Sprites;
                    break;
                case 4:
                    sprites = _category5Sprites;
                    break;
                case StickerCategoryIndex:
                    sprites = _categoryRecentStickers;
                    break;
            }

            var firstSlice = new List<VListItemBase>();
            var secondSlice = new List<VListItemBase>();
            if (sprites == null)
            {
                sprites = new List<VListItemBase>();

                if (index == StickerCategoryIndex)
                {
                    CurrentCategory = index;

                    LoadingProgressBar.Visibility = Visibility.Visible;
                    var stateService = IoC.Get<IStateService>();
                    stateService.GetAllStickersAsync(cachedStickers =>
                    {
                        var hash = cachedStickers != null ? cachedStickers.Hash : TLString.Empty;

                        if (_reloadStickerSprites)
                        {
                            Execute.BeginOnUIThread(() =>
                            {
                                _reloadStickerSprites = false;
                                LoadingProgressBar.Visibility = Visibility.Collapsed;
                                CreateSetsAndUpdatePanel(index, cachedStickers);
                            });
                            return;
                        }

                        var mtProtoService = IoC.Get<IMTProtoService>();
                        mtProtoService.GetAllStickersAsync(hash,
                            result => Execute.BeginOnUIThread(() =>
                            {
                                var allStickers = result as TLAllStickers29;
                                if (allStickers != null)
                                {
                                    var cachedStickers29 = cachedStickers as TLAllStickers29;
                                    if (cachedStickers29 != null)
                                    {
                                        allStickers.IsDefaultSetVisible = cachedStickers29.IsDefaultSetVisible;
                                        allStickers.RecentlyUsed = cachedStickers29.RecentlyUsed;
                                        allStickers.Date = TLUtils.DateToUniversalTimeTLInt(0, DateTime.Now);
                                    }

                                    cachedStickers = allStickers;
                                    stateService.SaveAllStickersAsync(cachedStickers);
                                }

                                LoadingProgressBar.Visibility = Visibility.Collapsed;
                                CreateSetsAndUpdatePanel(index, cachedStickers);
                            }),
                            error => Execute.BeginOnUIThread(() =>
                            {
                                LoadingProgressBar.Visibility = Visibility.Collapsed;
                                CreateSetsAndUpdatePanel(index, cachedStickers);

                                Execute.ShowDebugMessage("messages.getAllStickers error " + error);
                            }));
                    });

                    return;
                }

                for (var i = 0; i < EmojiData.SpritesByCategory[index].Length; i++)
                {
                    var item = new EmojiSpriteItem(EmojiData.SpritesByCategory[index][i], index, i);
                    item.EmojiSelected += OnEmojiSelected;
                    sprites.Add(item);
                }

                switch (index)
                {
                    case 0:
                        _category1Sprites = sprites;
                        break;
                    case 1:
                        _category2Sprites = sprites;
                        break;
                    case 2:
                        _category3Sprites = sprites;
                        break;
                    case 3:
                        _category4Sprites = sprites;
                        break;
                    case 4:
                        _category5Sprites = sprites;
                        break;
                }
            }

            if (index == StickerCategoryIndex)
            {
                index = RecentStickersCategoryIndex;
            }

            CurrentCategory = index;
            var firstSliceCount = index == StickerCategoryIndex ? FirstStickerSliceCount : 1;
            for (var i = 0; i < sprites.Count; i++)
            {
                if (i < firstSliceCount)
                {
                    firstSlice.Add(sprites[i]);
                }
                else
                {
                    secondSlice.Add(sprites[i]);
                }
            }

            VirtPanel.AddItems(firstSlice);

            if (index < StickerCategoryIndex)
            {
                CreateButtonsBackgrounds(index);
            }

            if (!_wasRendered)
            {
                // Display LoadingProgressBar only once
                LoadingProgressBar.Visibility = Visibility.Collapsed;
                _wasRendered = true;
            }

            // Delayed rendering of the rest parts - speeds up initial load
            ThreadPool.QueueUserWorkItem(state =>
            {
                Thread.Sleep(100);
                Deployment.Current.Dispatcher.BeginInvoke(() =>
                {
                    if (_currentCategory != index)
                        return;

                    VirtPanel.AddItems(secondSlice);
                });
            });
        }

        private void CreateSetsAndUpdatePanel(int index, TLAllStickers allStickers)
        {
            if (allStickers == null) return;

            var recentlyUsed = new Dictionary<long, long>();
            var allStickers29 = allStickers as TLAllStickers29;
            if (allStickers29 != null)
            {
                foreach (var recentlyUsedSticker in allStickers29.RecentlyUsed)
                {
                    recentlyUsed[recentlyUsedSticker.Id.Value] = recentlyUsedSticker.Count.Value;
                }
            }
            
            var recentlyUsedSetId = @"tlg/recentlyUsed";
            _stickerSets.Clear();
            for (var i = 0; i < allStickers.Documents.Count; i++)
            {
                var document22 = allStickers.Documents[i] as TLDocument22;
                if (document22 != null)
                {
                    if (recentlyUsed.ContainsKey(document22.Id.Value))
                    {
                        List<TLDocument22> stickers;
                        if (_stickerSets.TryGetValue(recentlyUsedSetId, out stickers))
                        {
                            stickers.Add(document22);
                        }
                        else
                        {
                            _stickerSets[recentlyUsedSetId] = new List<TLDocument22> { document22 };
                        }
                        //continue;   // skip recently used stickers
                    }

                    if (document22.StickerSet != null)
                    {
                        var setId = document22.StickerSet.Name;
                        List<TLDocument22> stickers;
                        if (_stickerSets.TryGetValue(setId, out stickers))
                        {
                            stickers.Add(document22);
                        }
                        else
                        {
                            _stickerSets[setId] = new List<TLDocument22> {document22};
                        }
                    }
                }
            }

            if (_stickerSets.ContainsKey(recentlyUsedSetId))
            {
                _stickerSets[recentlyUsedSetId] = _stickerSets[recentlyUsedSetId].OrderByDescending(x => recentlyUsed[x.Id.Value]).ToList();
            }

            UpdateStickersPanel(index);
        }

        public static void OnRootFrameTransformChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
        {
            ((EmojiControl)source).OnRootFrameTransformChanged();
        }

        public void OnRootFrameTransformChanged()
        {
            if (!_isOpen) return;

            var offset = _isPortrait ? -PortraitOrientationHeight : -AlbumOrientationHeight;
            _frameTransform.Y = offset;
        }

        #region Recents

        public static readonly DependencyProperty RecentItemsProperty = DependencyProperty.Register(
            "RecentItems", typeof (IList<EmojiDataItem>), typeof (EmojiControl), new PropertyMetadata(default(IList<EmojiDataItem>)));

        public IList<EmojiDataItem> RecentItems
        {
            get { return (IList<EmojiDataItem>) GetValue(RecentItemsProperty); }
            set { SetValue(RecentItemsProperty, value); }
        }

        public void LoadRecents()
        {
            CurrentCategory = RecentsCategoryIndex;

            if (EmojiData.Recents == null)
            {
                EmojiData.LoadRecents();
            }

            RecentItems = new ObservableCollection<EmojiDataItem>(EmojiData.Recents ?? new List<EmojiDataItem>());

            CSV.IsHitTestVisible = false;
            Recents.Visibility = Visibility.Visible;
        }

        public void UnloadRecents()
        {
            CSV.IsHitTestVisible = true;
            Recents.Visibility = Visibility.Collapsed;
        }

        #endregion Recents

        private void OnEmojiSelected(object sender, EmojiSelectedEventArgs args)
        {
            TextBoxTarget.Dispatcher.BeginInvoke(() =>
            {
                var selectionStart = TextBoxTarget.SelectionStart;
                TextBoxTarget.Text = TextBoxTarget.Text.Insert(selectionStart, args.DataItem.String);
                TextBoxTarget.Select(selectionStart + args.DataItem.String.Length, 0);
            });

            if (_currentCategory == RecentsCategoryIndex) return;

            var that = args.DataItem;
            ThreadPool.QueueUserWorkItem(state => EmojiData.AddToRecents(that));
        }

        public event EventHandler<StickerSelectedEventArgs> StickerSelected;

        protected virtual void RaiseStickerSelected(StickerSelectedEventArgs e)
        {
            var handler = StickerSelected;
            if (handler != null) handler(this, e);
        }

        private void OnStickerSelected(object sender, StickerSelectedEventArgs args)
        {
            UpdateRecentlyUsedStickers(args);

            RaiseStickerSelected(args);
        }

        private bool _reloadStickerSprites;

        private void UpdateRecentlyUsedStickers(StickerSelectedEventArgs args)
        {
            if (args == null) return;
            var stickerId = args.Sticker.Document.Id;

            Execute.BeginOnThreadPool(() =>
            {
                var stateService = IoC.Get<IStateService>();
                stateService.GetAllStickersAsync(cachedStickers =>
                {
                    var allStickers = cachedStickers as TLAllStickers29;
                    if (allStickers != null)
                    {
                        var recentlyUsed = allStickers.RecentlyUsed;
                        if (recentlyUsed != null)
                        {
                            var isAdded = false;
                            for (var i = 0; i < recentlyUsed.Count; i++)
                            {
                                var recentlyUsedSticker = recentlyUsed[i];

                                if (recentlyUsed[i].Id.Value == stickerId.Value)
                                {
                                    recentlyUsed[i].Count = new TLLong(recentlyUsed[i].Count.Value + 1);

                                    var newPosition = i;
                                    for (var j = i - 1; j >= 0; j--)
                                    {
                                        if (recentlyUsed[j].Count.Value <= recentlyUsed[i].Count.Value)
                                        {
                                            newPosition = j;
                                        }
                                    }

                                    if (i != newPosition)
                                    {
                                        allStickers.RecentlyUsed.RemoveAt(i);
                                        allStickers.RecentlyUsed.Insert(newPosition, recentlyUsedSticker);
                                        _reloadStickerSprites = true;
                                    }
                                    isAdded = true;
                                    break;
                                }
                            }

                            if (!isAdded)
                            {
                                for (var i = 0; i < recentlyUsed.Count; i++)
                                {
                                    if (recentlyUsed[i].Count.Value <= 1)
                                    {
                                        recentlyUsed.Insert(i, new TLRecentlyUsedSticker
                                        {
                                            Id = stickerId,
                                            Count = new TLLong(1)
                                        });
                                        _reloadStickerSprites = true;
                                        isAdded = true;
                                        break;
                                    }
                                }

                                if (!isAdded)
                                {
                                    recentlyUsed.Add(new TLRecentlyUsedSticker
                                    {
                                        Id = stickerId,
                                        Count = new TLLong(1)
                                    });
                                    _reloadStickerSprites = true;
                                }
                            }

                            if (_stickerSets != null)
                            {
                                var recentlyUsedCache = new Dictionary<long, long>();
                                foreach (var recentlyUsedSticker in allStickers.RecentlyUsed)
                                {
                                    recentlyUsedCache[recentlyUsedSticker.Id.Value] = recentlyUsedSticker.Count.Value;
                                }
                                var recentlyUsedSetId = @"tlg/recentlyUsed";
                                _stickerSets.Clear();
                                for (var i = 0; i < allStickers.Documents.Count; i++)
                                {
                                    var document22 = allStickers.Documents[i] as TLDocument22;
                                    if (document22 != null)
                                    {
                                        if (recentlyUsedCache.ContainsKey(document22.Id.Value))
                                        {
                                            List<TLDocument22> stickers;
                                            if (_stickerSets.TryGetValue(recentlyUsedSetId, out stickers))
                                            {
                                                stickers.Add(document22);
                                            }
                                            else
                                            {
                                                _stickerSets[recentlyUsedSetId] = new List<TLDocument22> { document22 };
                                            }
                                        }

                                        if (document22.StickerSet != null)
                                        {
                                            var setId = document22.StickerSet.Name;
                                            List<TLDocument22> stickers;
                                            if (_stickerSets.TryGetValue(setId, out stickers))
                                            {
                                                stickers.Add(document22);
                                            }
                                            else
                                            {
                                                _stickerSets[setId] = new List<TLDocument22> { document22 };
                                            }
                                        }
                                    }
                                }

                                if (_stickerSets.ContainsKey(recentlyUsedSetId))
                                {
                                    _stickerSets[recentlyUsedSetId] = _stickerSets[recentlyUsedSetId].OrderByDescending(x => recentlyUsedCache[x.Id.Value]).ToList();
                                }
                            }

                            stateService.SaveAllStickersAsync(cachedStickers);
                        }
                    }
                });
            });
        }

        /// <summary>
        /// Emoji control backspace button logic
        /// </summary>
        private void BackspaceButtonOnClick(object sender, RoutedEventArgs routedEventArgs)
        {
            var text = TextBoxTarget.Text;
            var selectionStart = TextBoxTarget.SelectionStart;

            if (text.Length <= 0) return;
            if (selectionStart == 0) return;

            int toSubstring;

            if (text.Length > 1)
            {
                var prevSymbol = text[selectionStart - 2];
                var prevBytes = BitConverter.GetBytes(prevSymbol);

                var curSymbol = text[selectionStart - 1];
                var curBytes = BitConverter.GetBytes(curSymbol);

                if (prevBytes[1] == 0xD8 && (prevBytes[0] == 0x3D || prevBytes[0] == 0x3C))
                    toSubstring = 2;
                else if (curBytes[1] == 0x20 && curBytes[0] == 0xE3)
                    toSubstring = 2;
                else
                    toSubstring = 1;
            }
            else
            {
                toSubstring = 1;
            }

            TextBoxTarget.Text = text.Remove(selectionStart - toSubstring, toSubstring);
            TextBoxTarget.SelectionStart = selectionStart - toSubstring;
        }

        #region User Interface

        private readonly Button _abcButton = new Button { ClickMode = ClickMode.Release };
        private readonly Button _recentsButton = new Button { ClickMode = ClickMode.Press };
        private readonly Button _cat0Button = new Button { ClickMode = ClickMode.Press };
        private readonly Button _cat1Button = new Button { ClickMode = ClickMode.Press };
        private readonly Button _cat2Button = new Button { ClickMode = ClickMode.Press };
        private readonly Button _cat3Button = new Button { ClickMode = ClickMode.Press };
        private readonly Button _cat4Button = new Button { ClickMode = ClickMode.Press };
        private readonly Button _stickerButton = new Button { ClickMode = ClickMode.Press };
        private readonly RepeatButton _backspaceButton = new RepeatButton { ClickMode = ClickMode.Release, Interval = 100 };

        private readonly Button _emojiButton = new Button { ClickMode = ClickMode.Press };
        private readonly Button _recentStickersButton = new Button { ClickMode = ClickMode.Press };
        private readonly List<Button> _stickerSetButtons = new List<Button>(); 

        public const int RecentsCategoryIndex = 5;
        public const int StickerCategoryIndex = 6;
        public const int EmojiCategoryIndex = 7;
        public const int RecentStickersCategoryIndex = 8;

        private Button GetCategoryButtonByIndex(int index)
        {
            if (index > RecentStickersCategoryIndex)
            {
                index -= RecentStickersCategoryIndex;
                if (index > 0
                    && _stickerSetButtons.Count >= index)
                {
                    return _stickerSetButtons[index - 1];
                }
            }

            switch (index)
            {
                case 0:
                    return _cat0Button;
                case 1:
                    return _cat1Button;
                case 2:
                    return _cat2Button;
                case 3:
                    return _cat3Button;
                case 4:
                    return _cat4Button;
                case RecentsCategoryIndex:
                    return _recentsButton;
                case StickerCategoryIndex:
                    return _stickerButton;
                case EmojiCategoryIndex:
                    return _emojiButton;
                case RecentStickersCategoryIndex:
                    return _recentStickersButton;
                default:
                    return null;
            }
        }

        public Brush ButtonBackground
        {
            get
            {
                var isLightTheme = (Visibility)Application.Current.Resources["PhoneLightThemeVisibility"] == Visibility.Visible;

                return isLightTheme
                    ? new SolidColorBrush(Colors.White)
                    : new SolidColorBrush(Color.FromArgb(255, 71, 71, 71));
            }
        }

        private void UpdateButtons(bool isStickersPanelVisible)
        {
            IsStickersPanelVisible = isStickersPanelVisible;

            ButtonsGrid.Children.Clear();
            ButtonsGrid.ColumnDefinitions.Clear();

            var columnsCount = IsStickersPanelVisible ? 9 : 8;
            for (var i = 0; i < columnsCount; i++)
            {
                ButtonsGrid.ColumnDefinitions.Add(new ColumnDefinition());
            }

            ButtonsGrid.Children.Add(_abcButton);
            ButtonsGrid.Children.Add(_recentsButton);
            ButtonsGrid.Children.Add(_cat0Button);
            ButtonsGrid.Children.Add(_cat1Button);
            ButtonsGrid.Children.Add(_cat2Button);
            ButtonsGrid.Children.Add(_cat3Button);
            ButtonsGrid.Children.Add(_cat4Button);
            if (IsStickersPanelVisible)
            {
                ButtonsGrid.Children.Add(_stickerButton);
            }
            ButtonsGrid.Children.Add(_backspaceButton);
        }

        public void LoadButtons()
        {
            var isLightTheme = (Visibility)Application.Current.Resources["PhoneLightThemeVisibility"] == Visibility.Visible;
            var buttonStyleResourceKey = isLightTheme ? "CategoryButtonLightThemeStyle" : "CategoryButtonDarkThemeStyle";
            var buttonStyle = (Style)Resources[buttonStyleResourceKey];

            _abcButton.Style = buttonStyle;
            _recentsButton.Style = buttonStyle;
            _cat0Button.Style = buttonStyle;
            _cat1Button.Style = buttonStyle;
            _cat2Button.Style = buttonStyle;
            _cat3Button.Style = buttonStyle;
            _cat4Button.Style = buttonStyle;
            _stickerButton.Style = buttonStyle;

            _emojiButton.Style = buttonStyle;
            _recentStickersButton.Style = buttonStyle;

            var repeatButtonStyleResourceKey = isLightTheme ? "RepeatButtonLightThemeStyle" : "RepeatButtonDarkThemeStyle";
            _backspaceButton.Style = (Style)Resources[repeatButtonStyleResourceKey];

            var prefix = isLightTheme ? "light." : string.Empty;
            _abcButton.Content = new Image
            {
                Source = new BitmapImage(Helpers.GetAssetUri(prefix + "emoji.abc")),
                Width = 34,
                Height = 32
            };
            _recentsButton.Content = new Image
            {
                Source = new BitmapImage(Helpers.GetAssetUri(prefix + "emoji.recent")),
                Width = 34,
                Height = 32
            };
            _cat0Button.Content = new Image
            {
                Source = new BitmapImage(Helpers.GetAssetUri(prefix + "emoji.category.1")),
                Width = 34,
                Height = 32
            };
            _cat1Button.Content = new Image
            {
                Source = new BitmapImage(Helpers.GetAssetUri(prefix + "emoji.category.2")),
                Width = 34,
                Height = 32
            };
            _cat2Button.Content = new Image
            {
                Source = new BitmapImage(Helpers.GetAssetUri(prefix + "emoji.category.3")),
                Width = 34,
                Height = 32
            };
            _cat3Button.Content = new Image
            {
                Source = new BitmapImage(Helpers.GetAssetUri(prefix + "emoji.category.4")),
                Width = 34,
                Height = 32
            };
            _cat4Button.Content = new Image
            {
                Source = new BitmapImage(Helpers.GetAssetUri(prefix + "emoji.category.5")),
                Width = 34,
                Height = 32
            };
            _stickerButton.Content = new Image
            {
                Source = new BitmapImage(Helpers.GetAssetUri(prefix + "emoji.sticker")),
                Width = 34,
                Height = 32
            };
            _backspaceButton.Content = new Image
            {
                Source = new BitmapImage(Helpers.GetAssetUri(prefix + "emoji.backspace")),
                Width = 34,
                Height = 32
            };

            Grid.SetColumn(_abcButton, 0);
            Grid.SetColumn(_recentsButton, 1);
            Grid.SetColumn(_cat0Button, 2);
            Grid.SetColumn(_cat1Button, 3);
            Grid.SetColumn(_cat2Button, 4);
            Grid.SetColumn(_cat3Button, 5);
            Grid.SetColumn(_cat4Button, 6);
            Grid.SetColumn(_stickerButton, 7);
            Grid.SetColumn(_backspaceButton, 8);

            _emojiButton.Width = 47.0;
            _emojiButton.Content = new Image
            {
                Source = new BitmapImage(Helpers.GetAssetUri(prefix + "emoji.category.1")),
                Width = 34,
                Height = 32
            };
            _recentStickersButton.Width = 47.0;
            _recentStickersButton.Content = new Image
            {
                Source = new BitmapImage(Helpers.GetAssetUri(prefix + "emoji.recent")),
                Width = 34,
                Height = 32
            };

            //Grid.SetColumn(_emojiButton, 0);
            //Grid.SetColumn(_recentStickersButton, 1);

            _abcButton.Click += AbcButtonOnClick;
            _cat0Button.Click += CategoryButtonClick;
            _cat1Button.Click += CategoryButtonClick;
            _cat2Button.Click += CategoryButtonClick;
            _cat3Button.Click += CategoryButtonClick;
            _cat4Button.Click += CategoryButtonClick;
            _stickerButton.Click += StickerButtonOnClick;
            _recentsButton.Click += CategoryButtonClick;
            _backspaceButton.Click += BackspaceButtonOnClick;

            _emojiButton.Click += EmojiButtonOnClick;
            _recentStickersButton.Click += RecentStickersButtonOnClick;

            StickersPanel.Children.Clear();
            StickersPanel.Children.Add(_emojiButton);
            StickersPanel.Children.Add(_recentStickersButton);

            UpdateButtons(IsStickersPanelVisible);
        }

        private void AbcButtonOnClick(object sender, RoutedEventArgs routedEventArgs)
        {
            TextBoxTarget.Focus();
        }

        private void RecentStickersButtonOnClick(object sender, RoutedEventArgs routedEventArgs)
        {
            var key = @"tlg/recentlyUsed";
            List<TLDocument22> stickerSet;
            if (_stickerSets != null && _stickerSets.TryGetValue(@"tlg/recentlyUsed", out stickerSet))
            {
                LoadStickerSet(key, stickerSet);
            }
            else
            {
                VirtPanel.ClearItems();
                _categoryRecentStickers = new List<VListItemBase>();
                CurrentCategory = RecentStickersCategoryIndex;
            }
        }

        private void StickerButtonOnClick(object sender, RoutedEventArgs routedEventArgs)
        {
            ButtonsGrid.Visibility = Visibility.Collapsed;
            StickersGrid.Visibility = Visibility.Visible;
            LoadCategory(StickerCategoryIndex);
        }

        private void EmojiButtonOnClick(object sender, RoutedEventArgs routedEventArgs)
        {
            ButtonsGrid.Visibility = Visibility.Visible;
            StickersGrid.Visibility = Visibility.Collapsed;
            LoadCategory(RecentsCategoryIndex);
        }

        private void CategoryButtonClick(object sender, RoutedEventArgs routedEventArgs)
        {
            if (sender == _cat0Button)
                LoadCategory(0);
            else if (sender == _cat1Button)
                LoadCategory(1);
            else if (sender == _cat2Button)
                LoadCategory(2);
            else if (sender == _cat3Button)
                LoadCategory(3);
            else if (sender == _cat4Button)
                LoadCategory(4);
            else if (sender == _recentsButton)
                LoadCategory(RecentsCategoryIndex);
            else if (sender == _stickerButton)
                LoadCategory(StickerCategoryIndex);
        }

        private void CreateButtonsBackgrounds(int categoryIndex)
        {
            var sprites = EmojiData.SpriteRowsCountByCategory[categoryIndex];
            var buttonBackgroundColor = ButtonBackground;
            for (var i = 0; i < sprites.Length; i++)
            {
                var rowsCount = sprites[i];

                var block = new Rectangle
                {
                    Width = EmojiSpriteItem.SpriteWidth,
                    Height = EmojiSpriteItem.RowHeight * rowsCount,
                    Fill = buttonBackgroundColor,
                    Margin = new Thickness(4, 0, 4, 0)
                };
                Canvas.SetTop(block, (EmojiSpriteItem.SpriteHeight) * i);
                VirtPanel.Children.Insert(0, block);
            }
        }

        private void InitializeOrientation(Orientation orientation)
        {
            switch (orientation)
            {
                case Orientation.Vertical:
                    ButtonsGrid.Height = 78;
                    ButtonsGrid.Margin = new Thickness(0, 6, 0, 0);
                    EmojiContainer.Height = PortraitOrientationHeight;
                    //_frameTransform.Y = -PortraitOrientationHeight;
                    break;

                case Orientation.Horizontal:
                    ButtonsGrid.Height = 58;
                    ButtonsGrid.Margin = new Thickness(0, 6, 0, 3);
                    EmojiContainer.Height = AlbumOrientationHeight;
                    //_frameTransform.Y = -AlbumOrientationHeight;
                    break;
            }
        }

        #endregion User Interface


        /// <summary>
        /// Orientation change handler
        /// </summary>
        private void OnSizeChanged(object sender, SizeChangedEventArgs sizeChangedEventArgs)
        {
            var currentOrientation = ((PhoneApplicationFrame)Application.Current.RootVisual).Orientation;
            var isPortrait = currentOrientation == PageOrientation.PortraitUp ||
                             currentOrientation == PageOrientation.PortraitDown ||
                             currentOrientation == PageOrientation.Portrait;

            if (_isPortrait == isPortrait && _wasRendered) return;

            _isPortrait = isPortrait;
            InitializeOrientation(isPortrait ? Orientation.Vertical : Orientation.Horizontal);
        }

        private void UIElement_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {

            ((Border)sender).Background = (Brush)Application.Current.Resources["PhoneAccentBrush"];
        }

        private void UIElement_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ((Border) sender).Background = ButtonBackground;
        }

        private void UIElement_OnMouseLeave(object sender, MouseEventArgs e)
        {
            ((Border) sender).Background = ButtonBackground;
        }

        private void EmojiButton_OnTap(object sender, GestureEventArgs e)
        {
            var button = (FrameworkElement)sender;
            var emojiItem = (EmojiDataItem)button.DataContext;

            OnEmojiSelected(sender, new EmojiSelectedEventArgs{DataItem = emojiItem});

            ////RaiseEmojiAdded(new EmojiAddedEventArgs { Emoji = emojiItem.String });

            //if (_currentCategory != RecentsCategoryIndex)
            //{
            //    var prevItem = RecentItems.FirstOrDefault(x => x.Code == emojiItem.Code);
            //    if (prevItem != null)
            //    {
            //        RecentItems.Remove(prevItem);
            //        RecentItems.Insert(0, prevItem);
            //    }
            //    else
            //    {
            //        RecentItems.Insert(0, emojiItem);
            //        RecentItems = RecentItems.Take(30).ToList();
            //    }
            //}
        }

        public void ReloadStickerSprites()
        {
            if (_reloadStickerSprites)
            {
                _categoryRecentStickers = null;
            }
        }

        public void OpenStickerSprites()
        {
            if (_reloadStickerSprites)
            {
                if (_currentCategory == StickerCategoryIndex)
                LoadCategory(_currentCategory);
            }
        }
    }
}
