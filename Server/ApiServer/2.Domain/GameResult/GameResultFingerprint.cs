using System.Security.Cryptography;
using System.Text.Json;

namespace ApiServer.Domain.GameResult;

public static class GameResultFingerprint
{
    public static string Compute(GameResultRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("matchId", record.MatchId);
            writer.WriteString("roomId", record.RoomId);
            writer.WriteString("mapId", record.MapId);
            writer.WriteString("hostUid", record.HostUid);
            writer.WriteNumber("hostActorId", record.HostActorId);
            writer.WriteBoolean("isClear", record.IsClear);
            writer.WriteNumber("reportedPlayTimeMs", record.ReportedPlayTimeMs);
            writer.WriteNumber("verifiedPlayTimeMs", record.VerifiedPlayTimeMs);
            writer.WriteNumber("totalDamage", record.TotalDamage);
            writer.WriteNumber("submittedAtMs", record.SubmittedAtMs);

            writer.WriteStartArray("playerUids");
            foreach (var uid in record.PlayerUids.OrderBy(x => x, StringComparer.Ordinal))
                writer.WriteStringValue(uid);
            writer.WriteEndArray();

            writer.WritePropertyName("telemetry");
            if (record.Telemetry == null)
            {
                writer.WriteNullValue();
            }
            else
            {
                writer.WriteStartObject();
                foreach (var entry in record.Telemetry.OrderBy(x => x.Key, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(entry.Key);
                    WriteCanonicalJson(writer, entry.Value);
                }
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        var hash = SHA256.HashData(stream.ToArray());
        return $"sha256-v1:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    private static void WriteCanonicalJson(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonicalJson(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                    WriteCanonicalJson(writer, item);
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(element.GetRawText(), skipInputValidation: true);
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            default:
                writer.WriteNullValue();
                break;
        }
    }
}
