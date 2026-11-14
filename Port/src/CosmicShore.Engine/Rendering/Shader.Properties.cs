namespace CosmicShore.Engine.Rendering
{
    /// <summary>A shader property's type (original: UnityEngine.Rendering.ShaderPropertyType; values match).</summary>
    public enum ShaderPropertyType { Color = 0, Vector = 1, Float = 2, Range = 3, Texture = 4, Int = 5 }
}

namespace CosmicShore.Engine
{
    using CosmicShore.Engine.Rendering;

    public sealed partial class Shader
    {
        /// <summary>
        /// Original contract: the type of declared property <paramref name="propertyIndex"/>, read from the default the
        /// content layer gave it (a Color is Color, a Vector4 Vector, a float Float, null a Texture). The catalogue
        /// does not keep Range or Int apart from Float, so those read as Float.
        /// </summary>
        public ShaderPropertyType GetPropertyType(int propertyIndex)
        {
            _ = Declared;
            if (_declaredList == null || propertyIndex < 0 || propertyIndex >= _declaredList.Count)
                throw new System.ArgumentOutOfRangeException(nameof(propertyIndex));
            return _declaredList[propertyIndex].Value switch
            {
                Color => ShaderPropertyType.Color,
                Vector4 => ShaderPropertyType.Vector,
                null => ShaderPropertyType.Texture,
                int => ShaderPropertyType.Int,
                _ => ShaderPropertyType.Float,
            };
        }

        /// <summary>Original contract: <see cref="PropertyToID"/> of declared property <paramref name="propertyIndex"/>.</summary>
        public int GetPropertyNameId(int propertyIndex)
        {
            var name = GetPropertyName(propertyIndex);
            if (name == null) throw new System.ArgumentOutOfRangeException(nameof(propertyIndex));
            return PropertyToID(name);
        }

        /// <summary>
        /// Original contract: the value of tag <paramref name="tagName"/> in pass <paramref name="passIndex"/>
        /// (ShaderTagId.none when absent). PLACEHOLDER: the port does not read pass tags from shader source yet, so
        /// every tag is absent.
        /// </summary>
        public ShaderTagId FindPassTagValue(int passIndex, ShaderTagId tagName) => ShaderTagId.none;
    }
}
