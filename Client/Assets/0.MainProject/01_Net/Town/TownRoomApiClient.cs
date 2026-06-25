using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace NetClient.Town
{
    public sealed class TownRoomApiClient
    {
        private readonly ApiClient _api;

        public TownRoomApiClient(ApiClient api)
        {
            _api = api;
        }

        public Task<ApiResult<TownRoomListResponse>> ListAsync(string mapId, string cursor = "")
        {
            var encodedMap = UnityWebRequest.EscapeURL(mapId ?? "");
            var encodedCursor = UnityWebRequest.EscapeURL(cursor ?? "");
            return _api.GetJsonAsync<TownRoomListResponse>(
                $"/townRooms?mapId={encodedMap}&limit=20&cursor={encodedCursor}",
                attachAuth: true);
        }

        public Task<ApiResult<TownRoomSummaryDto>> GetAsync(string roomId)
        {
            return _api.GetJsonAsync<TownRoomSummaryDto>(
                $"/townRooms/{UnityWebRequest.EscapeURL(roomId ?? "")}",
                attachAuth: true);
        }

        public Task<ApiResult<CreateTownRoomResponse>> CreateAsync(
            string title,
            string mapId,
            int maxPlayers,
            string steamId64,
            string clientVersion,
            bool isPublic = true)
        {
            return _api.PostJsonAsync<CreateTownRoomResponse>(
                "/townRooms",
                new CreateTownRoomRequest
                {
                    title = title ?? "",
                    mapId = mapId ?? "",
                    maxPlayers = maxPlayers,
                    steamId64 = steamId64 ?? "",
                    clientVersion = clientVersion ?? "",
                    isPublic = isPublic
                },
                attachAuth: true);
        }

        public Task<ApiResult<JoinTownRoomResponse>> JoinAsync(string roomId, string steamId64, string clientVersion)
        {
            return _api.PostJsonAsync<JoinTownRoomResponse>(
                $"/townRooms/{UnityWebRequest.EscapeURL(roomId ?? "")}/join",
                new JoinTownRoomRequest
                {
                    steamId64 = steamId64 ?? "",
                    clientVersion = clientVersion ?? ""
                },
                attachAuth: true);
        }

        public Task<ApiResult<object>> LeaveAsync(string roomId)
        {
            return _api.PostJsonAsync<object>(
                $"/townRooms/{UnityWebRequest.EscapeURL(roomId ?? "")}/leave",
                new object(),
                attachAuth: true);
        }

        public Task<ApiResult<object>> BindSteamLobbyAsync(string roomId, string steamLobbyId)
        {
            return _api.PostJsonAsync<object>(
                $"/townRooms/{UnityWebRequest.EscapeURL(roomId ?? "")}/steam-lobby",
                new BindSteamLobbyRequest { steamLobbyId = steamLobbyId ?? "" },
                attachAuth: true);
        }

        public Task<ApiResult<TownGameRoomResponse>> SetActiveGameRoomAsync(
            string roomId,
            string gameRoomId,
            string mapId,
            string title)
        {
            return _api.PostJsonAsync<TownGameRoomResponse>(
                $"/townRooms/{UnityWebRequest.EscapeURL(roomId ?? "")}/game-room",
                new SetTownGameRoomRequest
                {
                    gameRoomId = gameRoomId ?? "",
                    mapId = mapId ?? "",
                    title = title ?? ""
                },
                attachAuth: true);
        }

        public Task<ApiResult<TownGameRoomResponse>> ClearActiveGameRoomAsync(string roomId)
        {
            return _api.PostJsonAsync<TownGameRoomResponse>(
                $"/townRooms/{UnityWebRequest.EscapeURL(roomId ?? "")}/game-room/clear",
                new object(),
                attachAuth: true);
        }

        [Serializable]
        public sealed class CreateTownRoomRequest
        {
            public string title;
            public string mapId;
            public int maxPlayers;
            public string steamId64;
            public string clientVersion;
            public bool isPublic = true;
        }

        [Serializable]
        public sealed class JoinTownRoomRequest
        {
            public string steamId64;
            public string clientVersion;
        }

        [Serializable]
        public sealed class BindSteamLobbyRequest
        {
            public string steamLobbyId;
        }

        [Serializable]
        public sealed class SetTownGameRoomRequest
        {
            public string gameRoomId;
            public string mapId;
            public string title;
        }

        [Serializable]
        public sealed class CreateTownRoomResponse
        {
            public string roomId;
            public TownRoomSummaryDto room;
        }

        [Serializable]
        public sealed class JoinTownRoomResponse
        {
            public TownRoomSummaryDto room;
        }

        [Serializable]
        public sealed class TownGameRoomResponse
        {
            public TownRoomSummaryDto room;
        }

        [Serializable]
        public sealed class TownRoomListResponse
        {
            public List<TownRoomSummaryDto> rooms;
            public string nextCursor;
        }

        [Serializable]
        public sealed class TownRoomSummaryDto
        {
            public string roomId;
            public string title;
            public string mapId;
            public int maxPlayers;
            public int memberCount;
            public string status;
            public string ownerUid;
            public string hostUid;
            public bool isPublic = true;
            public string steamLobbyId;
            public string activeGameRoomId;
            public string activeGameMapId;
            public string activeGameTitle;
            public string activeGameHostUid;
            public long activeGameCreatedAtMs;
            public long createdAtMs;
            public List<TownRoomParticipantDto> participants;
        }

        [Serializable]
        public sealed class TownRoomParticipantDto
        {
            public string uid;
            public string name;
            public string steamId64;
            public string clientVersion;
            public long joinedAtMs;
        }
    }
}
