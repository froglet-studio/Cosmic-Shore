using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Core
{
    /// <summary>
    /// Applies the device tier's per-scene render choices (<see cref="PlatformProfile.Current"/>)
    /// as each scene loads. Today that is the skybox swap: a scene authors its sky in its lighting
    /// settings (<c>RenderSettings.skybox</c>), so the tier's replacement is put in place after every
    /// load and active-scene change, before the scene's first frame.
    ///
    /// The other Step 4 choices are applied where their owner already is: HDR in
    /// <c>GraphicsSettingsApplier.ApplyQuality</c>, the membrane capsule cap in
    /// <c>CapsuleMembrane.Awake</c>, the fold-gate window cap in <c>FoldGatePortalView</c>.
    /// <c>Docs/PLATFORM_UNIFICATION.md</c>, Step 4.
    ///
    /// Only the sky MATERIAL changes. Ambient light and reflections were baked from the authored sky
    /// and stay as they are, so a swapped scene is lit exactly as authored.
    /// </summary>
    public static class PlatformRenderApplier
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            // Enter Play Mode runs without a domain reload, so the previous session's handlers are
            // still attached to these static events: detach before attaching, never stack.
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplySkybox();

        static void OnActiveSceneChanged(Scene previous, Scene next) => ApplySkybox();

        /// <summary>Swap the active scene's skybox for the tier's replacement, if it has one.</summary>
        public static void ApplySkybox()
        {
            // The handlers outlive Play (no domain reload), and an edit-mode active-scene change
            // would otherwise write the tier's sky into a scene someone is editing.
            if (!Application.isPlaying) return;

            var profile = PlatformProfile.Current;
            if (!profile) return;

            var replacement = profile.SkyboxReplacementFor(RenderSettings.skybox);
            if (replacement) RenderSettings.skybox = replacement;
        }
    }
}
