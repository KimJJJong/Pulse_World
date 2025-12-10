public interface IClientWorldView
{
    void OnCreateMap(int width, int height);
    void OnSetTile(int x, int y, int tileKind);

    void OnClearEntities();
    void OnSpawnOrUpdateEntity(ClientEntityInfo info);

    void OnBeatAction(ClientBeatAction action, ClientEntityInfo entity);

    void OnInitGameCompleted();
}
