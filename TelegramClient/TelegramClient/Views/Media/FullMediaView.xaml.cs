using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using Caliburn.Micro;
using Microsoft.Phone.Shell;
using Telegram.Api.TL;
using Telegram.Api.TL.Interfaces;
using TelegramClient.Helpers;
using TelegramClient.Resources;
using TelegramClient.ViewModels;
using TelegramClient.ViewModels.Media;

namespace TelegramClient.Views.Media
{
    public partial class FullMediaView
    {
        private readonly AppBarButton _searchButton = new AppBarButton
        {
            Text = AppResources.Search,
            IsEnabled = true,
            IconUri = new Uri("/Images/ApplicationBar/appbar.feature.search.rest.png", UriKind.Relative)
        };

        private readonly AppBarButton _manageButton = new AppBarButton
        {
            Text = AppResources.Manage,
            IsEnabled = true,
            IconUri = new Uri("/Images/ApplicationBar/appbar.manage.rest.png", UriKind.Relative)
        };

        private readonly AppBarButton _forwardButton = new AppBarButton
        {
            Text = AppResources.Forward,
            IsEnabled = true,
            IconUri = new Uri("/Images/ApplicationBar/appbar.forwardmessage.png", UriKind.Relative)
        };

        private readonly AppBarButton _deleteButton = new AppBarButton
        {
            Text = AppResources.Delete,
            IsEnabled = true,
            IconUri = new Uri("/Images/ApplicationBar/appbar.delete.png", UriKind.Relative)
        };

        public FullMediaViewModel ViewModel 
        {
            get { return DataContext as FullMediaViewModel; }
        }

        public FullMediaView()
        {
            var timer = Stopwatch.StartNew();

            InitializeComponent();

            OptimizeFullHD();

            _searchButton.Click += (sender, args) => ViewModel.Search();
            _manageButton.Click += (sender, args) => ViewModel.Manage();
            _forwardButton.Click += (sender, args) => ViewModel.Forward();
            _deleteButton.Click += (sender, args) => ViewModel.Delete();

            Loaded += (sender, args) =>
            {
                TimerString.Text = timer.Elapsed.ToString();

                ViewModel.PropertyChanged += OnViewModelPropertyChanged;
                ViewModel.Files.PropertyChanged += OnFilesPropertyChanged;
                ViewModel.Links.PropertyChanged += OnLinksPropertyChanged;
                ViewModel.Music.PropertyChanged += OnMusicPropertyChanged;

                if (ViewModel.ImageViewer != null)
                    ViewModel.ImageViewer.PropertyChanged += OnImageViewerPropertyChanged;
                if (ViewModel.AnimatedImageViewer != null)
                    ViewModel.AnimatedImageViewer.PropertyChanged += OnAnimatedImageViewerPropertyChanged;

                ViewModel.Files.Items.CollectionChanged += OnFilesCollectionChanged;
                ViewModel.Links.Items.CollectionChanged += OnLinksCollectionChanged;
                ViewModel.Music.Items.CollectionChanged += OnMusicCollectionChanged;

                BuildLocalizedAppBar();
                //ReturnItemsVisibility();
            };

            Unloaded += (sender, args) =>
            {
                ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
                ViewModel.Files.PropertyChanged -= OnFilesPropertyChanged;
                ViewModel.Links.PropertyChanged -= OnLinksPropertyChanged;
                ViewModel.Music.PropertyChanged -= OnMusicPropertyChanged;

                if (ViewModel.ImageViewer != null)
                    ViewModel.ImageViewer.PropertyChanged -= OnImageViewerPropertyChanged;
                if (ViewModel.AnimatedImageViewer != null)
                    ViewModel.AnimatedImageViewer.PropertyChanged -= OnAnimatedImageViewerPropertyChanged;

                ViewModel.Files.Items.CollectionChanged -= OnFilesCollectionChanged;
                ViewModel.Links.Items.CollectionChanged -= OnLinksCollectionChanged;
                ViewModel.Music.Items.CollectionChanged -= OnMusicCollectionChanged;
            };

            Items.SelectionChanged += (sender, args) =>
            {
                if (ApplicationBar == null) return;

                ApplicationBar.IsVisible = false;

                if (Items.SelectedItem is FilesViewModel<IInputPeer>
                    || Items.SelectedItem is LinksViewModel<IInputPeer>
                    || Items.SelectedItem is MusicViewModel<IInputPeer>)
                {
                    if (Items.SelectedItem is FilesViewModel<IInputPeer>)
                    {
                        OnFilesCollectionChanged(sender, null);
                    }
                    else if (Items.SelectedItem is LinksViewModel<IInputPeer>)
                    {
                        OnLinksCollectionChanged(sender, null);
                    }
                    else
                    {
                        OnMusicCollectionChanged(sender, null);
                    }
                    ApplicationBar.IsVisible = true;
                    ApplicationBar.Buttons.Clear();
                    ApplicationBar.Buttons.Add(_searchButton);
                    ApplicationBar.Buttons.Add(_manageButton);
                }
            };
        }

