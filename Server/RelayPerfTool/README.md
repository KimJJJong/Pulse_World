# RelayPerfTool

`RelayPerfTool` compares `p2p-relay-metrics.csv` files produced by GameServer relay tests.

## Commands

```powershell
dotnet run --project Server\RelayPerfTool -- summarize --input server-relay.csv --name GameServerRelay
dotnet run --project Server\RelayPerfTool -- compare --steam steam-relay.csv --server server-relay.csv
dotnet run --project Server\RelayPerfTool -- template --output empty-metrics.csv
```

## Blazor dashboard

```powershell
dotnet run --project Server\RelayPerfDashboard --urls http://localhost:5177
```

Open `http://localhost:5177` and upload the Steam Relay CSV and GameServer Relay CSV.
