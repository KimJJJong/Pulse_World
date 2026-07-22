using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GameServer.Content.Item;
using GameServer.Infrastructure.Api;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using StackExchange.Redis;
using Xunit;

namespace GameServer.Tests;

public class InventoryTests
{
    private readonly Mock<ILogger<InventoryManager>> _mockLogger;
    private readonly Mock<IConnectionMultiplexer> _mockRedis;
    private readonly Mock<IDatabase> _mockDb;
    private readonly Mock<IApiServerClient> _mockApiClient;
    private readonly Mock<IItemTemplateManager> _mockTemplates;
    private readonly InventoryManager _manager;

    public InventoryTests()
    {
        _mockLogger = new Mock<ILogger<InventoryManager>>();
        _mockRedis = new Mock<IConnectionMultiplexer>();
        _mockDb = new Mock<IDatabase>();
        _mockApiClient = new Mock<IApiServerClient>();
        _mockTemplates = new Mock<IItemTemplateManager>();

        _mockRedis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_mockDb.Object);

        _manager = new InventoryManager(
            _mockLogger.Object,
            _mockRedis.Object,
            _mockApiClient.Object,
            _mockTemplates.Object
        );
    }

    [Fact]
    public async Task LoadInventoryAsync_CacheHit_ReturnsItemsFromRedis()
    {
        // Arrange
        var uid = "test_user";
        var item = new ItemInstance { InstanceId = 1, TemplateId = 101, Amount = 10, SlotIndex = 0 };
        var json = JsonConvert.SerializeObject(item);
        
        var hashEntry = new HashEntry("i_0", json);
        _mockDb.Setup(x => x.HashGetAllAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(new[] { hashEntry });

        // Act
        var result = await _manager.LoadInventoryAsync(uid);

        // Assert
        Assert.Single(result);
        Assert.Equal(101, result[0].TemplateId);
        Assert.Equal(10, result[0].Amount);
        
        // Verify ApiClient was NOT called
        _mockApiClient.Verify(x => x.GetAsync<object>(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SaveInventoryAsync_SendsCorrectRequestToApi()
    {
        // Arrange
        var uid = "test_user";
        var items = new List<ItemInstance>
        {
            new ItemInstance { InstanceId = 1, TemplateId = 101, Amount = 5, SlotIndex = 0 }, // Item
            new ItemInstance { InstanceId = 2, TemplateId = 201, SlotIndex = 1, IsEquipped = true } // Equipment
        };

        // Mock IsEquipment check
        _mockTemplates.Setup(x => x.Get(101)).Returns(new ItemTemplate { Id = 101, Type = ItemType.Consumable });
        _mockTemplates.Setup(x => x.Get(201)).Returns(new ItemTemplate { Id = 201, Type = ItemType.Equipment });

        _mockApiClient.Setup(x => x.PostAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(true);

        // Act
        await _manager.SaveInventoryAsync(uid, items);

        // Assert
        _mockApiClient.Verify(x => x.PostAsync(
            It.Is<string>(s => s.Contains(uid)), 
            It.IsAny<object>()), Times.Once);
    }
    [Fact]
    public async Task SaveInventoryAsync_ResolvesIdCollision()
    {
        // Arrange
        var uid = "test_user_collision";
        var items = new List<ItemInstance>
        {
            new ItemInstance { InstanceId = 1, TemplateId = 101, Amount = 1, SlotIndex = 0 }, // Item ID 1
            new ItemInstance { InstanceId = 2000000001, TemplateId = 201, SlotIndex = 0, IsEquipped = true } // Equipment ID 1 (Offset applied)
        };

        _mockTemplates.Setup(x => x.Get(101)).Returns(new ItemTemplate { Id = 101, Type = ItemType.Consumable });
        _mockTemplates.Setup(x => x.Get(201)).Returns(new ItemTemplate { Id = 201, Type = ItemType.Equipment });
        _mockApiClient.Setup(x => x.PostAsync(It.IsAny<string>(), It.IsAny<object>())).ReturnsAsync(true);

        // Act
        await _manager.SaveInventoryAsync(uid, items);

        // Assert
        _mockApiClient.Verify(x => x.PostAsync(
            It.Is<string>(s => s.Contains(uid)),
            It.Is<object>(obj => VerifyIds(obj, 1, 1)) // Verify both are sent as 1
        ), Times.Once);
    }

    private bool VerifyIds(object request, long expectedItemId, long expectedEquipId)
    {
        // Use reflection to access private DTO properties
        var type = request.GetType();
        var itemsProp = type.GetProperty("Items");
        var equipsProp = type.GetProperty("Equipments");
        
        var items = (System.Collections.IEnumerable)itemsProp.GetValue(request);
        var equips = (System.Collections.IEnumerable)equipsProp.GetValue(request);
        
        long actualItemId = -1;
        long actualEquipId = -1;

        foreach(var i in items) 
            actualItemId = (long)i.GetType().GetProperty("Id").GetValue(i);
            
        foreach(var e in equips) 
            actualEquipId = (long)e.GetType().GetProperty("Id").GetValue(e);

        return actualItemId == expectedItemId && actualEquipId == expectedEquipId;
    }
}
