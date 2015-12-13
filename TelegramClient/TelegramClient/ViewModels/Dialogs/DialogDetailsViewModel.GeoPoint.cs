using System;
using Telegram.Api.TL;

namespace TelegramClient.ViewModels.Dialogs
{
    public partial class DialogDetailsViewModel
    {
        private void SendVenue(TLMessageMediaVenue venue)
        {
            var message = GetMessage(TLString.Empty, venue);

            BeginOnUIThread(() =>
            {
                var previousMessage = InsertSendingMessage(message);
                IsEmptyDialog = Items.Count == 0 && LazyItems.Count == 0;

                BeginOnThreadPool(() =>
                    CacheService.SyncSendingMessage(
                        message, previousMessage,
                        TLUtils.InputPeerToPeer(Peer, StateService.CurrentUserId),
                        SendVenueInternal));
            });
        }

        private void SendVenueInternal(TLMessageBase message)
        {
            var message25 = message as TLMessage25;
            if (message25 == null) return;

            var mediaVenue = message25.Media as TLMessageMediaVenue;
            if (mediaVenue == null) return;

            var geoPoint = mediaVenue.Geo as TLGeoPoint;
            if (geoPoint == null) return;

            var inputMediaVenue = new TLInputMediaVenue
            {
                Title = mediaVenue.Title,
                Address = mediaVenue.Address,
                Provider = mediaVenue.Provider,
                VenueId = mediaVenue.VenueId,
                GeoPoint = new TLInputGeoPoint { Lat = ((TLGeoPoint)mediaVenue.Geo).Lat, Long = ((TLGeoPoint)mediaVenue.Geo).Long }
            };

            message25.InputMedia = inputMediaVenue;

            ShellViewModel.SendMediaInternal(message25, MTProtoService, StateService, CacheService);
        }

        private void SendGeoPoint(TLGeoPoint geoPoint)
        {
            var media = new TLMessageMediaGeo {Geo = geoPoint};

            var message = GetMessage(TLString.Empty, media);

            BeginOnUIThread(() =>
            {
                var previousMessage = InsertSendingMessage(message);
                IsEmptyDialog = Items.Count == 0 && LazyItems.Count == 0;

                BeginOnThreadPool(() =>
                    CacheService.SyncSendingMessage(
                        message, previousMessage,
                        TLUtils.InputPeerToPeer(Peer, StateService.CurrentUserId),
                        SendGeoPointInternal));
            });
        }

        private void SendGeoPointInternal(TLMessageBase message)
        {
            var message25 = message as TLMessage25;
            if (message25 == null) return;

            var mediaGeo = message25.Media as TLMessageMediaGeo;
            if (mediaGeo == null) return;

            var geoPoint = mediaGeo.Geo as TLGeoPoint;
            if (geoPoint == null) return;

            var inputMediaGeoPoint = new TLInputMediaGeoPoint
            {
                GeoPoint = new TLInputGeoPoint { Lat = geoPoint.Lat, Long = geoPoint.Long }
            };

            message25.InputMedia = inputMediaGeoPoint;

            ShellViewModel.SendMediaInternal(message25, MTProtoService, StateService, CacheService);
        }
    }
}
