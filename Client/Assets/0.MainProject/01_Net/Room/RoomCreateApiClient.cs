using System;
using System.Threading;
using System.Threading.Tasks;

namespace NetClient.Room
{
    public sealed class RoomCreateApiClient
    {
        readonly ApiClient _api;

        public RoomCreateApiClient(ApiClient api)
        {
            _api = api;
        }

        public Task<ApiResult<CreateRoomResponse>> CreateAsync(CreateRoomRequest req, CancellationToken ct = default)
        {
            // ct is ignored by ApiClient currently, can be extended later
            return _api.PostJsonAsync<CreateRoomResponse>("/rooms", req, attachAuth: true);
        }

        [Serializable]
        public sealed class CreateRoomRequest
        {
            public string roomId;     // Optional, used as title if title is empty
            public string mapId;
            public int maxPlayers;
            public string title;      
        }

        [Serializable]
        public sealed class CreateRoomResponse
        {
            public string roomId;
        }
    }
}
