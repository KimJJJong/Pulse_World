using ApiServer.Domain.Auth;
using ApiServer.Domain.Users;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace ApiServer.Infrastructure.Persistence;

public sealed class ApiDbContext : DbContext
{
    public ApiDbContext(DbContextOptions<ApiDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserIdentity> UserIdentities => Set<UserIdentity>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    
    // Inventory
    public DbSet<ApiServer.Domain.Items.Item> Items => Set<ApiServer.Domain.Items.Item>();
    public DbSet<ApiServer.Domain.Items.Equipment> Equipments => Set<ApiServer.Domain.Items.Equipment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApiDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
