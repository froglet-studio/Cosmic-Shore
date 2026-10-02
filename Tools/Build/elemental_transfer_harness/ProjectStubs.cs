// The project types the files under test call into, with signatures copied verbatim from the
// shipped sources. Kept separate from the Unity stubs so it is obvious which half is engine and
// which half is ours - and so a project signature change shows up as a one-file diff here.
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    // Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Abstract Effect Types/SkimmerCrystalEffectSO.cs
    public abstract class SkimmerCrystalEffectSO : ScriptableObject { }

    // Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs
    public class Crystal : MonoBehaviour
    {
        public void ApplyDomainPreview(Domains domain) { }
    }

    // Assets/_Scripts/Controller/ImpactEffects/Impactors/CrystalImpactor.cs (+ Elemental subclass)
    public class CrystalImpactor : MonoBehaviour { }
    public class ElementalCrystalImpactor : CrystalImpactor
    {
        public Crystal Crystal { get; set; }
        public void SetCollectionEffects(SkimmerCrystalEffectSO[] effects) { }
    }

    // Assets/_Scripts/Controller/ImpactEffects/ImpactCollider.cs
    public class ImpactCollider : MonoBehaviour
    {
        public void SetImpactor(object impactor) { }
    }

    // Assets/_Scripts/Controller/Vessel/IVessel.cs
    public interface IVessel { Transform Transform { get; } }

    // Assets/_Scripts/Controller/Vessel/ResourceSystem.cs - the transfer surface under test
    public class ResourceSystem : MonoBehaviour
    {
        public const float PetalNormalized = 0.1f;
        public float TakeableLevel(Element element) => 0f;
        public int AccrueElementalLoss(Element element, float normalizedAmount, ElementalDebuffSources source) => 0;
        public void GrantPetals(Element element, int petals) { }
        public void ClearPendingElementalLoss() { }
        public void ApplyElementalEffect(Element element, float magnitude, float duration,
            ElementalDebuffSources source = ElementalDebuffSources.Other) { }
        public bool AdjustLevel(Element element, float amount) => false;
    }

    // Assets/_Scripts/Controller/Vessel/IVesselStatus.cs
    public interface IVesselStatus
    {
        IVessel Vessel { get; }
        string PlayerName { get; }
        Domains Domain { get; }
        ResourceSystem ResourceSystem { get; }
        Vector3 Course { get; set; }
        float Speed { get; set; }
    }
}
