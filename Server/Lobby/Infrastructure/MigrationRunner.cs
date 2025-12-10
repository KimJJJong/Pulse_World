namespace Lobby.Infrastructure;
using Npgsql;
using System.IO;
    

public static class MigrationRunner
{
    public static async Task ApplyMigrationAsync(IConfiguration config)
    {
        var connStr = config.GetConnectionString("Database");
        
        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync();

        Console.WriteLine($"[MIGRATION] DB Connection = {connStr}");
        Console.WriteLine($"[MIGRATION] AppContext.BaseDirectory = {AppContext.BaseDirectory}");

        var possibleDirs = new[]
   {
            Path.Combine(AppContext.BaseDirectory, "migrations"),                      // bin/Debug/net8.0/migrations
            Path.Combine(Directory.GetCurrentDirectory(), "migrations"),                 // 개발환경에서 프로젝트경로/migrations
            "/app/migrations"                                                           // Docker 컨테이너 경로
        };

        string? dir = possibleDirs.FirstOrDefault(Directory.Exists);

        if (dir == null)
        {
            Console.WriteLine("[MIGRATION] ERROR: No valid migrations folder found.");
            foreach (var d in possibleDirs)
                Console.WriteLine($"  checked: {d}");
            return;
        }

        Console.WriteLine($"[MIGRATION] Using Directory: {dir}");

        //migration 기록용
        const string createTable = @"
CREATE TABLE IF NOT EXISTS __migrations (
    id SERIAL PRIMARY KEY,
    name TEXT UNIQUE NOT NULL,
    applied_at TIMESTAMPTZ  DEFAULT now()
);";
        await using (var cmd = new NpgsqlCommand(createTable, conn))
            await cmd.ExecuteNonQueryAsync();




        var files =Directory.GetFiles(dir, "*.sql").OrderBy(f => f)/*.ToList()*/;
        foreach (var file in files)
        {
            var name =Path.GetFileName(file);
            var sql = await File.ReadAllTextAsync(file);

            var check = new NpgsqlCommand("SELECT COUNT(*) FROM __migrations WHERE name = @n;", conn);
            check.Parameters.AddWithValue("n", name);
            var applied = (long)(await check.ExecuteScalarAsync() ?? 0);

            if (applied > 0) 
            {
                Console.WriteLine($"[MIGRATION] Already applied: {name}");
                continue;
            }

            Console.WriteLine($"[MIGRATION] Applying {name}...");
            await using var cmd = new NpgsqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync();

            var insert = new NpgsqlCommand("INSERT INTO __migrations (name) VALUES (@n);", conn);
            insert.Parameters.AddWithValue("n", name);
            await insert.ExecuteNonQueryAsync();
        }
        Console.WriteLine("[MIGRATION] Migration Applied");
    }
}

