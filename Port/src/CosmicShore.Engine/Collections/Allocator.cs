namespace CosmicShore.Engine.Collections
{
    /// <summary>Unity.Collections allocator labels (original numeric values; the port allocates managed memory).</summary>
    public enum Allocator { Invalid = 0, None = 1, Temp = 2, TempJob = 3, Persistent = 4, AudioKernel = 5, Domain = 6 }

    public enum NativeArrayOptions { UninitializedMemory = 0, ClearMemory = 1 }
}
