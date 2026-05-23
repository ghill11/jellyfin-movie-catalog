using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MovieCatalog;

/// <summary>
/// Pushes the JSON payload to the configured GitHub repo via the Contents
/// API. GET-current-SHA then PUT, retry once on 409 (SHA stale), hard-fail
/// on 401/403/422. Reads configuration via Plugin.Instance on every push
/// so PAT rotation takes effect immediately.
/// </summary>
public class GitHubPusher
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger _logger;

    public GitHubPusher(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory)
    {
        _httpClientFactory = httpClientFactory;
        _logger = loggerFactory.CreateLogger("JellyfinMovieCatalog");
    }

    public async Task PushAsync(byte[] payload, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration
            ?? throw new InvalidOperationException("Plugin.Instance.Configuration not available");

        ValidateConfig(config);

        var client = _httpClientFactory.CreateClient("github");

        var contentsUrl = $"repos/{config.Owner}/{config.Repo}/contents/{config.JsonPath}";

        for (int attempt = 1; attempt <= 2; attempt++)
        {
            string? sha = await GetCurrentShaAsync(client, contentsUrl, config, cancellationToken)
                .ConfigureAwait(false);

            var bodyJson = JsonSerializer.Serialize(
                new
                {
                    message = $"movie catalog sync {DateTime.UtcNow:O}",
                    content = Convert.ToBase64String(payload),
                    branch = config.Branch,
                    sha,
                },
                new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                });

            using var put = new HttpRequestMessage(HttpMethod.Put, contentsUrl)
            {
                Content = new StringContent(bodyJson, Encoding.UTF8, "application/json"),
            };
            AddAuthHeaders(put, config.PatToken);

            using var resp = await client.SendAsync(put, cancellationToken).ConfigureAwait(false);
            var status = (int)resp.StatusCode;

            if (status == 200 || status == 201)
            {
                _logger.LogInformation("GitHub push OK ({Status}); payload {Bytes} bytes", status, payload.Length);
                return;
            }
            if (status == 409)
            {
                _logger.LogWarning("GitHub push got 409 (SHA stale); attempt {Attempt}/2", attempt);
                continue;
            }
            if (status == 401 || status == 403)
            {
                _logger.LogError("GitHub push got {Status}; PAT bad or missing scope", status);
                throw new HttpRequestException($"GitHub auth failed ({status})");
            }
            if (status == 422)
            {
                var detail = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogError("GitHub push got 422 (malformed): {Detail}", detail);
                throw new HttpRequestException($"GitHub 422: {detail}");
            }
            _logger.LogError("GitHub push got unexpected {Status}", status);
            throw new HttpRequestException($"GitHub {status}");
        }

        throw new HttpRequestException("GitHub push: stale SHA conflict after retry");
    }

    private async Task<string?> GetCurrentShaAsync(HttpClient client, string contentsUrl, PluginConfiguration config, CancellationToken ct)
    {
        var url = $"{contentsUrl}?ref={Uri.EscapeDataString(config.Branch)}";
        using var get = new HttpRequestMessage(HttpMethod.Get, url);
        AddAuthHeaders(get, config.PatToken);

        using var resp = await client.SendAsync(get, ct).ConfigureAwait(false);
        var status = (int)resp.StatusCode;

        if (status == 200)
        {
            var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("sha").GetString();
        }
        if (status == 404)
        {
            return null;
        }
        if (status == 401 || status == 403)
        {
            throw new HttpRequestException($"GitHub auth failed ({status})");
        }
        throw new HttpRequestException($"GitHub GET {status}");
    }

    private static void AddAuthHeaders(HttpRequestMessage msg, string patToken)
    {
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", patToken);
        msg.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    private static void ValidateConfig(PluginConfiguration config)
    {
        if (string.IsNullOrWhiteSpace(config.Owner)) throw new InvalidOperationException("Owner not configured");
        if (string.IsNullOrWhiteSpace(config.Repo)) throw new InvalidOperationException("Repo not configured");
        if (string.IsNullOrWhiteSpace(config.Branch)) throw new InvalidOperationException("Branch not configured");
        if (string.IsNullOrWhiteSpace(config.JsonPath)) throw new InvalidOperationException("JsonPath not configured");
        if (string.IsNullOrWhiteSpace(config.PatToken)) throw new InvalidOperationException("PatToken not configured");
    }
}
