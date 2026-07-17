using System.Collections.Concurrent;

namespace ApiServer.Presentation.Http.Middleware;

public sealed class FirewallManager
{
    private readonly ConcurrentDictionary<string, DateTime> _bannedIps = new();
    private readonly ConcurrentDictionary<string, (int count, DateTime windowStart)> _failureTracker = new();
    private readonly ILogger<FirewallManager> _logger;

    // List of malicious path patterns that warrant an immediate 24-hour ban
    private static readonly string[] MaliciousPathContains = 
    {
        ".env",
        "/.git/",
        "/wp-",
        "/cgi-bin/",
        "/solr/",
        "/zabbix/",
        "/owncloud/",
        "/sitecore/",
        "/jasperserver",
        "/Telerik",
        "/OA_HTML",
        "/etc/passwd",
        "eval-stdin.php",
        "luci/;stok="
    };

    private static readonly string[] MaliciousPathSuffixes =
    {
        ".php",
        ".jsp",
        ".asp",
        ".aspx",
        ".cgi",
        ".pl",
        ".do",
        "/device.rsp",
        "/metadatauploader",
        "/sugar_version.json"
    };

    public FirewallManager(ILogger<FirewallManager> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Checks if the IP is currently banned and not expired.
    /// </summary>
    public bool IsBanned(string ip, out DateTime expiry)
    {
        if (_bannedIps.TryGetValue(ip, out expiry))
        {
            if (expiry > DateTime.UtcNow)
            {
                return true;
            }
            // Ban expired, remove it
            _bannedIps.TryRemove(ip, out _);
        }
        return false;
    }

    /// <summary>
    /// Checks if the requested path contains malicious scanning patterns.
    /// </summary>
    public bool IsMaliciousPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        // Case-insensitive checks
        var lowerPath = path.ToLowerInvariant();

        foreach (var pattern in MaliciousPathContains)
        {
            if (lowerPath.Contains(pattern.ToLowerInvariant()))
                return true;
        }

        foreach (var suffix in MaliciousPathSuffixes)
        {
            if (lowerPath.EndsWith(suffix.ToLowerInvariant()))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Bans the specified IP address for a given duration.
    /// </summary>
    public void BanIp(string ip, TimeSpan duration, string reason)
    {
        var expiry = DateTime.UtcNow.Add(duration);
        _bannedIps[ip] = expiry;
        _logger.LogWarning("Firewall IP BANNED: IP={ip} for {duration} due to '{reason}'. Expiry={expiry} UTC", ip, duration, reason, expiry);
    }

    /// <summary>
    /// Records an authentication/validation failure for the IP.
    /// If failures exceed 10 times in 1 minute, the IP is banned for 1 hour.
    /// </summary>
    public void RecordFailure(string ip)
    {
        var now = DateTime.UtcNow;
        _failureTracker.AddOrUpdate(ip,
            _ => (1, now),
            (_, current) =>
            {
                if (now - current.windowStart > TimeSpan.FromMinutes(1))
                {
                    // Reset window if 1 minute has passed
                    return (1, now);
                }

                var newCount = current.count + 1;
                if (newCount >= 10)
                {
                    // Ban for 1 hour in a background task to avoid blocking the request
                    Task.Run(() => BanIp(ip, TimeSpan.FromHours(1), "Too many auth failures (brute-force/probe)"));
                }
                return (newCount, current.windowStart);
            });
    }
}
