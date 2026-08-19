namespace PokeCorner.Core.Persistence;

public interface ISaveStore
{
    void Save(SaveData data);
    SaveData? Load();
}
