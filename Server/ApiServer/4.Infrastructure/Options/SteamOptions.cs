namespace ApiServer.Infrastructure.Options;

public sealed class SteamOptions
{
    public bool Enabled { get; set; }
    public string AppId { get; set; } = "";
    public string PublisherKey { get; set; } = "";
    public string WebApiBaseUrl { get; set; } = "https://api.steampowered.com";
}
