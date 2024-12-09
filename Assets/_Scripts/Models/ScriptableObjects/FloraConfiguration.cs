using System;

[Serializable]
public class FloraConfiguration
{
    public Flora Flora;
    public float SpawnProbability;
    public int initialSpawnCount;
    public bool OverrideDefaultPlantPeriod;
    public int NewPlantPeriod = int.MaxValue;

    public FloraConfiguration()
    {
        SpawnProbability = 1;
        NewPlantPeriod = int.MaxValue;
    }
}