using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace GameServer.Infrastructure.Api;

public class ApiServerOptions
{
    public string BaseUrl { get; set; } = "http://localhost:5000";
    public string SystemApiKey { get; set; } = "";
}

public interface IApiServerClient
{
    Task<T?> GetAsync<T>(string endpoint);
    Task<bool> PostAsync<T>(string endpoint, T payload);
    Task<bool> DeleteAsync(string endpoint);
}

public class ApiServerClient : IApiServerClient
{
    private readonly HttpClient _client;
    private readonly ILogger<ApiServerClient> _logger;
    private readonly ApiServerOptions _options;

    public ApiServerClient(HttpClient client, IOptions<ApiServerOptions> options, ILogger<ApiServerClient> logger)
    {
        _client = client;
        _logger = logger;
        _options = options.Value;

        _client.BaseAddress = new Uri(_options.BaseUrl);
        _client.DefaultRequestHeaders.Add("X-Server-Secret", _options.SystemApiKey);
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<T?> GetAsync<T>(string endpoint)
    {
        try
        {
            var response = await _client.GetAsync(endpoint);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<T>(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to GET {Endpoint}", endpoint);
            return default;
        }
    }

    public async Task<bool> PostAsync<T>(string endpoint, T payload)
    {
        try
        {
            var json = JsonConvert.SerializeObject(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _client.PostAsync(endpoint, content);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to POST {Endpoint}. Status: {Status}, Error: {Error}", endpoint, response.StatusCode, error);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to POST {Endpoint}", endpoint);
            return false;
        }
    }


    public async Task<bool> DeleteAsync(string endpoint)
    {
        try
        {
            var response = await _client.DeleteAsync(endpoint);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to DELETE {Endpoint}. Status: {Status}, Error: {Error}", endpoint, response.StatusCode, error);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to DELETE {Endpoint}", endpoint);
            return false;
        }
    }
}
