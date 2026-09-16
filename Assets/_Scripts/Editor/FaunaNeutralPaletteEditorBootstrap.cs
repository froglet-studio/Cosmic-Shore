using UnityEditor;
using UnityEngine;
using CosmicShore.Gameplay;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Publishes the fauna neutral pair in the EDITOR too, so a material preview, a scene view
    /// before Play, and the shader inspector all show a creature rather than a black one.
    ///
    /// <para><c>_FaunaNeutralBright</c> / <c>_FaunaNeutralDull</c> are UNEXPOSED Shader Graph
    /// properties, i.e. plain shader globals, and an unset global is ZERO. At runtime
    /// <see cref="FaunaNeutralPalette"/>'s <c>BeforeSceneLoad</c> hook covers that; nothing runs
    /// it in edit mode, which is exactly the context an artist judges the material in. Fifteen
    /// lines here is cheaper than the bug report.</para>
    ///
    /// <para>Lives under <c>Editor/</c> rather than behind a <c>#if UNITY_EDITOR</c> in the
    /// runtime class, per <c>Docs/CONDITIONAL_COMPILATION.md</c>: the guard pattern is what has
    /// broken the Release player build repeatedly, and the folder cannot.</para>
    /// </summary>
    [InitializeOnLoad]
    static class FaunaNeutralPaletteEditorBootstrap
    {
        static FaunaNeutralPaletteEditorBootstrap()
        {
            Publish();
            EditorApplication.projectChanged += Publish;
        }

        static void Publish()
        {
            // The live palette if the project has one wired, else the class's own fallback.
            var container = AssetDatabase.FindAssets("t:ThemeManagerDataContainerSO");
            foreach (var guid in container)
            {
                var so = AssetDatabase.LoadAssetAtPath<ThemeManagerDataContainerSO>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (so == null || so.ColorSet == null) continue;
                FaunaNeutralPalette.PublishFrom(so.ColorSet);
                return;
            }

            FaunaNeutralPalette.Publish(FaunaNeutralPalette.FallbackBright,
                                        FaunaNeutralPalette.FallbackDull);
        }
    }
}
