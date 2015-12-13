using System;
using System.Device.Location;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Navigation;
using Caliburn.Micro;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Controls.Maps;
using Microsoft.Phone.Shell;
using Microsoft.Phone.UserData;
using Telegram.Api.TL;
using TelegramClient.Resources;
using TelegramClient.ViewModels.Media;
using TelegramClient.Views.Media.MapTileSources;

namespace TelegramClient.Views.Media
{
    public partial class MapView
    {
        public static readonly DependencyProperty ContactLocationStringProperty = DependencyProperty.Register(
            "ContactLocationString", typeof (string), typeof (MapView), new PropertyMetadata(default(string)));

        public string ContactLocationString
        {
            get { return (string) GetValue(ContactLocationStringProperty); }
            set { SetValue(ContactLocationStringProperty, value); }
        }

        public static readonly DependencyProperty DistanceProperty = DependencyProperty.Register(
            "Distance", typeof (double), typeof (MapView), new PropertyMetadata(default(double)));

        public double Distance
        {
            get { return (double) GetValue(DistanceProperty); }
            set { SetValue(DistanceProperty, value); }
        }

        protected MapViewModel ViewModel
        {
            get { return DataContext as MapViewModel; }
        }

        GeoCoordinateWatcher _coordinatWatcher = new GeoCoordinateWatcher(GeoPositionAccuracy.High);

        private readonly AppBarButton _searchButton = new AppBarButton
        {
            Text = AppResources.Search,
            IsEnabled = false,
            IconUri = new Uri("/Images/ApplicationBar/appbar.feature.search.rest.png", UriKind.Relative)
        };

        private readonly AppBarButton _directionsButton = new AppBarButton
        {
            Text = AppResources.Directions,
            IconUri = new Uri("/Images/ApplicationBar/appbar.map.direction.png", UriKind.Relative)
        };

        private readonly AppBarButton _centerMeButton = new AppBarButton
        {
            Text = AppResources.MyLocation,
            IsEnabled = false,
            IconUri = new Uri("/Images/ApplicationBar/appbar.map.centerme.png", UriKind.Relative)
        };

        private readonly AppBarButton _attachButton = new AppBarButton
        {
            Text = AppResources.Attach,
            IsEnabled = false,
            IconUri = new Uri("/Images/ApplicationBar/appbar.map.checkin.png", UriKind.Relative)
        };

        private readonly AppBarButton _cancelButton = new AppBarButton
        {
            Text = AppResources.Cancel,
            IconUri = new Uri("/Images/ApplicationBar/appbar.cancel.rest.png", UriKind.Relative)
        };

        private readonly AppBarMenuItem _switchModeMenuItem = new AppBarMenuItem
        {
            Text = AppResources.SwitchMode
        };

        public MapView()
        {
            InitializeComponent();

            ContactLocation.Visibility = Visibility.Collapsed;
            CurrentLocation.Visibility = Visibility.Collapsed;

            _searchButton.Click += (sender, args) => ViewModel.SearchLocation(ContactLocation.Location);
            _attachButton.Click += (sender, args) => ViewModel.AttchLocation(ContactLocation.Location);
            _cancelButton.Click += (sender, args) => ViewModel.Cancel();
            _centerMeButton.Click += (sender, args) =>
            {
                var stateService = ViewModel.StateService;
                stateService.GetNotifySettingsAsync(settings =>
                {
                    if (settings.LocationServices)
                    {
                        if (_coordinatWatcher.Position.Location == GeoCoordinate.Unknown)
                        {
                            return;
                        }

                        Map.AnimationLevel = AnimationLevel.Full;
                        Map.SetView(_coordinatWatcher.Position.Location, 16.0);
                    }
                });       
            };

            _switchModeMenuItem.Click += (sender, args) =>
            {
                var tileSource = MapLayer.TileSources.FirstOrDefault() as GoogleMapsTileSource;
                if (tileSource != null)
                {
                    if (tileSource.MapsTileSourceType == GoogleMapsTileSourceType.Street)
                    {
                        tileSource.MapsTileSourceType = GoogleMapsTileSourceType.Satellite;
                    }
                    else if (tileSource.MapsTileSourceType == GoogleMapsTileSourceType.Satellite)
                    {
                        tileSource.MapsTileSourceType = GoogleMapsTileSourceType.Hybrid;
                    }
                    else
                    {
                        tileSource.MapsTileSourceType = GoogleMapsTileSourceType.Street;
                    }

                    MapLayer.TileSources.Clear();
                    MapLayer.TileSources.Add(tileSource);
                }
            };

            _directionsButton.Click += (sender, args) => ViewModel.ShowMapsDirections();

            _coordinatWatcher.StatusChanged += CoordinatWatcher_StatusChanged;
            _coordinatWatcher.PositionChanged += CoordinatWatcher_PositionChanged;

            Unloaded += (sender, args) =>
            {
                //Telegram.Api.Helpers.Execute.ShowDebugMessage(string.Format("MapView.Unloaded watcher.Stop [status={0}, accuracy={1}]", _coordinatWatcher.Status, _coordinatWatcher.Position.Location.HorizontalAccuracy));

                _coordinatWatcher.Stop();
            };

            Loaded += (sender, args) =>
            {
                StartWatching();

                //Telegram.Api.Helpers.Execute.ShowDebugMessage(string.Format("MapView.Loaded [status={0}, accuracy={1}]", _coordinatWatcher.Status, _coordinatWatcher.Position.Location.HorizontalAccuracy));

                if (ViewModel.MessageGeo != null)
                {
                    var mediaGeo = ViewModel.MessageGeo.Media as TLMessageMediaGeo;
                    if (mediaGeo != null)
                    {
                        var geoPoint = mediaGeo.Geo as TLGeoPoint;
                        if (geoPoint == null) return;

                        ContactLocation.Visibility = Visibility.Visible;
                        ContactLocation.Location = new GeoCoordinate
                        {
                            Latitude = geoPoint.Lat.Value,
                            Longitude = geoPoint.Long.Value
                        };
                        Map.AnimationLevel = AnimationLevel.UserInput;
                        Map.ZoomLevel = 16.0;

                        Map.Center = ContactLocation.Location;
                    }

                    BuildLocalizedAppBar(false);
                }
                else
                {
                    BuildLocalizedAppBar(true);
                }
            };
        }

