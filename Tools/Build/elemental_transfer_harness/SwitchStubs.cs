// Stubs for the PETAL-BURN SWITCH type-check (run.sh step 1, Docs/ELEMENTAL_ECONOMY.md §4.1): just
// enough of Unity and the project for VesselElementalDebuffByDangerPrismEffectSO.cs and
// CellConfigDataSO.cs - the two shipped files the switch edits - to bind. Library target; nothing
// here runs. Proves member names, arities and usings; NOT runtime behaviour (T8 in Driver.cs runs
// the resolver + settlement arithmetic on the asset's numbers).
using CosmicShore.Data;
using UnityEngine;

namespace UnityEngine
{
    public enum RuntimeInitializeLoadType { SubsystemRegistration = 4 }
    public class RuntimeInitializeOnLoadMethodAttribute : AttrBase
    {
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t) { }
    }
    public class Sprite : Object { }
}

namespace CosmicShore.Gameplay
{
    public abstract class ImpactEffectSO : ScriptableObject { }
    public abstract class VesselPrismEffectSO : ImpactEffectSO
    {
        public abstract void Execute(VesselImpactor impactor, PrismImpactor prismImpactee);
    }
    public class VesselImpactor : MonoBehaviour { public IVessel Vessel => null; }
    public class PrismImpactor : MonoBehaviour { public Prism Prism => null; }
    public class Prism : MonoBehaviour { public PrismProperties prismProperties; public Domains Domain => default; }
    public class PrismProperties { public bool IsDangerous; public float DangerWeight = 1f; }
    public class SnowChanger : MonoBehaviour { }
    public class CellModifier : ScriptableObject { }
    public class SpawnableBase : MonoBehaviour { }
}

namespace CosmicShore.Utility
{
    public class SpawnProfileSO : ScriptableObject { }
    public struct CellPhaseThresholds { public static CellPhaseThresholds Default => default; }
    public class CellRuntimeDataSO : ScriptableObject { public CellConfigDataSO Config; }
}
