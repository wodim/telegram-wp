using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Device.Location;
using System.Globalization;
using System.IO;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Windows.Navigation;
using Caliburn.Micro;
using Microsoft.Phone.Tasks;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using TelegramClient.Resources;
using TelegramClient.Services;
using TelegramClient.ViewModels.Contacts;
using TelegramClient.ViewModels.Media.Foursquire;
using TelegramClient.ViewModels.Search;

namespace TelegramClient.ViewModels.Media
{
    public class MapViewModel : Screen
    {
        //private bool _isPermissionGranted;

        //public bool IsPermissionGranted
        //{
        //    get { return _isPermissionGranted; }
        //    set
        //    {
        //        if (_isPermissionGranted != value)
        //        {
        //            _isPermissionGranted = value;
        //            NotifyOfPropertyChange(() => IsPermissionGranted);
        //        }
        //    }
        //}

        private string _status;

        public string Status
        {
            get { return _status; }
            set
            {
                if (_status != value)
                {
                    _status = value;
                    NotifyOfPropertyChange(() => Status);
                }
            }
        }

        private bool _isWorking;

        public bool IsWorking
        {
            get { return _isWorking; }
            set
            {
                if (_isWorking != value)
                {
                    _isWorking = value;
                    NotifyOfPropertyChange(() => IsWorking);
                }
            }
        }

        private TLMessage _messageGeo;

        public TLMessage MessageGeo
        {
            get { return _messageGeo; }
            set
            {
                if (_messageGeo != value)
                {
                    _messageGeo = value;
                    NotifyOfPropertyChange(() => MessageGeo);
                }
            }
        }

        public string PoweredBy
        {
            get { return "Foursquare"; }
        }

        private readonly INavigationService _navigationService;

        private readonly ICacheService _cacheService;

        public IStateService StateService { get; protected set; }

        public MapViewModel(ICacheService cacheService, IStateService stateService, INavigationService navigationService)
        {
            Venues = new ObservableCollection<TLMessageMediaVenue>();

            StateService = stateService;
            _cacheService = cacheService;
            _navigationService = navigationService;
        }

        protected override void OnActivate()
        {
            if (StateService.RemoveBackEntry)
            {
                StateService.RemoveBackEntry = false;
                _navigationService.RemoveBackEntry();
            }

            if (StateService.MediaMessage != null)
            {
                MessageGeo = StateService.MediaMessage;
                StateService.MediaMessage = null;               
            }

            if (StateService.DecryptedMediaMessage != null)
            {
                var geoPoint = StateService.DecryptedMediaMessage.Media as TLDecryptedMessageMediaGeoPoint;
                if (geoPoint == null) return;

                MessageGeo = new TLMessage17
                {
                    Media = new TLMessageMediaGeo
                    {
                        Geo = new TLGeoPoint
                        {
                            Lat = geoPoint.Lat,
                            Long = geoPoint.Long
                        }
                    },
                    FromId = StateService.DecryptedMediaMessage.FromId,
                };
                StateService.DecryptedMediaMessage = null;  
            }

            base.OnActivate();
        }

        public void Cancel()
        {
            _navigationService.GoBack();
        }

        public void AttachVenue(TLMessageMediaVenue venue)
        {
            if (venue == null) return;

            StateService.Venue = venue;
            _navigationService.GoBack();
        }

        public void AttchLocation(GeoCoordinate location)
        {
            if (location == null) return;
            if (location.Latitude == 0.0 && location.Longitude == 0.0) return;

            StateService.GeoPoint = new TLGeoPoint{ Lat = new TLDouble(location.Latitude), Long = new TLDouble(location.Longitude) };
            _navigationService.GoBack();
        }

        public void ShowMapsDirections()
        {
#if WP8
            if (MessageGeo == null || MessageGeo.From == null) return;

            var user = MessageGeo.From as TLUserBase;
            if (user == null) return;

            var label = user.FullName;
            if (string.IsNullOrEmpty(label)) return;

            var mediaGeo = MessageGeo.Media as TLMessageMediaGeo;
            if (mediaGeo == null) return;

            var geoPoint = mediaGeo.Geo as TLGeoPoint;
            if (geoPoint == null) return;

            var task = new MapsDirectionsTask
            {
                End =
                    new LabeledMapLocation(user.FullName,
                        new GeoCoordinate(geoPoint.Lat.Value, geoPoint.Long.Value))
            };

            task.Show();
#endif
        }

        public ObservableCollection<TLMessageMediaVenue> Venues { get; set; }

