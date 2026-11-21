namespace CosmicShore.Engine
{
    /// <summary>
    /// Original contract: UnityEngine.FindObjectsSortMode — whether
    /// <see cref="Object.FindObjectsByType{T}"/> sorts its results by InstanceID.
    /// The headless scene walk is deterministic, so this is accepted for parity but not applied.
    /// </summary>
    public enum FindObjectsSortMode
    {
        None = 0,
        InstanceID = 1,
    }
}
