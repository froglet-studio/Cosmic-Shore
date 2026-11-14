using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The default material of every uGUI Graphic that names none (uGUI keeps it in Graphic's protected static
    /// <c>s_DefaultUI</c>; this class exists only to reach it, and nothing ever adds it to a GameObject). While the game
    /// runs it is <c>CosmicShore/UI/GradedDefault</c>, UI/Default with the colour grade (ColorGrade): every sprite, card,
    /// portrait and illustration a canvas draws follows the worn colour set, including the ones code makes or assigns
    /// later. <see cref="Use"/>(null) gives uGUI back its own default.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public abstract class GradedDefaultUI : Graphic
    {
        /// <summary>Makes <paramref name="material"/> the default UI material (null: uGUI's own again). Call it before a
        /// canvas draws, or dirty the Graphics that already have.</summary>
        public static void Use(Material material) => s_DefaultUI = material;
    }
}
