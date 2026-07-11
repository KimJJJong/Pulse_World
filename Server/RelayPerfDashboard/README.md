# RelayPerfDashboard

Blazor UI for checking `p2p-relay-metrics.csv` output from GameServer relay tests.

## Run

```powershell
dotnet run --project Server\RelayPerfDashboard --urls http://localhost:5177
```

Open:

```text
http://localhost:5177
```

## Flow

1. Upload the Steam Relay CSV on the left.
2. Upload the GameServer Relay CSV on the right.
3. Check relay send/byte load, forward p95, queue p95, drops, rejects, and result validation.
4. Copy the generated Markdown into Notion when the result is ready.

The page includes sample data so the UI can be verified without a full 5-minute play test.