        private static readonly Uri ExternalUri = new Uri(@"app://external/");

        private bool _fromExternalUri;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.NavigationMode == NavigationMode.Back 
                && _fromExternalUri
//                && !_isWatching
                )
            {
                StartWatching();
            }

            base.OnNavigatedTo(e);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            _fromExternalUri = e.Uri == ExternalUri;

            base.OnNavigatedFrom(e);
        }

        private void StartWatching()
        {
            var stateService = ViewModel.StateService;
            stateService.GetNotifySettingsAsync(
                settings =>
                {
                    if (!settings.LocationServices)
                    {
                        settings.AskAllowingLocationServices = true;
                        stateService.SaveNotifySettingsAsync(settings);

                        Deployment.Current.Dispatcher.BeginInvoke(() =>
                        {
                            var result = MessageBox.Show(
                                AppResources.AllowLocationServiceText,
                                AppResources.AllowLocationServicesTitle,
                                MessageBoxButton.OKCancel);

                            if (result == MessageBoxResult.OK)
                            {
                                settings.LocationServices = true;
                                stateService.SaveNotifySettingsAsync(settings);

                                ContinueStartWatching();
                            }
                            else
                            {
                                //_isWatching = false;
                            }
                        });
                    }
                    else
                    {
                        ContinueStartWatching();
                    }
                });
        }

        private void OpenLocationSettings()
        {
#if WP8
            Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings-location:"));
#endif
        }

        private void ContinueStartWatching()
        {
            if (_coordinatWatcher.Permission != GeoPositionPermission.Granted)
            {
                _coordinatWatcher.Stop();
                _coordinatWatcher = new GeoCoordinateWatcher(GeoPositionAccuracy.High);
                _coordinatWatcher.PositionChanged += CoordinatWatcher_PositionChanged;
                _coordinatWatcher.StatusChanged += CoordinatWatcher_StatusChanged;
            }

            if (_coordinatWatcher.Permission != GeoPositionPermission.Granted)
            {
                UpdateAttaching(false);
            }
            else
            {
                UpdateAttaching(true);
                SetContactLocationString();
            }

            _coordinatWatcher.Start(true);
        }

        private void UpdateAttaching(bool isEnabled)
        {
            AttachButton.Visibility = isEnabled? Visibility.Visible : Visibility.Collapsed;
            LocationSettingsPanel.Visibility = isEnabled? Visibility.Collapsed : Visibility.Visible;
            _centerMeButton.IsEnabled = isEnabled;
            _attachButton.IsEnabled = isEnabled;
            _searchButton.IsEnabled = isEnabled;
        }

        //private bool _isWatching;

        private void CoordinatWatcher_PositionChanged(object sender, GeoPositionChangedEventArgs<GeoCoordinate> e)
        {
            //Telegram.Api.Helpers.Execute.ShowDebugMessage(string.Format("MapView.PositionChanged [status={0}, accuracy={1}]", _coordinatWatcher.Status, _coordinatWatcher.Position.Location.HorizontalAccuracy));

            var distance = CurrentLocation.Location.GetDistanceTo(ContactLocation.Location);

            Distance = distance;
            CurrentLocation.Location = e.Position.Location;

            if (ViewModel.MessageGeo == null && distance < Constants.GlueGeoPointsDistance)
            {
                ContactLocation.Location = e.Position.Location;
            }

            SetContactLocationString();
        }

        private void CoordinatWatcher_StatusChanged(object o, GeoPositionStatusChangedEventArgs e)
        {
            //Telegram.Api.Helpers.Execute.ShowDebugMessage(string.Format("MapView.StatusChanged [status={0}, accuracy={1}]", _coordinatWatcher.Status, _coordinatWatcher.Position.Location.HorizontalAccuracy));


            if (e.Status == GeoPositionStatus.Ready || _coordinatWatcher.Position.Location != GeoCoordinate.Unknown)
            {
                Map.AnimationLevel = AnimationLevel.UserInput;
                Map.ZoomLevel = 16.0;

                CurrentLocation.Visibility = Visibility.Visible;
                CurrentLocation.Location = _coordinatWatcher.Position.Location;
                if (!CurrentLocation.Location.IsUnknown)
                {
                    UpdateAttaching(true);
                }

                Distance = CurrentLocation.Location.GetDistanceTo(ContactLocation.Location);

                if (ViewModel.MessageGeo == null && !_contactLocationChoosen)
                {
                    ContactLocation.Visibility = Visibility.Visible;
                    ContactLocation.Location = _coordinatWatcher.Position.Location;
                    Map.Center = _coordinatWatcher.Position.Location;

                    SetContactLocationString();
                }

                if (ViewModel.MessageGeo == null)
                {
                    ViewModel.GetVenues(_coordinatWatcher.Position.Location);
                }
            }
        }


