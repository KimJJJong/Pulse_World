using System.Threading.Tasks;

public sealed class PlayerStateApi
{
    readonly ApiClient _api;

    public PlayerStateApi(ApiClient api) => _api = api;

    public Task<ApiResult<PlayerStateDtos.PlayerStateResponse>> GetPlayerStateAsync(string uid)
        => _api.GetJsonAsync<PlayerStateDtos.PlayerStateResponse>(
            $"/api/game/player-state/{uid}",
            attachAuth: true);

    public Task<ApiResult<PlayerStateDtos.PlayerStateResponse>> SetAppearanceAsync(string uid, int appearanceId)
        => _api.PostJsonAsync<PlayerStateDtos.PlayerStateResponse>(
            $"/api/game/player-state/{uid}/appearance",
            new PlayerStateDtos.SetAppearanceRequest(appearanceId),
            attachAuth: true);
}
