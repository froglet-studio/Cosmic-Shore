using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IDataPersistence 
{
    void LoadData(GameData data);
    void SaveData(ref GameData data);

    void LoadData(HangerData data);
    void SaveData(ref HangerData data);
}
