using ApiServer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ApiServer.Bootstrap;

public static class MigrationExtension
{
    public static async Task ApplyMigrationsAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        
        // 데이터베이스가 없으면 생성, 마이그레이션이 있으면 적용
        // "relation does not exist"는 마이그레이션이 적용되지 않았다는 뜻이므로
        // 이미 마이그레이션 파일이 있다면 MigrateAsync가 최선.
        
        try 
        {
            await context.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            // 로그라도 남기는 것이 좋겠지만, 여기선 단순히 rethrow
            Console.WriteLine($"[Migration] Failed: {ex.Message}");
            throw;
        }
    }
}
