using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine; // For Debug/Logging if needed

namespace NetClient.Room
{
    /// <summary>
    /// HTTP API Client for Room List.
    /// Uses <see cref="ApiClient"/> for transport (Auth, Retry, Serialization).
    /// </summary>
    public sealed class RoomListApiClient
    {
        readonly ApiClient _api;
        string _lastEtag;

        public event Action<List<RoomSummaryDto>, string> OnRoomsUpdated;
        public event Action<string> OnWarn;

        public RoomListApiClient(ApiClient api)
        {
            _api = api;
        }

        public async Task RefreshAsync(string cursor = "")
        {
            var etag = string.IsNullOrEmpty(cursor) ? _lastEtag : null;
            var path = $"/rooms?limit=20&cursor={cursor}";

            // ApiClient handles Auth & Refresh automatically
            var result = await _api.GetJsonAsync<RoomListWrapper>(path, attachAuth: true, etag: etag);

            if (result.StatusCode == 304)
                return;

            if (!result.Ok)
            {
                OnWarn?.Invoke($"RoomList Error: {result.StatusCode} {result.Error}");
                return;
            }

            // Save ETag only for first page
            if (string.IsNullOrEmpty(cursor))
                _lastEtag = result.ETag;

            if (result.Data != null)
            {
                OnRoomsUpdated?.Invoke(result.Data.rooms ?? new List<RoomSummaryDto>(), result.Data.nextCursor);
            }
        }

        // DTO for JSON Deserialization (Newtonsoft compatible)
        public class RoomListWrapper
        {
            public List<RoomSummaryDto> rooms { get; set; }
            public string nextCursor { get; set; }
        }
    }

    [Serializable]
    public class RoomSummaryDto
    {
        public string roomId;
        public string title;
        public string mapId;
        public int maxPlayers;
        public int memberCount;
        public string status;
        public string ownerUid;
    }
}
