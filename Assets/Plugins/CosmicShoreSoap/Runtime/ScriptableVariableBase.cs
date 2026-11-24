using UnityEngine;

namespace CosmicShore.Soap
{
    /// <summary>Base of the shared-value assets; see <see cref="ScriptableVariable{T}"/>.</summary>
    public abstract class ScriptableVariableBase : SoapAsset
    {
#pragma warning disable CS0169, CS0414, CS0649
        [SerializeField, HideInInspector] string _guid;  // kept for the asset format
        [SerializeField, HideInInspector] int _saveGuid; // kept for the asset format
#pragma warning restore CS0169, CS0414, CS0649
    }
}
