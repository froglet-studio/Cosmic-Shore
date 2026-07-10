// Ported from Assets/_Scripts/ScriptableObjects/SO_Captain.cs (Store unit
// 2026-07-10) — verbatim; UnityEngine / UnityEngine.Serialization → CosmicShore.Engine
// ([FormerlySerializedAs] carried by the engine's attribute shim; duplicate usings
// collapsed).
using CosmicShore.Data;
using CosmicShore.Engine;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(fileName = "Spike Spiegel", menuName = "ScriptableObjects/Captain/Captain", order = 3)]
    [System.Serializable]
    public class SO_Captain : ScriptableObject
    {
        [SerializeField] public string Name;
        [SerializeField] public string Description;
        [SerializeField] public string AIBehaviorDescription;
        [SerializeField] public string Flavor;
        [SerializeField] public Sprite Image;
        [SerializeField] public Sprite HeadshotImage;
        [FormerlySerializedAs("SelectedIcon")]
        [SerializeField] public Sprite IconActive;
        [FormerlySerializedAs("Icon")]
        [SerializeField] public Sprite IconInactive;
        [FormerlySerializedAs("Ship")]
        [SerializeField] public SO_Vessel Vessel;
        [SerializeField] public Element PrimaryElement;
        [SerializeField] public SO_Element Element;
        [SerializeField] public ResourceCollection InitialResourceLevels;
    }
}
