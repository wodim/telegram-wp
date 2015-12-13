using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Device.Location;
using System.IO;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Text;
using Caliburn.Micro;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using TelegramClient.Helpers;
using TelegramClient.Resources;
using TelegramClient.Services;
using TelegramClient.ViewModels.Media.Foursquire;
using Execute = Telegram.Api.Helpers.Execute;

namespace TelegramClient.ViewModels.Search
{
    public class SearchVenuesViewModel : ItemsViewModelBase<TLObject>
    {
        public bool IsNotWorking { get { return !IsWorking; } }

        private string _text;

        public string Text
        {
            get { return _text; }
            set { SetField(ref _text, value, () => Text); }
        }

        private readonly GeoCoordinate _location;

        public SearchVenuesViewModel(ICacheService cacheService, ICommonErrorHandler errorHandler, IStateService stateService, INavigationService navigationService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator) 
            : base(cacheService, errorHandler, stateService, navigationService, mtProtoService, eventAggregator)
        {
            EventAggregator.Subscribe(this);

            Items = new ObservableCollection<TLObject>();
            Status = AppResources.NoResults;

            if (StateService.GeoCoordinate != null)
            {
                _location = StateService.GeoCoordinate;
                StateService.GeoCoordinate = null;
            }

            PropertyChanged += (sender, args) =>
            {
                if (Property.NameEquals(args.PropertyName, () => Text))
                {
                    if (!string.IsNullOrEmpty(Text))
                    {
                        Search(Text);
                    }
                    else
                    {
                        Items.Clear();
                        Status = AppResources.NoResults;
                    }
                }
            };
        }

        public void AttachVenue(TLMessageMediaVenue venue)
        {
            if (venue == null) return;

            StateService.Venue = venue;
            NavigationService.RemoveBackEntry();
            NavigationService.GoBack();
        }

        private readonly Dictionary<string, List<TLMessageMediaVenue>> _cache = new Dictionary<string, List<TLMessageMediaVenue>>();

        public void Search(string inputText)
        {
            if (inputText == null)
            {
                return;
            }

            var text = inputText.Trim();

            Execute.BeginOnUIThread(TimeSpan.FromMilliseconds(300), () =>
            {
                if (!string.Equals(Text, text))
                {
                    return;
                }

                List<TLMessageMediaVenue> cachedResult;
                if (_cache.TryGetValue(text, out cachedResult))
                {
                    Items.Clear();
                    foreach (var venue in cachedResult)
                    {
                        Items.Add(venue);
                    }

                    IsWorking = false;
                    Status = Items.Count > 0 ? string.Empty : AppResources.NoResults;

                    return;
                }

                Execute.BeginOnThreadPool(() =>
                {
                    var client = new WebClient();
                    client.DownloadStringCompleted += (sender, args) =>
                    {
                        if (args.Error != null)
                        {
                            BeginOnUIThread(() =>
                            {
                                Items.Clear();
                                IsWorking = false;
                                Status = AppResources.NoResults;
                            });

                            return;
                        }

                        var venues = new List<TLMessageMediaVenue>();
                        var serializer = new DataContractJsonSerializer(typeof(RootObject));
                        var rootObject = (RootObject)serializer.ReadObject(new MemoryStream(Encoding.UTF8.GetBytes(args.Result ?? "")));
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

                        BeginOnUIThread(() =>
                        {
                            _cache[text] = venues;

                            if (!string.Equals(Text, text))
                            {
                                return;
                            }

                            Items.Clear();
                            foreach (var venue in venues)
                            {
                                Items.Add(venue);
                            }

                            IsWorking = false;
                            Status = Items.Count > 0 ? string.Empty : AppResources.NoResults;
                        });
                    };

                    IsWorking = true;
                    Status = Items.Count > 0 ? string.Empty : AppResources.Loading;
                    client.DownloadStringAsync(Media.Foursquire.Utils.GetSearchVenueUrl(_location, text));
                });
            });
        }
    }
}
