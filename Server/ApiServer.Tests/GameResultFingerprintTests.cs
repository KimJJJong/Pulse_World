using System.Text.Json;
using ApiServer.Domain.GameResult;

namespace ApiServer.Tests;

public sealed class GameResultFingerprintTests
{
    [Fact]
    public void Compute_IgnoresStorageTimestampAndCollectionOrder()
    {
        var first = CreateRecord(
            playerUids: new[] { "user-b", "user-a" },
            telemetryJson: """
                {
                  "network": {
                    "transport": "steam-relay",
                    "averageRttMs": 72
                  }
                }
                """,
            submittedAtMs: 100,
            storedAtMs: 200);

        var retry = CreateRecord(
            playerUids: new[] { "user-a", "user-b" },
            telemetryJson: """
                {
                  "network": {
                    "averageRttMs": 72,
                    "transport": "steam-relay"
                  }
                }
                """,
            submittedAtMs: 100,
            storedAtMs: 1000);

        Assert.Equal(
            GameResultFingerprint.Compute(first),
            GameResultFingerprint.Compute(retry));
    }

    [Fact]
    public void Compute_ChangesWhenAuthoritativeResultChanges()
    {
        var original = CreateRecord(new[] { "user-a" }, "{}", 100, 200);
        var changed = CreateRecord(new[] { "user-a" }, "{}", 100, 200);
        changed.TotalDamage++;

        Assert.NotEqual(
            GameResultFingerprint.Compute(original),
            GameResultFingerprint.Compute(changed));
    }

    [Fact]
    public void Compute_ChangesWhenSubmissionTimestampChanges()
    {
        var original = CreateRecord(new[] { "user-a" }, "{}", 100, 200);
        var changed = CreateRecord(new[] { "user-a" }, "{}", 100, 200);
        changed.SubmittedAtMs++;

        Assert.NotEqual(
            GameResultFingerprint.Compute(original),
            GameResultFingerprint.Compute(changed));
    }

    private static GameResultRecord CreateRecord(
        string[] playerUids,
        string telemetryJson,
        long submittedAtMs,
        long storedAtMs)
    {
        return new GameResultRecord
        {
            MatchId = "room-17:1784628000000",
            RoomId = "room-17",
            MapId = "Game_Forest_01",
            HostUid = "user-a",
            HostActorId = 1,
            IsClear = true,
            ReportedPlayTimeMs = 181000,
            VerifiedPlayTimeMs = 179800,
            TotalDamage = 95000,
            PlayerUids = playerUids,
            SubmittedAtMs = submittedAtMs,
            StoredAtMs = storedAtMs,
            Telemetry = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(telemetryJson)
        };
    }
}

