using UnityEngine;

[System.Serializable]
public struct TrailBlockProperties
{
    public Vector3 position;
    public float volume;
    public float speedDebuffAmount; //don't use more than two sig figs, see ship.DebuffSpeed
}