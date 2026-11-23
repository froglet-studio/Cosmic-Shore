using CosmicShore.Engine.Rendering;

namespace CosmicShore.Engine.Rendering
{
    /// <summary>
    /// Original contract: a shader keyword scoped to one shader (Rendering.LocalKeyword), set on a material with
    /// <see cref="Material.SetKeyword(in LocalKeyword, bool)"/>. The port keeps keywords by name, so this carries the name.
    /// </summary>
    public readonly struct LocalKeyword
    {
        public readonly string name;
        public readonly bool isValid;

        public LocalKeyword(Shader shader, string name)
        {
            this.name = name;
            isValid = !string.IsNullOrEmpty(name);
        }

        public override string ToString() => name;
    }
}

namespace CosmicShore.Engine
{
    public partial class Material
    {
        public void SetKeyword(in LocalKeyword keyword, bool value)
        {
            if (!keyword.isValid) return;
            if (value) EnableKeyword(keyword.name); else DisableKeyword(keyword.name);
        }

        public bool IsKeywordEnabled(in LocalKeyword keyword) => keyword.isValid && IsKeywordEnabled(keyword.name);
    }
}
