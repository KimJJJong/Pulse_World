using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ApiServer.Infrastructure.Options;
using ApiServer.Shared.Errors;
using Microsoft.Extensions.Options;

namespace ApiServer.Infrastructure.Steam;

public sealed class SteamTicketVerifier
{
    private readonly HttpClient _httpClient;
    private readonly SteamOptions _options;
    private readonly ILogger<SteamTicketVerifier> _logger;

    public SteamTicketVerifier(
        HttpClient httpClient,
        IOptions<SteamOptions> options,
        ILogger<SteamTicketVerifier> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> ValidateUserTicketAsync(
        string steamId64,
        string ticketHex,
        string identity,
        CancellationToken ct)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.AppId) || string.IsNullOrWhiteSpace(_options.PublisherKey))
            throw new ApiException(503, ErrorCodes.SteamAuthUnavailable, "Steam authentication is not configured.");

        string baseUrl = string.IsNullOrWhiteSpace(_options.WebApiBaseUrl)
            ? "https://partner.steam-api.com"
            : _options.WebApiBaseUrl.TrimEnd('/');

        string requestUrl =
            $"{baseUrl}/ISteamUserAuth/AuthenticateUserTicket/v1/" +
            $"?key={Uri.EscapeDataString(_options.PublisherKey)}" +
            $"&appid={Uri.EscapeDataString(_options.AppId)}" +
            $"&ticket={Uri.EscapeDataString(ticketHex)}" +
            $"&identity={Uri.EscapeDataString(identity)}";

        using var response = await _httpClient.GetAsync(requestUrl, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Steam auth HTTP failure. status={Status} body={Body}", (int)response.StatusCode, errorBody);
            throw new ApiException(401, ErrorCodes.SteamAuthFailed, "Steam authentication request failed.");
        }

        var payload = await response.Content.ReadFromJsonAsync<AuthenticateUserTicketEnvelope>(cancellationToken: ct);
        var result = payload?.Response?.Params?.Result ?? "";
        var validatedSteamId = payload?.Response?.Params?.SteamId ?? "";

        if (!string.IsNullOrWhiteSpace(result) && !string.Equals(result, "OK", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Steam auth failed. result={Result} steamid={SteamId}", result, steamId64);
            throw new ApiException(401, ErrorCodes.SteamAuthFailed, $"Steam authentication failed: {result}");
        }

        if (string.IsNullOrWhiteSpace(validatedSteamId))
            throw new ApiException(401, ErrorCodes.SteamAuthFailed, "Steam authentication returned no steamid.");

        if (!string.Equals(validatedSteamId, steamId64, StringComparison.Ordinal))
        {
            _logger.LogWarning("Steam auth mismatch. requested={Requested} validated={Validated}", steamId64, validatedSteamId);
            throw new ApiException(401, ErrorCodes.SteamAuthFailed, "Steam authentication steamid mismatch.");
        }

        return validatedSteamId;
    }

    private sealed class AuthenticateUserTicketEnvelope
    {
        [JsonPropertyName("response")]
        public AuthenticateUserTicketResponse? Response { get; set; }
    }

    private sealed class AuthenticateUserTicketResponse
    {
        [JsonPropertyName("params")]
        public AuthenticateUserTicketParams? Params { get; set; }
    }

    private sealed class AuthenticateUserTicketParams
    {
        [JsonPropertyName("result")]
        public string? Result { get; set; }

        [JsonPropertyName("steamid")]
        public string? SteamId { get; set; }

        [JsonPropertyName("ownersteamid")]
        public string? OwnerSteamId { get; set; }
    }
}
