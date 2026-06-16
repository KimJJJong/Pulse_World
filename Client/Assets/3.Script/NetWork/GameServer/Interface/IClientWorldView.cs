public interface IClientWorldView
{
    void OnCreateMap(int width, int height);
    void OnSetAppearancePalette(AppearanceAutoTilePalette palette);
    void OnSetTile(int x, int y, int tileKind);
    void OnSetAppearanceTile(int x, int y, int appearanceKind, int appearanceVariant);

    void OnClearEntities();
    void OnSpawnOrUpdateEntity(ClientEntityInfo info);

    void OnBeatAction(ClientBeatAction action, ClientEntityInfo entity);

    void OnInitGameCompleted();
    void OnDespawnEntity(int entityId);

}

public interface IClientWorldViewMapUpdateBatch
{
    void BeginMapVisualUpdate(int expectedTileCount);
    void EndMapVisualUpdate();
}