        private void OptimizeFullHD()
        {
#if WP8
            var isFullHD = Application.Current.Host.Content.ScaleFactor == 225;
            //if (!isFullHD) return;
#endif

            Items.HeaderTemplate = (DataTemplate)Application.Current.Resources["FullHDPivotHeaderTemplate"];
        }

        private void OnFilesPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (Property.NameEquals(e.PropertyName, () => ViewModel.Files.IsSelectionEnabled))
            {
                if (ViewModel.Files.IsSelectionEnabled)
                {
                    SwitchToSelectionMode(ViewModel.Files.IsGroupActionEnabled);
                }
                else
                {
                    SwithToNormalMode();
                }
            }
            else if (Property.NameEquals(e.PropertyName, () => ViewModel.Files.IsGroupActionEnabled))
            {
                var isGroupActionEnabled = ViewModel.Files.IsGroupActionEnabled;

                _forwardButton.IsEnabled = isGroupActionEnabled;
                _deleteButton.IsEnabled = isGroupActionEnabled;
            }
        }

        private void OnLinksPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (Property.NameEquals(e.PropertyName, () => ViewModel.Links.IsSelectionEnabled))
            {
                if (ViewModel.Links.IsSelectionEnabled)
                {
                    SwitchToSelectionMode(ViewModel.Links.IsGroupActionEnabled);
                }
                else
                {
                    SwithToNormalMode();
                }
            }
            else if (Property.NameEquals(e.PropertyName, () => ViewModel.Links.IsGroupActionEnabled))
            {
                var isGroupActionEnabled = ViewModel.Links.IsGroupActionEnabled;

                _forwardButton.IsEnabled = isGroupActionEnabled;
                _deleteButton.IsEnabled = isGroupActionEnabled;
            }
        }



        private void OnMusicPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (Property.NameEquals(e.PropertyName, () => ViewModel.Music.IsSelectionEnabled))
            {
                if (ViewModel.Music.IsSelectionEnabled)
                {
                    SwitchToSelectionMode(ViewModel.Music.IsGroupActionEnabled);
                }
                else
                {
                    SwithToNormalMode();
                }
            }
            else if (Property.NameEquals(e.PropertyName, () => ViewModel.Music.IsGroupActionEnabled))
            {
                var isGroupActionEnabled = ViewModel.Music.IsGroupActionEnabled;

                _forwardButton.IsEnabled = isGroupActionEnabled;
                _deleteButton.IsEnabled = isGroupActionEnabled;
            }
        }

        private void SwithToNormalMode()
        {
            if (ApplicationBar == null || ApplicationBar.Buttons == null) return;

            ApplicationBar.Buttons.Clear();
            ApplicationBar.Buttons.Add(_searchButton);
            ApplicationBar.Buttons.Add(_manageButton);

#if WP8
            Items.IsLocked = false;
            
#endif
        }

        private void SwitchToSelectionMode(bool isGroupActionEnabled)
        {
            if (ApplicationBar == null || ApplicationBar.Buttons == null) return;

            var channel = ViewModel.CurrentItem as TLChannel;
            //var isGroupActionEnabled = ViewModel.Files.IsGroupActionEnabled;

            _forwardButton.IsEnabled = isGroupActionEnabled;
            _deleteButton.IsEnabled = isGroupActionEnabled;

            ApplicationBar.Buttons.Clear();
            ApplicationBar.Buttons.Add(_forwardButton);

            if (channel == null || channel.Creator)
            {
                ApplicationBar.Buttons.Add(_deleteButton);
            }
#if WP8
            Items.IsLocked = true;
#endif
        }

        private void ReturnItemsVisibility()
        {
            var selectedIndex = Items.SelectedIndex;
            ((ViewModelBase)Items.Items[selectedIndex]).Visibility = Visibility.Visible;

            Execute.BeginOnUIThread(() =>
            {
                foreach (ViewModelBase item in Items.Items)
                {
                    item.Visibility = Visibility.Visible;
                }
            });
        }

        private void BuildLocalizedAppBar()
        {
            if (ApplicationBar != null) return;

            ApplicationBar = new ApplicationBar();
            ApplicationBar.IsVisible = false;
        }

        private IApplicationBar _prevApplicationBar;

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (Property.NameEquals(e.PropertyName, () => ViewModel.AnimatedImageViewer)
                && ViewModel.AnimatedImageViewer != null)
            {
                ViewModel.AnimatedImageViewer.PropertyChanged += OnAnimatedImageViewerPropertyChanged;
            }
            else if (Property.NameEquals(e.PropertyName, () => ViewModel.ImageViewer)
                && ViewModel.ImageViewer != null)
            {
                ViewModel.ImageViewer.PropertyChanged += OnImageViewerPropertyChanged;
            }
        }

        private void OnImageViewerPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (Property.NameEquals(e.PropertyName, () => ViewModel.ImageViewer.IsOpen))
            {
                ViewModel.NotifyOfPropertyChange(() => ViewModel.IsViewerOpen);

                if (ViewModel.ImageViewer.IsOpen)
                {
                    _prevApplicationBar = ApplicationBar;
                    ApplicationBar = ((ImageViewerView)ImageViewer.Content).ApplicationBar;
                }
                else
                {
                    if (_prevApplicationBar != null)
                    {
                        ApplicationBar = _prevApplicationBar;
                    }
                }
            }
        }

        private void OnAnimatedImageViewerPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            ApplicationBar.IsVisible = !ViewModel.AnimatedImageViewer.IsOpen;
        }

        private void OnFilesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            _searchButton.IsEnabled = ViewModel.Files.Items.Count > 0;
            _manageButton.IsEnabled = ViewModel.Files.Items.Count > 0;
            ViewModel.Files.NotifyOfPropertyChange(() => ViewModel.Files.IsEmptyList);
        }

        private void OnLinksCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            _searchButton.IsEnabled = ViewModel.Links.Items.Count > 0;
            _manageButton.IsEnabled = ViewModel.Links.Items.Count > 0;
            ViewModel.Links.NotifyOfPropertyChange(() => ViewModel.Links.IsEmptyList);
        }

        private void OnMusicCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            _searchButton.IsEnabled = ViewModel.Music.Items.Count > 0;
            _manageButton.IsEnabled = ViewModel.Music.Items.Count > 0;
            ViewModel.Music.NotifyOfPropertyChange(() => ViewModel.Music.IsEmptyList);
        }

        private void FullMediaView_OnBackKeyPress(object sender, CancelEventArgs e)
        {
            if (ViewModel.ImageViewer != null
                && ViewModel.ImageViewer.IsOpen)
            {
                ViewModel.ImageViewer.CloseViewer();
                e.Cancel = true;
                return;
            }

            if (ViewModel.AnimatedImageViewer != null
                && ViewModel.AnimatedImageViewer.IsOpen)
            {
                ViewModel.AnimatedImageViewer.CloseViewer();
                e.Cancel = true;
                return;
            }

            if (ViewModel.Files.IsSelectionEnabled)
            {
                ViewModel.Files.IsSelectionEnabled = false;
                e.Cancel = true;
                return;
            }

            if (ViewModel.Links.IsSelectionEnabled)
            {
                ViewModel.Links.IsSelectionEnabled = false;
                e.Cancel = true;
                return;
            }

            if (ViewModel.Music.IsSelectionEnabled)
            {
                ViewModel.Music.IsSelectionEnabled = false;
                e.Cancel = true;
                return;
            }

            ViewModel.CancelLoading();
        }

        private static readonly Uri ExternalUri = new Uri(@"app://external/");

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            if (e.Uri != ExternalUri)
            {
                var selectedIndex = Items.SelectedIndex;
                for (var i = 0; i < Items.Items.Count; i++)
                {
                    //if (selectedIndex != i)
                    {
                        ((ViewModelBase)Items.Items[i]).Visibility = Visibility.Collapsed;
                    }
                }
            }
        }

        private void TelegramNavigationTransition_OnEndTransition(object sender, RoutedEventArgs e)
        {
            //MessageBox.Show("EndTransition");
            ReturnItemsVisibility();
        }
    }
}