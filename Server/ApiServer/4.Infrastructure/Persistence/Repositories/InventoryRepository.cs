using ApiServer.Domain.Items;
using Microsoft.EntityFrameworkCore;

namespace ApiServer.Infrastructure.Persistence.Repositories;

public interface IInventoryRepository
{
    Task<List<GameItem>> GetInventoryAsync(string uid);
    Task UpdateInventoryAsync(string uid, List<GameItem> items);
    Task DeleteItemAsync(string uid, long itemInstanceId);
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

    public async Task<List<GameItem>> GetInventoryAsync(string uid)
    {
        return await _context.GameItems
            .Where(x => x.OwnerUid == uid)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task UpdateInventoryAsync(string uid, List<GameItem> items)
    {
        // Transaction
        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // 0. Concurrency Control: Lock the User (Pessimistic Lock)
            await _context.Database.ExecuteSqlRawAsync("SELECT 1 FROM users WHERE uid = {0} FOR UPDATE", uid);

            // 1. Fetch existing items
            var existingItems = await _context.GameItems.Where(x => x.OwnerUid == uid).ToListAsync();

            // 2. Sync Logic (O(N))
            var inputMap = items.Where(x => x.Id > 0).ToDictionary(x => x.Id);
            var existingIds = new HashSet<long>(existingItems.Select(x => x.Id));

            // A. Update Existing & B. Delete Missing
            foreach (var dbItem in existingItems)
            {
                if (inputMap.TryGetValue(dbItem.Id, out var inputItem))
                {
                    // Update
                    dbItem.Amount = inputItem.Amount;
                    dbItem.SlotIndex = inputItem.SlotIndex;
                    dbItem.TemplateId = inputItem.TemplateId;
                    dbItem.EnhancementLevel = inputItem.EnhancementLevel;
                    dbItem.IsEquipped = inputItem.IsEquipped;
                    dbItem.BaseStats = inputItem.BaseStats;
                    dbItem.RandomOptions = inputItem.RandomOptions;
                }
                else
                {
                    // Delete
                    _logger.LogInformation($"[Repo] Deleting GameItem {dbItem.Id} (TID: {dbItem.TemplateId})");
                    _context.GameItems.Remove(dbItem);
                }
            }

            // C. Insert New
            foreach (var inputItem in items)
            {
                if (inputItem.Id == 0 || !existingIds.Contains(inputItem.Id))
                {
                    if (inputItem.Id != 0) inputItem.Id = 0;
                    inputItem.OwnerUid = uid;
                    _logger.LogInformation($"[Repo] Inserting New GameItem (TID: {inputItem.TemplateId})");
                    await _context.GameItems.AddAsync(inputItem);
                }
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

    public async Task DeleteItemAsync(string uid, long itemInstanceId)
    {
        var item = await _context.GameItems
            .Where(x => x.OwnerUid == uid && x.Id == itemInstanceId)
            .FirstOrDefaultAsync();

        if (item != null)
        {
            _context.GameItems.Remove(item);
            await _context.SaveChangesAsync();
            _logger.LogInformation($"[Repo] Deleted Item {itemInstanceId} for User {uid}");
        }
        else
        {
            _logger.LogWarning($"[Repo] Delete Item Failed - Not Found: {itemInstanceId} for User {uid}");
        }
    }
}