        public void GetVenues(GeoCoordinate location)
        {
            var client = new WebClient();
            client.DownloadStringCompleted += (sender, args) =>
            {
                if (args.Error != null)
                {
                    Execute.BeginOnUIThread(() =>
                    {
                        Venues.Clear();
                        IsWorking = false;
                        Status = AppResources.NoResults;
                    });

                    return;
                }

                var venues = new List<TLMessageMediaVenue>();
                var serializer = new DataContractJsonSerializer(typeof(RootObject));
                var rootObject = (RootObject)serializer.ReadObject(new MemoryStream(Encoding.UTF8.GetBytes(args.Result ?? string.Empty)));
                if (rootObject != null
                    && rootObject.response != null
                    && rootObject.response.venues != null)
                {
                    foreach (var venue in rootObject.response.venues)
                    {
                        if (venue.location == null) continue;

                        var mediaVenue = new TLMessageMediaVenue
                        {
                            VenueId = new TLString(venue.id),
                            Title = new TLString(venue.name),
                            Address = new TLString(venue.location.address ?? venue.location.city ?? venue.location.country),
                            Provider = new TLString("foursquare"),
                            Geo = new TLGeoPoint { Lat = new TLDouble(venue.location.lat), Long = new TLDouble(venue.location.lng) }
                        };

                        venues.Add(mediaVenue);
                    }
                }

                Execute.BeginOnUIThread(() =>
                {
                    Venues.Clear();
                    foreach (var venue in venues)
                    {
                        Venues.Add(venue);
                    }

                    IsWorking = false;
                    Status = Venues.Count > 0 ? AppResources.NearbyPlaces : AppResources.NoResults;
                });
            };

            IsWorking = true;
            Status = Venues.Count > 0 ? Status : AppResources.Loading;
            client.DownloadStringAsync(Foursquire.Utils.GetSearchVenueUrl(location));
        }

        public void OpenContactDetails()
        {
            if (MessageGeo == null || MessageGeo.From == null) return;
            if (MessageGeo.Media is TLMessageMediaVenue) return;

            StateService.CurrentContact = MessageGeo.From as TLUserBase;
            _navigationService.UriFor<ContactViewModel>().Navigate();
        }

        public void SearchLocation(GeoCoordinate location)
        {
            StateService.GeoCoordinate = location;
            _navigationService.UriFor<SearchVenuesViewModel>().Navigate();
        }
    }

    namespace Foursquire
    {
        public class Utils
        {
            public static Uri GetSearchVenueUrl(GeoCoordinate location, string query = null)
            {
                var urlBuilder = new StringBuilder(Constants.FoursquireSearchEndpointUrl);
                if (!string.IsNullOrEmpty(query))
                {
                    urlBuilder.Append(string.Format("{0}={1}&", "query", HttpUtility.UrlEncode(query)));
                }
                urlBuilder.Append(string.Format("{0}={1}&", "v", Constants.FoursquareVersion));
                urlBuilder.Append(string.Format("{0}={1}&", "locale", Constants.FoursquareLocale));
                urlBuilder.Append(string.Format("{0}={1}&", "limit", Constants.FoursquareVenuesCountLimit));
                urlBuilder.Append(string.Format("{0}={1}&", "client_id", Constants.FoursquareClientId));
                urlBuilder.Append(string.Format("{0}={1}&", "client_secret", Constants.FoursquareClientSecret));
                urlBuilder.Append(string.Format("{0}={1},{2}&", "ll", location.Latitude.ToString(new CultureInfo("en-US")), location.Longitude.ToString(new CultureInfo("en-US"))));

                return new Uri(urlBuilder.ToString(), UriKind.Absolute);
            }
        }

        public class Meta
        {
            public int code { get; set; }
        }

        public class Contact
        {
            public string phone { get; set; }
            public string formattedPhone { get; set; }
            public string twitter { get; set; }
            public string facebook { get; set; }
            public string facebookUsername { get; set; }
            public string facebookName { get; set; }
        }

        public class Location
        {
            public string address { get; set; }
            public double lat { get; set; }
            public double lng { get; set; }
            public int distance { get; set; }
            public string cc { get; set; }
            public string city { get; set; }
            public string country { get; set; }
            public List<string> formattedAddress { get; set; }
            public string crossStreet { get; set; }
            public string postalCode { get; set; }
            public string neighborhood { get; set; }
        }

        public class Icon
        {
            public string prefix { get; set; }
            public string suffix { get; set; }
        }

        public class Category
        {
            public string id { get; set; }
            public string name { get; set; }
            public string pluralName { get; set; }
            public string shortName { get; set; }
            public Icon icon { get; set; }
            public bool primary { get; set; }
        }

        public class Stats
        {
            public int checkinsCount { get; set; }
            public int usersCount { get; set; }
            public int tipCount { get; set; }
        }

        public class Specials
        {
            public int count { get; set; }
            public List<object> items { get; set; }
        }

        public class HereNow
        {
            public int count { get; set; }
            public string summary { get; set; }
            public List<object> groups { get; set; }
        }

        public class VenuePage
        {
            public string id { get; set; }
        }

        public class Menu
        {
            public string type { get; set; }
            public string label { get; set; }
            public string anchor { get; set; }
            public string url { get; set; }
            public string mobileUrl { get; set; }
            public string externalUrl { get; set; }
        }

        public class Venue
        {
            public string id { get; set; }
            public string name { get; set; }
            public Contact contact { get; set; }
            public Location location { get; set; }
            public List<Category> categories { get; set; }
            public bool verified { get; set; }
            public Stats stats { get; set; }
            public Specials specials { get; set; }
            public HereNow hereNow { get; set; }
            public string referralId { get; set; }
            public string url { get; set; }
            public VenuePage venuePage { get; set; }
            public string storeId { get; set; }
            public Menu menu { get; set; }
        }

        public class Response
        {
            public List<Venue> venues { get; set; }
            public bool confident { get; set; }
        }

        public class RootObject
        {
            public Meta meta { get; set; }
            public Response response { get; set; }
        }
    }
}
