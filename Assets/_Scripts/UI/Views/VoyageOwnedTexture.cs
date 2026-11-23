using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Destroys a texture a graphic made at runtime when the graphic's object goes (the route poster's own water,
    /// <see cref="VoyageRoutePoster"/>). A component of its own because a <see cref="UnityEngine.UI.Graphic"/>'s OnDestroy
    /// is protected in the shipped uGUI and public in the compile gate's publicized reference, so it cannot be overridden
    /// in a way both accept.
    /// </summary>
    public sealed class VoyageOwnedTexture : MonoBehaviour
    {
        public Texture Texture;

        void OnDestroy()
        {
            if (Texture) Destroy(Texture);
            Texture = null;
        }
    }
}
