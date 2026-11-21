using System;
using System.Runtime.InteropServices;

namespace CosmicShore.Engine
{
    /// <summary>
    /// Texture2D's NativeArray readers (original contract: Texture2D.GetPixelData / GetRawTextureData&lt;T&gt;). They live
    /// here because NativeArray is a Compat type; the engine's own Texture2D keeps its byte[] API.
    /// </summary>
    public static class Texture2DNativeExtensions
    {
        /// <summary>
        /// A copy of mip <paramref name="mipLevel"/>'s pixels reinterpreted as <typeparamref name="T"/>. The port stores
        /// every texture as RGBA32, so a caller asking for another format's layout (RGBAHalf as ushort) gets the RGBA32
        /// bytes reinterpreted, not that format's values. Only mip 0 is stored; other mips read empty.
        /// </summary>
        public static Collections.NativeArray<T> GetPixelData<T>(this Texture2D texture, int mipLevel) where T : struct
        {
            var bytes = mipLevel == 0 ? texture?.GetRawTextureData() : null;
            if (bytes == null) return new Collections.NativeArray<T>(0, Collections.Allocator.None);
            var view = MemoryMarshal.Cast<byte, T>(bytes.AsSpan());
            return new Collections.NativeArray<T>(view.ToArray(), Collections.Allocator.None);
        }
    }
}
