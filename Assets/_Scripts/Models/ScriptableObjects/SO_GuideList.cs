using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "Guide List", menuName = "CosmicShore/GuideList", order = 11)]
[System.Serializable]
public class SO_GuideList : ScriptableObject
{
    [FormerlySerializedAs("VesselList")]
    public List<SO_Guide> GuideList;
}