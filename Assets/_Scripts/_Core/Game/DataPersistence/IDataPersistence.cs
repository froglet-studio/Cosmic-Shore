public abstract class DataPersistenceBase<T>
{
    public abstract T LoadData();
    public abstract void SaveData();
}