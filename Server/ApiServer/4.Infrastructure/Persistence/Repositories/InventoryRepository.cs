using ApiServer.Domain.Items;
using Microsoft.EntityFrameworkCore;

namespace ApiServer.Infrastructure.Persistence.Repositories;

public interface IInventoryRepository
{
    Task<List<Item>> GetItemsAsync(string uid);
    Task<List<Equipment>> GetEquipmentsAsync(string uid);
    Task UpdateInventoryAsync(string uid, List<Item> items, List<Equipment> equipments);
}

public class InventoryRepository : IInventoryRepository
{
    private readonly ApiDbContext _context;
    private readonly ILogger<InventoryRepository> _logger;

    public InventoryRepository(ApiDbContext context, ILogger<InventoryRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<Item>> GetItemsAsync(string uid)
    {
        return await _context.Items
            .Where(x => x.OwnerUid == uid)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<List<Equipment>> GetEquipmentsAsync(string uid)
    {
        return await _context.Equipments
            .Where(x => x.OwnerUid == uid)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task UpdateInventoryAsync(string uid, List<Item> items, List<Equipment> equipments)
    {
        // Transaction
        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // 1. Delete all existing (Simple approach for now)
            // Optimization: Diff check is better for performance, but full replace is safer and easier to implement first.
            await _context.Items.Where(x => x.OwnerUid == uid).ExecuteDeleteAsync();
            await _context.Equipments.Where(x => x.OwnerUid == uid).ExecuteDeleteAsync();

            // 2. Insert new
            if (items.Any())
            {
                foreach (var item in items) item.OwnerUid = uid; // Ensure UID
                await _context.Items.AddRangeAsync(items);
            }

            if (equipments.Any())
            {
                foreach (var eq in equipments) eq.OwnerUid = uid; // Ensure UID
                await _context.Equipments.AddRangeAsync(equipments);
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update inventory for {Uid}", uid);
            await transaction.RollbackAsync();
            throw;
        }
    }
}
