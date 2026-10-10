using System;

namespace AOT
{
    /// <summary>
    /// Original contract: AOT.MonoPInvokeCallbackAttribute, which IL2CPP needs on a static
    /// method a native library calls back into (the game marks its FMOD EVENT_CALLBACK with it).
    /// Data-only here: the engine calls the delegate directly.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class MonoPInvokeCallbackAttribute : Attribute
    {
        public MonoPInvokeCallbackAttribute(Type type) { DelegateType = type; }
        public Type DelegateType { get; }
    }
}
