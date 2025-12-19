using GameServer.Content.Map;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

public static class MapLoader
{
    public static Dictionary<string, MapContent> LoadFromDirectory(string dir, out ContentReport report)
    {
        report = new ContentReport("Maps");
        var maps = new Dictionary<string, MapContent>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(dir))
        {
            report.Error($"Directory not found: {dir}");
            return maps;
        }

        foreach (var file in Directory.GetFiles(dir, "*.json"))
        {
            var fileName = Path.GetFileName(file);

            try
            {
                var json = File.ReadAllText(file);
                var dto = JsonSerializer.Deserialize<MapJson>(json)
                          ?? throw new Exception("JSON parse failed");

                if (dto.width <= 0 || dto.height <= 0)
                    throw new Exception("width/height must be > 0");

                int n = dto.width * dto.height;

                if (dto.cells == null)
                    throw new Exception("cells is null");

                if (dto.cells.Length != n)
                    throw new Exception($"Cells length mismatch (expected {n}, got {dto.cells.Length})");

                var kind = new TileKind[n];
                var variant = new byte[n];

                for (int i = 0; i < n; i++)
                {
                    kind[i] = (TileKind)dto.cells[i].k;
                    variant[i] = dto.cells[i].v;

                }

                var mapId = Path.GetFileNameWithoutExtension(file);
                maps[mapId] = new MapContent
                {
                    MapId = mapId,
                    Width = dto.width,
                    Height = dto.height,
                    Kind = kind,
                    Variant = variant
                };
                //for(int i=0; i<n; i++)
                //{
                //    if(i%dto.width ==0) Console.WriteLine();
                //    Console.Write((int)kind[i]);
                //}

                report.Ok(fileName);
            }
            catch (Exception e)
            {
                report.Error($"{fileName} : {e.Message}");
            }
        }

        if (maps.Count == 0)
            report.Warn("No map files loaded.");

        return maps;
    }
}


