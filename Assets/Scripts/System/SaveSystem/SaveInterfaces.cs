public interface IGameSaveAble
{
    void OnSave(GameSaveData data);
    void OnLoad(GameSaveData data);
}

public interface IGameSaveParty
{
    void OnSaveParty(PartyData data);
    void OnLoadParty(PartyData data);
}
public interface ISaveOrder
{
    int LoadOrder { get; }
}