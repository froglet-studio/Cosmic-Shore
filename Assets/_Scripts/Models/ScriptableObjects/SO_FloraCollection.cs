using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "FloraCollection", menuName = "ScriptableObjects/FloraCollection")]
public class FloraCollection : ScriptableObject
{
    public List<Flora> prefabs;

    public Flora GetRandomPrefab()
    {
        if (prefabs == null || prefabs.Count == 0)
        {
            Debug.LogWarning("No prefabs in the collection.");
            return null;
        }

        int randomIndex = Random.Range(0, prefabs.Count);
        return prefabs[randomIndex];
    }
}
