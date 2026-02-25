using CosmicShore.Core;
using System;
using CosmicShore.Game.IO;
using UnityEngine.Serialization;

[Serializable]
public struct ImpactProperties
{
    public float fuelBonus;
    public float fuelPenalty;
    public int scoreBonus;
    public int scorePenalty;
    public float tailLengthBonus;
    public float tailLengthPenalty;
    public float speedBonus;
    public float speedPenalty;
    public HapticType hapticType;
    [FormerlySerializedAs("trailBlock")] public Prism prism;
}