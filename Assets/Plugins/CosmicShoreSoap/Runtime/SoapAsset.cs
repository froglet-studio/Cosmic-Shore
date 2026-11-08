using UnityEngine;

namespace CosmicShore.Soap
{
    /// <summary>
    /// Base of every SOAP asset: Cosmic Shore's own small ScriptableObject layer of event
    /// channels (<see cref="ScriptableEvent{T}"/>), shared values
    /// (<see cref="ScriptableVariable{T}"/>) and reactive lists (<see cref="ScriptableList{T}"/>).
    /// It replaced the Obvious "Soap" Asset Store plugin on 2026-11-06.
    ///
    /// The asset format is the plugin's: the concrete classes keep its script GUIDs (in their
    /// .meta files) and its serialized field names, so every asset, prefab and scene made
    /// with the plugin loads unchanged. Fields commented "kept for the asset format" do
    /// nothing at runtime; they stay so those files, and the Tools/Build generators that
    /// write them, re-save byte for byte. SoapFormatTests guards both.
    /// </summary>
    public abstract class SoapAsset : ScriptableObject
    {
        [HideInInspector] public int CategoryIndex; // kept for the asset format

        [Tooltip("What this asset is for. A note for people; nothing reads it at runtime.")]
        [TextArea(1, 6)]
        public string Description = "";
    }

    /// <summary>When a variable returns to its starting value, or a list empties.</summary>
    public enum ResetType
    {
        /// <summary>Whenever a scene is loaded in Single mode.</summary>
        SceneLoaded = 0,

        /// <summary>Once, when the application starts: the value survives scene loads.</summary>
        ApplicationStarts = 1,
    }
}
