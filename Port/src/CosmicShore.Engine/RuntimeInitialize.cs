namespace CosmicShore.Engine
{
    /// <summary>Original engine contract: UnityEngine.RuntimeInitializeLoadType (numeric values preserved).</summary>
    public enum RuntimeInitializeLoadType
    {
        AfterSceneLoad = 0,
        BeforeSceneLoad = 1,
        AfterAssembliesLoaded = 2,
        BeforeSplashScreen = 3,
        SubsystemRegistration = 4,
    }

    /// <summary>
    /// Data-only marker (original contract: UnityEngine.RuntimeInitializeOnLoadMethodAttribute).
    /// The original engine invokes tagged statics on domain reload; this engine has no domain
    /// reload, so the attribute compiles verbatim but nothing dispatches it — harnesses call
    /// the tagged reset methods directly where isolation needs them.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = false)]
    public sealed class RuntimeInitializeOnLoadMethodAttribute : System.Attribute
    {
        public RuntimeInitializeOnLoadMethodAttribute() { }
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType loadType) { }
    }
}