#if DEBUG
        //~MapView()
        //{
        //    TLUtils.WritePerformance("++MapView dstr");
        //}
#endif

        private void BuildLocalizedAppBar(bool attaching)
        {
            if (ApplicationBar == null)
            {
                ApplicationBar = new ApplicationBar();
                var foregroundColor = Colors.White;
                foregroundColor.A = 254;
                ApplicationBar.ForegroundColor = foregroundColor;

                var backgroundColor = new Color();
                backgroundColor.A = 255;
                backgroundColor.R = 31;
                backgroundColor.G = 31;
                backgroundColor.B = 31;
                ApplicationBar.BackgroundColor = backgroundColor;
                if (attaching)
                {
                    ApplicationBar.Buttons.Add(_searchButton);               
                }
                else
                {
#if WP8
                    ApplicationBar.Buttons.Add(_directionsButton);
#endif
                }
                //var color = new Color { A = 217 };
                //ApplicationBar.BackgroundColor = color;
                ApplicationBar.Buttons.Add(_centerMeButton);
                ApplicationBar.MenuItems.Add(_switchModeMenuItem);
            }
        }

        private bool _contactLocationChoosen;

        private void GestureListener_Hold(object sender, GestureEventArgs e)
        {
            if (ViewModel.MessageGeo != null) return;

            var point = new Point(e.GetPosition(Map).X, e.GetPosition(Map).Y);
            var location = Map.ViewportPointToLocation(point);
            _contactLocationChoosen = true;
            ContactLocation.Visibility = Visibility.Visible;
            ContactLocation.Location = location;

            var distance = CurrentLocation.Location.GetDistanceTo(ContactLocation.Location);
            if (distance < Constants.GlueGeoPointsDistance)
            {
                ContactLocation.Location = CurrentLocation.Location;
            }

            UpdateAttaching(true);
            SetContactLocationString();
        }

        public void SetContactLocationString()
        {
            if (ContactLocation.Location.IsUnknown
                || (ContactLocation.Location.Latitude == 0.0 && ContactLocation.Location.Longitude == 0.0))
            {
                ContactLocationString = AppResources.Loading;
                return;
            }

            ContactLocationString = string.Format("({0}, {1})", ContactLocation.Location.Latitude.ToString("###.#######", new CultureInfo("en-us")), ContactLocation.Location.Longitude.ToString("###.#######", new CultureInfo("en-us")));            
        }

        private void UIElement_OnTap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            ViewModel.AttchLocation(ContactLocation.Location);
        }

        private void LocationSettings_OnClick(object sender, RoutedEventArgs e)
        {
            OpenLocationSettings();
        }
    }
}